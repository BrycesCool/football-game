using System.Collections.Generic;
using NUnit.Framework;
using Gridiron;

namespace Gridiron.Tests
{
    /// <summary>PRD §14: same-tick precedence INTERCEPT &gt; SWAT &gt; CATCH &gt; GROUND (§9).</summary>
    public class ResolutionLogicTests
    {
        [Test]
        public void SimultaneousSwatAndCatch_SwatWins()
        {
            var events = new List<ResolutionEventType> { ResolutionEventType.Catch, ResolutionEventType.Swat };
            int winner = ResolutionLogic.PickWinner(events);
            Assert.AreEqual(ResolutionEventType.Swat, events[winner]);
        }

        [Test]
        public void SimultaneousInterceptAndSwat_InterceptWins()
        {
            var events = new List<ResolutionEventType> { ResolutionEventType.Swat, ResolutionEventType.Intercept, ResolutionEventType.Catch };
            int winner = ResolutionLogic.PickWinner(events);
            Assert.AreEqual(ResolutionEventType.Intercept, events[winner]);
        }

        [Test]
        public void CatchBeatsGround()
        {
            var events = new List<ResolutionEventType> { ResolutionEventType.Ground, ResolutionEventType.Catch };
            int winner = ResolutionLogic.PickWinner(events);
            Assert.AreEqual(ResolutionEventType.Catch, events[winner]);
        }

        [Test]
        public void EmptyList_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, ResolutionLogic.PickWinner(new List<ResolutionEventType>()));
        }

        [Test]
        public void Tie_EarliestWins()
        {
            var events = new List<ResolutionEventType> { ResolutionEventType.Swat, ResolutionEventType.Swat };
            Assert.AreEqual(0, ResolutionLogic.PickWinner(events));
        }
    }
}