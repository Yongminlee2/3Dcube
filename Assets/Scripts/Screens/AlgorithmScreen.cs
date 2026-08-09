using System;
using UnityEngine;
using UnityEngine.UI;
using Cube.Core;

namespace Cube.App
{
    /// 공식 모아보기. 카드를 누르면 공유 큐브에서 바로 시연한다.
    public sealed class AlgorithmScreen : MonoBehaviour
    {
        Palette _p;
        CubeRenderer _renderer;
        LayerRotator _rotator;
        TouchController _touch;
        LessonPlayer _player;

        Text _status;
        RectTransform _statusCard;
        Image _statusIcon;
        Button _selectedAlgorithm;

        public void Build(RectTransform parent, Action onBack)
        {
            _p = ThemeService.Current;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            AttachCube();

            var title = UiKit.ScreenHeader(transform, "공식 모아보기", _p, onBack);
            title.fontSize = 40;

            BuildPreviewArea();
            BuildLibrary();
            BuildStatusAndReset();

            ResetCube();
        }

        void AttachCube()
        {
            var cubeRoot = AppBootstrap.Instance != null
                ? AppBootstrap.Instance.CubeRoot
                : new GameObject("CubeRoot").transform;
            _renderer = GetOrAdd<CubeRenderer>(cubeRoot.gameObject);
            _rotator = GetOrAdd<LayerRotator>(cubeRoot.gameObject);
            _touch = GetOrAdd<TouchController>(cubeRoot.gameObject);
            _player = GetOrAdd<LessonPlayer>(cubeRoot.gameObject);
        }

        void BuildPreviewArea()
        {
            var previewColor = _p.SurfaceRaised;
            previewColor.a = 0.16f;
            var preview = UiKit.Panel(transform, "CubePreview", previewColor);
            var image = preview.GetComponent<Image>();
            image.sprite = UiKit.Rounded;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            UiKit.AddSoftOutline(image, _p.Border, 1f);
            UiKit.Stretch(preview,
                new Vector2(0.055f, 0.63f), new Vector2(0.945f, 0.885f), Vector4.zero);

            var plate = UiKit.IconPlate(preview, "PreviewIcon", "cube", _p, _p.Accent);
            UiKit.Stretch(plate,
                new Vector2(0.03f, 0.72f), new Vector2(0.105f, 0.94f), Vector4.zero);

            var heading = UiKit.Label(preview, "PreviewTitle", "큐브 미리보기", 22,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            heading.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)heading.transform,
                new Vector2(0.125f, 0.72f), new Vector2(0.55f, 0.94f), Vector4.zero);

            var caption = UiKit.Label(preview, "PreviewCaption", "카드를 누르면 이곳에서 천천히 보여줘요", 19,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)caption.transform,
                new Vector2(0.035f, 0.05f), new Vector2(0.68f, 0.22f), Vector4.zero);
        }

        void BuildLibrary()
        {
            var heading = UiKit.Label(transform, "LibraryHeading", "배워 둔 공식",
                UiMetrics.SectionTitle, _p.TextPrimary, TextAnchor.MiddleLeft);
            heading.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)heading.transform,
                new Vector2(0.055f, 0.575f), new Vector2(0.72f, 0.615f), Vector4.zero);

            var countPill = UiKit.Panel(transform, "CountPill", _p.SurfaceMuted);
            var countImage = countPill.GetComponent<Image>();
            countImage.sprite = UiKit.RoundedPill;
            countImage.type = Image.Type.Sliced;
            UiKit.Stretch(countPill,
                new Vector2(0.79f, 0.58f), new Vector2(0.945f, 0.61f), Vector4.zero);
            var count = UiKit.Label(countPill, "Count", $"{LessonData.Library.Count}개", 18,
                _p.TextSecondary);
            count.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)count.transform,
                Vector2.zero, Vector2.one, Vector4.zero);

            var scroll = UiKit.ScrollList(transform, "List", _p,
                out var content, spacing: 10f, padding: 2f);
            UiKit.Stretch((RectTransform)scroll.transform,
                new Vector2(0.055f, 0.145f), new Vector2(0.945f, 0.57f), Vector4.zero);

            var library = LessonData.Library;
            for (int i = 0; i < library.Count; i++)
            {
                var alg = library[i];
                Button card = null;
                card = UiKit.Button(content, $"Alg{i}", alg.Name, _p,
                    () => Play(alg, card), ButtonVariant.Card);
                UiKit.SetLayoutHeight(card, 114f);
                UiKit.AddSoftOutline(card.image, _p.Border, 0.8f);

                var label = card.transform.Find("Label")?.GetComponent<Text>();
                if (label != null)
                {
                    label.fontSize = 23;
                    label.fontStyle = FontStyle.Bold;
                    label.alignment = TextAnchor.MiddleLeft;
                    UiKit.Stretch((RectTransform)label.transform,
                        new Vector2(0.15f, 0.58f), new Vector2(0.91f, 0.92f), Vector4.zero);
                }

                var plate = UiKit.IconPlate(card.transform, "PlayIcon", "player-play", _p, _p.Accent);
                UiKit.Stretch(plate,
                    new Vector2(0.025f, 0.19f), new Vector2(0.115f, 0.81f), Vector4.zero);

                var notation = UiKit.Label(card.transform, "Notation", alg.Notation, 20,
                    _p.Accent, TextAnchor.MiddleLeft);
                notation.fontStyle = FontStyle.Bold;
                UiKit.Stretch((RectTransform)notation.transform,
                    new Vector2(0.15f, 0.28f), new Vector2(0.95f, 0.61f), Vector4.zero);

                var when = UiKit.Label(card.transform, "When", alg.When, 17,
                    _p.TextSecondary, TextAnchor.MiddleLeft);
                UiKit.Wrap(when);
                UiKit.Stretch((RectTransform)when.transform,
                    new Vector2(0.15f, 0.05f), new Vector2(0.95f, 0.30f), Vector4.zero);

                var chevron = UiKit.Icon(card.transform, "Chevron", "chevron-right", _p.TextSecondary);
                UiKit.Stretch((RectTransform)chevron.transform,
                    new Vector2(0.92f, 0.66f), new Vector2(0.965f, 0.86f), Vector4.zero);
            }
        }

        void BuildStatusAndReset()
        {
            _statusCard = UiKit.Card(transform, "StatusCard", _p);
            UiKit.Stretch(_statusCard,
                new Vector2(0.055f, 0.035f), new Vector2(0.70f, 0.125f), Vector4.zero);
            UiKit.AddSoftOutline(_statusCard.GetComponent<Image>(), _p.Border, 0.8f);

            _statusIcon = UiKit.Icon(_statusCard, "StatusIcon", "sparkles", _p.TextSecondary);
            UiKit.Stretch((RectTransform)_statusIcon.transform,
                new Vector2(0.04f, 0.30f), new Vector2(0.11f, 0.70f), Vector4.zero);

            _status = UiKit.Label(_statusCard, "Status", "", 19,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            _status.fontStyle = FontStyle.Bold;
            UiKit.Wrap(_status);
            UiKit.Stretch((RectTransform)_status.transform,
                new Vector2(0.14f, 0.08f), new Vector2(0.96f, 0.92f), Vector4.zero);

            var reset = UiKit.Button(transform, "Reset", "처음으로", _p,
                ResetCube, ButtonVariant.Secondary);
            UiKit.Stretch((RectTransform)reset.transform,
                new Vector2(0.72f, 0.035f), new Vector2(0.945f, 0.125f), Vector4.zero);

            var label = reset.transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.fontSize = 22;
                label.fontStyle = FontStyle.Bold;
                UiKit.Stretch((RectTransform)label.transform,
                    new Vector2(0.30f, 0f), new Vector2(0.94f, 1f), Vector4.zero);
            }

            var icon = UiKit.Icon(reset.transform, "ResetIcon", "arrow-left", _p.TextSecondary);
            UiKit.Stretch((RectTransform)icon.transform,
                new Vector2(0.10f, 0.31f), new Vector2(0.27f, 0.69f), Vector4.zero);
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        public void ResetCube()
        {
            _rotator.FinishAllImmediately();
            _renderer.Build(CubeState.Solved(3));
            _rotator.Init(_renderer);

            var cam = AppBootstrap.Instance != null ? AppBootstrap.Instance.CubeCamera : Camera.main;
            var orbit = GetOrAdd<OrbitCamera>(_renderer.gameObject);
            orbit.Init(_renderer.transform);
            if (cam != null) _touch.Init(cam, _renderer, _rotator, orbit);
            _player.Init(_renderer, _rotator, _touch);

            SelectAlgorithm(null);
            SetStatus("공식 카드를 누르면 큐브에서 보여줍니다.", active: false);
        }

        void Play(Algorithm alg, Button source)
        {
            SelectAlgorithm(source);
            SetStatus($"{alg.Name}  ·  {alg.When}", active: true);
            _player.Play(alg.Notation);
        }

        void SelectAlgorithm(Button selected)
        {
            if (_selectedAlgorithm != null)
                UiKit.StyleButton(_selectedAlgorithm, _p, ButtonVariant.Card);

            _selectedAlgorithm = selected;
            if (_selectedAlgorithm != null)
                _selectedAlgorithm.image.color = _p.AccentSoft;
        }

        void SetStatus(string text, bool active)
        {
            if (_status == null || _statusCard == null) return;

            _status.text = text;
            _status.color = active ? _p.Accent : _p.TextSecondary;
            _statusIcon.color = active ? _p.Accent : _p.TextSecondary;
            _statusCard.GetComponent<Image>().color = active ? _p.AccentSoft : _p.Surface;
        }
    }
}
