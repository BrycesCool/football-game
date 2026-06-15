using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Gridiron.EditorTools
{
    /// <summary>
    /// Procedurally authors a humanoid QB throwing motion as muscle curves (no FBX needed):
    /// athletic stance → windup (torso closes, ball cocked behind the ear, off-arm points
    /// downfield) → hip-led drive (torso opens, elbow leads, forearm extends, wrist snaps)
    /// → release at 0.35 s (ReleaseBall event) → follow-through across the body → recover.
    /// 1.0 s total @ 60 fps. Regenerating overwrites the clip IN PLACE (same GUID).
    /// Every humanoid muscle gets a curve so nothing collapses to T-pose.
    /// </summary>
    public static class QBThrowClipGenerator
    {
        public const string ClipPath = "Assets/_Game/Art/QB_Throw_Procedural.anim";
        public const float ReleaseTime = 0.35f; // s — keep MatchRules.throwReleaseFallback above this

        [MenuItem("Gridiron/4. Generate QB Throw Clip")]
        public static void Generate()
        {
            var curves = new Dictionary<string, AnimationCurve>();

            // ---------- Throwing arm (right) ----------
            // Arm height: cocked high behind the ear, whips over the top, finishes low across the body.
            Set(curves, "Right Arm Down-Up", K(0f, -0.50f), K(0.20f, 0.45f), K(0.30f, 0.58f), K(0.38f, 0.30f), K(0.58f, -0.35f), K(1f, -0.50f));
            // Arm reach: drawn behind on the windup, driven hard forward through release.
            Set(curves, "Right Arm Front-Back", K(0f, 0.10f), K(0.20f, -0.55f), K(0.33f, 0.45f), K(0.38f, 0.70f), K(0.60f, 0.25f), K(1f, 0.10f));
            Set(curves, "Right Arm Twist In-Out", K(0f, 0f), K(0.20f, 0.35f), K(0.38f, -0.25f), K(0.60f, 0f), K(1f, 0f));
            // Elbow: deep flex on the cock, near-full extension at the release point.
            Set(curves, "Right Forearm Stretch", K(0f, -0.40f), K(0.20f, -0.85f), K(0.32f, -0.35f), K(0.37f, 0.55f), K(0.60f, -0.10f), K(1f, -0.40f));
            Set(curves, "Right Forearm Twist In-Out", K(0f, 0f), K(0.22f, 0.30f), K(0.40f, -0.30f), K(1f, 0f));
            // Wrist: cocked back, snaps over on release.
            Set(curves, "Right Hand Down-Up", K(0f, 0f), K(0.22f, 0.35f), K(0.36f, -0.55f), K(0.60f, -0.15f), K(1f, 0f));
            Set(curves, "Right Hand In-Out", K(0f, 0f), K(0.36f, -0.15f), K(1f, 0f));
            Set(curves, "Right Shoulder Down-Up", K(0f, 0f), K(0.20f, 0.30f), K(0.38f, 0.10f), K(1f, 0f));
            Set(curves, "Right Shoulder Front-Back", K(0f, 0f), K(0.20f, -0.40f), K(0.38f, 0.50f), K(0.65f, 0.10f), K(1f, 0f));

            // ---------- Guide arm (left): points at the target, then rips down/back through the throw ----------
            Set(curves, "Left Arm Down-Up", K(0f, -0.50f), K(0.16f, 0.05f), K(0.33f, -0.15f), K(0.50f, -0.60f), K(1f, -0.50f));
            Set(curves, "Left Arm Front-Back", K(0f, 0.10f), K(0.16f, 0.60f), K(0.38f, -0.20f), K(0.60f, 0.00f), K(1f, 0.10f));
            Set(curves, "Left Forearm Stretch", K(0f, -0.40f), K(0.16f, 0.20f), K(0.40f, -0.65f), K(1f, -0.40f));
            Set(curves, "Left Shoulder Front-Back", K(0f, 0f), K(0.16f, 0.35f), K(0.40f, -0.30f), K(1f, 0f));

            // ---------- Torso: close on the windup, open hard through release ----------
            foreach (var m in new[] { "Spine Twist Left-Right", "Chest Twist Left-Right" })
                Set(curves, m, K(0f, 0f), K(0.20f, -0.45f), K(0.33f, 0.15f), K(0.42f, 0.55f), K(0.65f, 0.30f), K(1f, 0f));
            Set(curves, "Spine Front-Back", K(0f, 0f), K(0.20f, 0.12f), K(0.45f, -0.18f), K(0.70f, -0.08f), K(1f, 0f));
            Set(curves, "Chest Front-Back", K(0f, 0f), K(0.20f, 0.10f), K(0.45f, -0.15f), K(1f, 0f));
            Set(curves, "Spine Left-Right", K(0f, 0f), K(0.22f, 0.10f), K(0.45f, -0.10f), K(1f, 0f));

            // ---------- Head: eyes stay downfield, countering the torso twist ----------
            Set(curves, "Head Turn Left-Right", K(0f, 0f), K(0.20f, 0.40f), K(0.42f, -0.20f), K(0.70f, 0f), K(1f, 0f));
            Set(curves, "Neck Turn Left-Right", K(0f, 0f), K(0.20f, 0.25f), K(0.42f, -0.12f), K(1f, 0f));
            Set(curves, "Head Nod Down-Up", K(0f, 0f), K(0.40f, -0.10f), K(1f, 0f));

            // ---------- Legs: subtle stride + weight shift, feet stay planted (no foot IK) ----------
            Set(curves, "Left Upper Leg Front-Back", K(0f, 0.05f), K(0.20f, 0.28f), K(0.55f, 0.10f), K(1f, 0.05f));
            Set(curves, "Right Upper Leg Front-Back", K(0f, 0.05f), K(0.20f, -0.12f), K(0.45f, 0.10f), K(1f, 0.05f));
            Set(curves, "Left Lower Leg Stretch", K(0f, -0.25f), K(0.20f, -0.40f), K(0.45f, -0.20f), K(1f, -0.25f));
            Set(curves, "Right Lower Leg Stretch", K(0f, -0.25f), K(0.20f, -0.35f), K(0.45f, -0.30f), K(1f, -0.25f));
            Set(curves, "Left Upper Leg In-Out", K(0f, 0.10f), K(1f, 0.10f));
            Set(curves, "Right Upper Leg In-Out", K(0f, 0.10f), K(1f, 0.10f));
            Set(curves, "Left Foot Up-Down", K(0f, 0f), K(0.20f, 0.15f), K(0.45f, 0f), K(1f, 0f));
            Set(curves, "Right Foot Up-Down", K(0f, 0f), K(0.40f, 0.10f), K(1f, 0f));

            // ---------- Right-hand fingers: gripping the ball until release, then relaxed ----------
            foreach (var muscle in HumanTrait.MuscleName)
            {
                if (muscle.StartsWith("Right") && muscle.Contains("Stretched") && !curves.ContainsKey(muscle))
                    curves[muscle] = Curve(K(0f, -0.55f), K(0.34f, -0.55f), K(0.42f, 0.20f), K(0.80f, -0.20f), K(1f, -0.20f));
            }

            // ---------- Baseline for every remaining muscle (never T-pose) ----------
            foreach (var muscle in HumanTrait.MuscleName)
                if (!curves.ContainsKey(muscle))
                    curves[muscle] = Curve(K(0f, Neutral(muscle)), K(1f, Neutral(muscle)));

            // ---------- Build the clip ----------
            var clip = new AnimationClip { frameRate = 60f };
            foreach (var kv in curves)
                clip.SetCurve("", typeof(Animator), kv.Key, kv.Value);

            // Body position: slight crouch into the windup, weight driving forward through release.
            clip.SetCurve("", typeof(Animator), "RootT.x", Curve(K(0f, 0f), K(1f, 0f)));
            clip.SetCurve("", typeof(Animator), "RootT.y", Curve(K(0f, 1.00f), K(0.20f, 0.965f), K(0.40f, 0.985f), K(0.70f, 1.00f), K(1f, 1.00f)));
            clip.SetCurve("", typeof(Animator), "RootT.z", Curve(K(0f, 0f), K(0.18f, -0.04f), K(0.45f, 0.06f), K(0.80f, 0f), K(1f, 0f)));
            clip.SetCurve("", typeof(Animator), "RootQ.x", Curve(K(0f, 0f), K(1f, 0f)));
            clip.SetCurve("", typeof(Animator), "RootQ.y", Curve(K(0f, 0f), K(1f, 0f)));
            clip.SetCurve("", typeof(Animator), "RootQ.z", Curve(K(0f, 0f), K(1f, 0f)));
            clip.SetCurve("", typeof(Animator), "RootQ.w", Curve(K(0f, 1f), K(1f, 1f)));

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            // Persist IN PLACE so the controller's reference (GUID) survives regeneration.
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            AnimationClip persisted;
            if (existing == null)
            {
                AssetDatabase.CreateAsset(clip, ClipPath);
                persisted = clip;
            }
            else
            {
                EditorUtility.CopySerialized(clip, existing);
                persisted = existing;
            }

            AnimationUtility.SetAnimationEvents(persisted, new[]
            {
                new AnimationEvent { functionName = "ReleaseBall", time = ReleaseTime }
            });

            EditorUtility.SetDirty(persisted);
            AssetDatabase.SaveAssets();
            Debug.Log("[Gridiron] QB throw clip generated at " + ClipPath + " (length " + persisted.length +
                      " s, ReleaseBall @ " + ReleaseTime + " s). Re-run 'Gridiron > 2. Setup Animations' to wire it.");
        }

        static float Neutral(string muscle)
        {
            if (muscle.Contains("Arm Down-Up")) return -0.5f;      // arms relaxed at the sides
            if (muscle.Contains("Forearm Stretch")) return -0.4f;  // slight elbow bend
            if (muscle.Contains("Lower Leg Stretch")) return -0.25f; // athletic knee bend
            if (muscle.Contains("Stretched")) return -0.2f;        // fingers slightly curled
            return 0f;
        }

        static Keyframe K(float t, float v) => new Keyframe(t, v);

        static AnimationCurve Curve(params Keyframe[] keys)
        {
            var c = new AnimationCurve(keys);
            for (int i = 0; i < c.keys.Length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        /// <summary>Resolve a human-readable muscle query against HumanTrait.MuscleName, then set the curve.</summary>
        static void Set(Dictionary<string, AnimationCurve> curves, string muscleQuery, params Keyframe[] keys)
        {
            string resolved = null;
            foreach (var m in HumanTrait.MuscleName)
            {
                if (string.Equals(m, muscleQuery, System.StringComparison.OrdinalIgnoreCase)) { resolved = m; break; }
            }
            if (resolved == null)
            {
                string q = muscleQuery.ToLowerInvariant();
                foreach (var m in HumanTrait.MuscleName)
                    if (m.ToLowerInvariant().Contains(q)) { resolved = m; break; }
            }
            if (resolved == null)
            {
                Debug.LogWarning("[Gridiron] QBThrowClipGenerator: muscle not found: '" + muscleQuery + "' — skipped.");
                return;
            }
            curves[resolved] = Curve(keys);
        }
    }
}