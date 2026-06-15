using System;
using System.Collections;
using UnityEngine;

namespace Gridiron
{
    /// <summary>
    /// Master state machine (PRD §5). The ONLY system allowed to advance game flow.
    /// Also the single allowed singleton/service locator (PRD §3).
    /// Tracks currentLOS/first-down target/down/drive/score (§10.4).
    /// </summary>
    public class MatchManager : MonoBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Header("Config")]
        public MatchRules rules;
        public CBDifficulty[] difficulties;
        public int difficultyIndex = 1;
        public PlayDefinition[] playbook;

        [Header("Scene refs")]
        public QBController qb;
        public WRController wr;
        public CBController cb;
        public Football ball;
        public FieldBounds field;
        public PlayResolver resolver;
        public Transform losMarker;
        public Transform firstDownMarker;

        // ---- State ----
        public GameState State { get; private set; } = GameState.Boot;
        public PlayDefinition CurrentPlay { get; private set; }
        public bool CurrentPlayMirrored { get; private set; }
        public DriveState Drive { get; private set; }
        public int DriveIndex { get; private set; }   // 0-based
        public int Score { get; private set; }
        public MatchStats Stats { get; } = new MatchStats();
        public float PlayClockRemaining { get; private set; }
        public ThrowData LastThrow { get; private set; }
        public PlayResult LastResult { get; private set; }

        public CBDifficulty Difficulty => difficulties[Mathf.Clamp(difficultyIndex, 0, difficulties.Length - 1)];

        // ---- Events (PRD §5.2) ----
        public event Action<GameState> OnStateChanged;
        public event Action OnSnap;
        public event Action<ThrowData> OnThrow;
        public event Action<PlayResult, string, Vector3> OnPlayResolved; // result, banner text, spot
        public event Action OnScoreChanged;
        public event Action<MatchResult> OnMatchEnd;

        Coroutine playClockRoutine;
        Coroutine racRoutine;
        bool throwStarted;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Time.maximumDeltaTime = 0.05f; // cap per-frame sim step: editor hitches must never teleport characters
            Drive = new DriveState(45.72f, rules != null ? rules.sackPenaltyYards : 5f);
        }

        void Start()
        {
            SetState(GameState.Boot);
            SetState(GameState.MainMenu);
        }

        void SetState(GameState s)
        {
            State = s;
            OnStateChanged?.Invoke(s);
        }

        // ================= Match lifecycle =================

        public void StartMatch()
        {
            Score = 0;
            Stats.Reset();
            DriveIndex = -1;
            SetState(GameState.MatchStart);
            StartNextDrive();
        }

        public void SetDifficulty(int index) => difficultyIndex = Mathf.Clamp(index, 0, difficulties.Length - 1);

        public void QuitToMenu()
        {
            StopAllPlayRoutines();
            SetState(GameState.MainMenu);
        }

        void StartNextDrive()
        {
            DriveIndex++;
            if (DriveIndex >= rules.drivesPerMatch)
            {
                EndMatch();
                return;
            }
            float startZ = -field.goalLineZ + rules.startOwnYardLine * FieldBounds.YardsToMeters; // own 25 (§10.1)
            Drive.StartDrive(startZ);
            SetState(GameState.PlaySelect);
        }

        void EndMatch()
        {
            var result = new MatchResult { score = Score, stats = Stats, grade = Stats.Grade(Score) };
            SetState(GameState.MatchEnd);
            OnMatchEnd?.Invoke(result);
        }

        // ================= Per-play flow =================

        /// <summary>From PlaySelectUI: player picked a play (§5.1).</summary>
        public void SelectPlay(PlayDefinition play, bool mirrored)
        {
            if (State != GameState.PlaySelect) return;
            CurrentPlay = play;
            CurrentPlayMirrored = mirrored;
            EnterPreSnap();
        }

        public void BackToPlaySelect()
        {
            if (State != GameState.PreSnap) return;
            SetState(GameState.PlaySelect);
        }

        void EnterPreSnap()
        {
            throwStarted = false;
            resolver.BeginPlay();

            float losZ = Drive.LosZ;
            float mirror = CurrentPlayMirrored ? -1f : 1f;

            // QB shotgun at LOS −1.5 m, center of formation (§5.1)
            qb.ResetForPlay(new Vector3(0f, 0f, losZ - 1.5f));

            // WR at the play's receiverAlignment offset, kept inside the field
            Vector2 a = CurrentPlay.receiverAlignment;
            Vector3 wrPos = field.ClampToField(new Vector3(a.x * mirror, 0f, losZ + a.y), 1f);
            wrPos.z = losZ + a.y;
            wr.ResetForPlay(wrPos, CurrentPlay.route);

            // CB at cushion downfield of WR, facing him (§5.1)
            cb.ResetForPlay(new Vector3(wrPos.x, 0f, wrPos.z + rules.cbCushion));

            // Ball kinematic, parented to QB hand
            ball.AttachTo(qb.socket.Socket, BallState.HeldQB);

            UpdateMarkers();
            SetState(GameState.PreSnap);
        }

        void UpdateMarkers()
        {
            if (losMarker != null) losMarker.position = new Vector3(0f, 0f, Drive.LosZ);
            if (firstDownMarker != null) firstDownMarker.position = new Vector3(0f, 0f, Drive.FirstDownTargetZ);
        }

        /// <summary>From QBController: Hike pressed (§5.1).</summary>
        public void RequestSnap()
        {
            if (State != GameState.PreSnap) return;
            SetState(GameState.RouteRunning);
            wr.OnSnapped(CurrentPlay.route, CurrentPlayMirrored);
            cb.OnSnapped();
            OnSnap?.Invoke();
            playClockRoutine = StartCoroutine(PlayClock());
        }

        IEnumerator PlayClock()
        {
            PlayClockRemaining = rules.playClockSeconds;
            while (PlayClockRemaining > 0f)
            {
                if (State != GameState.RouteRunning || throwStarted) yield break;
                PlayClockRemaining -= Time.deltaTime;
                yield return null;
            }
            // Held it too long (§5.1): lost down, −5 yd
            ResolvePlay(PlayResult.SackTimerExpired, qb.transform.position);
        }

        /// <summary>Throw input accepted — freeze the play clock while the throw animation winds up.</summary>
        public void NotifyThrowStarted()
        {
            throwStarted = true;
        }

        /// <summary>From BallLauncher at the actual release moment.</summary>
        public void NotifyThrow(ThrowData data)
        {
            if (State != GameState.RouteRunning) return;
            LastThrow = data;
            Stats.attempts++;
            SetState(GameState.BallInAir);
            wr.OnThrowNotify(data);
            cb.OnThrowNotify(data);
            OnThrow?.Invoke(data);
        }

        /// <summary>From PlayResolver: in-bounds catch outside the end zone — run-after-catch window (§8.2, §9.4).</summary>
        public void BeginRunAfterCatch(Vector3 catchSpot)
        {
            wr.BeginRunAfterCatch();
            racRoutine = StartCoroutine(RunAfterCatch());
        }

        IEnumerator RunAfterCatch()
        {
            float t = 0f;
            bool touchdown = false;
            bool tackled = false;
            while (t < rules.racWindowSeconds)
            {
                t += Time.deltaTime;
                if (wr.transform.position.z >= field.goalLineZ) { touchdown = true; break; }
                float cbDist = Vector3.Distance(
                    new Vector3(cb.transform.position.x, 0f, cb.transform.position.z),
                    new Vector3(wr.transform.position.x, 0f, wr.transform.position.z));
                if (cbDist <= rules.tackleDistance) { tackled = true; break; } // contact: spot the ball here
                yield return null;
            }
            Vector3 spot = field.ClampToField(wr.transform.position, 0f);

            // Audit H5: tackles exist as motion — short steer-together window with full-body
            // Tackle/Fall one-shots (safe no-ops until tackle clips are added to Assets/Animations).
            if (tackled && !touchdown && rules.tackleSequenceSeconds > 0f)
            {
                wr.EndPlay();
                cb.EndPlay();
                var wrDriver = wr.animDriver;
                var cbDriver = cb.animDriver;
                cbDriver?.Trigger("Tackle");
                wrDriver?.Trigger("Fall");
                float cbSpeed = cb.Speed;
                float seq = 0f;
                while (seq < rules.tackleSequenceSeconds)
                {
                    float dt = Mathf.Min(Time.deltaTime, 0.04f);
                    seq += dt;
                    // steer the CB into the WR and square both up
                    Vector3 toWR = wr.transform.position - cb.transform.position; toWR.y = 0f;
                    if (toWR.magnitude > 0.45f)
                        cb.transform.position = Vector3.MoveTowards(cb.transform.position, wr.transform.position, cbSpeed * dt);
                    if (toWR.sqrMagnitude > 1e-4f)
                    {
                        Quaternion face = Quaternion.LookRotation(toWR.normalized);
                        cb.transform.rotation = Quaternion.RotateTowards(cb.transform.rotation, face, 720f * dt);
                        wr.transform.rotation = Quaternion.RotateTowards(wr.transform.rotation, Quaternion.LookRotation(-toWR.normalized), 720f * dt);
                    }
                    yield return null;
                }
            }
            ResolvePlay(touchdown ? PlayResult.Touchdown : PlayResult.Catch, spot);
        }

        /// <summary>Central outcome application (§9, §10). Called by PlayResolver / play clock / RAC.</summary>
        public void ResolvePlay(PlayResult result, Vector3 spot)
        {
            if (State == GameState.PlayResult || State == GameState.MatchEnd || State == GameState.MainMenu) return;
            StopAllPlayRoutines();

            float losBefore = Drive.LosZ;
            float gainYards = (Mathf.Min(spot.z, field.goalLineZ) - losBefore) / FieldBounds.YardsToMeters;

            // Stats
            switch (result)
            {
                case PlayResult.Catch:
                    Stats.completions++;
                    Stats.yards += gainYards;
                    Stats.longestPlay = Mathf.Max(Stats.longestPlay, gainYards);
                    break;
                case PlayResult.Touchdown:
                    Stats.completions++;
                    Stats.touchdowns++;
                    float tdGain = (field.goalLineZ - losBefore) / FieldBounds.YardsToMeters;
                    Stats.yards += tdGain;
                    Stats.longestPlay = Mathf.Max(Stats.longestPlay, tdGain);
                    break;
                case PlayResult.Intercept:
                    Stats.interceptions++;
                    break;
            }

            DriveOutcome outcome = Drive.ApplyResult(result, spot.z);
            if (outcome == DriveOutcome.Touchdown)
            {
                Score += rules.pointsPerTD;
                OnScoreChanged?.Invoke();
            }

            LastResult = result;
            wr.EndPlay();
            cb.EndPlay();
            UpdateMarkers();

            string banner = BannerText(result, gainYards);
            SetState(GameState.PlayResult);
            OnPlayResolved?.Invoke(result, banner, spot);
            StartCoroutine(AfterResult());
        }

        IEnumerator AfterResult()
        {
            yield return new WaitForSeconds(rules.resultBannerSeconds); // 2.5 s banner (§5.1)
            if (Drive.DriveOver) StartNextDrive();
            else SetState(GameState.PlaySelect);
        }

        string BannerText(PlayResult result, float gainYards)
        {
            switch (result)
            {
                case PlayResult.Catch: return "COMPLETE +" + Mathf.Max(0, Mathf.RoundToInt(gainYards)) + " YDS";
                case PlayResult.Touchdown: return "TOUCHDOWN!";
                case PlayResult.Swat: return "SWATTED";
                case PlayResult.Intercept: return "INTERCEPTED — DRIVE OVER";
                case PlayResult.OutOfBounds: return "OUT OF BOUNDS";
                case PlayResult.SackTimerExpired: return "TOO LATE!";
                default: return "INCOMPLETE";
            }
        }

        void StopAllPlayRoutines()
        {
            if (playClockRoutine != null) { StopCoroutine(playClockRoutine); playClockRoutine = null; }
            if (racRoutine != null) { StopCoroutine(racRoutine); racRoutine = null; }
            PlayClockRemaining = 0f;
        }

        // ---- HUD helpers (§11.3) ----
        public string HudLine =>
            "DRIVE " + (DriveIndex + 1) + "/" + rules.drivesPerMatch +
            "  ·  " + Drive.DownDistanceText +
            "  ·  BALL ON " + field.YardLineLabel(Drive.LosZ) +
            "  ·  SCORE " + Score;
    }
}