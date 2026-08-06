using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cube.App
{
    /// 코드로 UI를 세울 때 쓰는 도우미.
    /// TextMeshPro를 쓰지 않는다 — 폰트 애셋 임포트라는 설치 단계를 피하기 위함이다.
    public static class UiKit
    {
        static Font _font;
        public static Font DefaultFont
            => _font != null ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static RectTransform Panel(Transform parent, string name, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = bg;
            img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        public static Image Cell(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size,
                                 Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, string label,
                                    Palette p, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = p.Surface;

            var text = Label(go.transform, "Label", label, 34, p.TextPrimary);
            Stretch((RectTransform)text.transform, Vector2.zero, Vector2.one, Vector4.zero);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        /// padding은 (left, bottom, right, top) 순서다.
        public static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector4 padding)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(padding.x, padding.y);
            rt.offsetMax = new Vector2(-padding.z, -padding.w);
        }
    }
}
