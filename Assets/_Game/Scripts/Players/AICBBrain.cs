using System.Collections.Generic;
using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// v1 man-coverage AI (PRD §8.4): BACKPEDAL → MIRROR → BALL_ATTACK, with BEATEN_RECOVER.
    /// The reaction-delay ring buffer (+ break penalty) is the key separation-creating knob.
    /// </summary>
    public class AICBBrain : ICBBrain
    {
        enum Phase { LinedUp, Backpedal, Mirror, BallAttack, BeatenRecover }

        Phase phase = Phase.LinedUp;
        readonly List<(float time, Vector3 pos)> wrHistory = new List<(float, Vector3)>(512);
        float extraDelay;
        float wrStartZ;
        bool throwReachable;
        ThrowData activeThrow;
        bool hasThrow;
        Vector3 smoothedWRDir = Vector3.forward;

        public void ResetForPlay(CBController cb)
        {
            phase = Phase.LinedUp;
            wrHistory.Clear();
            extraDelay = 0f;
            hasThrow = false;
            throwReachable = false;
            smoothedWRDir = Vector3.forward;
        }

        public void OnSnap(CBController cb)
        {
            phase = Phase.Backpedal;
            wrStartZ = cb.wr.transform.position.z;
        }

        public void OnThrow(CBController cb, ThrowData data)
        {
            activeThrow = data;
            hasThrow = true;
            // Can we drive on the ball before/at the catch window? (cbSpeed × 1.05 burst, §8.4)
            Vector3 target = data.targetPoint; target.y = 0f;
            Vector3 self = cb.transform.position; self.y = 0f;
            float reachTime = Vector3.Distance(self, target) / (cb.Speed * cb.Rules.cbBurstMultiplier);
            throwReachable = reachTime <= data.flightTime + 0.15f;
            phase = Phase.BallAttack;
        }

        public void NotifyWRBreak()
        {
            // No-op: there is no fake/break animation, so the CB must NOT bite or take a reaction
            // penalty on the WR's cut — he simply keeps following him in man coverage.
        }

        float cbBreakPenalty;
        const float followDelay = 0.05f;   // tight man-coverage tracking lag (replaces diff.reactionDelay)
        const float coverTrail = 0.5f;      // how far off the WR's hip the CB trails
        const float coverBallSide = 0.35f;  // ball-side leverage

        public CBCommand Tick(CBController cb, float dt)
        {
            var diff = cb.Difficulty;
            cbBreakPenalty = diff.breakPenalty;
            Vector3 wrPos = cb.wr.transform.position;
            wrHistory.Add((Time.time, wrPos));
            if (wrHistory.Count > 500) wrHistory.RemoveRange(0, 100);

            // Break penalty decays so it feels like recovery, not a permanent handicap.
            if (extraDelay > 0f) extraDelay = Mathf.Max(0f, extraDelay - dt * (diff.breakPenalty / 0.6f + 0.01f));

            // Tight coverage: small fixed follow delay, and NO break penalty (extraDelay stays 0).
            float delay = Mathf.Min(diff.reactionDelay, followDelay);
            Vector3 delayed = Sample(Time.time - delay, wrPos);
            Vector3 rawDir = EstimateDirection(Time.time - delay);
            if (rawDir.sqrMagnitude > 0.0004f)
                smoothedWRDir = Vector3.Slerp(smoothedWRDir, rawDir.normalized, Mathf.Clamp01(10f * dt));
            Vector3 delayedDir = smoothedWRDir;

            float separation = Vector3.Distance(Flat(cb.transform.position), Flat(wrPos));
            var cmd = new CBCommand { maxSpeed = cb.Speed, backpedal = false };

            switch (phase)
            {
                case Phase.LinedUp:
                    cmd.moveTarget = cb.transform.position;
                    break;

                case Phase.Backpedal:
                {
                    // Maintain cushion, match WR's lateral X (§8.4).
                    cmd.moveTarget = new Vector3(delayed.x, 0f, wrPos.z + cb.Rules.cbCushion);
                    cmd.backpedal = true;
                    cmd.maxSpeed = Mathf.Max(cb.Speed, Flat(cb.wr.Velocity).magnitude * 1.05f);
                    if (wrPos.z - wrStartZ >= 0.6f * cb.Rules.cbCushion) phase = Phase.Mirror;
                    break;
                }

                case Phase.Mirror:
                {
                    Vector3 ballSide = Flat(cb.BallPosition - delayed);
                    ballSide = ballSide.sqrMagnitude > 1e-4f ? ballSide.normalized : Vector3.back;
                    Vector3 behind = delayedDir.sqrMagnitude > 1e-4f ? -delayedDir.normalized : Vector3.forward;
                    // Sit in the WR's hip pocket (tight man coverage) with a little ball-side leverage.
                    cmd.moveTarget = delayed + behind * coverTrail + ballSide * coverBallSide;
                    // Match the WR's speed (plus a hair) so he can't simply out-run the coverage.
                    cmd.maxSpeed = Mathf.Max(cb.Speed, Flat(cb.wr.Velocity).magnitude * 1.08f);
                    if (separation > cb.Rules.beatenSeparation) phase = Phase.BeatenRecover;
                    break;
                }

                case Phase.BeatenRecover:
                {
                    // Pure pursuit of a stale route prediction — recovery speed, not psychic AI (§8.4).
                    cmd.moveTarget = cb.wr.routeRunner.PredictPosition(cb.Rules.beatenPredictionTime);
                    if (separation <= cb.Rules.beatenSeparation * 0.6f) phase = Phase.Mirror;
                    break;
                }

                case Phase.BallAttack:
                {
                    if (hasThrow && throwReachable)
                    {
                        Vector3 t = activeThrow.targetPoint; t.y = 0f;
                        cmd.moveTarget = t;
                        cmd.maxSpeed = cb.Speed * cb.Rules.cbBurstMultiplier;
                    }
                    else
                    {
                        cmd.moveTarget = delayed; // can't get there: stay glued to the WR
                        cmd.maxSpeed = cb.Speed * cb.Rules.cbBurstMultiplier;
                    }
                    break;
                }
            }
            return cmd;
        }

        Vector3 Sample(float t, Vector3 fallback)
        {
            if (wrHistory.Count == 0) return fallback;
            if (t <= wrHistory[0].time) return wrHistory[0].pos;
            for (int i = wrHistory.Count - 1; i >= 0; i--)
            {
                if (wrHistory[i].time <= t) return wrHistory[i].pos;
            }
            return wrHistory[wrHistory.Count - 1].pos;
        }

        Vector3 EstimateDirection(float t)
        {
            Vector3 a = Sample(t - 0.15f, Vector3.zero);
            Vector3 b = Sample(t, Vector3.zero);
            return Flat(b - a);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}