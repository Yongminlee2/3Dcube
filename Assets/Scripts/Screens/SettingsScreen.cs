using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cube.App
{
    public sealed class SettingsScreen : MonoBehaviour
    {
        static readonly int[] SpeedSteps = { 0, 120, 220, 320, 400 };

        Palette _p;
        const float RowHeight = 0.075f;

        Button _theme, _inspection, _pad, _speed, _skin, _bgm, _effects;
        Text _themeValue, _inspectionValue, _padValue, _speedValue, _skinValue;
        Text _bgmValue, _effectsValue;

        public void Build(RectTransform parent, Action onBack, Action onSkins)
        {
            _p = ThemeService.Current;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            BuildHeader(onBack);

            Section("화면", 0.842f);
            _theme = Row("Row_테마", "테마", "다크와 라이트를 전환해요", "moon-stars", 0.760f,
                () => ThemeService.Apply(!AppSettings.DarkTheme), out _themeValue);
            _skin = Row("Row_스킨", "큐브 스킨", "큐브의 색과 질감을 골라요", "palette", 0.675f,
                () => onSkins?.Invoke(), out _skinValue, chevron: true);

            Section("소리", 0.620f);
            _bgm = Row("Row_배경음", "배경음", "집중을 돕는 잔잔한 음악", "player-play", 0.535f,
                () =>
                {
                    AppSettings.BackgroundMusic = !AppSettings.BackgroundMusic;
                    AudioService.Refresh();
                    RefreshLabels();
                }, out _bgmValue);
            _effects = Row("Row_효과음", "효과음", "버튼과 큐브 회전 소리", "hand-click", 0.450f,
                () =>
                {
                    AppSettings.SoundEffects = !AppSettings.SoundEffects;
                    AudioService.Refresh();
                    RefreshLabels();
                }, out _effectsValue);

            Section("연습", 0.395f);
            _inspection = Row("Row_인스펙션", "15초 인스펙션", "섞은 뒤 미리 살펴볼 시간을 줘요", "clock", 0.310f,
                () => { AppSettings.Inspection = !AppSettings.Inspection; RefreshLabels(); }, out _inspectionValue);
            _pad = Row("Row_노테이션 버튼", "노테이션 버튼", "화면 버튼으로도 회전할 수 있어요", "hand-click", 0.225f,
                () => { AppSettings.ShowPad = !AppSettings.ShowPad; RefreshLabels(); }, out _padValue);
            _speed = Row("Row_애니메이션 속도", "회전 속도", "큐브가 돌아가는 시간을 조절해요", "sparkles", 0.140f,
                () =>
                {
                    int i = Array.IndexOf(SpeedSteps, AppSettings.AnimationMs);
                    AppSettings.AnimationMs = SpeedSteps[(i + 1) % SpeedSteps.Length];
                    RefreshLabels();
                }, out _speedValue);

            var saved = UiKit.Card(transform, "SavedNotice", _p);
            UiKit.Stretch(saved, new Vector2(0.08f, 0.045f), new Vector2(0.92f, 0.115f), Vector4.zero);
            var savedIcon = UiKit.Icon(saved, "SavedIcon", "check", _p.Success);
            UiKit.Stretch((RectTransform)savedIcon.transform,
                new Vector2(0.22f, 0.29f), new Vector2(0.29f, 0.71f), Vector4.zero);
            var savedLabel = UiKit.Label(saved, "SavedLabel", "바꾼 설정은 자동으로 저장돼요", 22,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)savedLabel.transform,
                new Vector2(0.32f, 0f), new Vector2(0.82f, 1f), Vector4.zero);

            RefreshLabels();
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
            SetValue(_themeValue, AppSettings.DarkTheme ? "다크" : "라이트", true);
            SetValue(_skinValue, SkinService.Current.DisplayName, true);
            SetValue(_bgmValue, AppSettings.BackgroundMusic ? "켬" : "끔", AppSettings.BackgroundMusic);
            SetValue(_effectsValue, AppSettings.SoundEffects ? "켬" : "끔", AppSettings.SoundEffects);
            SetValue(_inspectionValue, AppSettings.Inspection ? "켬" : "끔", AppSettings.Inspection);
            SetValue(_padValue, AppSettings.ShowPad ? "켬" : "끔", AppSettings.ShowPad);
            SetValue(_speedValue, AppSettings.AnimationMs == 0 ? "즉시" : $"{AppSettings.AnimationMs}ms", true);
        }

        void SetValue(Text label, string text, bool active)
        {
            if (label == null) return;
            label.text = text;
            label.color = active ? _p.Accent : _p.TextSecondary;
        }
    }
}
