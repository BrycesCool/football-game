using UnityEngine;

namespace Gridiron
{
    [System.Serializable]
    public struct ThrowTuning
    {
        public float throwSpeed;        // m/s horizontal (PRD §7.3)
        public float tMin;
        public float tMax;
        public float inaccuracyRadius;  // m, horizontal disc
    }

    /// <summary>All match-level tunables (PRD §10, §17). Designer-editable, no magic numbers in code.</summary>
    [CreateAssetMenu(menuName = "Gridiron/Match Rules", fileName = "MatchRules")]
    public class MatchRules : ScriptableObject
    {
        [Header("Match structure (§10)")]
        public int drivesPerMatch = 3;
        public int pointsPerTD = 7;
        public float startOwnYardLine = 25f;

        [Header("Timing (§5.1, §8.2, §11.3)")]
        public float playClockSeconds = 6f;
        public float racWindowSeconds = 1f;
        public float resultBannerSeconds = 2.5f;
        public float sackPenaltyYards = 5f;

        [Header("Throws (§7.3, §7.4)")]
        public ThrowTuning bullet = new ThrowTuning { throwSpeed = 19f, tMin = 0.4f, tMax = 1.6f, inaccuracyRadius = 0.35f };
        public ThrowTuning lob = new ThrowTuning { throwSpeed = 12f, tMin = 0.8f, tMax = 2.6f, inaccuracyRadius = 0.6f };
        public float lobHoldThreshold = 0.25f;
        public float gravityScale = 1.15f;
        public float midBreakInaccuracyMultiplier = 1.5f;
        public float chestHeight = 1.3f;
        public float throwReleaseFallback = 1.4f; // safety launch if no ReleaseBall anim event fires; keep ABOVE the event time (frame 37 of Throw ≈ 1.23 s) or it preempts the animation

        [Header("WR (§8.2)")]
        public float wrBurstMultiplier = 1.1f;
        public float catchRadius = 1.1f;
        [Range(0f, 1f)] public float baseCatch = 0.95f;
        [Range(0f, 1f)] public float contestPenalty = 0.35f;
        public float contestDistance = 1.2f;
        [Range(0f, 1f)] public float highBallPenalty = 0.15f;
        public float highBallHeight = 2.2f;
        public float tackleDistance = 0.8f;

        [Header("CB (§8.4)")]
        public float cbCushion = 4.57f;
        public float coverOffsetBehind = 0.8f;
        public float coverOffsetBallSide = 0.3f;
        public float cbCatchRadius = 1.0f;
        public float cbBurstMultiplier = 1.05f;
        public float beatenSeparation = 4f;
        public float beatenPredictionTime = 0.5f;
        public float softPushMaxSpeed = 2f;

        [Header("Locomotion feel (audit C1/C2/H1)")]
        [Tooltip("m/s² — how fast players gain speed")]
        public float accel = 8f;
        [Tooltip("m/s² — how fast players shed speed / reverse")]
        public float brake = 14f;
        [Tooltip("deg/s base turn rate (scales down with speed)")]
        public float turnRateDeg = 540f;
        [Tooltip("deg/s turn rate during the plant window at route breaks")]
        public float plantTurnRateDeg = 880f;
        [Tooltip("s — duration of the fast-turn window after a break")]
        public float plantTurnWindow = 0.35f;
        [Range(0.1f, 1f), Tooltip("speed factor at a 90° direction change")]
        public float minTurnSpeedFactor = 0.45f;
        [Range(0.1f, 1f), Tooltip("route speed factor through break waypoints")]
        public float cutSpeedFactor = 0.6f;
        [Tooltip("m — decel zone approaching a break waypoint")]
        public float cutDecelRadius = 2.5f;

        [Header("Animation matching (audit C1/C3)")]
        [Tooltip("m/s — measured ground speed of the Running clip (written by Gridiron > Setup Animations)")]
        public float runClipNaturalSpeed = 3.6f;
        [Tooltip("s — Animator float damp time for Speed/MoveX/MoveZ")]
        public float animSpeedDamp = 0.12f;
        [Tooltip("cap on run-clip playback multiplier")]
        public float maxAnimSpeedMult = 2.2f;

        [Header("WR pursuit / RAC (audit H4/H6/L2)")]
        public float pursuitStopDistance = 0.15f;
        public float racDodgeOffset = 0.35f;
        [Tooltip("m — CB lateral window that triggers a dodge")]
        public float racDodgeWindow = 3f;
        [Tooltip("s — minimum time between dodge-side re-evaluations")]
        public float racDodgeInterval = 0.3f;

        [Header("CB feel (audit H2/H3/L2)")]
        [Tooltip("s — SmoothDamp time on the brain's move target")]
        public float cbTargetSmoothTime = 0.1f;
        [Tooltip("m — CB/WR capsule contact distance (2 × radius)")]
        public float cbWRMinDistance = 0.7f;
        [Tooltip("s — speed-cap window when flipping backpedal → run")]
        public float hipFlipDuration = 0.25f;
        [Range(0.1f, 1f)] public float hipFlipSpeedFactor = 0.6f;

        [Header("Catch & tackle presentation (audit H5/H7)")]
        [Tooltip("s — ball lerp from flight position into the hand (Catch PRD R5)")]
        public float catchAttachLerp = 0.12f;
        [Tooltip("s — complete the catch anyway if no HandsOnBall anim event")]
        public float handsOnBallFallback = 0.3f;
        [Tooltip("s — tackle steer window before the result banner")]
        public float tackleSequenceSeconds = 0.35f;

        [Header("Receiver catching (Catch PRD §4)")]
        [Tooltip("m — reach envelope radius around the chest; the only place a ball can be secured (R3)")]
        public float catchReach = 0.55f;
        [Tooltip("m — extra reach when catching in stride (velocity within 45° of the arrival point) (R3)")]
        public float catchReachStride = 0.25f;
        [Tooltip("m — max ball→hand gap allowed at the securing frame (R5)")]
        public float maxSecureGap = 0.35f;
        [Range(0f, 1f), Tooltip("lateral share of accuracy noise; the rest is along the route path (R1)")]
        public float noiseLateralFactor = 0.3f;
    }
}