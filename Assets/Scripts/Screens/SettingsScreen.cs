using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cube.App
{
    public sealed class SettingsScreen : MonoBehaviour
    {
        static readonly int[] SpeedSteps = { 0, 60, 120, 200, 250 };

        Palette _p;
        Button _theme, _inspection, _pad, _speed;

        public void Build(RectTransform parent, Action onBack)
        {
            _p = ThemeService.Current;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            var title = UiKit.Label(transform, "Title", "설정", 48, _p.TextPrimary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)title.transform, new Vector2(0f, 0.90f), new Vector2(1f, 0.97f), new Vector4(48, 0, 48, 0));

            _theme = Row("테마", 0.78f, () =>
            {
                ThemeService.Apply(!AppSettings.DarkTheme);
                RefreshLabels();
            });
            _inspection = Row("인스펙션", 0.67f, () =>
            {
                AppSettings.Inspection = !AppSettings.Inspection;
                RefreshLabels();
            });
            _pad = Row("노테이션 버튼", 0.56f, () =>
            {
                AppSettings.ShowPad = !AppSettings.ShowPad;
                RefreshLabels();
            });
            _speed = Row("애니메이션 속도", 0.45f, () =>
            {
                int i = Array.IndexOf(SpeedSteps, AppSettings.AnimationMs);
                AppSettings.AnimationMs = SpeedSteps[(i + 1) % SpeedSteps.Length];
                RefreshLabels();
            });

            var back = UiKit.Button(transform, "Back", "돌아가기", _p, () => onBack?.Invoke());
            UiKit.Stretch((RectTransform)back.transform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.13f), Vector4.zero);

            RefreshLabels();
        }

        Button Row(string label, float yMin, Action action)
        {
            var btn = UiKit.Button(transform, $"Row_{label}", label, _p, () => action());
            UiKit.Stretch((RectTransform)btn.transform,
                new Vector2(0.08f, yMin), new Vector2(0.92f, yMin + 0.09f), Vector4.zero);
            return btn;
        }

        public void RefreshLabels()
        {
            _p = ThemeService.Current;
            SetText(_theme, $"테마   {(AppSettings.DarkTheme ? "다크" : "라이트")}");
            SetText(_inspection, $"인스펙션 15초   {(AppSettings.Inspection ? "켬" : "끔")}");
            SetText(_pad, $"노테이션 버튼   {(AppSettings.ShowPad ? "켬" : "끔")}");
            SetText(_speed, $"애니메이션 속도   {AppSettings.AnimationMs}ms");
        }

        static void SetText(Button b, string text)
        {
            if (b != null) b.GetComponentInChildren<Text>().text = text;
        }
    }
}
