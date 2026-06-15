using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace Gridiron.EditorTools
{
    /// <summary>
    /// PRD §12: trims the QB pass sub-clip, adds animation events (normalized times on ModelImporter
    /// clips), builds the upper-body avatar mask and the three Animator Controllers using only the
    /// in-project clips + §12.2 substitution table. Idempotent.
    ///
    /// Audit pass:
    ///  - C1: run-state playback speed driven by a "SpeedMult" parameter; the Running clip's natural
    ///        ground speed is measured and written into MatchRules.runClipNaturalSpeed.
    ///  - C4: CB base layer is a 2D freeform-directional blend tree (MoveX/MoveZ in local space) using
    ///        forward run, backward run, and the backward-diagonal jog + a mirrored sub-clip.
    ///  - C5: WR "Plant" is a FULL-BODY base-layer one-shot (legs plant too); Catch/Swat stay upper-body.
    ///  - H5: optional full-body Tackle (CB) / Fall (WR) one-shots if matching FBX clips exist.
    ///  - M3: loud warnings whenever a required sub-clip is missing and a long source clip is substituted.
    ///  - Controllers are rebuilt IN PLACE (same asset, same GUID) so scene Animator references never break.
    /// </summary>
    public static class GridironAnimationBuilder
    {
        const string AnimDir = "Assets/Animations/";
        const string OutDir = "Assets/_Game/Art/";
        const string RulesPath = "Assets/_Game/Data/MatchRules.asset";

        [MenuItem("Gridiron/2. Setup Animations")]
        public static void BuildAll()
        {
            FixRootMotionSettings();
            AddEventsAndSubClips();
            var mask = BuildUpperBodyMask();
            float runNaturalSpeed = MeasureAndStoreRunSpeed();
            BuildControllers(mask, runNaturalSpeed);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Gridiron] Animation setup complete. Run clip natural speed: " + runNaturalSpeed.ToString("0.00") + " m/s.");
        }

        // ---------- Import-level events & sub-clips ----------

        static readonly string[] AllAnimFbx = { "Running", "Running Backward", "Jog Backward Diagonal", "Backward Right Turn", "Running Right Turn", "Offensive Idle", "Defender", "Quarterback Pass", "Football Catch", "Receiver Catch" };

        /// <summary>
        /// In-place setup for script-driven movement: root XZ motion must be EXTRACTED to root
        /// curves (then discarded, applyRootMotion = false) — NOT baked into the pose. Baked XZ
        /// made meshes lurch away from their roots and snap back every loop (glitchy running),
        /// and left the Defender clip's body meters from the capsule.
        /// </summary>
        static void FixRootMotionSettings()
        {
            foreach (var name in AllAnimFbx)
            {
                ModifyImporter(AnimDir + name + ".fbx", clips =>
                {
                    foreach (var c in clips)
                    {
                        c.lockRootPositionXZ = false;   // extract XZ -> discarded at runtime (in-place)
                        c.lockRootRotation = true;      // rotation stays in pose
                        c.keepOriginalOrientation = true;
                        c.lockRootHeightY = true;
                        c.keepOriginalPositionY = true;
                    }
                    return clips;
                });
            }
        }

        static void AddEventsAndSubClips()
        {
            // Quarterback Pass: trim a throw sub-clip (§12.1) with ReleaseBall event.
            // Frame window is a first-pass guess (clip is 7.67 s of windup+idle) — logged in TODO_ANIMS.md.
            ModifyImporter(AnimDir + "Quarterback Pass.fbx", clips =>
            {
                var baseClip = clips[0];
                if (!clips.Any(c => c.name == "QB Pass Throw"))
                {
                    var sub = CloneClip(baseClip);
                    sub.name = "QB Pass Throw";
                    sub.firstFrame = 30f;   // ~1.0 s
                    sub.lastFrame = 66f;    // ~2.2 s
                    sub.loopTime = false;
                    sub.events = new[] { Evt("ReleaseBall", 0.3f) };
                    clips.Add(sub);
                }
                return clips;
            });

            // Football Catch: HandsOnBall
            ModifyImporter(AnimDir + "Football Catch.fbx", clips =>
            {
                EnsureEvent(clips[0], "HandsOnBall", 0.3f);
                return clips;
            });

            // Receiver Catch: HandsOnBall + "Swat" sub-clip (first 0.4 s arm-raise, §12.2)
            ModifyImporter(AnimDir + "Receiver Catch.fbx", clips =>
            {
                EnsureEvent(clips[0], "HandsOnBall", 0.3f);
                if (!clips.Any(c => c.name == "Swat"))
                {
                    var sub = CloneClip(clips[0]);
                    sub.name = "Swat";
                    sub.firstFrame = 0f;
                    sub.lastFrame = 12f;    // ~0.4 s @30fps
                    sub.loopTime = false;
                    sub.events = new[] { Evt("SwatContact", 0.75f) };
                    clips.Add(sub);
                }
                return clips;
            });

            // Defender: full 3.6 s clip dives to the ground — trim a loopable pre-snap stance.
            ModifyImporter(AnimDir + "Defender.fbx", clips =>
            {
                if (!clips.Any(c => c.name == "Defender Stance"))
                {
                    var sub = CloneClip(clips[0]);
                    sub.name = "Defender Stance";
                    sub.firstFrame = 0f;
                    sub.lastFrame = 24f;
                    sub.loopTime = true;
                    clips.Add(sub);
                }
                return clips;
            });

            // Running Right Turn: PlantFoot for WR cuts
            ModifyImporter(AnimDir + "Running Right Turn.fbx", clips =>
            {
                EnsureEvent(clips[0], "PlantFoot", 0.25f);
                return clips;
            });

            // Jog Backward Diagonal: mirrored sub-clip so the CB's 2D tree covers both sides (audit C4).
            ModifyImporter(AnimDir + "Jog Backward Diagonal.fbx", clips =>
            {
                var baseClip = clips[0];
                baseClip.loopTime = true;
                if (!clips.Any(c => c.name == "Jog Backward Diagonal Mirror"))
                {
                    var sub = CloneClip(baseClip);
                    sub.name = "Jog Backward Diagonal Mirror";
                    sub.mirror = true;
                    sub.loopTime = true;
                    clips.Add(sub);
                }
                return clips;
            });

            // Locomotion clips must loop.
            foreach (var name in new[] { "Running", "Running Backward", "Offensive Idle" })
            {
                ModifyImporter(AnimDir + name + ".fbx", clips =>
                {
                    clips[0].loopTime = true;
                    return clips;
                });
            }
        }

        static void ModifyImporter(string path, System.Func<List<ModelImporterClipAnimation>, List<ModelImporterClipAnimation>> edit)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { Debug.LogWarning("[Gridiron] Missing animation FBX: " + path); return; }
            var clips = importer.clipAnimations != null && importer.clipAnimations.Length > 0
                ? importer.clipAnimations.ToList()
                : importer.defaultClipAnimations.ToList();
            clips = edit(clips);
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        static ModelImporterClipAnimation CloneClip(ModelImporterClipAnimation src)
        {
            return new ModelImporterClipAnimation
            {
                takeName = src.takeName,
                name = src.name,
                firstFrame = src.firstFrame,
                lastFrame = src.lastFrame,
                loopTime = src.loopTime,
                lockRootRotation = src.lockRootRotation,
                lockRootHeightY = src.lockRootHeightY,
                lockRootPositionXZ = src.lockRootPositionXZ,
                keepOriginalOrientation = src.keepOriginalOrientation,
                keepOriginalPositionY = src.keepOriginalPositionY,
                keepOriginalPositionXZ = src.keepOriginalPositionXZ
            };
        }

        static AnimationEvent Evt(string functionName, float normalizedTime)
        {
            return new AnimationEvent { functionName = functionName, time = normalizedTime };
        }

        static void EnsureEvent(ModelImporterClipAnimation clip, string functionName, float t)
        {
            var events = clip.events != null ? clip.events.ToList() : new List<AnimationEvent>();
            if (!events.Any(e => e.functionName == functionName))
            {
                events.Add(Evt(functionName, t));
                clip.events = events.ToArray();
            }
        }

        // ---------- Clip speed measurement (audit C1) ----------

        /// <summary>Measured ground speed of the Running clip, persisted to MatchRules.runClipNaturalSpeed.</summary>
        static float MeasureAndStoreRunSpeed()
        {
            float speed = 3.6f; // sensible Mixamo-run fallback
            var run = FindClip("Running", "Running");
            if (run != null)
            {
                float measured = new Vector3(run.averageSpeed.x, 0f, run.averageSpeed.z).magnitude;
                if (measured > 0.5f) speed = measured;
                else Debug.LogWarning("[Gridiron] Running clip averageSpeed unmeasurable (" + measured.ToString("0.00") + ") — using fallback " + speed + " m/s. Check root motion import settings.");
            }
            var rules = AssetDatabase.LoadAssetAtPath<MatchRules>(RulesPath);
            if (rules != null)
            {
                rules.runClipNaturalSpeed = speed;
                EditorUtility.SetDirty(rules);
            }
            else Debug.LogWarning("[Gridiron] MatchRules.asset not found at " + RulesPath + " — runClipNaturalSpeed not persisted.");
            return speed;
        }

        /// <summary>Planar average velocity of a clip with fallback when root motion wasn't measurable.</summary>
        static Vector2 ClipPlanarVelocity(AnimationClip clip, Vector2 fallback)
        {
            if (clip == null) return fallback;
            var v = new Vector2(clip.averageSpeed.x, clip.averageSpeed.z);
            return v.magnitude > 0.5f ? v : fallback;
        }

        // ---------- Avatar mask ----------

        static AvatarMask BuildUpperBodyMask()
        {
            string path = OutDir + "UpperBody.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                    mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                AssetDatabase.CreateAsset(mask, path);
            }
            return mask;
        }

        // ---------- Clip lookup ----------

        static AnimationClip FindClip(string fbxName, string clipName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(AnimDir + fbxName + ".fbx");
            foreach (var a in assets)
            {
                var clip = a as AnimationClip;
                if (clip != null && clip.name == clipName && !clip.name.StartsWith("__preview"))
                    return clip;
            }
            Debug.LogWarning("[Gridiron] Clip not found: " + clipName + " in " + fbxName);
            return null;
        }

        /// <summary>Audit M3: when a trimmed sub-clip is missing, substitute loudly — never silently.</summary>
        static AnimationClip FindClipOrSubstitute(string fbxName, string clipName, string fallbackClipName)
        {
            var clip = FindClip(fbxName, clipName);
            if (clip != null) return clip;
            Debug.LogError("[Gridiron] AUDIT M3: sub-clip '" + clipName + "' missing from " + fbxName +
                ".fbx — substituting full-length '" + fallbackClipName + "'. One-shots will hold their final pose far too long. Re-run Gridiron > 2. Setup Animations.");
            return FindClip(fbxName, fallbackClipName);
        }

        /// <summary>Audit H5: optional clips — first FBX in Assets/Animations whose filename contains a keyword.</summary>
        static AnimationClip FindOptionalClip(params string[] nameKeywords)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Animations" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (!nameKeywords.Any(k => file.Contains(k))) continue;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var clip = a as AnimationClip;
                    if (clip != null && !clip.name.StartsWith("__preview")) return clip;
                }
            }
            return null;
        }

        // ---------- Animator controllers ----------

        static void BuildControllers(AvatarMask mask, float runNaturalSpeed)
        {
            var idle = FindClip("Offensive Idle", "Offensive Idle");
            var run = FindClip("Running", "Running");
            var runBack = FindClip("Running Backward", "Running Backward");
            var jogDiag = FindClip("Jog Backward Diagonal", "Jog Backward Diagonal");
            var jogDiagMirror = FindClip("Jog Backward Diagonal", "Jog Backward Diagonal Mirror");
            var defender = FindClipOrSubstitute("Defender", "Defender Stance", "Defender");
            var qbThrow = FindClipOrSubstitute("Quarterback Pass", "QB Pass Throw", "Quarterback Pass");
            var footballCatch = FindClip("Football Catch", "Football Catch");
            var receiverCatch = FindClip("Receiver Catch", "Receiver Catch");
            var swat = FindClipOrSubstitute("Receiver Catch", "Swat", "Receiver Catch");
            var plant = FindClip("Running Right Turn", "Running Right Turn");

            // Audit H5: optional tackle/fall clips — drop matching FBX files into Assets/Animations to enable.
            var tackle = FindOptionalClip("tackle", "takedown", "dive");
            var fall = FindOptionalClip("fall", "stumble", "knock");
            if (tackle == null || fall == null)
                Debug.Log("[Gridiron] AUDIT H5: no tackle/fall FBX found in Assets/Animations (looked for *tackle*/*takedown*/*dive* and *fall*/*stumble*/*knock*). " +
                          "Tackle presentation runs without clips until they are added; Trigger() is a safe no-op.");

            // QB throw: the procedural full-body clip (Gridiron > 4) when present — the QB steps
            // into the throw with his whole body; the trimmed mocap sub-clip is the upper-body fallback.
            var proceduralThrow = AssetDatabase.LoadAssetAtPath<AnimationClip>(QBThrowClipGenerator.ClipPath);
            if (proceduralThrow == null)
                Debug.Log("[Gridiron] No procedural QB throw clip — run 'Gridiron > 4. Generate QB Throw Clip' for a full-body throwing motion. Using mocap sub-clip on the upper body.");

            // QB: 1D locomotion (stationary in v1) + throw + upper-body catch.
            BuildController(OutDir + "QB.controller", runNaturalSpeed, mask,
                (ctrl, baseSm) => Build1DLocomotion(ctrl, baseSm, idle, run, runNaturalSpeed),
                (ctrl, baseSm, locomotion) =>
                {
                    if (proceduralThrow != null)
                        AddFullBodyOneShot(baseSm, locomotion, "Throw", proceduralThrow, 0.05f);
                },
                action =>
                {
                    if (proceduralThrow == null) AddOneShot(action, "Throw", qbThrow);
                    AddOneShot(action, "Catch", footballCatch); // QB snap-catch substitute (§12.2)
                });

            // WR: 1D locomotion + FULL-BODY plant on the base layer (audit C5) + upper-body catch.
            BuildController(OutDir + "WR.controller", runNaturalSpeed, mask,
                (ctrl, baseSm) => Build1DLocomotion(ctrl, baseSm, idle, run, runNaturalSpeed),
                (ctrl, baseSm, locomotion) =>
                {
                    AddFullBodyOneShot(baseSm, locomotion, "Plant", plant, 0.1f);
                    AddFullBodyOneShot(baseSm, locomotion, "Fall", fall, 0.1f); // audit H5 (optional clip)
                },
                action =>
                {
                    AddOneShot(action, "Catch", footballCatch);
                });

            // CB: 2D freeform-directional locomotion (audit C4) + upper-body swat/catch + full-body tackle.
            BuildController(OutDir + "CB.controller", runNaturalSpeed, mask,
                (ctrl, baseSm) => Build2DLocomotion(ctrl, baseSm, defender, run, runBack, jogDiag, jogDiagMirror),
                (ctrl, baseSm, locomotion) =>
                {
                    AddFullBodyOneShot(baseSm, locomotion, "Tackle", tackle, 0.08f); // audit H5 (optional clip)
                },
                action =>
                {
                    AddOneShot(action, "Swat", swat);
                    AddOneShot(action, "Catch", receiverCatch);
                });
        }

        /// <summary>1D idle→run blend on Speed; run fully on at the clip's natural speed, SpeedMult scales playback (audit C1).</summary>
        static AnimatorState Build1DLocomotion(AnimatorController ctrl, AnimatorStateMachine baseSm, AnimationClip idle, AnimationClip run, float runNaturalSpeed)
        {
            BlendTree tree;
            var locomotion = ctrl.CreateBlendTreeInController("Locomotion", out tree, 0);
            tree.blendParameter = "Speed";
            tree.blendType = BlendTreeType.Simple1D;
            tree.useAutomaticThresholds = false;
            if (idle != null) tree.AddChild(idle, 0f);
            if (run != null) tree.AddChild(run, runNaturalSpeed);
            locomotion.speedParameterActive = true;   // audit C1
            locomotion.speedParameter = "SpeedMult";
            baseSm.defaultState = locomotion;
            return locomotion;
        }

        /// <summary>2D freeform-directional tree on local-space MoveX/MoveZ (audit C4). Child positions = measured clip velocities.</summary>
        static AnimatorState Build2DLocomotion(AnimatorController ctrl, AnimatorStateMachine baseSm, AnimationClip idle, AnimationClip run,
            AnimationClip runBack, AnimationClip jogDiag, AnimationClip jogDiagMirror)
        {
            BlendTree tree;
            var locomotion = ctrl.CreateBlendTreeInController("Locomotion2D", out tree, 0);
            tree.blendType = BlendTreeType.FreeformDirectional2D;
            tree.blendParameter = "MoveX";
            tree.blendParameterY = "MoveZ";

            Vector2 fwd = ClipPlanarVelocity(run, new Vector2(0f, 3.6f));
            Vector2 back = ClipPlanarVelocity(runBack, new Vector2(0f, -2.4f));
            Vector2 diag = ClipPlanarVelocity(jogDiag, new Vector2(-1.7f, -1.7f));
            // Force clean axes for pure forward/backward, keep measured magnitudes.
            fwd = new Vector2(0f, fwd.magnitude);
            back = new Vector2(0f, -back.magnitude);
            if (Mathf.Abs(diag.x) < 0.3f) diag = new Vector2(-diag.magnitude * 0.707f, -diag.magnitude * 0.707f);
            if (diag.y > 0f) diag.y = -diag.y; // it's a backward jog

            var children = new List<ChildMotion>();
            if (idle != null) children.Add(Child(idle, Vector2.zero));
            if (run != null) children.Add(Child(run, fwd));
            if (runBack != null) children.Add(Child(runBack, back));
            if (jogDiag != null) children.Add(Child(jogDiag, diag));
            if (jogDiagMirror != null) children.Add(Child(jogDiagMirror, new Vector2(-diag.x, diag.y)));
            tree.children = children.ToArray();

            locomotion.speedParameterActive = true;   // audit C1
            locomotion.speedParameter = "SpeedMult";
            baseSm.defaultState = locomotion;
            return locomotion;
        }

        static ChildMotion Child(Motion motion, Vector2 position)
        {
            return new ChildMotion { motion = motion, position = position, timeScale = 1f };
        }

        static void BuildController(string path, float runNaturalSpeed, AvatarMask mask,
            System.Func<AnimatorController, AnimatorStateMachine, AnimatorState> buildBase,
            System.Action<AnimatorController, AnimatorStateMachine, AnimatorState> addBaseActions,
            System.Action<AnimatorStateMachine> addUpperActions)
        {
            // Rebuild IN PLACE: deleting + recreating changes the GUID and breaks scene Animator refs.
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl == null)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            }
            else
            {
                while (ctrl.layers.Length > 0) ctrl.RemoveLayer(0);
                while (ctrl.parameters.Length > 0) ctrl.RemoveParameter(0);
                // Purge orphaned sub-assets (old state machines/states/blend trees) so the asset doesn't bloat.
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (sub == null || sub is AnimatorController) continue;
                    Object.DestroyImmediate(sub, true);
                }
                ctrl.AddLayer("Base Layer");
            }

            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("MoveZ", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter
            { name = "SpeedMult", type = AnimatorControllerParameterType.Float, defaultFloat = 1f }); // default 1, never 0 (audit C1)
            ctrl.AddParameter("Backpedal", AnimatorControllerParameterType.Bool);
            foreach (var trig in new[] { "Hike", "Throw", "Catch", "Swat", "Drop", "Plant", "Tackle", "Fall" })
                ctrl.AddParameter(trig, AnimatorControllerParameterType.Trigger);

            var baseSm = ctrl.layers[0].stateMachine;
            var locomotion = buildBase(ctrl, baseSm);
            addBaseActions(ctrl, baseSm, locomotion);

            // Action layer: upper-body mask, trigger-driven one-shots (§12.3)
            ctrl.AddLayer("Actions");
            var layers = ctrl.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 1f;
            ctrl.layers = layers;
            var actionSm = ctrl.layers[1].stateMachine;
            var empty = actionSm.AddState("Empty");
            actionSm.defaultState = empty;
            addUpperActions(actionSm);

            EditorUtility.SetDirty(ctrl);
        }

        /// <summary>Upper-body one-shot on the Actions layer (Catch/Swat/Throw — audit C5 keeps these masked).</summary>
        static void AddOneShot(AnimatorStateMachine sm, string trigger, AnimationClip clip)
        {
            if (clip == null) return;
            var state = sm.AddState(trigger + "State");
            state.motion = clip;
            var t = sm.AddAnyStateTransition(state);
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            t.hasExitTime = false;
            t.duration = 0.05f;
            t.canTransitionToSelf = false;
            var back = state.AddTransition(sm.defaultState);
            back.hasExitTime = true;
            back.exitTime = 0.95f;
            back.duration = 0.1f;
        }

        /// <summary>Full-body one-shot on the BASE layer: interrupts locomotion, returns on exit time (audit C5/H5).</summary>
        static void AddFullBodyOneShot(AnimatorStateMachine sm, AnimatorState locomotion, string trigger, AnimationClip clip, float enterDuration)
        {
            if (clip == null) return;
            var state = sm.AddState(trigger + "State");
            state.motion = clip;
            var t = sm.AddAnyStateTransition(state);
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            t.hasExitTime = false;
            t.duration = enterDuration;
            t.canTransitionToSelf = false;
            var back = state.AddTransition(locomotion);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            back.duration = 0.15f;
        }
    }
}