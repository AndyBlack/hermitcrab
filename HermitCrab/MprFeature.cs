﻿using SIL.APRE;

namespace SIL.HermitCrab
{
    /// <summary>
    /// This class represents a morphological/phonological rule feature. It is used to restrict
    /// the application of rules for exception cases.
    /// </summary>
    public class MprFeature : IDBearer
    {
    	public MprFeature(string id, string desc)
            : base(id, desc)
        {
        	Group = null;
        }

    	/// <summary>
    	/// Gets or sets the MPR feature group.
    	/// </summary>
    	/// <value>The group.</value>
    	public MprFeatureGroup Group { get; internal set; }
    }
}
