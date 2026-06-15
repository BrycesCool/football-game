using NUnit.Framework;
using UnityEngine;
using Gridiron;

namespace Gridiron.Tests
{
    /// <summary>PRD §14: PredictPosition correctness for multi-segment routes incl. speed multipliers and post-route extrapolation (±0.05 m).</summary>
    public class RoutePathTests
    {
        const float Tol = 0.05f;

        static RouteWaypoint WP(float x, float z, float mult = 1f, bool isBreak = false)
            => new RouteWaypoint { offset = new Vector2(x, z), speedMultiplier = mult, isBreak = isBreak };

        static RoutePath Make(Vector3 start, RouteWaypoint[] wps, float baseSpeed = 7.5f, bool hold = false, bool mirror = false)
            => new RoutePath(start, wps, baseSpeed, hold, mirror);

        [Test]
        public void Predict_StraightSegment_MatchesAnalytic()
        {
            var path = Make(Vector3.zero, new[] { WP(0f, 20f) }, 8f);
            Vector3 p = path.PredictPosition(0.5f); // well before the waypoint
            Assert.AreEqual(0f, p.x, Tol);
            Assert.AreEqual(4f, p.z, Tol);
        }

        [Test]
        public void Predict_MatchesAdvance_MultiSegmentWithSpeedMultipliers()
        {
            var wps = new[] { WP(0f, 9.144f, 0.9f, true), WP(-5.4864f, 7.3152f, 1f) };
            var predictPath = Make(new Vector3(10f, 0f, -20f), wps);
            var advancePath = Make(new Vector3(10f, 0f, -20f), wps);

            foreach (float t in new[] { 0.4f, 0.9f, 1.3f, 1.8f, 2.5f })
            {
                Vector3 predicted = predictPath.PredictPosition(t);
                // advance a fresh copy in small steps to time t
                var sim = Make(new Vector3(10f, 0f, -20f), wps);
                float acc = 0f;
                while (acc < t) { float dt = Mathf.Min(0.011f, t - acc); sim.Advance(dt); acc += dt; }
                Assert.AreEqual(sim.Position.x, predicted.x, Tol, "x @ t=" + t);
                Assert.AreEqual(sim.Position.z, predicted.z, Tol, "z @ t=" + t);
            }
            // keep predictPath/advancePath referenced
            Assert.NotNull(predictPath); Assert.NotNull(advancePath);
        }

        [Test]
        public void Predict_ExtrapolatesPastFinalWaypoint_AlongFinalDirection()
        {
            float baseSpeed = 7.5f;
            var path = Make(Vector3.zero, new[] { WP(0f, 9.144f) }, baseSpeed);
            // waypoint boundary (reach radius 0.3) hit at (9.144-0.3)/7.5 s
            float tBoundary = (9.144f - RoutePath.ReachRadius) / baseSpeed;
            float t = tBoundary + 1f;
            Vector3 p = path.PredictPosition(t);
            float expectedZ = (9.144f - RoutePath.ReachRadius) + baseSpeed * 1f;
            Assert.AreEqual(0f, p.x, Tol);
            Assert.AreEqual(expectedZ, p.z, Tol);
        }

        [Test]
        public void Predict_ClampsAtHoldPoint_ForCurlComeback()
        {
            var wps = new[] { WP(0f, 8.2296f, 0.9f, true), WP(0f, 6.4008f, 0.85f) };
            var path = Make(Vector3.zero, wps, 7.5f, hold: true);
            Vector3 a = path.PredictPosition(8f);
            Vector3 b = path.PredictPosition(20f);
            Assert.AreEqual(a.x, b.x, 1e-3f);
            Assert.AreEqual(a.z, b.z, 1e-3f);
            // hold point is near the final waypoint (within reach radius + tol)
            Assert.AreEqual(6.4008f, a.z, RoutePath.ReachRadius + Tol);
        }

        [Test]
        public void Mirror_FlipsX()
        {
            var wps = new[] { WP(-5f, 8f) };
            var normal = Make(Vector3.zero, wps, 7.5f);
            var mirrored = Make(Vector3.zero, wps, 7.5f, mirror: true);
            Vector3 pn = normal.PredictPosition(1.2f);
            Vector3 pm = mirrored.PredictPosition(1.2f);
            Assert.AreEqual(pn.x, -pm.x, 1e-3f);
            Assert.AreEqual(pn.z, pm.z, 1e-3f);
        }

        [Test]
        public void SpeedMultiplier_SlowsSegment()
        {
            var fast = Make(Vector3.zero, new[] { WP(0f, 20f, 1f) }, 7.5f);
            var slow = Make(Vector3.zero, new[] { WP(0f, 20f, 0.5f) }, 7.5f);
            Assert.AreEqual(7.5f, fast.PredictPosition(1f).z, Tol);
            Assert.AreEqual(3.75f, slow.PredictPosition(1f).z, Tol);
        }

        /// <summary>
        /// Catch PRD AC5: with the tuned (non-Instant) MotionTuning — accel, brake, cut decel —
        /// PredictPosition must match Advance within ±0.05 m. The lead-throw solver depends on this:
        /// if prediction and movement ever diverge, throws stop landing on the receiver's path.
        /// </summary>
        [Test]
        public void Predict_MatchesAdvance_WithTunedMotion()
        {
            var tuning = new RoutePath.MotionTuning { accel = 8f, brake = 14f, cutSpeedFactor = 0.6f, cutDecelRadius = 2.5f };
            var wps = new[] { WP(0f, 9.144f, 0.9f, isBreak: true), WP(-5.4864f, 7.3152f, 1f) };
            var predictPath = new RoutePath(new Vector3(10f, 0f, -20f), wps, 7.5f, false, false, tuning);

            foreach (float t in new[] { 0.3f, 0.8f, 1.4f, 2.0f, 2.8f, 3.5f })
            {
                Vector3 predicted = predictPath.PredictPosition(t);
                var sim = new RoutePath(new Vector3(10f, 0f, -20f), wps, 7.5f, false, false, tuning);
                float acc = 0f;
                while (acc < t) { float dt = Mathf.Min(0.011f, t - acc); sim.Advance(dt); acc += dt; }
                Assert.AreEqual(sim.Position.x, predicted.x, Tol, "x @ t=" + t);
                Assert.AreEqual(sim.Position.z, predicted.z, Tol, "z @ t=" + t);
            }
        }
    }
}