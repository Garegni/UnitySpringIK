using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace SpringIK
{
    /// <summary>The four world rotations a solve produces. Assign them parent first.</summary>
    public struct SpringIKChainPose
    {
        /// <summary>Final world rotation for the hip.</summary>
        public Quaternion Root;
        /// <summary>Final world rotation for the knee.</summary>
        public Quaternion Knee;
        /// <summary>Final world rotation for the hock.</summary>
        public Quaternion Hock;
        /// <summary>Final world rotation for the foot.</summary>
        public Quaternion Tip;
        /// <summary>False when nothing was solved; the caller should leave the chain alone.</summary>
        public bool Valid;
        /// <summary>Bend plane normal the solve settled on, after aim and roll. For gizmos.</summary>
        public Vector3 PlaneNormal;
        /// <summary>Roll applied about the root-to-target axis to serve the hints, in degrees.</summary>
        public float HintRollDegrees;
    }

    /// <summary>
    /// Geometry for the spring-IK chain, and the single write path: turns a current pose plus a
    /// target into the four world rotations that realise the solve. Pure, stateless,
    /// allocation-free, Burst-legal.
    /// </summary>
    /// <remarks>Assign the returned rotations parent first; any other order does not reproduce the
    /// incremental sequence. Angles in <see cref="SpringIKSolveResult"/> are radians.</remarks>
    public static class SpringIKPose
    {
        /// <summary>Solves the chain from world-space positions and rotations, read before any write.</summary>
        public static SpringIKChainPose Solve(
            Vector3 a, Vector3 b, Vector3 c, Vector3 e,
            Quaternion rRoot, Quaternion rKnee, Quaternion rHock, Quaternion rTip,
            Vector3 targetPos, Quaternion targetRot,
            bool hasKneeHint, Vector3 kneeHintPos,
            bool hasHockHint, Vector3 hockHintPos,
            float bias, float targetPositionWeight, float targetRotationWeight,
            float kneeHintWeight, float hockHintWeight,
            out SpringIKSolveResult solve)
        {
            SpringIKChainPose pose = default;
            pose.Root = rRoot; pose.Knee = rKnee; pose.Hock = rHock; pose.Tip = rTip;
            pose.PlaneNormal = Vector3.up;

            float l1 = Vector3.Distance(a, b);
            float l2 = Vector3.Distance(b, c);
            float l3 = Vector3.Distance(c, e);

            Vector3 tPos = Vector3.Lerp(e, targetPos, targetPositionWeight);
            Quaternion tRot = Quaternion.Lerp(rTip, targetRot, targetRotationWeight);

            Vector3 at = tPos - a;
            float atSq = Vector3.Dot(at, at);

            solve = SpringIKMath.Solve(l1, l2, l3, bias, Mathf.Sqrt(atSq));
            if (solve.Degenerate) return pose;   // Valid stays false

            Vector3 u1 = (b - a).normalized;
            Vector3 u2 = (c - b).normalized;
            Vector3 u3 = (e - c).normalized;

            // Bend plane fallbacks, in order: the incoming pose first, so the solve composes with
            // animation instead of imposing a plane of its own; then each hint; then the target.
            Vector3 m = Vector3.Cross(u1, u2);
            if (m.sqrMagnitude < SpringIKMath.SqrEpsilon) m = Vector3.Cross(u2, u3);
            if (m.sqrMagnitude < SpringIKMath.SqrEpsilon && hasKneeHint) m = Vector3.Cross(kneeHintPos - a, at);
            if (m.sqrMagnitude < SpringIKMath.SqrEpsilon && hasHockHint) m = Vector3.Cross(at, hockHintPos - a);
            if (m.sqrMagnitude < SpringIKMath.SqrEpsilon) m = Vector3.Cross(at, Vector3.up);
            if (m.sqrMagnitude < SpringIKMath.SqrEpsilon) m = Vector3.forward;
            m = m.normalized;

            // Signs are fixed here and never re-derived: reach is monotonic only for opposed bends,
            // so a sign free to flip would let the root find converge to the wrong answer.
            float turnKnee = solve.KneeBend * Mathf.Rad2Deg;
            float turnHock = -solve.HockBend * Mathf.Rad2Deg;

            Quaternion qKnee = QuaternionExt.FromToRotation(u2, Quaternion.AngleAxis(turnKnee, m) * u1);
            Quaternion nKnee = qKnee * rKnee;
            Quaternion nHock = qKnee * rHock;
            Vector3 c2 = b + qKnee * (c - b);
            Vector3 e2 = b + qKnee * (e - b);

            Vector3 u2b = (c2 - b).normalized;
            Vector3 u3b = (e2 - c2).normalized;
            Quaternion qHock = QuaternionExt.FromToRotation(u3b, Quaternion.AngleAxis(turnHock, m) * u2b);
            nHock = qHock * nHock;
            Vector3 e3 = c2 + qHock * (e2 - c2);

            Quaternion nRoot = rRoot;

            Vector3 ac = e3 - a;
            if (atSq > SpringIKMath.SqrEpsilon && ac.sqrMagnitude > SpringIKMath.SqrEpsilon)
            {
                // Guarded: FromToRotation with a zero vector builds a non-unit quaternion.
                Quaternion aim = QuaternionExt.FromToRotation(ac, at);
                nRoot = aim * nRoot; nKnee = aim * nKnee; nHock = aim * nHock;
                m = aim * m;
            }

            // Roll: the two hints compete for the one remaining degree of freedom.
            float roll = 0f;
            if (atSq > SpringIKMath.SqrEpsilon && (kneeHintWeight > 0f || hockHintWeight > 0f))
            {
                Vector3 n = at / Mathf.Sqrt(atSq);
                // THE DIRECTION THE KNEE DISPLACES TOWARD, and the sign here is load-bearing.
                // The knee turns +theta about m, landing on the OUTSIDE of that turn: cross(n, m),
                // not cross(m, n). Reversed, every bend goes to the wrong side of its hint.
                Vector3 kneeSide = Vector3.Cross(n, m);
                if (kneeSide.sqrMagnitude > SpringIKMath.SqrEpsilon)
                {
                    kneeSide = kneeSide.normalized;
                    float thr = 1e-6f * Mathf.Max(1f, atSq);
                    Vector3 acc = Vector3.zero;

                    // Each hint is weighted by its OWN bend's share, so it is missed in proportion
                    // to the other bend's share and the two residuals sum to their disagreement.
                    // At bias +/-1 the rigid bend's hint is correctly ignored outright.
                    if (hasKneeHint && kneeHintWeight > 0f)
                    {
                        Vector3 pk = kneeHintPos - a;
                        pk -= n * Vector3.Dot(pk, n);
                        if (Vector3.Dot(pk, pk) > thr) acc += solve.KneeWeight * pk.normalized;
                    }
                    if (hasHockHint && hockHintWeight > 0f)
                    {
                        Vector3 ph = hockHintPos - a;
                        ph -= n * Vector3.Dot(ph, n);
                        if (Vector3.Dot(ph, ph) > thr) acc -= solve.HockWeight * ph.normalized;
                    }

                    acc -= n * Vector3.Dot(acc, n);
                    if (Vector3.Dot(acc, acc) > 1e-18f)
                    {
                        Vector3 accN = acc.normalized;
                        // Atan2, not Vector3.SignedAngle: that goes through Acos, which loses
                        // precision exactly where this angle is small.
                        float signed = Mathf.Atan2(Vector3.Dot(Vector3.Cross(kneeSide, accN), n),
                                                   Vector3.Dot(kneeSide, accN)) * Mathf.Rad2Deg;
                        // The DIRECTION blends the two hints; the AMOUNT is the stronger weight, so
                        // a half-weighted hint gives a half correction rather than a full one.
                        roll = signed * Mathf.Max(kneeHintWeight, hockHintWeight);
                        Quaternion rollQ = Quaternion.AngleAxis(roll, n);
                        nRoot = rollQ * nRoot; nKnee = rollQ * nKnee; nHock = rollQ * nHock;
                        m = rollQ * m;
                    }
                }
            }

            pose.Root = nRoot;
            pose.Knee = nKnee;
            pose.Hock = nHock;
            pose.Tip = tRot;           // last, exactly as the stock two-bone solver does
            pose.PlaneNormal = m;
            pose.HintRollDegrees = roll;
            pose.Valid = true;
            return pose;
        }
    }
}
