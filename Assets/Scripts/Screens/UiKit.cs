using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cube.App
{
    public enum ButtonVariant
    {
        Secondary,
        Primary,
        Card,
        Ghost,
        Segment,
        SegmentSelected,
        Danger,
    }

    public static class UiMetrics
    {
        public const float PageGutter = 44f;
        public const int Display = 56;       // 타이머처럼 화면에서 가장 큰 숫자
        public const int HeroTitle = 46;
        public const int ScreenTitle = 48;
        public const int SectionTitle = 24;
        public const int Body = 25;
        public const int Caption = 21;
        public const int Micro = 18;         // SCRAMBLE 같은 작은 이름표
        public const int Button = 30;
    }

    /// 코드로 UI를 세울 때 쓰는 도우미.
    /// TextMeshPro를 쓰지 않는다 — 폰트 애셋 임포트라는 설치 단계를 피하기 위함이다.
    public static class UiKit
    {
        static Font _font;
        static readonly Dictionary<int, Sprite> RoundedSprites = new Dictionary<int, Sprite>();
        static readonly Dictionary<string, Sprite> IconSprites = new Dictionary<string, Sprite>();
        public static Font DefaultFont
        {
            get
            {
                if (_font != null) return _font;
#if UNITY_ANDROID && !UNITY_EDITOR
                // Android's sans-serif family routes each script to the matching Noto font.
                // This covers CJK, Cyrillic, Vietnamese and Thai without shipping a huge
                // single-script font that would render another locale as empty boxes.
                _font = Font.CreateDynamicFontFromOSFont("sans-serif", 32);
#endif
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

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

        public static Sprite RoundedTight => RoundedFor(14);
        public static Sprite RoundedPill => RoundedFor(30);

        public static Sprite RoundedFor(int radius)
        {
            if (RoundedSprites.TryGetValue(radius, out var sprite) && sprite != null) return sprite;
            sprite = BuildRounded(64, Mathf.Clamp(radius, 2, 31));
            RoundedSprites[radius] = sprite;
            return sprite;
        }

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

        public static RectTransform Card(Transform parent, string name, Palette p, bool raised = false)
        {
            var panel = Panel(parent, name, raised ? p.SurfaceRaised : p.Surface);
            var image = panel.GetComponent<Image>();
            image.sprite = Rounded;
            image.type = Image.Type.Sliced;
            return panel;
        }

        public static RectTransform Divider(Transform parent, string name, Color color)
        {
            return Panel(parent, name, color);
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
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var localized = go.AddComponent<LocalizedText>();
            localized.Bind(text);
            return t;
        }

        public static void Wrap(Text text)
        {
            if (text == null) return;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// 칸에 맞춰 글자를 줄이되, 가로 폭도 같이 본다.
        ///
        /// 유니티의 자동 축소는 줄바꿈이 꺼져 있으면 세로만 맞추고 가로는 그냥
        /// 넘겨 버린다. 그래서 번역이 길어지는 언어에서 글자가 칸 밖으로 나가
        /// 잘렸다(프랑스어 "Historique d'entraînemer"). 두 설정은 늘 같이 켜야
        /// 하므로 한 곳에 묶어 둔다.
        public static void Fit(Text text, int min, int max)
        {
            if (text == null) return;
            Wrap(text);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = min;
            text.resizeTextMaxSize = max;
        }

        public static Image Icon(Transform parent, string name, string resourceName, Color tint)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = LoadIcon(resourceName);
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        public static Image Artwork(Transform parent, string name, string resourceName,
                                    bool preserveAspect = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = LoadTextureSprite(resourceName);
            image.color = Color.white;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        public static Text ScreenHeader(Transform parent, string title, Palette p, Action onBack)
        {
            var back = Button(parent, $"Back_{title}", "", p,
                () => onBack?.Invoke(), ButtonVariant.Ghost);
            Stretch((RectTransform)back.transform,
                new Vector2(0.03f, 0.91f), new Vector2(0.12f, 0.98f), Vector4.zero);
            var backIcon = Icon(back.transform, "BackIcon", "arrow-left", p.TextPrimary);
            Stretch((RectTransform)backIcon.transform,
                new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector4.zero);

            var heading = Label(parent, "Title", title, UiMetrics.ScreenTitle,
                p.TextPrimary, TextAnchor.MiddleLeft);
            heading.fontStyle = FontStyle.Bold;
            Fit(heading, 28, UiMetrics.ScreenTitle);
            Stretch((RectTransform)heading.transform,
                new Vector2(0.14f, 0.915f), new Vector2(0.82f, 0.975f), Vector4.zero);

            var divider = Divider(parent, "HeaderDivider", p.Border);
            Stretch(divider, new Vector2(0f, 0.902f), new Vector2(1f, 0.903f), Vector4.zero);
            return heading;
        }

        public static RectTransform IconPlate(Transform parent, string name, string iconName,
                                              Palette p, Color iconColor)
        {
            var plate = Panel(parent, name, p.SurfaceMuted);
            var image = plate.GetComponent<Image>();
            image.sprite = RoundedPill;
            image.type = Image.Type.Sliced;
            var icon = Icon(plate, "Icon", iconName, iconColor);
            Stretch((RectTransform)icon.transform,
                new Vector2(0.23f, 0.23f), new Vector2(0.77f, 0.77f), Vector4.zero);
            return plate;
        }

        public static Image AddArtworkOverlay(Transform parent, string name, string resourceName,
                                              Vector2 anchorMin, Vector2 anchorMax, float alpha = 1f)
        {
            var image = Artwork(parent, name, resourceName, false);
            image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            Stretch((RectTransform)image.transform, anchorMin, anchorMax, Vector4.zero);
            image.transform.SetAsFirstSibling();
            return image;
        }

        public static ScrollRect ScrollList(Transform parent, string name, Palette p,
                                            out RectTransform content, float spacing = 14f,
                                            float padding = 4f)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            root.transform.SetParent(parent, false);

            var viewport = Panel(root.transform, "Viewport", new Color(0, 0, 0, 0));
            viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            Stretch(viewport, Vector2.zero, Vector2.one, Vector4.zero);

            var contentGo = new GameObject("Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewport, false);
            content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            // LayoutElement preferred heights must drive rows; leaving this false collapses
            // larger empty/status cards to the RectTransform default height on device.
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = root.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            return scroll;
        }

        public static LayoutElement SetLayoutHeight(Component component, float height)
        {
            var element = component.GetComponent<LayoutElement>();
            if (element == null) element = component.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return element;
        }

        static Sprite LoadIcon(string resourceName)
        {
            return LoadTextureSprite($"UiIcons/{resourceName}");
        }

        static Sprite LoadTextureSprite(string resourceName)
        {
            if (IconSprites.TryGetValue(resourceName, out var cached) && cached != null) return cached;
            var texture = Resources.Load<Texture2D>(resourceName);
            if (texture == null) return null;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            IconSprites[resourceName] = sprite;
            return sprite;
        }

        public static Button Button(Transform parent, string name, string label,
                                    Palette p, UnityAction onClick)
            => Button(parent, name, label, p, onClick, ButtonVariant.Secondary);

        public static Button Button(Transform parent, string name, string label,
                                    Palette p, UnityAction onClick, ButtonVariant variant)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;

            var text = Label(go.transform, "Label", label, UiMetrics.Button, p.TextPrimary);

            // 줄바꿈을 허용해야 자동 축소가 가로 폭까지 본다.
            //
            // Overflow로 두면 유니티는 세로만 맞추고 가로는 넘치도록 내버려 둔다.
            // 그래서 자동 축소를 켜 두었는데도 "반시계"가 영어 "Counterclockwise"로
            // 바뀌자 버튼을 뚫고 화면 밖까지 나갔다. Wrap이면 두 줄로 내려가고,
            // 두 줄로도 모자라면 그때 글자가 줄어든다.
            Wrap(text);
            Fit(text, 14, UiMetrics.Button);
            Stretch((RectTransform)text.transform, Vector2.zero, Vector2.one, new Vector4(6, 2, 6, 2));

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            StyleButton(btn, p, variant);

            if (onClick != null) btn.onClick.AddListener(onClick);
            btn.onClick.AddListener(AudioService.PlayClick);
            return btn;
        }

        public static void StyleButton(Button button, Palette p, ButtonVariant variant)
        {
            if (button == null) return;
            var image = button.image;
            var labelTransform = button.transform.Find("Label");
            var label = labelTransform != null
                ? labelTransform.GetComponent<Text>()
                : button.GetComponentInChildren<Text>();
            Color bg;
            Color fg;

            switch (variant)
            {
                case ButtonVariant.Primary:
                    bg = p.Accent;
                    fg = p.TextOnAccent;
                    break;
                case ButtonVariant.Card:
                    bg = p.SurfaceRaised;
                    fg = p.TextPrimary;
                    break;
                case ButtonVariant.Ghost:
                    bg = new Color(0, 0, 0, 0);
                    fg = p.TextSecondary;
                    break;
                case ButtonVariant.Segment:
                    bg = p.SurfaceMuted;
                    fg = p.TextSecondary;
                    break;
                case ButtonVariant.SegmentSelected:
                    bg = p.Accent;
                    fg = p.TextOnAccent;
                    break;
                case ButtonVariant.Danger:
                    bg = new Color(0.55f, 0.15f, 0.18f, 1f);
                    fg = Color.white;
                    break;
                default:
                    bg = p.Surface;
                    fg = p.TextPrimary;
                    break;
            }

            if (image != null) image.color = bg;
            if (label != null) label.color = fg;

            // 이미지의 고유색은 유지하고, 상태색을 곱해서 짧고 분명한 피드백을 낸다.
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.42f);
            colors.fadeDuration = 0.055f;
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }

        public static void AddSoftOutline(Graphic graphic, Color color, float width = 1.2f)
        {
            if (graphic == null) return;
            var outline = graphic.GetComponent<Outline>();
            if (outline == null) outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
            outline.useGraphicAlpha = true;
        }

        public static void AddSoftShadow(Graphic graphic, Color color, float distance = 5f)
        {
            if (graphic == null) return;
            var shadow = graphic.GetComponent<Shadow>();
            if (shadow == null) shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = new Vector2(0f, -distance);
            shadow.useGraphicAlpha = true;
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
