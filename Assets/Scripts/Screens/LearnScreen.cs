using System;
using UnityEngine;
using UnityEngine.UI;
using Cube.Core;

namespace Cube.App
{
    /// 학습 홈. 짧은 코스 진행률과 7단계를 한눈에 보여준다.
    public sealed class LearnScreen : MonoBehaviour
    {
        Palette _p;
        Button[] _stageButtons;
        Text[] _stageTitles;
        Text[] _stageStatuses;
        Text _notice;
        Text _progressText;
        Image _progressFill;
        Action<int> _onOpenLesson;

        public void Build(RectTransform parent, Action<int> onOpenLesson, Action onLibrary, Action onBack)
        {
            _p = ThemeService.Current;
            _onOpenLesson = onOpenLesson;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            BuildHeader(onBack);
            BuildProgress();

            _stageButtons = new Button[StageChecker.LastStage];
            _stageTitles = new Text[StageChecker.LastStage];
            _stageStatuses = new Text[StageChecker.LastStage];
            for (int i = 0; i < StageChecker.LastStage; i++) BuildStageRow(i + 1, i);

            _notice = UiKit.Label(transform, "Notice", "", 21, _p.Warning, TextAnchor.MiddleCenter);
            UiKit.Wrap(_notice);
            UiKit.Stretch((RectTransform)_notice.transform,
                new Vector2(0.06f, 0.178f), new Vector2(0.94f, 0.225f), Vector4.zero);

            var library = UiKit.Button(transform, "Library", "공식 모아보기", _p,
                () => onLibrary?.Invoke(), ButtonVariant.Secondary);
            UiKit.Stretch((RectTransform)library.transform,
                new Vector2(0.055f, 0.075f), new Vector2(0.945f, 0.155f), Vector4.zero);
            var libraryLabel = library.GetComponentInChildren<Text>();
            libraryLabel.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)libraryLabel.transform,
                new Vector2(0.34f, 0f), new Vector2(0.78f, 1f), Vector4.zero);
            var libraryPlate = UiKit.IconPlate(library.transform, "LibraryIconPlate", "list-details", _p, _p.Accent);
            UiKit.Stretch(libraryPlate,
                new Vector2(0.235f, 0.18f), new Vector2(0.34f, 0.82f), Vector4.zero);
            var libraryChevron = UiKit.Icon(library.transform, "LibraryChevron", "chevron-right", _p.TextSecondary);
            UiKit.Stretch((RectTransform)libraryChevron.transform,
                new Vector2(0.89f, 0.34f), new Vector2(0.94f, 0.66f), Vector4.zero);

            Refresh();
        }

        void BuildHeader(Action onBack)
        {
            UiKit.ScreenHeader(transform, "배우기", _p, onBack);
        }

        void BuildProgress()
        {
            var card = UiKit.Card(transform, "CourseProgress", _p, raised: true);
            UiKit.Stretch(card, new Vector2(0.055f, 0.815f), new Vector2(0.945f, 0.89f), Vector4.zero);
            UiKit.AddSoftOutline(card.GetComponent<Image>(), _p.Border, 1f);

            var title = UiKit.Label(card, "CourseTitle", "3×3 입문 코스", 26,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)title.transform,
                new Vector2(0.045f, 0.42f), new Vector2(0.62f, 0.92f), Vector4.zero);

            _progressText = UiKit.Label(card, "ProgressText", "", 21,
                _p.Accent, TextAnchor.MiddleRight);
            _progressText.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)_progressText.transform,
                new Vector2(0.62f, 0.42f), new Vector2(0.95f, 0.92f), Vector4.zero);

            var track = UiKit.Panel(card, "ProgressTrack", _p.SurfaceMuted);
            track.GetComponent<Image>().sprite = UiKit.RoundedPill;
            track.GetComponent<Image>().type = Image.Type.Sliced;
            UiKit.Stretch(track, new Vector2(0.045f, 0.16f), new Vector2(0.955f, 0.30f), Vector4.zero);
            var fill = UiKit.Panel(track, "ProgressFill", _p.Accent);
            _progressFill = fill.GetComponent<Image>();
            _progressFill.sprite = UiKit.RoundedPill;
            _progressFill.type = Image.Type.Sliced;
            UiKit.Stretch(fill, Vector2.zero, new Vector2(0f, 1f), Vector4.zero);
        }

        void BuildStageRow(int stage, int index)
        {
            var lesson = LessonData.Get(stage);
            float top = 0.795f - index * 0.077f;
            var btn = UiKit.Button(transform, $"Stage{stage}", "", _p,
                () => Open(stage), ButtonVariant.Card);
            UiKit.Stretch((RectTransform)btn.transform,
                new Vector2(0.055f, top - 0.068f), new Vector2(0.945f, top), Vector4.zero);
            UiKit.AddSoftOutline(btn.image, _p.Border, 0.8f);

            var number = UiKit.Label(btn.transform, "Number", stage.ToString(), 24,
                _p.Accent, TextAnchor.MiddleCenter);
            number.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)number.transform,
                new Vector2(0.035f, 0f), new Vector2(0.105f, 1f), Vector4.zero);

            var title = UiKit.Label(btn.transform, "Title", lesson.Title, 26,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)title.transform,
                new Vector2(0.13f, 0f), new Vector2(0.73f, 1f), Vector4.zero);

            var status = UiKit.Label(btn.transform, "Status", "", 21,
                _p.TextSecondary, TextAnchor.MiddleRight);
            status.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)status.transform,
                new Vector2(0.73f, 0f), new Vector2(0.94f, 1f), Vector4.zero);

            _stageButtons[index] = btn;
            _stageTitles[index] = title;
            _stageStatuses[index] = status;
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

        public void Refresh()
        {
            if (_stageButtons == null) return;
            _p = ThemeService.Current;
            int completed = LearnProgress.Completed;
            if (_progressText != null) _progressText.text = $"{completed} / {StageChecker.LastStage} 완료";
            if (_progressFill != null)
            {
                var rt = (RectTransform)_progressFill.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = new Vector2(completed / (float)StageChecker.LastStage, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            for (int i = 0; i < _stageButtons.Length; i++)
            {
                int stage = i + 1;
                bool done = LearnProgress.IsDone(stage);
                bool unlocked = LearnProgress.IsUnlocked(stage);

                if (done)
                {
                    _stageButtons[i].image.color = _p.AccentSoft;
                    _stageTitles[i].color = _p.TextPrimary;
                    _stageStatuses[i].text = "완료";
                    _stageStatuses[i].color = _p.Success;
                }
                else if (unlocked)
                {
                    _stageButtons[i].image.color = _p.SurfaceRaised;
                    _stageTitles[i].color = _p.TextPrimary;
                    _stageStatuses[i].text = stage == completed + 1 ? "시작" : "열림";
                    _stageStatuses[i].color = _p.Accent;
                }
                else
                {
                    _stageButtons[i].image.color = _p.SurfaceMuted;
                    _stageTitles[i].color = _p.TextDisabled;
                    _stageStatuses[i].text = "잠김";
                    _stageStatuses[i].color = _p.TextDisabled;
                }
            }
        }
    }
}
