using System;
using NUnit.Framework;
using UnityEngine;

namespace SpringIK.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="SpringIKMath"/>. Every sweep also asserts that it reached the regime
    /// it tests (the VACUITY checks), so no test can pass by never running its branch.
    /// </summary>
    public class SpringIKMathTests
    {
        // A: equal bones. B: unequal, where reach depends on bias - a class of bug A alone cannot see.
        const float A1 = 0.5f, A2 = 0.5f, A3 = 0.5f;
        const float B1 = 0.5f, B2 = 0.5f, B3 = 1.0f;

        static readonly float[] Biases = { -1f, -0.9f, -0.5f, -0.25f, 0f, 0.25f, 0.5f, 0.9f, 1f };

        // The float32 solve reaches ~1e-6 m of tip error; nothing is asserted tighter than that.
        const float AngleTol = 2e-3f;    // radians, on angles that pass through the root find
        const float RatioTol = 1e-4f;    // the ratio is wKnee:wHock by construction, so it is exact
        const float ReachTol = 1e-4f;    // metres, on |reached - requested|

        /// <summary>Forward kinematics: tip distance for opposed bends of the given magnitudes.</summary>
        static float Chord(float l1, float l2, float l3, float kneeBend, float hockBend)
        {
            // Bone 1 along +x; the knee turns +kneeBend, the hock -hockBend.
            float a1 = 0f;
            float a2 = a1 + kneeBend;
            float a3 = a2 - hockBend;
            float x = l1 * Mathf.Cos(a1) + l2 * Mathf.Cos(a2) + l3 * Mathf.Cos(a3);
            float y = l1 * Mathf.Sin(a1) + l2 * Mathf.Sin(a2) + l3 * Mathf.Sin(a3);
            return Mathf.Sqrt(x * x + y * y);
        }

        /// <summary>The parallelogram law (bone 1 parallel to bone 3), which bias 0 must reproduce.</summary>
        static float ParallelogramBend(float l1, float l2, float l3, float d)
        {
            float p = l1 + l3, q = l2;
            float interior = Mathf.Acos(Mathf.Clamp((p * p + q * q - d * d) / (2f * p * q), -1f, 1f));
            return Mathf.PI - interior;
        }

        /// <summary>Lowest distance reachable at every bias: the floor of the window the ratio test runs on.</summary>
        static float BiasIntersectionFloor(float l1, float l2, float l3)
        {
            float worst = 0f;
            foreach (var b in Biases) worst = Mathf.Max(worst, SpringIKMath.FoldLimit(l1, l2, l3, b));
            return worst;
        }

        [Test]
        public void AtOrBeyondFullExtension_ChainIsExactlyStraight()
        {
            int straightened = 0;
            foreach (var b in Biases)
            {
                foreach (var d in new[] { 1.5f, 1.5000001f, 2f, 50f })
                {
                    var r = SpringIKMath.Solve(A1, A2, A3, b, d);
                    Assert.AreEqual(0f, r.TotalBend, "theta must be exactly 0 at/past extension");
                    Assert.AreEqual(0f, r.KneeBend);
                    Assert.AreEqual(0f, r.HockBend);
                    if (d > 1.5f) { Assert.IsTrue(r.OverReached, "over-reach flag"); straightened++; }
                }
            }
            Assert.Greater(straightened, 0, "VACUITY: no over-reach case was actually exercised");
        }

        [Test]
        public void Bias0ReproducesParallelogramLaw()
        {
            foreach (var g in new[] { new[] { A1, A2, A3 }, new[] { B1, B2, B3 } })
            {
                float t = g[0] + g[1] + g[2];
                float fold = SpringIKMath.FoldLimit(g[0], g[1], g[2], 0f);
                float worst = 0f;
                int n = 0;

                // Interior only: near the window's ends dTheta/dD -> -inf and the comparison stops
                // testing the law.
                for (int i = 1; i < 40; i++)
                {
                    float d = Mathf.Lerp(fold, t, i / 40f);
                    var r = SpringIKMath.Solve(g[0], g[1], g[2], 0f, d);
                    float expect = ParallelogramBend(g[0], g[1], g[2], d);

                    Assert.AreEqual(r.KneeBend, r.HockBend, RatioTol, "bias 0 must bend both joints equally");

                    worst = Mathf.Max(worst, Mathf.Abs(r.KneeBend - expect));
                    n++;
                }
                Assert.Greater(n, 30, "VACUITY: the sweep did not run");
                Assert.Less(worst, AngleTol,
                    $"worst |kneeBend - parallelogram| = {worst:E3} rad on {g[0]}/{g[1]}/{g[2]}");
            }
        }

        [Test]
        public void FixedBiasHoldsRatioAcrossCompression()
        {
            foreach (var g in new[] { new[] { A1, A2, A3 }, new[] { B1, B2, B3 } })
            {
                float t = g[0] + g[1] + g[2];
                float floor = BiasIntersectionFloor(g[0], g[1], g[2]);
                Assert.Less(floor, t, "VACUITY: the bias-intersection window is empty");

                foreach (var b in Biases)
                {
                    if (Mathf.Abs(Mathf.Abs(b) - 1f) < 1e-6f) continue;   // one weight is 0; ratio undefined
                    float expected = (1f - b) / (1f + b);
                    float spread = 0f;
                    int bent = 0;

                    for (int i = 1; i < 30; i++)
                    {
                        float d = Mathf.Lerp(floor, t, i / 30f);
                        var r = SpringIKMath.Solve(g[0], g[1], g[2], b, d);
                        if (r.TotalBend <= 1e-4f) continue;               // straight: the ratio carries no information
                        bent++;
                        spread = Mathf.Max(spread, Mathf.Abs(r.KneeBend / r.HockBend - expected));
                    }

                    Assert.Greater(bent, 20, $"VACUITY: bias {b} produced almost no bend to measure");
                    Assert.Less(spread, RatioTol, $"ratio drifted {spread:E3} at bias {b}");
                }
            }
        }

        [Test]
        public void BelowFoldLimit_ClampsAndHoldsRatio()
        {
            int clamped = 0;
            foreach (var b in Biases)
            {
                float fold = SpringIKMath.FoldLimit(B1, B2, B3, b);
                if (fold <= 1e-4f) continue;                              // nothing below it to test
                var atFold = SpringIKMath.Solve(B1, B2, B3, b, fold);
                foreach (var frac in new[] { 0.9f, 0.5f, 0.1f, 0f })
                {
                    var r = SpringIKMath.Solve(B1, B2, B3, b, fold * frac);
                    Assert.IsTrue(r.UnderReached, "under-reach flag must fire");
                    Assert.AreEqual(fold, r.SolvedDistance, 1e-5f, "distance must clamp to the fold limit");
                    Assert.AreEqual(atFold.TotalBend, r.TotalBend, AngleTol, "pose must hold at the fold");
                    Assert.IsFalse(float.IsNaN(r.TotalBend));
                    clamped++;
                }
            }
            Assert.Greater(clamped, 0, "VACUITY: the under-reach clamp never bound");
        }

        [Test]
        public void BiasMovesShareMonotonically_WithZeroReversals()
        {
            float floor = BiasIntersectionFloor(B1, B2, B3);
            float t = B1 + B2 + B3;
            foreach (var frac in new[] { 0.25f, 0.5f, 0.75f })
            {
                float d = Mathf.Lerp(floor, t, frac);
                float prev = float.NaN;
                int reversals = 0, samples = 0;
                float lo = float.MaxValue, hi = float.MinValue;

                for (int i = 0; i <= 200; i++)
                {
                    float b = Mathf.Lerp(-1f, 1f, i / 200f);
                    var r = SpringIKMath.Solve(B1, B2, B3, b, d);
                    float total = r.KneeBend + r.HockBend;
                    if (total <= 1e-5f) continue;
                    float share = r.KneeBend / total;
                    lo = Mathf.Min(lo, share); hi = Mathf.Max(hi, share);
                    if (!float.IsNaN(prev) && share > prev + 1e-6f) reversals++;   // share must DECREASE with bias
                    prev = share; samples++;
                }

                Assert.Greater(samples, 150, "VACUITY: the bias sweep produced almost no samples");
                Assert.Greater(hi - lo, 0.8f, "VACUITY: the share barely moved, so monotonicity is untested");
                Assert.AreEqual(0, reversals, $"share reversed direction {reversals} times at d={d}");
            }
        }

        /// <summary>
        /// The bend angles turn once in bias; only the share is monotone. Pinned so nobody "fixes" the
        /// solver to satisfy an angle-monotonicity assertion that would be wrong.
        /// </summary>
        [Test]
        public void BendAnglesAreNotMonotoneInBias_ByDesign()
        {
            float d = Mathf.Lerp(BiasIntersectionFloor(A1, A2, A3), A1 + A2 + A3, 0.4f);
            float prev = -1f; int turningPoints = 0; bool rising = true; int samples = 0;
            for (int i = 0; i <= 200; i++)
            {
                float b = Mathf.Lerp(-1f, 1f, i / 200f);
                float knee = SpringIKMath.Solve(A1, A2, A3, b, d).KneeBend;
                if (prev >= 0f)
                {
                    bool up = knee > prev + 1e-6f;
                    if (samples > 2 && up != rising && Mathf.Abs(knee - prev) > 1e-6f) { turningPoints++; rising = up; }
                    else if (Mathf.Abs(knee - prev) > 1e-6f) rising = up;
                }
                prev = knee; samples++;
            }
            Assert.Greater(samples, 150, "VACUITY: sweep did not run");
            Assert.GreaterOrEqual(turningPoints, 1,
                "the knee angle is expected to turn in bias; if it no longer does, the bias law has changed");
        }

        [Test]
        public void SolveReachesTheRequestedDistance()
        {
            float worst = 0f; int inWindow = 0;
            foreach (var g in new[] { new[] { A1, A2, A3 }, new[] { B1, B2, B3 }, new[] { 0.5f, 1.4f, 0.5f } })
            {
                float t = g[0] + g[1] + g[2];
                foreach (var b in Biases)
                {
                    float fold = SpringIKMath.FoldLimit(g[0], g[1], g[2], b);
                    for (int i = 1; i < 30; i++)
                    {
                        float d = Mathf.Lerp(fold, t, i / 30f);
                        var r = SpringIKMath.Solve(g[0], g[1], g[2], b, d);
                        float reached = Chord(g[0], g[1], g[2], r.KneeBend, r.HockBend);
                        worst = Mathf.Max(worst, Mathf.Abs(reached - d));
                        inWindow++;
                    }
                }
            }
            Assert.Greater(inWindow, 500, "VACUITY: too few samples");
            Assert.Less(worst, ReachTol, $"worst tip error {worst:E3} m");
        }

        [Test]
        public void NoNaNEver_AcrossHostileInput()
        {
            var rng = new System.Random(12345);
            float[] nasty = { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1e-9f, 1e6f };
            int degenerate = 0, solved = 0;

            for (int i = 0; i < 20000; i++)
            {
                float Pick() => rng.NextDouble() < 0.25
                    ? nasty[rng.Next(nasty.Length)]
                    : (float)(rng.NextDouble() * 3.0);

                float l1 = Pick(), l2 = Pick(), l3 = Pick();
                float bias = rng.NextDouble() < 0.25 ? nasty[rng.Next(nasty.Length)] : (float)(rng.NextDouble() * 4.0 - 2.0);
                float d = Pick();

                var r = SpringIKMath.Solve(l1, l2, l3, bias, d);
                Assert.IsFalse(float.IsNaN(r.TotalBend), $"NaN theta at {l1},{l2},{l3} bias {bias} d {d}");
                Assert.IsFalse(float.IsNaN(r.KneeBend) || float.IsNaN(r.HockBend));
                Assert.IsFalse(float.IsNaN(r.FoldLimit) || float.IsNaN(r.SolvedDistance));
                Assert.IsFalse(float.IsInfinity(r.TotalBend));
                if (r.Degenerate) degenerate++; else solved++;
            }
            Assert.Greater(degenerate, 100, "VACUITY: the degenerate branch was never taken");
            Assert.Greater(solved, 100, "VACUITY: the solving branch was never taken");
        }

        [Test]
        public void SolveIsBitDeterministic()
        {
            var rng = new System.Random(99);
            for (int i = 0; i < 2000; i++)
            {
                float l1 = (float)(rng.NextDouble() * 2 + 0.1), l2 = (float)(rng.NextDouble() * 2 + 0.1),
                      l3 = (float)(rng.NextDouble() * 2 + 0.1);
                float b = (float)(rng.NextDouble() * 2 - 1);
                float d = (float)(rng.NextDouble() * (l1 + l2 + l3));
                var x = SpringIKMath.Solve(l1, l2, l3, b, d);
                var y = SpringIKMath.Solve(l1, l2, l3, b, d);
                Assert.AreEqual(BitConverter.SingleToInt32Bits(x.TotalBend),
                                BitConverter.SingleToInt32Bits(y.TotalBend), "theta not bit-identical");
                Assert.AreEqual(BitConverter.SingleToInt32Bits(x.KneeBend),
                                BitConverter.SingleToInt32Bits(y.KneeBend));
            }
        }

        [Test]
        public void FoldLimitClosedFormMatchesNumericMinimumOfReach2()
        {
            float worst = 0f; int n = 0;
            foreach (var g in new[] { new[] { A1, A2, A3 }, new[] { B1, B2, B3 },
                                      new[] { 0.5f, 1.4f, 0.5f }, new[] { 0.5f, 0.5f, 2.0f } })
            {
                foreach (var b in Biases)
                {
                    SpringIKMath.BiasWeights(b, out float wk, out float wh);
                    float tMax = SpringIKMath.ThetaMax(wk, wh);
                    float min = float.MaxValue;
                    for (int i = 0; i <= 4000; i++)
                        min = Mathf.Min(min, SpringIKMath.Reach2(g[0], g[1], g[2], wk, wh, tMax * i / 4000f));
                    float numeric = Mathf.Sqrt(Mathf.Max(0f, min));
                    worst = Mathf.Max(worst, Mathf.Abs(numeric - SpringIKMath.FoldLimit(g[0], g[1], g[2], b)));
                    n++;
                }
            }
            Assert.Greater(n, 30, "VACUITY: no cases run");
            Assert.Less(worst, 1e-4f, $"closed form vs numeric fold: worst {worst:E3}");
        }

        [Test]
        public void ChainInnerLimitIsPolygonInequality_NotTheNaiveForm()
        {
            // Eight geometries, each with its correct inner limit.
            float[][] cases =
            {
                new[] { 0.5f, 0.5f, 0.5f, 0.0f }, new[] { 0.5f, 0.5f, 1.0f, 0.0f },
                new[] { 0.5f, 0.5f, 2.0f, 1.0f }, new[] { 1.0f, 0.2f, 0.3f, 0.5f },
                new[] { 0.3f, 1.0f, 0.2f, 0.5f }, new[] { 0.2f, 0.3f, 1.0f, 0.5f },
                new[] { 1.0f, 1.0f, 1.0f, 0.0f }, new[] { 2.0f, 0.5f, 0.5f, 1.0f },
            };
            int differed = 0;
            foreach (var c in cases)
            {
                float got = SpringIKMath.ChainInnerLimit(c[0], c[1], c[2]);
                Assert.AreEqual(c[3], got, 1e-6f, $"inner limit for {c[0]}/{c[1]}/{c[2]}");
                if (Mathf.Abs(Mathf.Abs(c[0] - c[1] - c[2]) - c[3]) > 1e-6f) differed++;
            }
            // The cases only tell the two forms apart if the naive one gets most of them wrong.
            Assert.AreEqual(6, differed, "the naive |l1-l2-l3| must disagree on exactly 6 of the 8 cases");
        }

        /// <summary>Monotone Reach2 is what makes the bisection safe.</summary>
        [Test]
        public void Reach2IsMonotoneNonIncreasingInTheta()
        {
            var rng = new System.Random(7);
            int violations = 0, steps = 0;
            for (int c = 0; c < 400; c++)
            {
                float l1 = (float)(rng.NextDouble() * 3 + 0.05), l2 = (float)(rng.NextDouble() * 3 + 0.05),
                      l3 = (float)(rng.NextDouble() * 3 + 0.05);
                float b = (float)(rng.NextDouble() * 2 - 1);
                SpringIKMath.BiasWeights(b, out float wk, out float wh);
                float tMax = SpringIKMath.ThetaMax(wk, wh);
                float prev = SpringIKMath.Reach2(l1, l2, l3, wk, wh, 0f);
                for (int i = 1; i <= 400; i++)
                {
                    float v = SpringIKMath.Reach2(l1, l2, l3, wk, wh, tMax * i / 400f);
                    if (v > prev + 1e-4f) violations++;
                    prev = v; steps++;
                }
            }
            Assert.Greater(steps, 100000, "VACUITY: too few steps evaluated");
            Assert.AreEqual(0, violations, $"{violations} monotonicity violations of {steps}");
        }

        [Test]
        public void Reach2AtZeroIsChainLengthSquared()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 500; i++)
            {
                float l1 = (float)(rng.NextDouble() * 3), l2 = (float)(rng.NextDouble() * 3), l3 = (float)(rng.NextDouble() * 3);
                float b = (float)(rng.NextDouble() * 2 - 1);
                SpringIKMath.BiasWeights(b, out float wk, out float wh);
                float t = l1 + l2 + l3;
                Assert.AreEqual(t * t, SpringIKMath.Reach2(l1, l2, l3, wk, wh, 0f), 1e-4f * Mathf.Max(1f, t * t));
            }
        }
    }
}
