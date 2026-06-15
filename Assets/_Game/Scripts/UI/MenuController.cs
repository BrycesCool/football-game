using UnityEngine;
using UnityEngine.UI;

namespace Gridiron
{
    /// <summary>Main menu + match-end screen (PRD §11.4). Built at runtime as panels in the Main scene.</summary>
    public class MenuController : MonoBehaviour
    {
        RectTransform mainPanel;
        RectTransform endPanel;
        Text difficultyLabel;
        Text endStats;
        MatchManager mm;

        static readonly string[] DifficultyNames = { "EASY", "NORMAL", "HARD" };

        void Start()
        {
            mm = MatchManager.Instance;
            BuildMainMenu();
            BuildEndScreen();
            mm.OnStateChanged += HandleState;
            mm.OnMatchEnd += HandleMatchEnd;
            HandleState(mm.State);
        }

        void BuildMainMenu()
        {
            mainPanel = UIFactory.Panel(transform, "MainMenu", new Color(0.02f, 0.05f, 0.09f, 0.95f));
            UIFactory.Stretch(mainPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var title = UIFactory.Label(mainPanel, "Title", "1v1 GRIDIRON", 64, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f));
            var trt = (RectTransform)title.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.72f);

            var sub = UIFactory.Label(mainPanel, "Sub", "ONE ROUTE. ONE THROW. BEAT THE CORNER.", 18, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.8f));
            var srt = (RectTransform)sub.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.63f);

            var play = UIFactory.TextButton(mainPanel, "Play", "PLAY", 26, new Vector2(280f, 60f), () => mm.StartMatch());
            SetCenter(play, 0.46f);

            var diff = UIFactory.TextButton(mainPanel, "Difficulty", "", 20, new Vector2(280f, 52f), CycleDifficulty);
            SetCenter(diff, 0.35f);
            difficultyLabel = diff.GetComponentInChildren<Text>();
            UpdateDifficultyLabel();

            var quit = UIFactory.TextButton(mainPanel, "Quit", "QUIT", 20, new Vector2(280f, 52f), () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
            SetCenter(quit, 0.24f);
        }

        void BuildEndScreen()
        {
            endPanel = UIFactory.Panel(transform, "MatchEnd", new Color(0.02f, 0.05f, 0.09f, 0.95f));
            UIFactory.Stretch(endPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var title = UIFactory.Label(endPanel, "Title", "FINAL", 56, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f));
            var trt = (RectTransform)title.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.8f);

            endStats = UIFactory.Label(endPanel, "Stats", "", 24, TextAnchor.MiddleCenter, Color.white, FontStyle.Normal);
            var ert = (RectTransform)endStats.transform;
            ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0.55f);

            var rematch = UIFactory.TextButton(endPanel, "Rematch", "REMATCH", 24, new Vector2(280f, 56f), () => mm.StartMatch());
            SetCenter(rematch, 0.3f);

            var menu = UIFactory.TextButton(endPanel, "Menu", "MENU", 20, new Vector2(280f, 50f), () => mm.QuitToMenu());
            SetCenter(menu, 0.2f);
        }

        void SetCenter(Button b, float anchorY)
        {
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY);
        }

        void CycleDifficulty()
        {
            mm.SetDifficulty((mm.difficultyIndex + 1) % mm.difficulties.Length);
            UpdateDifficultyLabel();
        }

        void UpdateDifficultyLabel()
        {
            string name = mm.difficultyIndex < DifficultyNames.Length ? DifficultyNames[mm.difficultyIndex] : mm.Difficulty.label.ToUpperInvariant();
            if (difficultyLabel != null) difficultyLabel.text = "DIFFICULTY: " + name;
        }

        void HandleState(GameState s)
        {
            if (mainPanel != null) mainPanel.gameObject.SetActive(s == GameState.MainMenu);
            if (endPanel != null) endPanel.gameObject.SetActive(s == GameState.MatchEnd);
        }

        void HandleMatchEnd(MatchResult r)
        {
            endStats.text =
                "SCORE  " + r.score + "        GRADE  " + r.grade + "\n\n" +
                "Completions  " + r.stats.completions + " / " + r.stats.attempts + "  (" + r.stats.CompletionPercent + "%)\n" +
                "Yards  " + Mathf.RoundToInt(r.stats.yards) + "\n" +
                "Touchdowns  " + r.stats.touchdowns + "\n" +
                "Interceptions  " + r.stats.interceptions + "\n" +
                "Longest play  " + Mathf.RoundToInt(r.stats.longestPlay) + " yds";
        }
    }
}