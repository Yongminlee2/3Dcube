using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cube.App
{
    /// 첫 화면. 반복되는 메뉴 대신 연습을 가장 먼저 보이게 하고 나머지는 보조 행동으로 묶는다.
    public sealed class HomeScreen : MonoBehaviour
    {
        int _size = 3;
        Button[] _sizeButtons;
        Image[] _sizeGradient;
        Image[] _sizeIcons;
        Image[] _sizeSelectedIcons;
        Palette _p;
        Text _notice;
        Text _learnProgress;
        Text _practiceLabel;
        Action _onLearn;

        public void Build(RectTransform parent, Action<int> onPractice, Action onLearn,
                          Action onColorInput, Action onRecords, Action onSkins, Action onSettings)
        {
            _onLearn = onLearn;
            _p = ThemeService.Current;
            _size = AppSettings.CubeSize;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            BuildHeader(onSettings);
            BuildHero();
            BuildSizeSelector();

            var practice = UiKit.Button(transform, "Menu_연습 시작", "연습 시작", _p,
                () => onPractice?.Invoke(_size), ButtonVariant.Primary);
            UiKit.Stretch((RectTransform)practice.transform,
                new Vector2(0.05f, 0.405f), new Vector2(0.95f, 0.485f), Vector4.zero);
            _practiceLabel = practice.GetComponentInChildren<Text>();
            _practiceLabel.fontSize = 35;
            _practiceLabel.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)_practiceLabel.transform,
                new Vector2(0.37f, 0f), new Vector2(0.75f, 1f), Vector4.zero);
            var play = UiKit.Icon(practice.transform, "PlayIcon", "player-play", _p.TextOnAccent);
            UiKit.Stretch((RectTransform)play.transform,
                new Vector2(0.29f, 0.28f), new Vector2(0.37f, 0.72f), Vector4.zero);
            if (AppSettings.DarkTheme)
            {
                UiKit.AddArtworkOverlay(practice.transform, "PrimaryGradient",
                    "UiArt/PrimaryButtonGradient", Vector2.zero, Vector2.one);
            }
            UiKit.AddSoftShadow(practice.image, _p.Shadow, 7f);

            BuildLearnCard(onLearn);
            BuildUtilityCard("Menu_실물 큐브 넣기", "실물 큐브", "촬영해서 넣기",
                "cube", new Vector2(0.05f, 0.14f), new Vector2(0.335f, 0.245f), onColorInput);
            BuildUtilityCard("Menu_기록", "기록", "연습 기록",
                "chart-bar", new Vector2(0.3575f, 0.14f), new Vector2(0.6425f, 0.245f), onRecords);
            BuildUtilityCard("Menu_스킨", "큐브 스킨", "색상 · 캐릭터",
                "palette", new Vector2(0.665f, 0.14f), new Vector2(0.95f, 0.245f), onSkins);

            _notice = UiKit.Label(transform, "Notice", "", 22, _p.Warning, TextAnchor.MiddleCenter);
            UiKit.Wrap(_notice);
            UiKit.Stretch((RectTransform)_notice.transform,
                new Vector2(0.06f, 0.075f), new Vector2(0.94f, 0.125f), Vector4.zero);

            RefreshSizeButtons();
            RefreshProgress();
            RefreshContinueState();
        }

        void BuildHeader(Action onSettings)
        {
            var title = UiKit.Label(transform, "Title", "큐브 연습장", UiMetrics.ScreenTitle,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)title.transform,
                new Vector2(0.05f, 0.91f), new Vector2(0.72f, 0.985f), Vector4.zero);

            var settings = UiKit.Button(transform, "Menu_설정", "", _p,
                () => onSettings?.Invoke(), ButtonVariant.Ghost);
            UiKit.Stretch((RectTransform)settings.transform,
                new Vector2(0.85f, 0.915f), new Vector2(0.95f, 0.98f), Vector4.zero);
            var icon = UiKit.Icon(settings.transform, "SettingsIcon", "settings", _p.TextPrimary);
            UiKit.Stretch((RectTransform)icon.transform,
                new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector4.zero);

            var divider = UiKit.Divider(transform, "HeaderDivider", _p.Border);
            UiKit.Stretch(divider, new Vector2(0f, 0.902f), new Vector2(1f, 0.903f), Vector4.zero);
        }

        void BuildHero()
        {
            if (AppSettings.DarkTheme)
            {
                var glow = UiKit.Artwork(transform, "HeroGlow", "UiArt/BlueGlow", false);
                UiKit.Stretch((RectTransform)glow.transform,
                    new Vector2(0.34f, 0.57f), new Vector2(1.02f, 0.91f), Vector4.zero);
                glow.color = new Color(1f, 1f, 1f, 0.92f);

                var yellowSparkle = UiKit.Icon(transform, "YellowSparkle", "sparkles", _p.Warning);
                UiKit.Stretch((RectTransform)yellowSparkle.transform,
                    new Vector2(0.415f, 0.805f), new Vector2(0.47f, 0.845f), Vector4.zero);
                var blueSparkle = UiKit.Icon(transform, "BlueSparkle", "sparkles", _p.Accent);
                UiKit.Stretch((RectTransform)blueSparkle.transform,
                    new Vector2(0.405f, 0.695f), new Vector2(0.46f, 0.74f), Vector4.zero);
            }

            var headline = UiKit.Label(transform, "Headline", "오늘도\n한 번 맞춰볼까요?",
                UiMetrics.HeroTitle, _p.TextPrimary, TextAnchor.MiddleLeft);
            headline.fontStyle = FontStyle.Bold;
            UiKit.Wrap(headline);
            UiKit.Stretch((RectTransform)headline.transform,
                new Vector2(0.05f, 0.675f), new Vector2(0.53f, 0.855f), Vector4.zero);

            var subtitle = UiKit.Label(transform, "Subtitle", "차근차근 연습하면\n누구나 완성할 수 있어요",
                UiMetrics.Body, _p.TextSecondary, TextAnchor.UpperLeft);
            subtitle.lineSpacing = 1.12f;
            UiKit.Wrap(subtitle);
            UiKit.Stretch((RectTransform)subtitle.transform,
                new Vector2(0.05f, 0.595f), new Vector2(0.52f, 0.695f), Vector4.zero);

            var cube = UiKit.Artwork(transform, "HeroCube", "UiArt/HomeCubeHero");
            UiKit.Stretch((RectTransform)cube.transform,
                new Vector2(0.46f, 0.595f), new Vector2(0.95f, 0.875f), Vector4.zero);
        }

        void BuildSizeSelector()
        {
            var sizes = UiKit.Card(transform, "Sizes", _p);
            UiKit.Stretch(sizes, new Vector2(0.05f, 0.505f), new Vector2(0.95f, 0.585f), Vector4.zero);
            UiKit.AddSoftOutline(sizes.GetComponent<Image>(), _p.Border, 1f);

            _sizeButtons = new Button[3];
            _sizeGradient = new Image[3];
            _sizeIcons = new Image[3];
            _sizeSelectedIcons = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                int size = i + 2;
                var btn = UiKit.Button(sizes, $"Size{size}", $"{size}×{size}", _p,
                    () => SelectSize(size), ButtonVariant.Segment);
                var rt = (RectTransform)btn.transform;
                rt.anchorMin = new Vector2(i / 3f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 3f, 1f);
                rt.offsetMin = new Vector2(3f, 3f);
                rt.offsetMax = new Vector2(-3f, -3f);
                btn.image.sprite = UiKit.RoundedTight;
                var label = btn.GetComponentInChildren<Text>();
                label.fontStyle = FontStyle.Bold;
                label.fontSize = 26;
                UiKit.Stretch((RectTransform)label.transform,
                    new Vector2(0.49f, 0f), new Vector2(0.92f, 1f), Vector4.zero);

                if (AppSettings.DarkTheme)
                {
                    _sizeGradient[i] = UiKit.AddArtworkOverlay(btn.transform, "SelectedGradient",
                        "UiArt/SegmentButtonGradient", Vector2.zero, Vector2.one);
                }

                _sizeIcons[i] = UiKit.Artwork(btn.transform, "SizeIcon", $"UiArt/Size{size}Cube");
                UiKit.Stretch((RectTransform)_sizeIcons[i].transform,
                    new Vector2(0.11f, 0.16f), new Vector2(0.46f, 0.84f), Vector4.zero);

                if (size == 3)
                {
                    _sizeSelectedIcons[i] = UiKit.Artwork(btn.transform, "SelectedSizeIcon",
                        "UiArt/Size3CubeSelected");
                    UiKit.Stretch((RectTransform)_sizeSelectedIcons[i].transform,
                        new Vector2(0.11f, 0.16f), new Vector2(0.46f, 0.84f), Vector4.zero);
                }
                _sizeButtons[i] = btn;
            }
        }

        void BuildLearnCard(Action onLearn)
        {
            var learn = UiKit.Button(transform, "Learn", "배우기", _p, OpenLearn, ButtonVariant.Card);
            UiKit.Stretch((RectTransform)learn.transform,
                new Vector2(0.05f, 0.265f), new Vector2(0.95f, 0.385f), Vector4.zero);
            UiKit.AddSoftOutline(learn.image, _p.Border, 1f);

            var label = learn.GetComponentInChildren<Text>();
            label.fontSize = 31;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            UiKit.Stretch((RectTransform)label.transform,
                new Vector2(0.19f, 0.47f), new Vector2(0.58f, 0.90f), Vector4.zero);

            var plate = UiKit.IconPlate(learn.transform, "LearnIconPlate", "book-2", _p, _p.Accent);
            UiKit.Stretch(plate, new Vector2(0.04f, 0.26f), new Vector2(0.17f, 0.74f), Vector4.zero);

            _learnProgress = UiKit.Label(learn.transform, "Progress", "", 22,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)_learnProgress.transform,
                new Vector2(0.19f, 0.10f), new Vector2(0.63f, 0.50f), Vector4.zero);

            var course = UiKit.Label(learn.transform, "Course", "7단계 코스", 25,
                _p.Accent, TextAnchor.MiddleRight);
            course.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)course.transform,
                new Vector2(0.64f, 0.25f), new Vector2(0.89f, 0.75f), Vector4.zero);
            var chevron = UiKit.Icon(learn.transform, "Chevron", "chevron-right", _p.TextSecondary);
            UiKit.Stretch((RectTransform)chevron.transform,
                new Vector2(0.90f, 0.34f), new Vector2(0.95f, 0.66f), Vector4.zero);
        }

        void BuildUtilityCard(string name, string title, string subtitle, string iconName,
                              Vector2 min, Vector2 max, Action action)
        {
            var card = UiKit.Button(transform, name, title, _p, () => action?.Invoke(), ButtonVariant.Card);
            UiKit.Stretch((RectTransform)card.transform, min, max, Vector4.zero);
            UiKit.AddSoftOutline(card.image, _p.Border, 1f);

            var label = card.GetComponentInChildren<Text>();
            label.fontSize = 25;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 20;
            label.resizeTextMaxSize = 25;
            UiKit.Stretch((RectTransform)label.transform,
                new Vector2(0.30f, 0.46f), new Vector2(0.91f, 0.88f), Vector4.zero);

            var subtitleLabel = UiKit.Label(card.transform, "Subtitle", subtitle, 18,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            subtitleLabel.resizeTextForBestFit = true;
            subtitleLabel.resizeTextMinSize = 14;
            subtitleLabel.resizeTextMaxSize = 18;
            UiKit.Stretch((RectTransform)subtitleLabel.transform,
                new Vector2(0.30f, 0.10f), new Vector2(0.93f, 0.48f), Vector4.zero);

            var plate = UiKit.IconPlate(card.transform, "IconPlate", iconName, _p, _p.Accent);
            UiKit.Stretch(plate, new Vector2(0.055f, 0.29f), new Vector2(0.265f, 0.71f), Vector4.zero);
        }

        void OnEnable()
        {
            RefreshProgress();
            RefreshContinueState();
        }

        void RefreshProgress()
        {
            if (_learnProgress == null) return;
            int done = LearnProgress.Completed;
            _learnProgress.text = done == 0 ? "처음부터 차근차근 배워요" : $"{done}/7 단계 완료";
        }

        /// 학습 코스는 3×3만 있다. 다른 크기를 고른 상태면 안내만 하고 넘기지 않는다.
        void OpenLearn()
        {
            if (_size != 3)
            {
                if (_notice != null) _notice.text = "배우기는 3×3부터 시작합니다. 3×3을 골라 주세요.";
                return;
            }
            if (_notice != null) _notice.text = "";
            _onLearn?.Invoke();
        }

        void SelectSize(int size)
        {
            _size = size;
            AppSettings.CubeSize = size;
            if (_notice != null) _notice.text = "";
            RefreshSizeButtons();
            RefreshContinueState();
        }

        public void RefreshContinueState()
        {
            if (_practiceLabel == null) return;
            _practiceLabel.text = CubeProgressStore.HasUnfinishedPractice(_size)
                ? "이어하기"
                : "연습 시작";
        }

        void RefreshSizeButtons()
        {
            if (_sizeButtons == null) return;
            for (int i = 0; i < _sizeButtons.Length; i++)
            {
                UiKit.StyleButton(_sizeButtons[i], _p,
                    i + 2 == _size ? ButtonVariant.SegmentSelected : ButtonVariant.Segment);
                if (_sizeGradient != null && i < _sizeGradient.Length && _sizeGradient[i] != null)
                    _sizeGradient[i].gameObject.SetActive(i + 2 == _size);

                bool useSelectedArtwork = i == 1 && i + 2 == _size;
                if (_sizeIcons != null && _sizeIcons[i] != null)
                    _sizeIcons[i].gameObject.SetActive(!useSelectedArtwork);
                if (_sizeSelectedIcons != null && _sizeSelectedIcons[i] != null)
                    _sizeSelectedIcons[i].gameObject.SetActive(useSelectedArtwork);
            }
        }
    }
}
