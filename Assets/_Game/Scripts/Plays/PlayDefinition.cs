using UnityEngine;

namespace Gridiron
{
    [CreateAssetMenu(menuName = "Gridiron/Play", fileName = "Play")]
    public class PlayDefinition : ScriptableObject
    {
        public string playName;
        public Sprite playArt;
        public RouteDefinition route;
        /// <summary>WR offset (x, z) in meters from the ball at the LOS (PRD §6.1).</summary>
        public Vector2 receiverAlignment = new Vector2(10f, 0f);
        public ThrowProfileHint hint = ThrowProfileHint.Either;
    }
}