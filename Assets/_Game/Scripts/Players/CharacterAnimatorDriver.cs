using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Animation facade + animation-event receiver. MUST live on the same GameObject as the Animator
    /// so AnimationEvents (ReleaseBall/HandsOnBall/SwatContact/PlantFoot) reach it (PRD §12).
    /// All methods are safe no-ops when the Animator or a parameter is missing, so gameplay logic
    /// is fully functional regardless of animation fidelity (PRD §12.2 directive).
    /// </summary>
    public class CharacterAnimatorDriver : MonoBehaviour
    {
        public Animator animator;
        [Tooltip("Damp time for Speed/MoveX/MoveZ floats (audit C3) — overwritten from MatchRules by controllers")]
        public float floatDampTime = 0.12f;

        public event Action ReleaseBallEvent;
        public event Action HandsOnBallEvent;
        public event Action SwatContactEvent;
        public event Action PlantFootEvent;

        HashSet<string> paramNames;

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            CacheParams();
        }

        void CacheParams()
        {
            paramNames = new HashSet<string>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var p in animator.parameters) paramNames.Add(p.name);
        }

        bool Has(string name) => animator != null && animator.runtimeAnimatorController != null && paramNames != null && paramNames.Contains(name);

        /// <summary>Instant set — use only at play start/end. Per-frame callers use the damped overload (audit C3).</summary>
        public void SetSpeed(float metersPerSecond)
        {
            if (Has("Speed")) animator.SetFloat("Speed", metersPerSecond);
        }

        /// <summary>Damped Speed write — kills blend-tree flicker from per-frame speed jitter (audit C3).</summary>
        public void SetSpeed(float metersPerSecond, float dt)
        {
            if (Has("Speed")) animator.SetFloat("Speed", metersPerSecond, floatDampTime, dt);
        }

        /// <summary>Run-clip playback multiplier so foot speed matches ground speed (audit C1).</summary>
        public void SetSpeedMult(float mult, float dt)
        {
            if (Has("SpeedMult")) animator.SetFloat("SpeedMult", mult, floatDampTime, dt);
        }

        /// <summary>Local-space velocity for the CB's 2D directional blend tree (audit C4).</summary>
        public void SetMoveLocal(float x, float z, float dt)
        {
            if (Has("MoveX")) animator.SetFloat("MoveX", x, floatDampTime, dt);
            if (Has("MoveZ")) animator.SetFloat("MoveZ", z, floatDampTime, dt);
        }

        public void SetBackpedal(bool value)
        {
            if (Has("Backpedal")) animator.SetBool("Backpedal", value);
        }

        public void Trigger(string name)
        {
            if (Has(name)) animator.SetTrigger(name);
        }
        // ---- Upper-body catch overlay (WR): code-driven layer-weight ramp so he catches IN STRIDE ----
        // Shared driver: only manages a layer once a caller opts in. QB/CB never call these, so untouched.
        [Tooltip("Animator layer name for the upper-body catch overlay (WR).")]
        public string overlayLayerName = "Actions";
        [Tooltip("Seconds for the catch overlay to blend 0->1.")]
        public float overlayBlendTime = 0.2f;
        int overlayLayer = -1;
        bool manageOverlay;
        float overlayWeight, overlayTarget;

        /// <summary>WR: ramp the upper-body catch layer toward full weight (catch in stride).</summary>
        public void SetCatchOverlay(bool on)
        {
            manageOverlay = true;
            overlayTarget = on ? 1f : 0f;
        }

        /// <summary>WR: ramp the catch overlay back to 0 (smooth ramp-OUT) at play reset / catch end.</summary>
        /// <summary>WR: snap the catch overlay back to 0 at play reset.</summary>
        public void ResetCatchOverlay()
        {
            manageOverlay = true;
            overlayTarget = 0f; // ramp OUT over overlayBlendTime so arms lower smoothly (no frozen arms over a running body)
        }

        void ApplyOverlay()
        {
            if (overlayLayer < 0 || animator == null) return;
            animator.SetLayerWeight(overlayLayer, overlayWeight);
        }

        void Update()
        {
            if (!manageOverlay || animator == null) return;
            if (overlayLayer < 0)
            {
                overlayLayer = animator.GetLayerIndex(overlayLayerName);
                if (overlayLayer < 0) { manageOverlay = false; return; } // no such layer on this character
            }
            if (!Mathf.Approximately(overlayWeight, overlayTarget))
            {
                float step = Time.deltaTime / Mathf.Max(0.001f, overlayBlendTime);
                overlayWeight = Mathf.MoveTowards(overlayWeight, overlayTarget, step);
                ApplyOverlay();
            }
        }


        // ---- AnimationEvent receivers (names must match clip events exactly) ----
        public void ReleaseBall() => ReleaseBallEvent?.Invoke();
        public void HandsOnBall() => HandsOnBallEvent?.Invoke();
        public void SwatContact() => SwatContactEvent?.Invoke();
        public void PlantFoot() => PlantFootEvent?.Invoke();
    }
}