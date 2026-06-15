using UnityEngine;
using UnityEngine.InputSystem;

namespace Gridiron
{
    /// <summary>
    /// Player-controlled QB (PRD §8.1). v1: stationary; Hike input, Throw input with
    /// tap = bullet / hold ≥ lobHoldThreshold = lob (§7.4), animation triggering.
    /// </summary>
    public class QBController : MonoBehaviour
    {
        public InputActionAsset actions;
        public BallLauncher launcher;
        public BallSocket socket;
        public CharacterAnimatorDriver animDriver;
        public QBFacing facing;
        ThrowType lastAimType = ThrowType.Bullet;

        InputAction hikeAction;
        InputAction throwAction;
        InputAction backAction;

        float chargeStart = -1f;
        public bool HasThrown { get; private set; }
        public bool IsCharging => chargeStart >= 0f && !HasThrown;
        public float ChargeTime => IsCharging ? Time.time - chargeStart : 0f;

        void Awake()
        {
            if (facing == null) facing = GetComponent<QBFacing>();
            if (facing != null) facing.AimProvider = GetAimPoint;
            if (actions != null)
            {
                var map = actions.FindActionMap("Gameplay", false);
                if (map != null)
                {
                    hikeAction = map.FindAction("Hike", false);
                    throwAction = map.FindAction("Throw", false);
                    backAction = map.FindAction("Back", false);
                    map.Enable();
                }
            }
        }

        public void ResetForPlay(Vector3 position)
        {
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(Vector3.forward); // face downfield
            HasThrown = false;
            chargeStart = -1f;
            launcher.ResetForPlay();
        }

        void Update()
        {
            var mm = MatchManager.Instance;
            if (mm == null) return;

            switch (mm.State)
            {
                case GameState.PreSnap:
                    if (Pressed(hikeAction)) { animDriver?.Trigger("Hike"); mm.RequestSnap(); }
                    else if (Pressed(backAction)) mm.BackToPlaySelect();
                    break;

                case GameState.RouteRunning:
                    if (HasThrown) break;
                    if (Pressed(throwAction)) chargeStart = Time.time;
                    if (chargeStart >= 0f && Released(throwAction))
                    {
                        ThrowType type = (Time.time - chargeStart) >= mm.rules.lobHoldThreshold
                            ? ThrowType.Lob : ThrowType.Bullet;
                        BeginThrow(type);
                    }
                    break;
            }
        }

        void BeginThrow(ThrowType type)
        {
            HasThrown = true;
            chargeStart = -1f;
            MatchManager.Instance.NotifyThrowStarted();
            lastAimType = type;
            if (facing != null) facing.BeginThrow(); // keep subtly tracking (heavier) through the windup
            animDriver?.Trigger("Throw");
            launcher.QueueLaunch(type); // actual launch on ReleaseBall anim event or fallback timer (§7.3)
        }

        /// <summary>The single shared aim point - the lead/catch point the ball is thrown to.</summary>
        public Vector3 GetAimPoint()
        {
            if (launcher != null) return launcher.ComputeAimPoint(lastAimType);
            return transform.position + transform.forward * 8f;
        }

        static bool Pressed(InputAction a) => a != null && a.WasPressedThisFrame();
        static bool Released(InputAction a) => a != null && a.WasReleasedThisFrame();
    }
}