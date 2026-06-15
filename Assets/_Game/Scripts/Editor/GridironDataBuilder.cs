using UnityEngine;
using UnityEditor;
using Gridiron;

namespace Gridiron.EditorTools
{
    /// <summary>Authors the v1 playbook (PRD §6.2), difficulty SOs (§8.5) and MatchRules. Idempotent.</summary>
    public static class GridironDataBuilder
    {
        const float Y = 0.9144f; // yards → meters; assets STORE METERS (§6.2 recommendation)

        [MenuItem("Gridiron/1. Build Data Assets")]
        public static void BuildAll()
        {
            BuildRules();
            BuildDifficulties();
            BuildRoutesAndPlays();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Gridiron] Data assets built.");
        }

        static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        static void BuildRules()
        {
            var rules = GetOrCreate<MatchRules>("Assets/_Game/Data/MatchRules.asset");
            EditorUtility.SetDirty(rules); // defaults in class match §17
        }

        static void BuildDifficulties()
        {
            Set(GetOrCreate<CBDifficulty>("Assets/_Game/Data/Difficulty/Easy.asset"), "Easy", 0.90f, 0.40f, 0.35f, 0.05f, 0.55f);
            Set(GetOrCreate<CBDifficulty>("Assets/_Game/Data/Difficulty/Normal.asset"), "Normal", 0.97f, 0.25f, 0.20f, 0.15f, 0.75f);
            Set(GetOrCreate<CBDifficulty>("Assets/_Game/Data/Difficulty/Hard.asset"), "Hard", 1.01f, 0.15f, 0.10f, 0.25f, 0.90f);
        }

        static void Set(CBDifficulty d, string label, float speed, float react, float brk, float pInt, float pSwat)
        {
            d.label = label;
            d.speedRatio = speed;
            d.reactionDelay = react;
            d.breakPenalty = brk;
            d.interceptChance = pInt;
            d.swatChance = pSwat;
            EditorUtility.SetDirty(d);
        }

        struct WP { public float x, z, mult; public bool brk; public WP(float x, float z, float mult, bool brk) { this.x = x; this.z = z; this.mult = mult; this.brk = brk; } }

        static void BuildRoutesAndPlays()
        {
            // §6.2 — waypoints authored in yards here, stored in meters.
            BuildPlay("Slant", new[] { new WP(0, 3, 0.9f, true), new WP(-6, 8, 1f, false) }, false, ThrowProfileHint.BulletFriendly);
            BuildPlay("Out", new[] { new WP(0, 7, 0.9f, true), new WP(6, 7, 1f, false) }, false, ThrowProfileHint.BulletFriendly);
            BuildPlay("Dig", new[] { new WP(0, 10, 0.9f, true), new WP(-8, 10, 1f, false) }, false, ThrowProfileHint.BulletFriendly);
            BuildPlay("Curl", new[] { new WP(0, 9, 0.9f, true), new WP(0, 7, 0.85f, false) }, true, ThrowProfileHint.BulletFriendly);
            BuildPlay("Comeback", new[] { new WP(0, 12, 0.9f, true), new WP(2, 9, 0.85f, false) }, true, ThrowProfileHint.Either);
            BuildPlay("Post", new[] { new WP(0, 8, 0.9f, true), new WP(-10, 22, 1f, false) }, false, ThrowProfileHint.LobFriendly);
            BuildPlay("Corner", new[] { new WP(0, 8, 0.9f, true), new WP(10, 22, 1f, false) }, false, ThrowProfileHint.LobFriendly);
            BuildPlay("Go", new[] { new WP(0, 28, 1f, false) }, false, ThrowProfileHint.LobFriendly);
        }

        static void BuildPlay(string name, WP[] wpsYards, bool hold, ThrowProfileHint hint)
        {
            var route = GetOrCreate<RouteDefinition>("Assets/_Game/Data/Routes/Route_" + name + ".asset");
            route.routeName = name;
            route.baseSpeed = 7.5f;
            route.holdAtFinalPoint = hold;
            route.waypoints = new RouteWaypoint[wpsYards.Length];
            for (int i = 0; i < wpsYards.Length; i++)
            {
                route.waypoints[i] = new RouteWaypoint
                {
                    offset = new Vector2(wpsYards[i].x * Y, wpsYards[i].z * Y),
                    speedMultiplier = wpsYards[i].mult,
                    isBreak = wpsYards[i].brk
                };
            }
            EditorUtility.SetDirty(route);

            var play = GetOrCreate<PlayDefinition>("Assets/_Game/Data/Plays/Play_" + name + ".asset");
            play.playName = name;
            play.route = route;
            play.receiverAlignment = new Vector2(10f, 0f); // +10 m from ball (§5.1 example)
            play.hint = hint;
            EditorUtility.SetDirty(play);
        }
    }
}