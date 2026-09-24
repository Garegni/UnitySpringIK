using UnityEngine;

namespace SpringIK
{
    /// <summary>
    /// The 3-bone spring-IK law: a chain that folds at a knee:hock angle ratio of (1-bias) : (1+bias),
    /// independent of compression. Static, stateless, allocation-free, Burst-legal. Angles in radians.
    /// </summary>
    public static class SpringIKMath
    {
        /// <summary>Matches Unity's k_SqrEpsilon so edge behaviour agrees with stock TwoBoneIK.</summary>
        public const float SqrEpsilon = 1e-8f;

        /// <summary>Below this a bone (or the whole chain) counts as degenerate.</summary>
        public const float LengthEpsilon = 1e-6f;

        /// <summary>Bisection steps. Fixed trip count: no data-dependent branching.</summary>
        public const int BisectIterations = 8;

        /// <summary>Newton steps after the bisection. 8 + 4 reaches the float32 floor; more do not help.</summary>
        public const int NewtonIterations = 4;

        /// <summary>
        /// Splits a bend into the two bend weights. Bias is clamped to [-1,+1]: 0 gives equal angles
        /// (bone 1 parallel to bone 3), -1 loads the knee, +1 loads the hock.
        /// </summary>
        public static void BiasWeights(float bias, out float kneeWeight, out float hockWeight)
        {
            bias = SafeBias(bias);
            kneeWeight = 0.5f * (1f - bias);
            hockWeight = 0.5f * (1f + bias);
        }

        /// <summary>
        /// End of the solve domain in radians, 2*PI/(1+|bias|): the larger bend reaches exactly PI
        /// here. <see cref="Reach2"/> is monotonic only on [0, ThetaMax].
        /// </summary>
        public static float ThetaMax(float kneeWeight, float hockWeight)
        {
            // Floored because a caller can pass junk weights; for bias in [-1,1] the larger is >= 0.5.
            float w = Mathf.Max(Mathf.Max(kneeWeight, hockWeight), LengthEpsilon);
            return Mathf.PI / w;
        }

        /// <summary>
        /// Squared end-to-end distance at total bend <paramref name="theta"/> radians, for opposed
        /// bends. At theta = 0 this is exactly (l1+l2+l3)^2.
        /// </summary>
        public static float Reach2(float l1, float l2, float l3, float kneeWeight, float hockWeight, float theta)
        {
            // |bias| from the weights so it cannot disagree with them; cos is even, so sign drops out.
            float s = Mathf.Abs(kneeWeight - hockWeight);
            return l1 * l1 + l2 * l2 + l3 * l3
                 + 2f * l1 * l2 * Mathf.Cos(kneeWeight * theta)
                 + 2f * l2 * l3 * Mathf.Cos(hockWeight * theta)
                 + 2f * l1 * l3 * Mathf.Cos(s * theta);
        }

        /// <summary>
        /// d(Reach2)/d(theta). Non-positive on [0, ThetaMax] — every sine argument lies in [0, PI]
        /// there — which is what makes the bisection bracket valid.
        /// </summary>
        public static float DReach2(float l1, float l2, float l3, float kneeWeight, float hockWeight, float theta)
        {
            float s = Mathf.Abs(kneeWeight - hockWeight);
            return -2f * l1 * l2 * kneeWeight * Mathf.Sin(kneeWeight * theta)
                   - 2f * l2 * l3 * hockWeight * Mathf.Sin(hockWeight * theta)
                   - 2f * l1 * l3 * s * Mathf.Sin(s * theta);
        }

        /// <summary>
        /// The chain's geometric minimum reach (polygon inequality), independent of bias.
        /// Not |l1 - l2 - l3|: that form is only correct when l1 is the dominant bone.
        /// </summary>
        public static float ChainInnerLimit(float l1, float l2, float l3)
        {
            float t = l1 + l2 + l3;
            float longest = Mathf.Max(l1, Mathf.Max(l2, l3));
            return Mathf.Max(0f, 2f * longest - t);
        }

        /// <summary>
        /// The law's minimum reach at this bias: the distance the chain reaches at maximum fold,
        /// in closed form. Equals sqrt(Reach2(ThetaMax)).
        /// </summary>
        public static float FoldLimit(float l1, float l2, float l3, float bias)
        {
            bias = SafeBias(bias);
            float a = l1 * l1 + l2 * l2 + l3 * l3;
            float abs = Mathf.Abs(bias);
            float q = (1f - abs) / (1f + abs);
            float cosQ = Mathf.Cos(Mathf.PI * q);
            float cosInvQ = Mathf.Cos(Mathf.PI * (1f - q));

            // The branches are asymmetric by design: it is always the larger weight whose bend
            // reaches PI, so which of the l1*l2 and l2*l3 terms carries cos(PI*q) follows the sign.
            float d2 = bias >= 0f
                ? a + 2f * l1 * l2 * cosQ - 2f * l2 * l3 + 2f * l1 * l3 * cosInvQ
                : a - 2f * l1 * l2 + 2f * l2 * l3 * cosQ + 2f * l1 * l3 * cosInvQ;

            return Mathf.Sqrt(Mathf.Max(0f, d2));
        }

        /// <summary>
        /// Total bend in radians that puts the end effector at <paramref name="distance"/>, which
        /// must already be clamped to [FoldLimit, ChainLength] — <see cref="Solve"/> does that.
        /// </summary>
        public static float SolveBend(float l1, float l2, float l3, float kneeWeight, float hockWeight, float distance)
        {
            float tMax = ThetaMax(kneeWeight, hockWeight);
            float d2 = distance * distance;

            // The early-outs also establish Reach2(0) > d2 > Reach2(tMax), which validates the bracket.
            if (Reach2(l1, l2, l3, kneeWeight, hockWeight, 0f) <= d2) return 0f;        // at or past full extension
            if (Reach2(l1, l2, l3, kneeWeight, hockWeight, tMax) >= d2) return tMax;    // at or past maximum fold

            float lo = 0f;
            float hi = tMax;
            for (int i = 0; i < BisectIterations; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (Reach2(l1, l2, l3, kneeWeight, hockWeight, mid) > d2) lo = mid; else hi = mid;
            }

            float theta = 0.5f * (lo + hi);
            for (int i = 0; i < NewtonIterations; i++)
            {
                float f = Reach2(l1, l2, l3, kneeWeight, hockWeight, theta) - d2;
                float g = DReach2(l1, l2, l3, kneeWeight, hockWeight, theta);
                // The derivative vanishes at both ends of the domain; step only where it is clearly
                // negative and let the bracket clamp catch the rest.
                if (g < -1e-30f) theta -= f / g;
                theta = Mathf.Clamp(theta, lo, hi);
            }
            return theta;
        }

        /// <summary>
        /// Entry point: solves a 3-bone chain of the given lengths to reach
        /// <paramref name="distance"/>, distributing the bend by <paramref name="bias"/>.
        /// </summary>
        public static SpringIKSolveResult Solve(float l1, float l2, float l3, float bias, float distance)
        {
            SpringIKSolveResult r = default;

            bias = SafeBias(bias);
            BiasWeights(bias, out r.KneeWeight, out r.HockWeight);
            r.RequestedDistance = distance;

            float t = l1 + l2 + l3;
            r.ChainLength = t;

            // Written as !(x >= eps), not (x < eps), so NaN falls into the degenerate branch.
            if (!(l1 >= LengthEpsilon) || !(l2 >= LengthEpsilon) || !(l3 >= LengthEpsilon)
                || !(t >= LengthEpsilon) || float.IsNaN(distance) || float.IsInfinity(distance))
            {
                r.Degenerate = true;
                return r;   // every angle stays 0
            }

            float fold = FoldLimit(l1, l2, l3, bias);
            // fold <= t always holds geometrically; the Min guards against an inverted clamp range.
            fold = Mathf.Min(fold, t);
            r.FoldLimit = fold;

            r.OverReached = distance > t;
            r.UnderReached = distance < fold;

            // Clamp the distance, never the resulting theta: dTheta/dD -> -inf near full extension.
            float d = Mathf.Clamp(distance, fold, t);
            r.SolvedDistance = d;

            float theta = SolveBend(l1, l2, l3, r.KneeWeight, r.HockWeight, d);
            r.TotalBend = theta;
            r.KneeBend = r.KneeWeight * theta;
            r.HockBend = r.HockWeight * theta;
            return r;
        }

        /// <summary>Clamps to [-1,1] with NaN mapped to 0; Mathf.Clamp alone passes NaN through.</summary>
        static float SafeBias(float bias)
        {
            if (float.IsNaN(bias)) return 0f;
            return Mathf.Clamp(bias, -1f, 1f);
        }
    }
}
