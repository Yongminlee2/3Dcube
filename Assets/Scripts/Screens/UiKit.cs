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

        const int RoundedSize = 64;
        const int CornerRadius = 20;
        static Sprite _rounded;

        const int SmallSize = 32;
        const int SmallRadius = 5;
        static Sprite _roundedSmall;

        /// 전개도 칸처럼 작은 요소용. 큰 반경을 그대로 쓰면 9분할 테두리가
        /// 서로 겹쳐 사각형이 동그라미가 된다.
        public static Sprite RoundedSmall
            => _roundedSmall != null
                ? _roundedSmall
                : (_roundedSmall = BuildRounded(SmallSize, SmallRadius));

        /// 모서리가 둥근 사각형. 코드로 한 번 그려 두고 모두가 나눠 쓴다.
        ///
        /// 각진 사각형만 쓰면 화면이 딱딱해 보인다는 의견이 있었다.
        /// 9분할로 늘리므로 버튼 크기가 달라져도 모서리 곡률은 그대로다.
        public static Sprite Rounded
            => _rounded != null
                ? _rounded
                : (_rounded = BuildRounded(RoundedSize, CornerRadius));

        static Sprite BuildRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // 모서리 안쪽은 불투명, 바깥은 투명. 경계는 한 픽셀 걸쳐 부드럽게.
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }

            tex.SetPixels32(px);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

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
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;

            var text = Label(go.transform, "Label", label, 34, p.TextPrimary);
            Stretch((RectTransform)text.transform, Vector2.zero, Vector2.one, Vector4.zero);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            // 눌렀을 때 눈에 보이게 한다. 기본값은 변화가 거의 없어 반응이 없어 보인다.
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;

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
