using UnityEngine;

namespace Gridiron
{
    public enum DriveOutcome { Continue, FirstDown, Touchdown, TurnoverOnDowns, Intercepted }

    /// <summary>
    /// Pure down/distance accounting (PRD §10), unit-testable without a scene (§14).
    /// Offense always attacks +Z; Z positions in meters.
    /// </summary>
    public class DriveState
    {
        public const float YardsToMeters = FieldBounds.YardsToMeters;
        public const float FirstDownYards = 10f;

        readonly float goalLineZ;
        readonly float ownGoalLineZ;
        readonly float sackPenaltyYards;

        public float LosZ { get; private set; }
        public float FirstDownTargetZ { get; private set; }
        public int Down { get; private set; } = 1;
        public bool DriveOver { get; private set; }

        public DriveState(float goalLineZ = 45.72f, float sackPenaltyYards = 5f)
        {
            this.goalLineZ = goalLineZ;
            ownGoalLineZ = -goalLineZ;
            this.sackPenaltyYards = sackPenaltyYards;
        }

        public void StartDrive(float startZ)
        {
            LosZ = startZ;
            Down = 1;
            DriveOver = false;
            ResetTarget();
        }

        void ResetTarget()
        {
            FirstDownTargetZ = Mathf.Min(LosZ + FirstDownYards * YardsToMeters, goalLineZ);
        }

        /// <summary>Apply a play result. spotZ = final ball spot (only used for Catch).</summary>
        public DriveOutcome ApplyResult(PlayResult result, float spotZ)
        {
            switch (result)
            {
                case PlayResult.Touchdown:
                    DriveOver = true;
                    return DriveOutcome.Touchdown;

                case PlayResult.Intercept:
                    DriveOver = true;
                    return DriveOutcome.Intercepted;

                case PlayResult.Catch:
                {
                    spotZ = Mathf.Min(spotZ, goalLineZ);
                    bool firstDown = spotZ >= FirstDownTargetZ;
                    LosZ = spotZ;
                    if (firstDown)
                    {
                        Down = 1;
                        ResetTarget();
                        return DriveOutcome.FirstDown;
                    }
                    return LoseDown();
                }

                case PlayResult.SackTimerExpired:
                    LosZ = Mathf.Max(LosZ - sackPenaltyYards * YardsToMeters, ownGoalLineZ + YardsToMeters); // min: own 1
                    return LoseDown();

                default: // Incomplete, Swat, OutOfBounds
                    return LoseDown();
            }
        }

        DriveOutcome LoseDown()
        {
            Down++;
            if (Down > 4)
            {
                DriveOver = true;
                return DriveOutcome.TurnoverOnDowns;
            }
            return DriveOutcome.Continue;
        }

        public float YardsToGo => Mathf.Max(0f, (FirstDownTargetZ - LosZ) / YardsToMeters);

        public string DownDistanceText
        {
            get
            {
                string[] ord = { "", "1st", "2nd", "3rd", "4th" };
                string d = Down >= 1 && Down <= 4 ? ord[Down] : Down + "th";
                bool goalToGo = Mathf.Approximately(FirstDownTargetZ, goalLineZ) && LosZ + FirstDownYards * YardsToMeters > goalLineZ;
                return d + " & " + (goalToGo ? "GOAL" : Mathf.Max(1, Mathf.RoundToInt(YardsToGo)).ToString());
            }
        }
    }
}