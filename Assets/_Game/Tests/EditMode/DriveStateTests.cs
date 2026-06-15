using NUnit.Framework;
using Gridiron;

namespace Gridiron.Tests
{
    /// <summary>PRD §14: down/distance accounting — completion past marker → 1st &amp; 10; 4th-down incompletion → drive over.</summary>
    public class DriveStateTests
    {
        const float Y = DriveState.YardsToMeters;
        const float Own25 = -45.72f + 25f * Y; // -22.86

        DriveState NewDrive()
        {
            var d = new DriveState(45.72f, 5f);
            d.StartDrive(Own25);
            return d;
        }

        [Test]
        public void StartDrive_FirstAndTen()
        {
            var d = NewDrive();
            Assert.AreEqual(1, d.Down);
            Assert.AreEqual(Own25 + 10f * Y, d.FirstDownTargetZ, 1e-3f);
            Assert.IsFalse(d.DriveOver);
        }

        [Test]
        public void CompletionPastMarker_ResetsToFirstAndTen()
        {
            var d = NewDrive();
            float spot = Own25 + 12f * Y;
            var outcome = d.ApplyResult(PlayResult.Catch, spot);
            Assert.AreEqual(DriveOutcome.FirstDown, outcome);
            Assert.AreEqual(1, d.Down);
            Assert.AreEqual(spot, d.LosZ, 1e-3f);
            Assert.AreEqual(spot + 10f * Y, d.FirstDownTargetZ, 1e-3f);
        }

        [Test]
        public void CompletionShortOfMarker_MovesBallLosesDown()
        {
            var d = NewDrive();
            float target = d.FirstDownTargetZ;
            float spot = Own25 + 4f * Y;
            var outcome = d.ApplyResult(PlayResult.Catch, spot);
            Assert.AreEqual(DriveOutcome.Continue, outcome);
            Assert.AreEqual(2, d.Down);
            Assert.AreEqual(spot, d.LosZ, 1e-3f);
            Assert.AreEqual(target, d.FirstDownTargetZ, 1e-3f); // marker unchanged
        }

        [Test]
        public void FourthDownIncompletion_DriveOver()
        {
            var d = NewDrive();
            d.ApplyResult(PlayResult.Incomplete, 0f);
            d.ApplyResult(PlayResult.Swat, 0f);
            d.ApplyResult(PlayResult.OutOfBounds, 0f);
            Assert.AreEqual(4, d.Down);
            Assert.IsFalse(d.DriveOver);
            var outcome = d.ApplyResult(PlayResult.Incomplete, 0f);
            Assert.AreEqual(DriveOutcome.TurnoverOnDowns, outcome);
            Assert.IsTrue(d.DriveOver);
        }

        [Test]
        public void Touchdown_EndsDrive()
        {
            var d = NewDrive();
            var outcome = d.ApplyResult(PlayResult.Touchdown, 46f);
            Assert.AreEqual(DriveOutcome.Touchdown, outcome);
            Assert.IsTrue(d.DriveOver);
        }

        [Test]
        public void Intercept_EndsDrive()
        {
            var d = NewDrive();
            var outcome = d.ApplyResult(PlayResult.Intercept, 0f);
            Assert.AreEqual(DriveOutcome.Intercepted, outcome);
            Assert.IsTrue(d.DriveOver);
        }

        [Test]
        public void SackTimer_Loses5Yards_ClampedAtOwn1()
        {
            var d = NewDrive();
            d.ApplyResult(PlayResult.SackTimerExpired, 0f);
            Assert.AreEqual(Own25 - 5f * Y, d.LosZ, 1e-3f);
            Assert.AreEqual(2, d.Down);

            // From own 2: sack clamps at own 1
            var d2 = new DriveState(45.72f, 5f);
            d2.StartDrive(-45.72f + 2f * Y);
            d2.ApplyResult(PlayResult.SackTimerExpired, 0f);
            Assert.AreEqual(-45.72f + 1f * Y, d2.LosZ, 1e-3f);
        }

        [Test]
        public void CompletionInsideTen_GoalToGo()
        {
            var d = new DriveState(45.72f, 5f);
            d.StartDrive(45.72f - 5f * Y); // opponent 5
            Assert.AreEqual(45.72f, d.FirstDownTargetZ, 1e-3f); // target clamped at goal line
        }
    }
}