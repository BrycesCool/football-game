using System;
using UnityEngine;

namespace Gridiron
{
    /// <summary>Payload for MatchManager.OnThrow (PRD §5.2).</summary>
    [Serializable]
    public struct ThrowData
    {
        public ThrowType type;
        public Vector3 releasePos;
        public Vector3 velocity;
        /// <summary>Actual ballistic arrival point (already includes accuracy noise and deep-bullet shortfall).</summary>
        public Vector3 targetPoint;
        public float flightTime;
        public float launchTime;       // Time.time at release
        public float ArrivalTime => launchTime + flightTime;
    }

    /// <summary>Running stat line for the end screen (PRD §10.2).</summary>
    [Serializable]
    public class MatchStats
    {
        public int attempts;
        public int completions;
        public int touchdowns;
        public int interceptions;
        public float yards;
        public float longestPlay;

        public int CompletionPercent => attempts == 0 ? 0 : Mathf.RoundToInt(100f * completions / attempts);

        public void Reset()
        {
            attempts = completions = touchdowns = interceptions = 0;
            yards = longestPlay = 0f;
        }

        /// <summary>Letter grade from points + completion % (PRD §10.2).</summary>
        public string Grade(int score)
        {
            float g = score + CompletionPercent / 10f;
            if (g >= 27f) return "S";
            if (g >= 21f) return "A";
            if (g >= 15f) return "B";
            if (g >= 9f) return "C";
            return "D";
        }
    }

    [Serializable]
    public struct MatchResult
    {
        public int score;
        public MatchStats stats;
        public string grade;
    }
}