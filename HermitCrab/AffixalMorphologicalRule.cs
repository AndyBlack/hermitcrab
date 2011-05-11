using System;
using System.Collections.Generic;
using SIL.APRE;

namespace SIL.HermitCrab
{
    /// <summary>
    /// This class represents an affixal morphological rule. It supports many different types of affixation,
    /// such as prefixation, suffixation, infixation, circumfixation, simulfixation, reduplication,
    /// and truncation.
    /// </summary>
    public class AffixalMorphologicalRule : MorphologicalRule
    {
        /// <summary>
        /// This represents a morphological subrule.
        /// </summary>
        public class Subrule : Allomorph
        {
            AlphaVariables m_alphaVars;
            MorphologicalTransform m_transform;
            Pattern m_lhsTemp;

            MprFeatureSet m_excludedMPRFeatures = null;
            MprFeatureSet m_requiredMPRFeatures = null;
            MprFeatureSet m_outputMPRFeatures = null;

            /// <summary>
            /// Initializes a new instance of the <see cref="Subrule"/> class.
            /// </summary>
            /// <param name="id">The id.</param>
            /// <param name="desc">The description.</param>
            /// <param name="morpher">The morpher.</param>
            /// <param name="lhs">The LHS.</param>
            /// <param name="rhs">The RHS.</param>
            /// <param name="alphaVars">The alpha variables.</param>
            /// <param name="redupMorphType">The full reduplication type.</param>
            public Subrule(string id, string desc, Morpher morpher, IEnumerable<Pattern> lhs,
                IEnumerable<MorphologicalOutput> rhs, AlphaVariables alphaVars, MorphologicalTransform.RedupMorphType redupMorphType)
                : base (id, desc, morpher)
            {
                m_alphaVars = alphaVars;

                m_transform = new MorphologicalTransform(lhs, rhs, redupMorphType);

                // the LHS template is generated by simply concatenating all of the
                // LHS partitions; it matches the entire word, so we check for both the
                // left and right margins.
                m_lhsTemp = new Pattern();
#if WANTPORT
                m_lhsTemp.Add(new MarginContext(Direction.LEFT));
                int partition = 0;
                foreach (Pattern pat in lhs)
                    m_lhsTemp.AddPartition(pat, partition++);
                m_lhsTemp.Add(new MarginContext(Direction.RIGHT));
#endif
            }

            /// <summary>
            /// Gets or sets the excluded MPR features.
            /// </summary>
            /// <value>The excluded MPR features.</value>
            public MprFeatureSet ExcludedMPRFeatures
            {
                get
                {
                    return m_excludedMPRFeatures;
                }

                set
                {
                    m_excludedMPRFeatures = value;
                }
            }

            /// <summary>
            /// Gets or sets the required MPR features.
            /// </summary>
            /// <value>The required MPR features.</value>
            public MprFeatureSet RequiredMPRFeatures
            {
                get
                {
                    return m_requiredMPRFeatures;
                }

                set
                {
                    m_requiredMPRFeatures = value;
                }
            }

            /// <summary>
            /// Gets or sets the output MPR features.
            /// </summary>
            /// <value>The output MPR features.</value>
            public MprFeatureSet OutputMPRFeatures
            {
                get
                {
                    return m_outputMPRFeatures;
                }

                set
                {
                    m_outputMPRFeatures = value;
                }
            }

            /// <summary>
            /// Unapplies this subrule to the input word analysis.
            /// </summary>
            /// <param name="input">The input word analysis.</param>
            /// <param name="output">The output word analyses.</param>
            /// <returns><c>true</c> if the subrule was successfully unapplied, otherwise <c>false</c></returns>
            public bool Unapply(WordAnalysis input, out ICollection<WordAnalysis> output)
            {
                VariableValues instantiatedVars = new VariableValues(m_alphaVars);
                IList<PatternMatch> matches;
                m_transform.RHSTemplate.IsMatch(input.Shape.First, Direction.LeftToRight, ModeType.Analysis, instantiatedVars, out matches);

                List<WordAnalysis> outputList = new List<WordAnalysis>();
                output = outputList;
                foreach (PatternMatch match in matches)
                {
                    PhoneticShape shape = UnapplyRHS(match);

                    if (shape.Count > 2)
                    {
                        // check to see if this is a duplicate of another output analysis, this is not strictly necessary, but
                        // it helps to reduce the search space
                        bool add = true;
                        for (int i = 0; i < output.Count; i++)
                        {
                            if (shape.Duplicates(outputList[i].Shape))
                            {
                                if (shape.Count > outputList[i].Shape.Count)
                                    // if this is a duplicate and it is longer, then use this analysis and remove the previous one
                                    outputList.RemoveAt(i);
                                else
                                    // if it is shorter, then do not add it to the output list
                                    add = false;
                                break;
                            }
                        }

                        if (add)
                        {
                            WordAnalysis wa = input.Clone();
                            wa.Shape = shape;
                            output.Add(wa);
                        }
                    }
                }

                return outputList.Count > 0;
            }

            PhoneticShape UnapplyRHS(PatternMatch match)
            {
                PhoneticShape output = new PhoneticShape();
                output.Add(new Margin(Direction.RightToLeft));
                // iterate thru LHS partitions, copying the matching partition from the
                // input to the output
                for (int i = 0; i < m_transform.PartitionCount; i++)
                    m_transform.Unapply(match, i, output);
                output.Add(new Margin(Direction.LeftToRight));
                return output;
            }

            /// <summary>
            /// Applies this subrule to the specified word synthesis.
            /// </summary>
            /// <param name="input">The input word synthesis.</param>
            /// <param name="output">The output word synthesis.</param>
            /// <returns><c>true</c> if the subrule was successfully applied, otherwise <c>false</c></returns>
            public bool Apply(WordSynthesis input, out WordSynthesis output)
            {
                output = null;

                // check MPR features
                if ((m_requiredMPRFeatures != null && m_requiredMPRFeatures.Count > 0 && !m_requiredMPRFeatures.IsMatch(input.MPRFeatures))
                    || (m_excludedMPRFeatures != null && m_excludedMPRFeatures.Count > 0 && m_excludedMPRFeatures.IsMatch(input.MPRFeatures)))
                    return false;

                VariableValues instantiatedVars = new VariableValues(m_alphaVars);
                IList<PatternMatch> matches;
                if (m_lhsTemp.IsMatch(input.Shape.First, Direction.LeftToRight, ModeType.Synthesis, instantiatedVars, out matches))
                {
                    output = input.Clone();
                    ApplyRHS(matches[0], input, output);

                    if (m_outputMPRFeatures != null)
                        output.MPRFeatures.AddOutput(m_outputMPRFeatures);
                    return true;
                }

                return false;
            }


            void ApplyRHS(PatternMatch match, WordSynthesis input, WordSynthesis output)
            {
                output.Shape.Clear();
                output.Morphs.Clear();
                output.Shape.Add(new Margin(Direction.LEFT));
                foreach (MorphologicalOutput outputMember in m_transform.RHS)
                    outputMember.Apply(match, input, output, this);
                output.Shape.Add(new Margin(Direction.RIGHT));
            }
        }

        List<Subrule> m_subrules;

        IDBearerSet<PartOfSpeech> m_requiredPOSs = null;
        PartOfSpeech m_outPOS = null;
        int m_maxNumApps = 1;
		FeatureStructure m_requiredHeadFeatures = null;
		FeatureStructure m_requiredFootFeatures = null;
		FeatureStructure m_outHeadFeatures = null;
		FeatureStructure m_outFootFeatures = null;
        IDBearerSet<Feature> m_obligHeadFeatures = null;
        // TODO: add subcats

		/// <summary>
		/// Initializes a new instance of the <see cref="MorphologicalRule"/> class.
		/// </summary>
		/// <param name="id">The id.</param>
		/// <param name="desc">The description.</param>
		/// <param name="morpher">The morpher.</param>
        public AffixalMorphologicalRule(string id, string desc, Morpher morpher)
            : base(id, desc, morpher)
        {
            m_subrules = new List<Subrule>();
        }

        /// <summary>
        /// Gets the maximum number of allowable applications of this rule.
        /// </summary>
        /// <value>The maximum number of applications.</value>
        public int MaxNumApps
        {
            get
            {
                return m_maxNumApps;
            }

            set
            {
                m_maxNumApps = value;
            }
        }

        /// <summary>
        /// Gets or sets the required parts of speech.
        /// </summary>
        /// <value>The required parts of speech.</value>
        public IEnumerable<PartOfSpeech> RequiredPOSs
        {
            get
            {
                return m_requiredPOSs;
            }

            set
            {
                m_requiredPOSs = new IDBearerSet<PartOfSpeech>(value);
            }
        }

        /// <summary>
        /// Gets or sets the output part of speech.
        /// </summary>
        /// <value>The output part of speech.</value>
        public PartOfSpeech OutPOS
        {
            get
            {
                return m_outPOS;
            }

            set
            {
                m_outPOS = value;
            }
        }

        /// <summary>
        /// Gets or sets the required head features.
        /// </summary>
        /// <value>The required head features.</value>
        public FeatureStructure RequiredHeadFeatures
        {
            get
            {
                return m_requiredHeadFeatures;
            }

            set
            {
                m_requiredHeadFeatures = value;
            }
        }

        /// <summary>
        /// Gets or sets the required foot features.
        /// </summary>
        /// <value>The required foot features.</value>
		public FeatureStructure RequiredFootFeatures
        {
            get
            {
                return m_requiredFootFeatures;
            }

            set
            {
                m_requiredFootFeatures = value;
            }
        }

        /// <summary>
        /// Gets or sets the output head features.
        /// </summary>
        /// <value>The output head features.</value>
		public FeatureStructure OutHeadFeatures
        {
            get
            {
                return m_outHeadFeatures;
            }

            set
            {
                m_outHeadFeatures = value;
            }
        }

        /// <summary>
        /// Gets or sets the output foot features.
        /// </summary>
        /// <value>The output foot features.</value>
		public FeatureStructure OutFootFeatures
        {
            get
            {
                return m_outFootFeatures;
            }

            set
            {
                m_outFootFeatures = value;
            }
        }

        /// <summary>
        /// Gets or sets the obligatory head features.
        /// </summary>
        /// <value>The obligatory head features.</value>
        public IEnumerable<Feature> ObligatoryHeadFeatures
        {
            get
            {
                return m_obligHeadFeatures;
            }

            set
            {
                m_obligHeadFeatures = new IDBearerSet<Feature>(value);
            }
        }

        /// <summary>
        /// Gets the subrules.
        /// </summary>
        /// <value>The subrules.</value>
        public IEnumerable<Subrule> Subrules
        {
            get
            {
                return m_subrules;
            }
        }

        /// <summary>
        /// Gets the number of subrules.
        /// </summary>
        /// <value>The number of subrules.</value>
        public override int SubruleCount
        {
            get
            {
                return m_subrules.Count;
            }
        }

        /// <summary>
        /// Adds a subrule.
        /// </summary>
        /// <param name="sr">The subrule.</param>
        public void AddSubrule(Subrule sr)
        {
            sr.Morpheme = this;
        	sr.Index = m_subrules.Count;
            m_subrules.Add(sr);
        }

		/// <summary>
		/// Performs any pre-processing required for unapplication of a word analysis. This must
		/// be called before <c>Unapply</c>. <c>Unapply</c> and <c>EndUnapplication</c> should only
		/// be called if this method returns <c>true</c>.
		/// </summary>
		/// <param name="input">The input word analysis.</param>
		/// <returns>
		/// 	<c>true</c> if the specified input is unapplicable, otherwise <c>false</c>.
		/// </returns>
        public override bool BeginUnapplication(WordAnalysis input)
        {
			return input.GetNumUnappliesForMorphologicalRule(this) < m_maxNumApps
				&& (m_outPOS == null || input.MatchPartOfSpeech(m_outPOS));
        }

        /// <summary>
        /// Unapplies the specified subrule to the specified word analysis.
        /// </summary>
        /// <param name="input">The input word analysis.</param>
        /// <param name="srIndex">Index of the subrule.</param>
        /// <param name="output">All resulting word analyses.</param>
        /// <returns>
        /// 	<c>true</c> if the subrule was successfully unapplied, otherwise <c>false</c>
        /// </returns>
        public override bool Unapply(WordAnalysis input, int srIndex, out ICollection<WordAnalysis> output)
        {
            if (m_subrules[srIndex].Unapply(input, out output))
            {
                foreach (WordAnalysis wa in output)
                {
                    if (m_requiredPOSs != null && m_requiredPOSs.Count > 0)
                    {
                        foreach (PartOfSpeech pos in m_requiredPOSs)
                            wa.AddPartOfSpeech(pos);
                    }
                    else if (m_outPOS == null)
                    {
                        wa.UninstantiatePartOfSpeech();
                    }

                    wa.MorphologicalRuleUnapplied(this);

					if (TraceAnalysis)
					{
						// create the morphological rule analysis trace record for each output analysis
						MorphologicalRuleAnalysisTrace trace = new MorphologicalRuleAnalysisTrace(this, input.Clone());
						trace.RuleAllomorph = m_subrules[srIndex];
						trace.Output = wa.Clone();
						wa.CurrentTrace.AddChild(trace);
						// set current trace record to the morphological rule trace record for each
						// output analysis
						wa.CurrentTrace = trace;
					}
                }
                return true;
            }

            output = null;
            return false;
        }

		/// <summary>
		/// Performs any post-processing required after the unapplication of a word analysis. This must
		/// be called after a successful <c>BeginUnapplication</c> call and any <c>Unapply</c> calls.
		/// </summary>
		/// <param name="input">The input word analysis.</param>
		/// <param name="unapplied">if set to <c>true</c> if the input word analysis was successfully unapplied.</param>
		public override void EndUnapplication(WordAnalysis input, bool unapplied)
		{
			if (TraceAnalysis && !unapplied)
				// create the morphological rule analysis trace record for a rule that did not succesfully unapply
				input.CurrentTrace.AddChild(new MorphologicalRuleAnalysisTrace(this, input.Clone()));
		}

		/// <summary>
		/// Determines whether this rule is applicable to the specified word synthesis.
		/// </summary>
		/// <param name="input">The input word synthesis.</param>
		/// <returns>
		/// 	<c>true</c> if the rule is applicable, otherwise <c>false</c>.
		/// </returns>
        public override bool IsApplicable(WordSynthesis input)
        {
            // TODO: check subcats.

            // check required parts of speech
            return input.NextRule == this && input.GetNumAppliesForMorphologicalRule(this) < m_maxNumApps
                && (m_requiredPOSs == null || m_requiredPOSs.Count == 0 || m_requiredPOSs.Contains(input.PartOfSpeech));
        }

		/// <summary>
		/// Applies the rule to the specified word synthesis.
		/// </summary>
		/// <param name="input">The input word synthesis.</param>
		/// <param name="output">The output word syntheses.</param>
		/// <returns>
		/// 	<c>true</c> if the rule was successfully applied, otherwise <c>false</c>
		/// </returns>
		public override bool Apply(WordSynthesis input, out ICollection<WordSynthesis> output)
		{
			output = null;

			// these should probably be moved to IsApplicable, but we will leave it here for
			// now so we don't have to call it again to set the features for the output word
			// synthesis record

			// check head features
			FeatureStructure headFeatures;
			if (!m_requiredHeadFeatures.UnifyDefaults(input.HeadFeatures, out headFeatures))
				return false;

			// check foot features
			FeatureStructure footFeatures;
			if (!m_requiredFootFeatures.UnifyDefaults(input.FootFeatures, out footFeatures))
				return false;

			MorphologicalRuleSynthesisTrace trace = null;
			if (TraceSynthesis)
			{
				// create morphological rule synthesis trace record
				trace = new MorphologicalRuleSynthesisTrace(this, input.Clone());
				input.CurrentTrace.AddChild(trace);
			}

			output = new List<WordSynthesis>();
			foreach (Subrule sr in m_subrules)
			{
				WordSynthesis ws;
				if (sr.Apply(input, out ws))
				{
					if (m_outPOS != null)
						ws.PartOfSpeech = m_outPOS;

					if (m_outHeadFeatures != null)
						ws.HeadFeatures = m_outHeadFeatures.Clone();

					ws.HeadFeatures.Add(headFeatures);

					if (m_outFootFeatures != null)
						ws.FootFeatures = m_outFootFeatures.Clone();

					ws.FootFeatures.Add(footFeatures);

					if (m_obligHeadFeatures != null)
					{
						foreach (Feature feature in m_obligHeadFeatures)
							ws.AddObligatoryHeadFeature(feature);
					}

					ws.MorphologicalRuleApplied(this);

					ws = CheckBlocking(ws);

					if (trace != null)
					{
						// set current trace record to the morphological rule trace record for each
						// output analysis
						ws.CurrentTrace = trace;
						// add output to morphological rule trace record
						trace.RuleAllomorph = sr;
						trace.Output = ws.Clone();
					}

					output.Add(ws);
					// return all word syntheses that match subrules that are constrained by environments,
					// HC violates the disjunctive property of allomorphs here because it cannot check the
					// environmental constraints until it has a surface form, we will enforce the disjunctive
					// property of allomorphs at that time
					if (sr.RequiredEnvironments == null && sr.ExcludedEnvironments == null)
					{
						break;
					}
				}
			}

			return output.Count > 0;
		}

		/// <summary>
		/// Applies the rule to the specified word synthesis. This method is used by affix templates.
		/// </summary>
		/// <param name="input">The input word synthesis.</param>
		/// <param name="origHeadFeatures">The original head features before template application.</param>
		/// <param name="output">The output word syntheses.</param>
		/// <returns>
		/// 	<c>true</c> if the rule was successfully applied, otherwise <c>false</c>
		/// </returns>
		public override bool ApplySlotAffix(WordSynthesis input, FeatureStructure origHeadFeatures, out ICollection<WordSynthesis> output)
        {
			return Apply(input, out output);
        }

        public override void Reset()
        {
            base.Reset();

            m_requiredPOSs = null;
            m_outPOS = null;
            m_maxNumApps = 1;
            m_requiredHeadFeatures = null;
            m_requiredFootFeatures = null;
            m_outHeadFeatures = null;
            m_outFootFeatures = null;
            m_obligHeadFeatures = null;
            m_subrules.Clear();
        }
    }
}