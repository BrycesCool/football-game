using UnityEngine;
using Unity.Cinemachine;

namespace Gridiron
{
    /// <summary>
    /// One CinemachineCamera per phase, blended by priority on MatchManager state change (PRD §11.1).
    /// Uses the Cinemachine 3.x API (CinemachineCamera + CinemachineBrain). Camera motion is
    /// script-driven on the vcam transforms; the Brain handles blending.
    /// </summary>
    public class CameraDirector : MonoBehaviour
    {
        public CinemachineCamera camPreSnap;
        public CinemachineCamera camQB;
        public CinemachineCamera camBall;
        public CinemachineCamera camResult;

        [Header("Offsets")]
        public Vector3 preSnapOffset = new Vector3(0f, 5.5f, -7f);
        public Vector3 qbOffset = new Vector3(0f, 5f, -6f);
        public float lookAheadDistance = 14f;
        public float ballCamDamping = 0.4f;
        public float resultOrbitSpeed = 30f;
        public float resultRadius = 6f;
        public float resultHeight = 3f;

        MatchManager mm;
        Vector3 resultSpot;
        float orbitAngle;
        Vector3 ballCamVelocity;

        void Start()
        {
            mm = MatchManager.Instance;
            if (mm != null)
            {
                mm.OnStateChanged += HandleState;
                mm.OnPlayResolved += (r, txt, spot) => { resultSpot = spot; orbitAngle = 0f; };
            }
            HandleState(GameState.MainMenu);
        }

        void HandleState(GameState s)
        {
            SetPriority(camPreSnap, s == GameState.PlaySelect || s == GameState.PreSnap || s == GameState.MainMenu || s == GameState.MatchEnd ? 20 : 0);
            SetPriority(camQB, s == GameState.RouteRunning ? 20 : 0);
            SetPriority(camBall, s == GameState.BallInAir ? 20 : 0);
            SetPriority(camResult, s == GameState.PlayResult ? 20 : 0);
        }

        static void SetPriority(CinemachineCamera cam, int p)
        {
            if (cam != null) cam.Priority = p;
        }

        void LateUpdate()
        {
            if (mm == null || mm.qb == null) return;
            Vector3 qbPos = mm.qb.transform.position;

            // Behind & above the QB, looking downfield — whole route area visible (§11.1)
            // Aim biased toward the WR so the whole route area (WR & CB) stays on screen (PRD 11.1)
            Vector3 wrAim = mm.wr != null ? mm.wr.transform.position : qbPos + Vector3.forward * lookAheadDistance;
            Vector3 downfieldAim = qbPos + Vector3.forward * lookAheadDistance + Vector3.up;
            Vector3 aim = Vector3.Lerp(downfieldAim, wrAim + Vector3.up, 0.45f);
            PlaceLookAt(camPreSnap, qbPos + preSnapOffset, aim);
            PlaceLookAt(camQB, qbPos + qbOffset, aim);

            if (camBall != null && mm.ball != null)
            {
                Vector3 ballPos = mm.ball.transform.position;
                Vector3 wrPos = mm.wr != null ? mm.wr.transform.position : ballPos;
                Vector3 desired = ballPos + new Vector3(0f, 3f, -6f);
                camBall.transform.position = Vector3.SmoothDamp(camBall.transform.position, desired, ref ballCamVelocity, ballCamDamping);
                Vector3 lookAt = (ballPos + wrPos) * 0.5f + Vector3.up; // midpoint of ball & WR (§11.1)
                camBall.transform.rotation = Quaternion.LookRotation(lookAt - camBall.transform.position);
            }

            if (camResult != null && mm.State == GameState.PlayResult)
            {
                orbitAngle += resultOrbitSpeed * Time.deltaTime;
                float rad = orbitAngle * Mathf.Deg2Rad;
                Vector3 pos = resultSpot + new Vector3(Mathf.Sin(rad) * resultRadius, resultHeight, Mathf.Cos(rad) * resultRadius);
                camResult.transform.position = pos;
                camResult.transform.rotation = Quaternion.LookRotation((resultSpot + Vector3.up) - pos);
            }
        }

        static void PlaceLookAt(CinemachineCamera cam, Vector3 pos, Vector3 lookAt)
        {
            if (cam == null) return;
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - pos);
        }
    }
}