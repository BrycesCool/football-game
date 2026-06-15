using NUnit.Framework;
using UnityEngine;
using Gridiron;

namespace Gridiron.Tests
{
    /// <summary>PRD §14: launched velocity lands within 0.1 m of target P at time T (simulated analytically).</summary>
    public class BallisticsSolverTests
    {
        const float GEff = 9.81f * 1.15f;

        [Test]
        public void SolveVelocity_ArrivesExactlyAtTarget()
        {
            var release = new Vector3(0f, 1.8f, -24f);
            var targets = new[]
            {
                new Vector3(5f, 1.3f, -10f),
                new Vector3(-8f, 1.3f, 6f),
                new Vector3(0f, 1.3f, 20f),
                new Vector3(12f, 2.2f, -20f)
            };
            foreach (var target in targets)
            {
                foreach (float T in new[] { 0.5f, 1.0f, 2.0f })
                {
                    Vector3 v0 = BallisticsSolver.SolveVelocity(release, target, T, GEff);
                    Vector3 arrival = BallisticsSolver.PointAtTime(release, v0, GEff, T);
                    Assert.Less(Vector3.Distance(arrival, target), 0.1f,
                        "target " + target + " T=" + T);
                }
            }
        }

        [Test]
        public void SolveVelocity_MatchesDiscreteIntegration()
        {
            // Semi-implicit Euler at fixed 0.02 s, approximating the PhysX step.
            var release = new Vector3(0f, 1.8f, 0f);
            var target = new Vector3(6f, 1.3f, 18f);
            const float T = 1.2f;
            Vector3 v0 = BallisticsSolver.SolveVelocity(release, target, T, GEff);

            Vector3 pos = release, vel = v0;
            const float dt = 0.02f;
            int steps = Mathf.RoundToInt(T / dt);
            for (int i = 0; i < steps; i++)
            {
                vel += Vector3.down * GEff * dt;
                pos += vel * dt;
            }
            // First-order integration drift is bounded; in-engine M1 check is the authoritative 0.1 m test.
            Assert.Less(Vector3.Distance(pos, target), 0.35f, "discrete integration drift too large: " + pos);
        }

        [Test]
        public void SolveLeadTime_ConvergesToFixedPoint()
        {
            var release = new Vector3(0f, 1.8f, -24.36f);
            // Receiver running straight downfield at 7.5 m/s from z = -14
            System.Func<float, Vector3> predict = t => new Vector3(10f, 1.3f, -14f + 7.5f * t);
            const float speed = 19f;
            float T = BallisticsSolver.SolveLeadTime(release, predict, speed, 0.4f, 1.6f);
            Vector3 P = predict(T);
            float d = BallisticsSolver.HorizontalDistance(release, P);
            float expected = Mathf.Clamp(d / speed, 0.4f, 1.6f);
            Assert.AreEqual(expected, T, 0.02f, "lead solve did not converge");
        }

        [Test]
        public void SolveLeadTime_RespectsClamps()
        {
            var release = Vector3.up * 1.8f;
            System.Func<float, Vector3> nearTarget = t => new Vector3(1f, 1.3f, 1f);
            System.Func<float, Vector3> farTarget = t => new Vector3(0f, 1.3f, 60f);
            Assert.AreEqual(0.4f, BallisticsSolver.SolveLeadTime(release, nearTarget, 19f, 0.4f, 1.6f), 1e-3f);
            Assert.AreEqual(1.6f, BallisticsSolver.SolveLeadTime(release, farTarget, 19f, 0.4f, 1.6f), 1e-3f);
        }
    }
}