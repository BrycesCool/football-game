using System.Collections.Generic;

namespace Gridiron
{
    /// <summary>Resolution event kinds, ordered by precedence value (PRD §9): INTERCEPT &gt; SWAT &gt; CATCH &gt; GROUND.</summary>
    public enum ResolutionEventType
    {
        Ground = 0,
        Catch = 1,
        Swat = 2,
        Intercept = 3
    }

    /// <summary>Pure precedence logic, unit-testable (§14).</summary>
    public static class ResolutionLogic
    {
        /// <summary>Index of the winning event in the list (highest precedence; earliest wins ties). -1 if empty.</summary>
        public static int PickWinner(IReadOnlyList<ResolutionEventType> events)
        {
            int best = -1;
            for (int i = 0; i < events.Count; i++)
            {
                if (best < 0 || (int)events[i] > (int)events[best]) best = i;
            }
            return best;
        }
    }
}