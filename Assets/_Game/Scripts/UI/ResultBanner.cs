using UnityEngine;
using UnityEngine.UI;

namespace Gridiron
{
    /// <summary>Big center result text for 2.5 s (PRD §11.3). Built at runtime; driven by OnPlayResolved.</summary>
    public class ResultBanner : MonoBehaviour
    {
        RectTransform root;
        Text text;
        MatchManager mm;

        void Start()
        {
            mm = MatchManager.Instance;
            root = UIFactory.Panel(transform, "Banner", new Color(0f, 0f, 0f, 0.6f));
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, 60f);
            root.sizeDelta = new Vector2(760f, 110f);
            text = UIFactory.Label(root, "Text", "", 52, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch((RectTransform)text.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.gameObject.SetActive(false);

            mm.OnPlayResolved += Show;
            mm.OnStateChanged += s => { if (s != GameState.PlayResult) root.gameObject.SetActive(false); };
        }

        void Show(PlayResult result, string banner, Vector3 spot)
        {
            text.text = banner;
            switch (result)
            {
                case PlayResult.Touchdown: text.color = new Color(1f, 0.85f, 0.2f); break;
                case PlayResult.Catch: text.color = new Color(0.4f, 1f, 0.5f); break;
                case PlayResult.Intercept: text.color = new Color(1f, 0.3f, 0.25f); break;
                default: text.color = Color.white; break;
            }
            root.gameObject.SetActive(true);
        }
    }
}