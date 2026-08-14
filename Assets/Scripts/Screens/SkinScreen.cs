using System;
using UnityEngine;
using UnityEngine.UI;
using Cube.Core;

namespace Cube.App
{
    /// 큐브 겉모습을 고르는 화면. 뒤에 보이는 큐브가 그대로 미리보기 역할을 한다 —
    /// CubeRenderer가 SkinService.Changed를 직접 구독하고 있어 고르는 즉시 입혀진다.
    public sealed class SkinScreen : MonoBehaviour
    {
        Palette _p;
        Skin[] _skins;
        Button[] _rows;
        Text[] _labels;
        Image[] _checks;
        Text _previewName;
        Button _repeatButton;
        Button _wholeFaceButton;
        Text _layoutGuide;

        public void Build(RectTransform parent, Action onBack)
        {
            _p = ThemeService.Current;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            AttachCube();

            UiKit.ScreenHeader(transform, "큐브 스킨", _p, onBack);

            var preview = UiKit.Card(transform, "PreviewStage", _p);
            UiKit.Stretch(preview, new Vector2(0.05f, 0.51f), new Vector2(0.95f, 0.885f), Vector4.zero);
            var previewImage = preview.GetComponent<Image>();
            var previewColor = _p.SurfaceRaised;
            previewColor.a = AppSettings.DarkTheme ? 0.18f : 0.28f;
            previewImage.color = previewColor;
            UiKit.AddSoftOutline(previewImage, _p.Border, 1f);

            var previewIcon = UiKit.IconPlate(preview, "PreviewIcon", "palette", _p, _p.Accent);
            UiKit.Stretch(previewIcon, new Vector2(0.035f, 0.80f), new Vector2(0.13f, 0.96f), Vector4.zero);
            var guide = UiKit.Label(preview, "Guide", "고르는 즉시 큐브에 입혀져요", 22,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)guide.transform,
                new Vector2(0.04f, 0.04f), new Vector2(0.72f, 0.16f), Vector4.zero);
            _previewName = UiKit.Label(preview, "PreviewName", "", 23,
                _p.Accent, TextAnchor.MiddleRight);
            _previewName.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)_previewName.transform,
                new Vector2(0.67f, 0.81f), new Vector2(0.95f, 0.95f), Vector4.zero);

            var layoutSection = UiKit.Label(transform, "LayoutSection", "그림 배치", UiMetrics.SectionTitle,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            layoutSection.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)layoutSection.transform,
                new Vector2(0.06f, 0.455f), new Vector2(0.94f, 0.495f), Vector4.zero);

            _repeatButton = UiKit.Button(transform, "LayoutRepeat", "조각마다 반복", _p,
                () => SelectLayout(SkinArtworkLayout.RepeatPerSticker), ButtonVariant.Segment);
            UiKit.Stretch((RectTransform)_repeatButton.transform,
                new Vector2(0.05f, 0.405f), new Vector2(0.49f, 0.455f), new Vector4(0f, 0f, 6f, 0f));

            _wholeFaceButton = UiKit.Button(transform, "LayoutWholeFace", "한 면 전체", _p,
                () => SelectLayout(SkinArtworkLayout.WholeFace), ButtonVariant.SegmentSelected);
            UiKit.Stretch((RectTransform)_wholeFaceButton.transform,
                new Vector2(0.51f, 0.405f), new Vector2(0.95f, 0.455f), new Vector4(6f, 0f, 0f, 0f));

            _layoutGuide = UiKit.Label(transform, "LayoutGuide", "그림 한 장을 3×3 조각으로 나눠 보여줘요",
                UiMetrics.Caption, _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)_layoutGuide.transform,
                new Vector2(0.06f, 0.372f), new Vector2(0.94f, 0.402f), Vector4.zero);

            var section = UiKit.Label(transform, "SkinSection", "스킨 고르기", UiMetrics.SectionTitle,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            section.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)section.transform,
                new Vector2(0.06f, 0.335f), new Vector2(0.94f, 0.37f), Vector4.zero);

            var list = UiKit.ScrollList(transform, "SkinList", _p, out var listPanel, 12f, 3f);
            UiKit.Stretch((RectTransform)list.transform,
                new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.33f), Vector4.zero);

            _skins = SkinService.All;
            _rows = new Button[_skins.Length];
            _labels = new Text[_skins.Length];
            _checks = new Image[_skins.Length];

            for (int i = 0; i < _skins.Length; i++)
            {
                var skin = _skins[i];
                var btn = UiKit.Button(listPanel, $"Row_{skin.name}", skin.DisplayName, _p,
                    () => SelectSkin(skin), ButtonVariant.Card);
                // 스크롤 중에도 행 높이가 흔들리지 않게 고정한다.
                UiKit.SetLayoutHeight(btn, 88f);
                btn.image.sprite = UiKit.RoundedTight;
                UiKit.AddSoftOutline(btn.image, _p.Border, 0.75f);

                var label = btn.transform.Find("Label").GetComponent<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 25;
                label.fontStyle = FontStyle.Bold;
                UiKit.Stretch((RectTransform)label.transform,
                    new Vector2(0.04f, 0f), new Vector2(0.34f, 1f), Vector4.zero);

                for (int c = 0; c < 6; c++)
                {
                    var cell = UiKit.Cell(btn.transform, $"C{c}", skin.StickerColors[c]);
                    cell.sprite = UiKit.RoundedSmall;
                    cell.type = Image.Type.Sliced;
                    var crt = (RectTransform)cell.transform;
                    crt.anchorMin = new Vector2(0.36f + c * 0.087f, 0.22f);
                    crt.anchorMax = new Vector2(0.36f + (c + 1) * 0.087f, 0.78f);
                    crt.offsetMin = new Vector2(2f, 0f);
                    crt.offsetMax = new Vector2(-2f, 0f);

                    var texture = skin.StickerTextures != null && c < skin.StickerTextures.Length
                        ? skin.StickerTextures[c] : null;
                    if (texture != null)
                    {
                        // 그림 스킨은 대표색 사각형 대신 실제 면 텍스처를 보여 준다.
                        // 마스크를 써서 기존 둥근 색상 칩과 같은 모양을 유지한다.
                        cell.color = Color.white;
                        var mask = cell.gameObject.AddComponent<Mask>();
                        mask.showMaskGraphic = true;

                        var previewGo = new GameObject("Texture", typeof(RectTransform), typeof(RawImage));
                        previewGo.transform.SetParent(cell.transform, false);
                        var raw = previewGo.GetComponent<RawImage>();
                        raw.texture = texture;
                        raw.color = Color.white;
                        raw.raycastTarget = false;
                        UiKit.Stretch((RectTransform)raw.transform, Vector2.zero, Vector2.one, Vector4.zero);
                    }
                }

                var check = UiKit.Icon(btn.transform, "SelectedCheck", "check", _p.Success);
                UiKit.Stretch((RectTransform)check.transform,
                    new Vector2(0.91f, 0.28f), new Vector2(0.97f, 0.72f), Vector4.zero);

                _rows[i] = btn;
                _labels[i] = label;
                _checks[i] = check;
            }

            var footnote = UiKit.Label(transform, "Footnote", "연습 중인 큐브 상태는 그대로 유지됩니다", 20,
                _p.TextSecondary, TextAnchor.MiddleCenter);
            UiKit.Stretch((RectTransform)footnote.transform,
                new Vector2(0.06f, 0.065f), new Vector2(0.94f, 0.115f), Vector4.zero);

            Refresh();
        }

        void AttachCube()
        {
            var cubeRoot = AppBootstrap.Instance != null
                ? AppBootstrap.Instance.CubeRoot
                : new GameObject("CubeRoot").transform;

            var renderer = GetOrAdd<CubeRenderer>(cubeRoot.gameObject);
            // 이 세션에서 연습·학습을 한 번도 열지 않았으면 큐브가 아직 없다.
            // 그럴 때만 새로 짓는다 — 이미 진행 중인 풀이가 있으면 건드리지 않는다.
            if (renderer.State == null) renderer.Build(CubeState.Solved(3));

            var orbit = GetOrAdd<OrbitCamera>(cubeRoot.gameObject);
            orbit.Init(renderer.transform);
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        void SelectSkin(Skin skin)
        {
            SkinService.Apply(skin);
            Refresh();
        }

        void SelectLayout(SkinArtworkLayout layout)
        {
            SkinService.SetArtworkLayout(layout);
            Refresh();
        }

        public void Refresh()
        {
            if (_rows == null) return;

            bool wholeFace = SkinService.ArtworkLayout == SkinArtworkLayout.WholeFace;
            UiKit.StyleButton(_repeatButton, _p,
                wholeFace ? ButtonVariant.Segment : ButtonVariant.SegmentSelected);
            UiKit.StyleButton(_wholeFaceButton, _p,
                wholeFace ? ButtonVariant.SegmentSelected : ButtonVariant.Segment);
            if (_layoutGuide != null)
                _layoutGuide.text = wholeFace
                    ? "그림 한 장을 3×3 조각으로 나눠 보여줘요"
                    : "같은 그림을 모든 조각에 반복해서 보여줘요";

            for (int i = 0; i < _skins.Length; i++)
            {
                bool selected = _skins[i] == SkinService.Current;
                _rows[i].image.color = selected ? _p.AccentSoft : _p.SurfaceRaised;
                _labels[i].text = _skins[i].DisplayName;
                _labels[i].color = selected ? _p.Accent : _p.TextPrimary;
                _checks[i].gameObject.SetActive(selected);
                if (selected && _previewName != null) _previewName.text = _skins[i].DisplayName;
            }
        }
    }
}
