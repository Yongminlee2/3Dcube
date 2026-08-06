using System;
using UnityEngine;
using UnityEngine.UI;
using Cube.Core;

namespace Cube.App
{
    /// 학습 홈. 7단계 목록과 진도를 보여준다.
    public sealed class LearnScreen : MonoBehaviour
    {
        Palette _p;
        Button[] _stageButtons;
        Text _notice;
        Action<int> _onOpenLesson;

        public void Build(RectTransform parent, Action<int> onOpenLesson, Action onLibrary, Action onBack)
        {
            _p = ThemeService.Current;
            _onOpenLesson = onOpenLesson;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            var title = UiKit.Label(transform, "Title", "배우기", 48, _p.TextPrimary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)title.transform, new Vector2(0f, 0.90f), new Vector2(1f, 0.97f), new Vector4(48, 0, 48, 0));

            var sub = UiKit.Label(transform, "Sub", "3×3 큐브를 처음부터 끝까지", 26, _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)sub.transform, new Vector2(0f, 0.855f), new Vector2(1f, 0.895f), new Vector4(48, 0, 48, 0));

            _stageButtons = new Button[StageChecker.LastStage];
            for (int i = 0; i < StageChecker.LastStage; i++)
            {
                int stage = i + 1;
                var lesson = LessonData.Get(stage);
                var btn = UiKit.Button(transform, $"Stage{stage}", "", _p, () => Open(stage));
                float top = 0.83f - i * 0.088f;
                UiKit.Stretch((RectTransform)btn.transform,
                    new Vector2(0.06f, top - 0.075f), new Vector2(0.94f, top), Vector4.zero);

                var label = btn.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                UiKit.Stretch((RectTransform)label.transform, Vector2.zero, Vector2.one, new Vector4(28, 0, 28, 0));
                label.text = $"{stage}. {lesson.Title}";

                _stageButtons[i] = btn;
            }

            var library = UiKit.Button(transform, "Library", "공식 모아보기", _p, () => onLibrary?.Invoke());
            UiKit.Stretch((RectTransform)library.transform, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.19f), Vector4.zero);

            var back = UiKit.Button(transform, "Back", "돌아가기", _p, () => onBack?.Invoke());
            UiKit.Stretch((RectTransform)back.transform, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.10f), Vector4.zero);

            _notice = UiKit.Label(transform, "Notice", "", 24, _p.TextSecondary);
            UiKit.Stretch((RectTransform)_notice.transform, new Vector2(0f, 0.195f), new Vector2(1f, 0.235f), Vector4.zero);

            Refresh();
        }

        void Open(int stage)
        {
            if (!LearnProgress.IsUnlocked(stage))
            {
                _notice.text = $"{stage - 1}단계를 먼저 마쳐 주세요.";
                return;
            }
            _notice.text = "";
            _onOpenLesson?.Invoke(stage);
        }

        /// 화면에 들어올 때마다 부른다. 진도가 바뀌었을 수 있다.
        public void Refresh()
        {
            _p = ThemeService.Current;
            for (int i = 0; i < _stageButtons.Length; i++)
            {
                int stage = i + 1;
                var lesson = LessonData.Get(stage);
                var label = _stageButtons[i].GetComponentInChildren<Text>();

                if (LearnProgress.IsDone(stage))
                {
                    label.text = $"{stage}. {lesson.Title}   완료";
                    label.color = _p.TextPrimary;
                    _stageButtons[i].image.color = _p.Accent;
                }
                else if (LearnProgress.IsUnlocked(stage))
                {
                    label.text = $"{stage}. {lesson.Title}";
                    label.color = _p.TextPrimary;
                    _stageButtons[i].image.color = _p.Surface;
                }
                else
                {
                    label.text = $"{stage}. {lesson.Title}   잠김";
                    label.color = _p.TextSecondary;
                    _stageButtons[i].image.color = _p.Surface;
                }
            }
        }
    }
}
