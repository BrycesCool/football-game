using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Live tuner for the WR catch socket. Place on the CatchSocket GameObject (child of the
    /// hand bone). The serialized Local Position / Local Euler are written to this transform via
    /// OnValidate — which fires the INSTANT you edit a field in the Inspector, even while the game
    /// is PAUSED — and via an ExecuteAlways Update for edit mode. Because the caught ball is
    /// parented to this socket, moving the socket moves the ball with it immediately (the child
    /// follows its parent without needing any script to run), so you can pause on a catch, nudge
    /// these values, and watch the ball slide into the hands in real time. The gizmo sphere shows
    /// the target (ball-sized) even when no ball is attached.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CatchSocketTuner : MonoBehaviour
    {
        [Tooltip("Local position of the socket relative to its parent (hand) bone. Tune until the gizmo/ball sits in the hands.")]
        public Vector3 localPosition;

        [Tooltip("Local rotation (euler) of the socket relative to the parent bone.")]
        public Vector3 localEuler;

        [Header("Preview gizmo")]
        [Tooltip("Radius of the preview sphere (the football's short radius is ~0.09).")]
        public float gizmoRadius = 0.09f;
        public Color gizmoColor = new Color(1f, 0.55f, 0.1f, 0.6f);

        void OnEnable() => Apply();
        void OnValidate() => Apply();   // fires on every Inspector edit — works while paused
        void Update() => Apply();       // ExecuteAlways: keeps it applied in edit mode and play

        void Apply()
        {
            transform.localPosition = localPosition;
            transform.localRotation = Quaternion.Euler(localEuler);
        }

        /// <summary>Right-click the component header -> copies the socket's current transform into the fields.</summary>
        [ContextMenu("Capture Current Local Transform")]
        void Capture()
        {
            localPosition = transform.localPosition;
            localEuler = transform.localEulerAngles;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, gizmoRadius);
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * gizmoRadius * 2.5f);
        }
    }
}
