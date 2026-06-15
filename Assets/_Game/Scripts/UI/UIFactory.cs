using System;
using UnityEngine;
using UnityEngine.UI;

namespace Gridiron
{
    /// <summary>Small helpers for building the runtime uGUI (legacy Text — zero external dependencies).</summary>
    public static class UIFactory
    {
        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static Text Label(Transform parent, string name, string text, int size, TextAnchor anchor, Color color, FontStyle style = FontStyle.Bold)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button TextButton(Transform parent, string name, string label, int fontSize, Vector2 size, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.25f, 0.35f, 0.5f);
            colors.pressedColor = new Color(0.4f, 0.55f, 0.75f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var txt = Label(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, Color.white);
            Stretch((RectTransform)txt.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return btn;
        }

        public static Image Bar(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.raycastTarget = false;
            return img;
        }

        public static Image Radial(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
            img.raycastTarget = false;
            return img;
        }
    }
}