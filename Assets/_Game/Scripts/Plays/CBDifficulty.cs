using UnityEngine;

namespace Gridiron
{
    /// <summary>AI difficulty knobs (PRD §8.5).</summary>
    [CreateAssetMenu(menuName = "Gridiron/CB Difficulty", fileName = "CBDifficulty")]
    public class CBDifficulty : ScriptableObject
    {
        public string label = "Normal";
        public float speedRatio = 0.97f;        // cbSpeed = wrBaseSpeed * speedRatio
        public float reactionDelay = 0.25f;     // s, ring-buffer staleness
        public float breakPenalty = 0.20f;      // s, extra delay at route breaks
        [Range(0f, 1f)] public float interceptChance = 0.15f;
        [Range(0f, 1f)] public float swatChance = 0.75f;
    }
}