using UnityEngine;
using UnityEngine.UI;

namespace Gridiron
{
    /// <summary>
    /// Play-select grid of 8 cards (PRD §11.2): procedural route diagram + name + BULLET/LOB hint,
    /// plus a Mirror toggle that flips the route to the other side of the field.
    /// </summary>
    public class PlaySelectUI : MonoBehaviour
    {
        RectTransform panel;
        bool mirrored;
        Text mirrorLabel;
        RawImage[] cardArts;
        MatchManager mm;
        bool built;

        void Start()
        {
            mm = MatchManager.Instance;
            mm.OnStateChanged += HandleState;
            HandleState(mm.State);
        }

        void HandleState(GameState s)
        {
            bool show = s == GameState.PlaySelect;
            if (show && !built) Build();
            if (panel != null) panel.gameObject.SetActive(show);
        }

        void Build()
        {
            built = true;
            panel = UIFactory.Panel(transform, "PlaySelect", new Color(0.03f, 0.06f, 0.1f, 0.88f));
            UIFactory.Stretch(panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var title = UIFactory.Label(panel, "Title", "PICK A PLAY", 40, TextAnchor.MiddleCenter, Color.white);
            var trt = (RectTransform)title.transform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -50f);

            // Mirror toggle (§11.2)
            var mirrorBtn = UIFactory.TextButton(panel, "Mirror", "", 18, new Vector2(190f, 44f), ToggleMirror);
            var mrt = (RectTransform)mirrorBtn.transform;
            mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 1f);
            mrt.anchoredPosition = new Vector2(0f, -105f);
            mirrorLabel = mirrorBtn.GetComponentInChildren<Text>();
            UpdateMirrorLabel();

            // Grid
            var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(panel, false);
            var grt = (RectTransform)gridGo.transform;
            UIFactory.Stretch(grt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            grt.sizeDelta = new Vector2(4 * 180f + 60f, 2 * 240f + 20f);
            grt.anchoredPosition = new Vector2(0f, -40f);
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(180f, 240f);
            grid.spacing = new Vector2(16f, 16f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            var plays = mm.playbook;
            cardArts = new RawImage[plays.Length];
            for (int i = 0; i < plays.Length; i++)
            {
                var play = plays[i];
                var card = UIFactory.TextButton(gridGo.transform, "Card_" + play.playName, "", 16, new Vector2(180f, 240f),
                    () => mm.SelectPlay(play, mirrored));
                // remove default centered label, build card content
                var defaultLabel = card.GetComponentInChildren<Text>();
                if (defaultLabel != null) Destroy(defaultLabel.gameObject);

                var artGo = new GameObject("Art", typeof(RectTransform), typeof(RawImage));
                artGo.transform.SetParent(card.transform, false);
                var art = artGo.GetComponent<RawImage>();
                art.texture = PlayArtGenerator.Generate(play.route);
                art.raycastTarget = false;
                UIFactory.Stretch((RectTransform)artGo.transform, new Vector2(0f, 0.28f), new Vector2(1f, 1f), new Vector2(8f, 4f), new Vector2(-8f, -8f));
                cardArts[i] = art;

                var name = UIFactory.Label(card.transform, "Name", play.playName.ToUpperInvariant(), 18, TextAnchor.MiddleCenter, Color.white);
                UIFactory.Stretch((RectTransform)name.transform, new Vector2(0f, 0.14f), new Vector2(1f, 0.28f), Vector2.zero, Vector2.zero);

                string hint = play.hint == ThrowProfileHint.BulletFriendly ? "● BULLET"
                            : play.hint == ThrowProfileHint.LobFriendly ? "◠ LOB" : "● / ◠ EITHER";
                Color hintCol = play.hint == ThrowProfileHint.BulletFriendly ? new Color(0.5f, 0.85f, 1f)
                            : play.hint == ThrowProfileHint.LobFriendly ? new Color(1f, 0.8f, 0.35f) : new Color(0.8f, 0.8f, 0.8f);
                var hintLbl = UIFactory.Label(card.transform, "Hint", hint, 14, TextAnchor.MiddleCenter, hintCol);
                UIFactory.Stretch((RectTransform)hintLbl.transform, new Vector2(0f, 0f), new Vector2(1f, 0.14f), Vector2.zero, Vector2.zero);
            }
        }

        void ToggleMirror()
        {
            mirrored = !mirrored;
            UpdateMirrorLabel();
            if (cardArts != null)
            {
                foreach (var art in cardArts)
                    if (art != null) art.uvRect = mirrored ? new Rect(1f, 0f, -1f, 1f) : new Rect(0f, 0f, 1f, 1f);
            }
        }

        void UpdateMirrorLabel()
        {
            if (mirrorLabel != null) mirrorLabel.text = "MIRROR: " + (mirrored ? "ON" : "OFF");
        }
    }
}