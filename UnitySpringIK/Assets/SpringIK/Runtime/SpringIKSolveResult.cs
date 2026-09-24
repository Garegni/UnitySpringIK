namespace SpringIK
{
    /// <summary>The outcome of one spring-IK solve, as named values. Blittable, so a Burst job can hold one.</summary>
    /// <remarks>All angles are in radians. Bend values are unsigned magnitudes; the opposed bend
    /// directions come from the constraint's hint transforms, not from here.</remarks>
    public struct SpringIKSolveResult
    {
        /// <summary>Total bend magnitude, radians. 0 = straight.</summary>
        public float TotalBend;

        /// <summary><see cref="KneeWeight"/> * <see cref="TotalBend"/>, radians.</summary>
        public float KneeBend;

        /// <summary><see cref="HockWeight"/> * <see cref="TotalBend"/>, radians.</summary>
        public float HockBend;

        /// <summary>(1 - bias) / 2.</summary>
        public float KneeWeight;

        /// <summary>(1 + bias) / 2.</summary>
        public float HockWeight;

        /// <summary>L1 + L2 + L3, the fully extended reach.</summary>
        public float ChainLength;

        /// <summary>Minimum reach at this bias, the distance at maximum fold. Varies with bias when the bones are unequal.</summary>
        public float FoldLimit;

        /// <summary>The distance asked for, before clamping.</summary>
        public float RequestedDistance;

        /// <summary>The distance solved for, after clamping to [FoldLimit, ChainLength].</summary>
        public float SolvedDistance;

        /// <summary>Target was beyond full extension; the chain straightens rather than stretching.</summary>
        public bool OverReached;

        /// <summary>Target was inside the fold limit; the chain holds at maximum fold, keeping its ratio.</summary>
        public bool UnderReached;

        /// <summary>A bone or the chain was ~0, or an input was not finite. Nothing was solved and every angle is 0.</summary>
        public bool Degenerate;
    }
}
