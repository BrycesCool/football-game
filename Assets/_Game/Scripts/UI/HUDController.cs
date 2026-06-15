using UnityEngine;
using UnityEngine.UI;

namespace Gridiron
{
    /// <summary>In-play HUD (PRD §11.3): top bar, play-clock radial, throw-charge indicator. Built at runtime.</summary>
    public class HUDController : MonoBehaviour
    {
        Text topBar;
        Image playClockRadial;
        Text playClockText;
        Image chargeBar;
        RectTransform chargeRoot;
        RectTransform clockRoot;
        MatchManager mm;

        void Start()
        {
            mm = MatchManager.Instance;
            Build();
            mm.OnStateChanged += _ => RefreshVisibility();
            RefreshVisibility();
        }

        void Build()
        {
            // Top bar
            var barRt = UIFactory.Panel(transform, "TopBar", new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Stretch(barRt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -42f), new Vector2(0f, 0f));
            topBar = UIFactory.Label(barRt, "Text", "", 22, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch((RectTransform)topBar.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Play clock radial (visible only in ROUTE_RUNNING)
            clockRoot = UIFactory.Panel(transform, "PlayClock", new Color(0f, 0f, 0f, 0.4f));
            clockRoot.anchorMin = clockRoot.anchorMax = new Vector2(1f, 1f);
            clockRoot.anchoredPosition = new Vector2(-60f, -100f);
            clockRoot.sizeDelta = new Vector2(72f, 72f);
            playClockRadial = UIFactory.Radial(clockRoot, "Fill", new Color(0.3f, 0.9f, 0.4f));
            UIFactory.Stretch((RectTransform)playClockRadial.transform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            playClockText = UIFactory.Label(clockRoot, "Num", "6.0", 24, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch((RectTransform)playClockText.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Throw charge indicator (fills to LOB at threshold)
            chargeRoot = UIFactory.Panel(transform, "ThrowCharge", new Color(0f, 0f, 0f, 0.5f));
            chargeRoot.anchorMin = chargeRoot.anchorMax = new Vector2(0.5f, 0f);
            chargeRoot.anchoredPosition = new Vector2(0f, 70f);
            chargeRoot.sizeDelta = new Vector2(260f, 26f);
            chargeBar = UIFactory.Bar(chargeRoot, "Fill", new Color(1f, 0.75f, 0.2f));
            UIFactory.Stretch((RectTransform)chargeBar.transform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            var lbl = UIFactory.Label(chargeRoot, "Lbl", "HOLD = LOB", 14, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch((RectTransform)lbl.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        void RefreshVisibility()
        {
            bool inPlay = mm.State == GameState.PreSnap || mm.State == GameState.RouteRunning ||
                          mm.State == GameState.BallInAir || mm.State == GameState.PlayResult ||
                          mm.State == GameState.PlaySelect;
            topBar.transform.parent.gameObject.SetActive(inPlay);
        }

        void Update()
        {
            if (mm == null || mm.Drive == null) return;

            if (topBar != null && topBar.transform.parent.gameObject.activeSelf)
                topBar.text = mm.HudLine;

            bool clockVisible = mm.State == GameState.RouteRunning;
            if (clockRoot != null && clockRoot.gameObject.activeSelf != clockVisible)
                clockRoot.gameObject.SetActive(clockVisible);
            if (clockVisible)
            {
                float t = mm.PlayClockRemaining;
                float frac = mm.rules.playClockSeconds > 0f ? t / mm.rules.playClockSeconds : 0f;
                playClockRadial.fillAmount = Mathf.Clamp01(frac);
                playClockText.text = t.ToString("0.0");
                bool danger = t < 2f;
                Color c = danger ? new Color(1f, 0.25f, 0.2f) : new Color(0.3f, 0.9f, 0.4f);
                if (danger && Mathf.PingPong(Time.time * 4f, 1f) > 0.5f) c = new Color(1f, 0.6f, 0.2f); // flash red < 2 s
                playClockRadial.color = c;
            }

            bool charging = mm.qb != null && mm.qb.IsCharging && mm.State == GameState.RouteRunning;
            if (chargeRoot != null && chargeRoot.gameObject.activeSelf != charging)
                chargeRoot.gameObject.SetActive(charging);
            if (charging)
                chargeBar.fillAmount = Mathf.Clamp01(mm.qb.ChargeTime / mm.rules.lobHoldThreshold);
        }
    }
}