using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cube.App
{
    public sealed class SettingsScreen : MonoBehaviour
    {
        static readonly int[] SpeedSteps = { 0, 120, 220, 320, 400 };

        Palette _p;
        const float RowHeight = 0.068f;

        Button _language, _theme, _inspection, _pad, _speed, _skin, _bgm, _effects;
        Text _languageValue;
        Text _themeValue, _inspectionValue, _padValue, _speedValue, _skinValue;
        Text _bgmValue, _effectsValue;
        GameObject _languagePicker;

        public void Build(RectTransform parent, Action onBack, Action onSkins)
        {
            _p = ThemeService.Current;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            BuildHeader(onBack);

            Section("화면", 0.840f);
            _language = Row("Row_언어", "언어", "앱에서 사용할 언어를 골라요", "list-details", 0.765f,
                OpenLanguagePicker, out _languageValue, chevron: true);
            _theme = Row("Row_테마", "테마", "다크와 라이트를 전환해요", "moon-stars", 0.690f,
                () => ThemeService.Apply(!AppSettings.DarkTheme), out _themeValue);
            _skin = Row("Row_스킨", "큐브 스킨", "색상, 질감, 캐릭터를 골라요", "palette", 0.615f,
                () => onSkins?.Invoke(), out _skinValue, chevron: true);

            Section("소리", 0.555f);
            _bgm = Row("Row_배경음", "배경음", "집중을 돕는 잔잔한 음악", "player-play", 0.480f,
                () =>
                {
                    AppSettings.BackgroundMusic = !AppSettings.BackgroundMusic;
                    AudioService.Refresh();
                    RefreshLabels();
                }, out _bgmValue);
            _effects = Row("Row_효과음", "큐브 효과음", "기본·말랑 팝·실제 소리를 골라요", "hand-click", 0.405f,
                () =>
                {
                    AppSettings.CubeSound = NextCubeSound(AppSettings.CubeSound);
                    AudioService.Refresh();
                    RefreshLabels();
                    AudioService.PlayMove();
                }, out _effectsValue);

            Section("연습", 0.345f);
            _inspection = Row("Row_인스펙션", "15초 인스펙션", "섞은 뒤 미리 살펴볼 시간을 줘요", "clock", 0.270f,
                () => { AppSettings.Inspection = !AppSettings.Inspection; RefreshLabels(); }, out _inspectionValue);
            _pad = Row("Row_노테이션 버튼", "노테이션 버튼", "화면 버튼으로도 회전할 수 있어요", "hand-click", 0.195f,
                () => { AppSettings.ShowPad = !AppSettings.ShowPad; RefreshLabels(); }, out _padValue);
            _speed = Row("Row_애니메이션 속도", "회전 속도", "큐브가 돌아가는 시간을 조절해요", "sparkles", 0.120f,
                () =>
                {
                    int i = Array.IndexOf(SpeedSteps, AppSettings.AnimationMs);
                    AppSettings.AnimationMs = SpeedSteps[(i + 1) % SpeedSteps.Length];
                    RefreshLabels();
                }, out _speedValue);

            var saved = UiKit.Card(transform, "SavedNotice", _p);
            UiKit.Stretch(saved, new Vector2(0.08f, 0.035f), new Vector2(0.92f, 0.102f), Vector4.zero);
            var savedIcon = UiKit.Icon(saved, "SavedIcon", "check", _p.Success);
            UiKit.Stretch((RectTransform)savedIcon.transform,
                new Vector2(0.22f, 0.29f), new Vector2(0.29f, 0.71f), Vector4.zero);
            var savedLabel = UiKit.Label(saved, "SavedLabel", "바꾼 설정은 자동으로 저장돼요", 22,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)savedLabel.transform,
                new Vector2(0.32f, 0f), new Vector2(0.82f, 1f), Vector4.zero);

            RefreshLabels();
            BuildLanguagePicker();
        }

        void BuildHeader(Action onBack)
        {
            UiKit.ScreenHeader(transform, "설정", _p, onBack);
        }

        void Section(string text, float y)
        {
            var label = UiKit.Label(transform, $"Section_{text}", text, UiMetrics.SectionTitle,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            label.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)label.transform,
                new Vector2(0.06f, y), new Vector2(0.94f, y + 0.05f), Vector4.zero);
        }

        Button Row(string name, string title, string subtitle, string iconName, float yMin,
                   Action action, out Text valueLabel, bool chevron = false)
        {
            var btn = UiKit.Button(transform, name, title, _p, () => action(), ButtonVariant.Card);
            UiKit.Stretch((RectTransform)btn.transform,
                new Vector2(0.055f, yMin), new Vector2(0.945f, yMin + RowHeight), Vector4.zero);
            UiKit.AddSoftOutline(btn.image, _p.Border, 1f);

            var titleLabel = btn.GetComponentInChildren<Text>();
            titleLabel.fontSize = 26;
            titleLabel.fontStyle = FontStyle.Bold;
            titleLabel.alignment = TextAnchor.MiddleLeft;
            UiKit.Stretch((RectTransform)titleLabel.transform,
                new Vector2(0.17f, 0.45f), new Vector2(0.67f, 0.91f), Vector4.zero);

            var sub = UiKit.Label(btn.transform, "Subtitle", subtitle, 18,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)sub.transform,
                new Vector2(0.17f, 0.08f), new Vector2(0.70f, 0.48f), Vector4.zero);

            var plate = UiKit.IconPlate(btn.transform, "IconPlate", iconName, _p, _p.Accent);
            UiKit.Stretch(plate,
                new Vector2(0.035f, 0.20f), new Vector2(0.14f, 0.80f), Vector4.zero);

            valueLabel = UiKit.Label(btn.transform, "Value", "", 23,
                _p.Accent, TextAnchor.MiddleRight);
            valueLabel.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)valueLabel.transform,
                new Vector2(0.68f, 0f), new Vector2(chevron ? 0.88f : 0.94f, 1f), Vector4.zero);

            if (chevron)
            {
                var arrow = UiKit.Icon(btn.transform, "Chevron", "chevron-right", _p.TextSecondary);
                UiKit.Stretch((RectTransform)arrow.transform,
                    new Vector2(0.90f, 0.34f), new Vector2(0.95f, 0.66f), Vector4.zero);
            }

            return btn;
        }

        public void RefreshLabels()
        {
            _p = ThemeService.Current;
            string language = LocalizationService.UsesSystemLanguage
                ? $"{LocalizationService.T("시스템 기본값")} · {LocalizationService.SystemLanguageName}"
                : LocalizationService.CurrentName;
            SetValue(_languageValue, language, true);
            SetValue(_themeValue, AppSettings.DarkTheme ? "다크" : "라이트", true);
            SetValue(_skinValue, SkinService.Current.DisplayName, true);
            SetValue(_bgmValue, AppSettings.BackgroundMusic ? "켬" : "끔", AppSettings.BackgroundMusic);
            string soundLabel = AppSettings.CubeSound == CubeSoundMode.Classic
                ? "기본"
                : AppSettings.CubeSound == CubeSoundMode.Cute
                    ? "말랑 팝"
                    : AppSettings.CubeSound == CubeSoundMode.Realistic ? "실제 큐브" : "끔";
            SetValue(_effectsValue, soundLabel, AppSettings.CubeSound != CubeSoundMode.Off);
            SetValue(_inspectionValue, AppSettings.Inspection ? "켬" : "끔", AppSettings.Inspection);
            SetValue(_padValue, AppSettings.ShowPad ? "켬" : "끔", AppSettings.ShowPad);
            SetValue(_speedValue, AppSettings.AnimationMs == 0 ? "즉시" : $"{AppSettings.AnimationMs}ms", true);
        }

        void BuildLanguagePicker()
        {
            var overlay = UiKit.Panel(transform, "LanguagePicker", new Color(0f, 0f, 0f, 0.72f));
            _languagePicker = overlay.gameObject;
            UiKit.Stretch(overlay, Vector2.zero, Vector2.one, Vector4.zero);

            var card = UiKit.Card(overlay, "PickerCard", _p, raised: true);
            UiKit.Stretch(card, new Vector2(0.045f, 0.09f), new Vector2(0.955f, 0.91f), Vector4.zero);
            UiKit.AddSoftOutline(card.GetComponent<Image>(), _p.Border, 1f);

            var title = UiKit.Label(card, "PickerTitle", "언어 선택", 32,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)title.transform,
                new Vector2(0.055f, 0.86f), new Vector2(0.76f, 0.96f), Vector4.zero);

            var close = UiKit.Button(card, "PickerClose", "닫기", _p,
                () => _languagePicker.SetActive(false), ButtonVariant.Ghost);
            UiKit.Stretch((RectTransform)close.transform,
                new Vector2(0.76f, 0.865f), new Vector2(0.95f, 0.955f), Vector4.zero);

            BuildLanguageChoice(card, 0, "", "시스템 기본값");
            var locales = LocalizationService.Supported;
            for (int i = 0; i < locales.Count; i++)
                BuildLanguageChoice(card, i + 1, locales[i].Code, locales[i].NativeName);

            _languagePicker.SetActive(false);
        }

        void BuildLanguageChoice(Transform parent, int index, string code, string label)
        {
            int column = index % 2;
            int row = index / 2;
            float xMin = column == 0 ? 0.055f : 0.515f;
            float xMax = column == 0 ? 0.485f : 0.945f;
            float top = 0.835f - row * 0.094f;
            bool selected = string.IsNullOrEmpty(code)
                ? LocalizationService.UsesSystemLanguage
                : !LocalizationService.UsesSystemLanguage && LocalizationService.CurrentCode == code;
            string shown = selected ? $"✓  {label}" : label;
            var button = UiKit.Button(parent, $"Language_{(string.IsNullOrEmpty(code) ? "System" : code)}",
                shown, _p, () => LocalizationService.SetLanguage(code),
                selected ? ButtonVariant.SegmentSelected : ButtonVariant.Card);
            UiKit.Stretch((RectTransform)button.transform,
                new Vector2(xMin, top - 0.078f), new Vector2(xMax, top), Vector4.zero);
            var text = button.GetComponentInChildren<Text>();
            text.fontSize = 21;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = 21;
        }

        void OpenLanguagePicker()
        {
            if (_languagePicker == null) return;
            _languagePicker.SetActive(true);
            _languagePicker.transform.SetAsLastSibling();
        }

        static CubeSoundMode NextCubeSound(CubeSoundMode current)
        {
            switch (current)
            {
                case CubeSoundMode.Classic: return CubeSoundMode.Cute;
                case CubeSoundMode.Cute: return CubeSoundMode.Realistic;
                case CubeSoundMode.Realistic: return CubeSoundMode.Off;
                default: return CubeSoundMode.Classic;
            }
        }

        void SetValue(Text label, string text, bool active)
        {
            if (label == null) return;
            label.text = text;
            label.color = active ? _p.Accent : _p.TextSecondary;
        }
    }
}
