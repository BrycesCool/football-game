using UnityEngine;

namespace Gridiron
{
    public enum BallState { HeldQB, InFlight, CaughtWR, CaughtCB, Swatted, DeadGround }

    /// <summary>
    /// The football (PRD §7.1/§7.2). Rigidbody projectile while in flight; kinematic + parented otherwise.
    /// Spiral is purely cosmetic (visual child aligned to velocity each frame).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Football : MonoBehaviour
    {
        public BallState State { get; private set; } = BallState.HeldQB;
        /// <summary>False once swatted/caught/dead — no further catch attempts allowed.</summary>
        public bool IsLive { get; private set; }

        [Tooltip("Visual mesh child, long axis along +Z. Spun cosmetically while in flight.")]
        public Transform visual;
        public TrailRenderer trail;
        public float spinDegPerSec = 900f;

        public ThrowData LastThrow { get; private set; }
        /// <summary>Current physics velocity — used by the WR's ball-tracking projection (audit H4).</summary>
        public Vector3 Velocity => rb != null && !rb.isKinematic ? rb.linearVelocity : Vector3.zero;

        float gravityScale = 1.15f;
        float spinAngle;
        Rigidbody rb;

        // Audit H7: smooth attach — lerp from flight pose into the hand instead of teleporting.
        float attachLerpDuration;
        float attachLerpRemaining;
        Vector3 attachStartLocalPos;
        Quaternion attachStartLocalRot;
        Vector3 attachTargetLocalPos;                          // lerp/pin destination (defaults to grip pose; WR catch overrides)
        Quaternion attachTargetLocalRot = Quaternion.identity;
        // Catch approach: home the ball into the WR catch socket for its final leg so it arrives AT
        // the hands (no flying into the body + snap). Started on a confirmed catch, cleared on attach.
        bool approaching;
        Transform approachSocket;
        Vector3 approachOffset;
        float approachDuration, approachTimer;
        Vector3 approachStartPos;
        [Header("Hand grip pose")]
        [Tooltip("Local position offset from the hand socket; tune so the ball sits in the palm instead of the bone pivot.")]
        public Vector3 gripLocalPosition;
        [Tooltip("Local euler rotation offset from the hand socket.")]
        public Vector3 gripLocalEuler;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = 0.42f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true;
            rb.useGravity = false;
            if (trail != null) trail.emitting = false;
        }

        /// <summary>Kinematic, parented to a hand socket (pre-snap / after catch).</summary>
        public void AttachTo(Transform socket, BallState newState, float lerpTime = 0f)
        {
            approaching = false;
            rb.isKinematic = true;
            rb.useGravity = false;
            if (lerpTime > 0f)
            {
                transform.SetParent(socket, true); // keep world pose, then ease into the hand (audit H7)
                attachLerpDuration = attachLerpRemaining = lerpTime;
                attachStartLocalPos = transform.localPosition;
                attachStartLocalRot = transform.localRotation;
                attachTargetLocalPos = gripLocalPosition;
                attachTargetLocalRot = Quaternion.Euler(gripLocalEuler);
            }
            else
            {
                transform.SetParent(socket, false);
                attachTargetLocalPos = gripLocalPosition;
                attachTargetLocalRot = Quaternion.Euler(gripLocalEuler);
                transform.localPosition = attachTargetLocalPos;
                transform.localRotation = attachTargetLocalRot;
                attachLerpRemaining = 0f;
            }
            State = newState;
            IsLive = false;
            if (trail != null) { trail.emitting = false; trail.Clear(); }
        }

        /// <summary>WR in-stride catch: ease the ball into a WR-only catch socket at a custom local offset (no teleport).</summary>
        public void CatchInto(Transform socket, Vector3 localPos, Vector3 localEuler, float lerpTime)
        {
            approaching = false;
            rb.isKinematic = true;
            rb.useGravity = false;
            transform.SetParent(socket, true); // keep current world pose, then ease in
            attachLerpDuration = attachLerpRemaining = Mathf.Max(0.0001f, lerpTime);
            attachStartLocalPos = transform.localPosition;
            attachStartLocalRot = transform.localRotation;
            attachTargetLocalPos = localPos;
            attachTargetLocalRot = Quaternion.Euler(localEuler);
            State = BallState.CaughtWR;
            IsLive = false;
            if (trail != null) { trail.emitting = false; trail.Clear(); }
        }

        public void Launch(ThrowData data, float gravityScale)
        {
            this.gravityScale = gravityScale;
            transform.SetParent(null, true); // detach but KEEP current world pose — launch from the hand, not a hardcoded point
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None; // clear any held-state freeze before flight
            rb.linearVelocity = data.velocity;
            rb.angularVelocity = Vector3.zero;
            LastThrow = data;
            State = BallState.InFlight;
            IsLive = true;
            if (trail != null) { trail.Clear(); trail.emitting = true; }
        }

        public void CatchBy(Transform socket, bool byCB, float lerpTime = 0f)
        {
            AttachTo(socket, byCB ? BallState.CaughtCB : BallState.CaughtWR, lerpTime);
        }

        /// <summary>CB deflection (PRD §8.4): ball becomes uncatchable, gets an impulse. Result already resolved at swat moment.</summary>
        public void Swat()
        {
            if (State != BallState.InFlight) return;
            State = BallState.Swatted;
            IsLive = false;
            Vector2 r = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3f);
            rb.linearVelocity = new Vector3(r.x, -2f, r.y);
        }

        /// <summary>
        /// Failed WR catch roll: ball deflects off hands (PRD §8.2).
        /// Catch PRD R6: deflection derives from the incoming velocity (damped to 25 %, perturbed),
        /// from the ball's actual position — not a random pop.
        /// </summary>
        /// <summary>WR catch: redirect the ball's final approach so it flies INTO the catch socket (hands)
        /// instead of the body, then attaches there — no teleport. Called on a confirmed catch.</summary>
        public void BeginCatchApproach(Transform socket, Vector3 localOffset, float duration)
        {
            if (socket == null) return;
            approachSocket = socket;
            approachOffset = localOffset;
            approachDuration = Mathf.Max(0.0001f, duration);
            approachTimer = approachDuration;
            approachStartPos = transform.position;
            approaching = true;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        public void DeflectOffHands(Vector3 incomingVelocity)
        {
            if (State != BallState.InFlight) return;
            State = BallState.Swatted; // dead ball, same physical behavior
            IsLive = false;
            Vector3 v = incomingVelocity * 0.25f + Random.insideUnitSphere * 0.6f;
            v.y = Mathf.Max(v.y, 0.4f); // hands knock it up slightly before it dies
            rb.linearVelocity = v;
        }

        void FixedUpdate()
        {
            if (!rb.isKinematic && gravityScale > 1f)
            {
                // Floaty-feel tuning via extra gravity (PRD §7.1): apply (gravityScale-1)·g
                rb.AddForce(Vector3.down * (9.81f * (gravityScale - 1f)), ForceMode.Acceleration);
            }
        }

        void Update()
        {
            // Catch approach: fly the ball into the catch socket for its final leg (no body pass-through).
            if (approaching && approachSocket != null && transform.parent == null)
            {
                approachTimer -= Time.deltaTime;
                float u = 1f - Mathf.Clamp01(approachTimer / approachDuration);
                u = u * u * (3f - 2f * u); // smoothstep ease into the hands
                transform.position = Vector3.Lerp(approachStartPos, approachSocket.TransformPoint(approachOffset), u);
                if (approachTimer <= 0f) approaching = false;
                return;
            }
            // Audit H7: ease the just-caught ball into the hand socket.
            if (attachLerpRemaining > 0f && transform.parent != null)
            {
                attachLerpRemaining -= Time.deltaTime;
                float t = 1f - Mathf.Clamp01(attachLerpRemaining / attachLerpDuration);
                transform.localPosition = Vector3.Lerp(attachStartLocalPos, attachTargetLocalPos, t);
                transform.localRotation = Quaternion.Slerp(attachStartLocalRot, attachTargetLocalRot, t);
            }
            else if (transform.parent != null &&
                     (State == BallState.HeldQB || State == BallState.CaughtWR || State == BallState.CaughtCB))
            {
                // Live grip tuning: keep a held ball pinned to the grip pose each frame.
                transform.localPosition = attachTargetLocalPos;
                transform.localRotation = attachTargetLocalRot;
            }

            if (visual == null) return;
            if (State == BallState.InFlight || State == BallState.Swatted)
            {
                Vector3 v = rb.linearVelocity;
                if (v.sqrMagnitude > 0.01f)
                {
                    spinAngle += spinDegPerSec * Time.deltaTime;
                    visual.rotation = Quaternion.LookRotation(v.normalized) * Quaternion.AngleAxis(spinAngle, Vector3.forward);
                }
            }
            else
            {
                visual.localRotation = Quaternion.identity;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (State == BallState.InFlight)
            {
                State = BallState.DeadGround;
                IsLive = false;
                if (trail != null) trail.emitting = false;
                var mm = MatchManager.Instance;
                if (mm != null && mm.resolver != null) mm.resolver.ReportGround(transform.position);
            }
            else if (State == BallState.Swatted)
            {
                if (trail != null) trail.emitting = false;
            }
        }
    }
}