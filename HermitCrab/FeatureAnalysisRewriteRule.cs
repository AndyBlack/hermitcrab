﻿using System;
using System.Linq;
using SIL.APRE;
using SIL.APRE.FeatureModel;
using SIL.APRE.Matching;

namespace SIL.HermitCrab
{
	public class FeatureAnalysisRewriteRule : AnalysisRewriteRule
	{
		private readonly Expression<PhoneticShapeNode> _analysisRhs;
		private readonly AnalysisReapplyType _reapplyType;

		public FeatureAnalysisRewriteRule(SpanFactory<PhoneticShapeNode> spanFactory, Direction synthesisDir, bool synthesisSimult,
			Expression<PhoneticShapeNode> lhs, Expression<PhoneticShapeNode> rhs, Expression<PhoneticShapeNode> leftEnv, Expression<PhoneticShapeNode> rightEnv)
			: base(spanFactory, synthesisDir == Direction.LeftToRight ? Direction.RightToLeft : Direction.LeftToRight, false,
			(input, match) => IsUnapplicationNonvacuous(match, rhs, synthesisDir))
		{
			_analysisRhs = new Expression<PhoneticShapeNode>();
			AddEnvironment("leftEnv", leftEnv);
			int i = 0;
			foreach (Tuple<PatternNode<PhoneticShapeNode>, PatternNode<PhoneticShapeNode>> tuple in lhs.Children.Zip(rhs.Children))
			{
				var lhsConstraint = (Constraint<PhoneticShapeNode>) tuple.Item1;
				var rhsConstraint = (Constraint<PhoneticShapeNode>) tuple.Item2;

				if (lhsConstraint.Type == "segment" && rhsConstraint.Type == "segment")
				{
					var targetConstraint = (Constraint<PhoneticShapeNode>)lhsConstraint.Clone();
					targetConstraint.FeatureStruct.Replace(rhsConstraint.FeatureStruct);
					targetConstraint.FeatureStruct.AddValue(HCFeatureSystem.Backtrack, HCFeatureSystem.NotSearched);
					Lhs.Children.Add(new Group<PhoneticShapeNode>("target" + i, targetConstraint));

					var fs = GetAntiFeatureStruct(rhsConstraint.FeatureStruct);
					fs.Subtract(GetAntiFeatureStruct(lhsConstraint.FeatureStruct));
					_analysisRhs.Children.Add(new Constraint<PhoneticShapeNode>("segment", fs));

					i++;
				}
			}
			AddEnvironment("rightEnv", rightEnv);

			if (synthesisSimult)
			{
				foreach (Constraint<PhoneticShapeNode> constraint in rhs.Children)
				{
					if (constraint.Type == "segment")
					{
						if (!IsUnifiable(constraint, leftEnv) || !IsUnifiable(constraint, rightEnv))
						{
							_reapplyType = AnalysisReapplyType.SelfOpaquing;
							break;
						}
					}
				}
			}
		}

		private static bool IsUnapplicationNonvacuous(PatternMatch<PhoneticShapeNode> match, Expression<PhoneticShapeNode> rhs, Direction synthesisDir)
		{
			Direction dir = synthesisDir == Direction.LeftToRight ? Direction.RightToLeft : Direction.LeftToRight;
			int i = 0;
			foreach (Constraint<PhoneticShapeNode> constraint in rhs.Children)
			{
				if (constraint.Type == "segment")
				{
					PhoneticShapeNode node = match["target" + i].GetStart(dir);
					if (node.Annotation.FeatureStruct.IsUnifiable(constraint.FeatureStruct, match.VariableBindings))
						return true;
					i++;
				}
			}

			return false;
		}

		private static FeatureStruct GetAntiFeatureStruct(FeatureStruct fs)
		{
			var result = new FeatureStruct();
			foreach (Feature feature in fs.Features)
			{
				FeatureValue value = fs.GetValue(feature);
				var childFS = value as FeatureStruct;
				FeatureValue newValue;
				if (childFS != null)
				{
					newValue = GetAntiFeatureStruct(childFS);
				}
				else
				{
					value.Negation(out newValue);
				}
				result.AddValue(feature, newValue);
			}
			return result;
		}

		private static bool IsUnifiable(Constraint<PhoneticShapeNode> constraint, Expression<PhoneticShapeNode> env)
		{
			foreach (Constraint<PhoneticShapeNode> curConstraint in env.GetNodes().OfType<Constraint<PhoneticShapeNode>>())
			{
				if (curConstraint.Type == "segment"
					&& !curConstraint.FeatureStruct.IsUnifiable(constraint.FeatureStruct))
				{
					return false;
				}
			}

			return true;
		}

		public override AnalysisReapplyType AnalysisReapplyType
		{
			get { return _reapplyType; }
		}

		public override Annotation<PhoneticShapeNode> ApplyRhs(IBidirList<Annotation<PhoneticShapeNode>> input, PatternMatch<PhoneticShapeNode> match)
		{
			PhoneticShapeNode endNode = null;
			int i = 0;
			foreach (Constraint<PhoneticShapeNode> constraint in _analysisRhs.Children)
			{
				PhoneticShapeNode node = match["target" + i].GetStart(Lhs.Direction);
				if (endNode == null || node.CompareTo(endNode, Lhs.Direction) > 0)
					endNode = node;
				node.Annotation.FeatureStruct.Merge(constraint.FeatureStruct, match.VariableBindings);
				i++;
			}

			MarkSearchedNodes(match.GetStart(Lhs.Direction), endNode);

			return match.GetStart(Lhs.Direction).Annotation;
		}
	}
}
