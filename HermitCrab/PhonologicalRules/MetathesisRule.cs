using System;
using System.Collections.Generic;
using SIL.Collections;
using SIL.Machine.Annotations;
using SIL.Machine.Matching;
using SIL.Machine.Rules;

namespace SIL.HermitCrab.PhonologicalRules
{
	/// <summary>
	/// This class represents a metathesis rule. Metathesis rules are phonological rules that
	/// reorder segments.
	/// </summary>
	public class MetathesisRule : IDBearerBase, IPhonologicalRule
	{
		private readonly List<string> _groupOrder;

		public MetathesisRule(string id)
			: base(id)
		{
			Pattern = Pattern<Word, ShapeNode>.New().Value;
			_groupOrder = new List<string>();
		}

		public Direction Direction { get; set; }

		public Pattern<Word, ShapeNode> Pattern { get; set; }

		public IList<string> GroupOrder
		{
			get { return _groupOrder; }
		}

		public IRule<Word, ShapeNode> CompileAnalysisRule(SpanFactory<ShapeNode> spanFactory, Morpher morpher)
		{
			return new AnalysisMetathesisRule(spanFactory, morpher, this);
		}

		public IRule<Word, ShapeNode> CompileSynthesisRule(SpanFactory<ShapeNode> spanFactory, Morpher morpher)
		{
			return new SynthesisMetathesisRule(spanFactory, morpher, this);
		}

		public void Traverse(Action<IHCRule> action)
		{
			action(this);
		}
	}
}