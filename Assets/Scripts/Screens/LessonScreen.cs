using System;
using UnityEngine;
using UnityEngine.UI;
using Cube.Core;

namespace Cube.App
{
    /// 7단계 학습 화면. 설명 카드, 공식 시연, 직접 연습을 한 화면에서 제공한다.
    public sealed class LessonScreen : MonoBehaviour
    {
        public int Stage { get; private set; }
        public bool InPractice { get; private set; }

        /// 현재 단계를 통과했을 때 알린다.
        public event Action<int> StagePassed;

        Palette _p;
        CubeRenderer _renderer;
        LayerRotator _rotator;
        TouchController _touch;
        LessonPlayer _player;

        Text _title, _body, _pageLabel, _status;
        Button _prev, _next, _practice, _hint, _rewind;
        Transform _algRoot;
        RectTransform _statusCard;
        Image _statusInfoIcon, _statusSuccessIcon;
        Lesson _lesson;
        int _page;

        // 설명 읽기 전용 요소(코치 카드·페이지 넘김·공식 목록)를 한데 묶는다.
        // 연습하기를 누르면 이 묶음을 통째로 숨기고 큐브를 크게, 가운데로 —
        // 설명 읽을 때 필요한 요소가 직접 손을 대는 동안엔 그냥 자리만 차지한다.
        GameObject _explainGroup;

        public void Build(RectTransform parent, Action onBack)
        {
            _p = ThemeService.Current;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            AttachCube();

            _title = UiKit.ScreenHeader(transform, "단계 학습", _p, onBack);
            _title.fontSize = 40;

            BuildPreviewLabel();

            var explainGroup = new GameObject("ExplainGroup", typeof(RectTransform));
            explainGroup.transform.SetParent(transform, false);
            UiKit.Stretch(explainGroup.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector4.zero);
            _explainGroup = explainGroup;

            BuildCoachCard();
            BuildPager();
            BuildAlgorithmArea();
            BuildStatusCard();
            BuildActions();
        }

        void BuildPreviewLabel()
        {
            var plate = UiKit.IconPlate(transform, "PreviewIcon", "cube", _p, _p.Accent);
            UiKit.Stretch(plate,
                new Vector2(0.055f, 0.835f), new Vector2(0.115f, 0.89f), Vector4.zero);

            var label = UiKit.Label(transform, "PreviewLabel", "직접 돌려 보며 따라와요",
                UiMetrics.Caption, _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)label.transform,
                new Vector2(0.13f, 0.835f), new Vector2(0.52f, 0.89f), Vector4.zero);
        }

        void BuildCoachCard()
        {
            var card = UiKit.Card(_explainGroup.transform, "CoachCard", _p, raised: true);
            UiKit.Stretch(card,
                new Vector2(0.055f, 0.435f), new Vector2(0.945f, 0.575f), Vector4.zero);
            UiKit.AddSoftOutline(card.GetComponent<Image>(), _p.Border, 1f);

            var plate = UiKit.IconPlate(card, "CoachIcon", "book-2", _p, _p.Accent);
            UiKit.Stretch(plate,
                new Vector2(0.03f, 0.55f), new Vector2(0.135f, 0.89f), Vector4.zero);

            var eyebrow = UiKit.Label(card, "CoachTitle", "코치의 설명", 21,
                _p.Accent, TextAnchor.MiddleLeft);
            eyebrow.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)eyebrow.transform,
                new Vector2(0.16f, 0.69f), new Vector2(0.95f, 0.94f), Vector4.zero);

            _body = UiKit.Label(card, "Body", "", 24,
                _p.TextPrimary, TextAnchor.UpperLeft);
            UiKit.Wrap(_body);
            UiKit.Stretch((RectTransform)_body.transform,
                new Vector2(0.16f, 0.08f), new Vector2(0.95f, 0.70f), Vector4.zero);
        }

        void BuildPager()
        {
            _prev = UiKit.Button(_explainGroup.transform, "Prev", "이전", _p,
                () => TurnPage(-1), ButtonVariant.Secondary);
            UiKit.Stretch((RectTransform)_prev.transform,
                new Vector2(0.055f, 0.36f), new Vector2(0.305f, 0.425f), Vector4.zero);
            StylePagerButton(_prev, "arrow-left", iconOnRight: false);

            var pagePill = UiKit.Panel(_explainGroup.transform, "PagePill", _p.SurfaceMuted);
            var pageImage = pagePill.GetComponent<Image>();
            pageImage.sprite = UiKit.RoundedPill;
            pageImage.type = Image.Type.Sliced;
            UiKit.Stretch(pagePill,
                new Vector2(0.365f, 0.365f), new Vector2(0.635f, 0.42f), Vector4.zero);

            _pageLabel = UiKit.Label(pagePill, "Page", "", 22, _p.TextSecondary);
            _pageLabel.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)_pageLabel.transform,
                Vector2.zero, Vector2.one, Vector4.zero);

            _next = UiKit.Button(_explainGroup.transform, "Next", "다음", _p,
                () => TurnPage(1), ButtonVariant.Secondary);
            UiKit.Stretch((RectTransform)_next.transform,
                new Vector2(0.695f, 0.36f), new Vector2(0.945f, 0.425f), Vector4.zero);
            StylePagerButton(_next, "chevron-right", iconOnRight: true);
        }

        void StylePagerButton(Button button, string iconName, bool iconOnRight)
        {
            var label = button.transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.fontSize = 23;
                label.fontStyle = FontStyle.Bold;
                UiKit.Stretch((RectTransform)label.transform,
                    new Vector2(iconOnRight ? 0.08f : 0.28f, 0f),
                    new Vector2(iconOnRight ? 0.72f : 0.92f, 1f), Vector4.zero);
            }

            var icon = UiKit.Icon(button.transform, "DirectionIcon", iconName, _p.TextSecondary);
            UiKit.Stretch((RectTransform)icon.transform,
                new Vector2(iconOnRight ? 0.72f : 0.10f, 0.30f),
                new Vector2(iconOnRight ? 0.90f : 0.28f, 0.70f), Vector4.zero);
        }

        void BuildAlgorithmArea()
        {
            var heading = UiKit.Label(_explainGroup.transform, "AlgorithmHeading", "바로 써보는 공식",
                UiMetrics.SectionTitle, _p.TextPrimary, TextAnchor.MiddleLeft);
            heading.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)heading.transform,
                new Vector2(0.055f, 0.322f), new Vector2(0.945f, 0.357f), Vector4.zero);

            var scroll = UiKit.ScrollList(_explainGroup.transform, "Algorithms", _p,
                out var content, spacing: 8f, padding: 2f);
            UiKit.Stretch((RectTransform)scroll.transform,
                new Vector2(0.055f, 0.205f), new Vector2(0.945f, 0.322f), Vector4.zero);
            _algRoot = content;
        }

        void BuildStatusCard()
        {
            _statusCard = UiKit.Card(transform, "StatusCard", _p);
            UiKit.Stretch(_statusCard,
                new Vector2(0.055f, 0.14f), new Vector2(0.945f, 0.195f), Vector4.zero);
            UiKit.AddSoftOutline(_statusCard.GetComponent<Image>(), _p.Border, 0.8f);

            _statusInfoIcon = UiKit.Icon(_statusCard, "InfoIcon", "sparkles", _p.Accent);
            UiKit.Stretch((RectTransform)_statusInfoIcon.transform,
                new Vector2(0.035f, 0.25f), new Vector2(0.095f, 0.75f), Vector4.zero);

            _statusSuccessIcon = UiKit.Icon(_statusCard, "SuccessIcon", "check", _p.Success);
            UiKit.Stretch((RectTransform)_statusSuccessIcon.transform,
                new Vector2(0.035f, 0.25f), new Vector2(0.095f, 0.75f), Vector4.zero);

            _status = UiKit.Label(_statusCard, "Status", "", 22,
                _p.Accent, TextAnchor.MiddleLeft);
            _status.fontStyle = FontStyle.Bold;
            UiKit.Wrap(_status);
            UiKit.Stretch((RectTransform)_status.transform,
                new Vector2(0.12f, 0.06f), new Vector2(0.96f, 0.94f), Vector4.zero);
            SetStatus("");
        }

        void BuildActions()
        {
            _practice = UiKit.Button(transform, "Practice", "연습하기", _p,
                Practice, ButtonVariant.Primary);
            UiKit.Stretch((RectTransform)_practice.transform,
                new Vector2(0.055f, 0.035f), new Vector2(0.425f, 0.12f), Vector4.zero);
            StyleActionButton(_practice, "player-play", _p.TextOnAccent, 0.28f);

            _hint = UiKit.Button(transform, "Hint", "힌트", _p,
                ShowHint, ButtonVariant.Secondary);
            UiKit.Stretch((RectTransform)_hint.transform,
                new Vector2(0.445f, 0.035f), new Vector2(0.69f, 0.12f), Vector4.zero);
            StyleActionButton(_hint, "sparkles", _p.Accent, 0.32f);

            _rewind = UiKit.Button(transform, "Rewind", "처음으로", _p,
                ResetCube, ButtonVariant.Secondary);
            UiKit.Stretch((RectTransform)_rewind.transform,
                new Vector2(0.71f, 0.035f), new Vector2(0.945f, 0.12f), Vector4.zero);
            StyleActionButton(_rewind, "arrow-left", _p.TextSecondary, 0.32f);
        }

        void StyleActionButton(Button button, string iconName, Color iconColor, float labelStart)
        {
            var label = button.transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.fontSize = 24;
                label.fontStyle = FontStyle.Bold;
                UiKit.Stretch((RectTransform)label.transform,
                    new Vector2(labelStart, 0f), new Vector2(0.94f, 1f), Vector4.zero);
            }

            var icon = UiKit.Icon(button.transform, "ActionIcon", iconName, iconColor);
            UiKit.Stretch((RectTransform)icon.transform,
                new Vector2(0.10f, 0.30f), new Vector2(labelStart - 0.04f, 0.70f), Vector4.zero);
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

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        /// 단계를 연다. 큐브는 완성 상태에서 시작한다.
        public void Open(int stage)
        {
            Stage = stage;
            _lesson = LessonData.Get(stage);
            _page = 0;
            InPractice = false;

            ResetCube();
            _title.text = $"{stage}단계 · {_lesson.Title}";
            SetStatus("");
            BuildAlgorithmCards();
            ShowPage();
        }

        void ResetCube()
        {
            _rotator.FinishAllImmediately();
            _renderer.Build(CubeState.Solved(3));
            _rotator.Init(_renderer);
            _rotator.MoveApplied -= OnMoveApplied;
            _rotator.MoveApplied += OnMoveApplied;

            var cam = AppBootstrap.Instance != null ? AppBootstrap.Instance.CubeCamera : Camera.main;
            var orbit = GetOrAdd<OrbitCamera>(_renderer.gameObject);
            orbit.Init(_renderer.transform);
            if (cam != null) _touch.Init(cam, _renderer, _rotator, orbit);
            _player.Init(_renderer, _rotator, _touch);

            InPractice = false;
            SetStatus("");
            RefreshPracticeLayout();
        }

        /// 연습하기를 누르면 설명 요소를 통째로 숨기고 큐브를 크게, 화면
        /// 가운데로 옮긴다 — 손으로 만지는 동안엔 설명 카드가 자리만 차지한다.
        void RefreshPracticeLayout()
        {
            if (_explainGroup != null) _explainGroup.SetActive(!InPractice);
            if (AppBootstrap.Instance == null) return;
            AppBootstrap.Instance.SetCubePresentation(
                InPractice ? 0.95f : 0.72f,
                InPractice ? 0.04f : 0.25f);
        }

        void BuildAlgorithmCards()
        {
            // Destroy is delayed in Play Mode, so detach first to keep rebuilds deterministic.
            for (int i = _algRoot.childCount - 1; i >= 0; i--)
            {
                var child = _algRoot.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            var algs = _lesson.Algorithms;
            if (algs.Length == 0)
            {
                var card = UiKit.Card(_algRoot, "NoAlgCard", _p);
                UiKit.SetLayoutHeight(card, 92f);
                UiKit.AddSoftOutline(card.GetComponent<Image>(), _p.Border, 0.8f);

                var plate = UiKit.IconPlate(card, "NoAlgIcon", "sparkles", _p, _p.Accent);
                UiKit.Stretch(plate,
                    new Vector2(0.025f, 0.18f), new Vector2(0.115f, 0.82f), Vector4.zero);

                var none = UiKit.Label(card, "NoAlg", "공식 없이 눈으로 찾아보는 단계예요", 22,
                    _p.TextPrimary, TextAnchor.MiddleLeft);
                none.fontStyle = FontStyle.Bold;
                UiKit.Stretch((RectTransform)none.transform,
                    new Vector2(0.14f, 0.47f), new Vector2(0.97f, 0.88f), Vector4.zero);

                var hint = UiKit.Label(card, "NoAlgHint", "큐브를 천천히 돌리며 조각을 살펴보세요", 18,
                    _p.TextSecondary, TextAnchor.MiddleLeft);
                UiKit.Stretch((RectTransform)hint.transform,
                    new Vector2(0.14f, 0.10f), new Vector2(0.97f, 0.51f), Vector4.zero);
                return;
            }

            for (int i = 0; i < algs.Length; i++)
            {
                var alg = algs[i];
                var btn = UiKit.Button(_algRoot, $"Alg{i}", alg.Name, _p,
                    () => PlayAlgorithm(alg), ButtonVariant.Card);
                UiKit.SetLayoutHeight(btn, 100f);
                UiKit.AddSoftOutline(btn.image, _p.Border, 0.8f);

                var label = btn.transform.Find("Label")?.GetComponent<Text>();
                if (label != null)
                {
                    label.alignment = TextAnchor.MiddleLeft;
                    label.fontSize = 22;
                    label.fontStyle = FontStyle.Bold;
                    UiKit.Stretch((RectTransform)label.transform,
                        new Vector2(0.15f, 0.48f), new Vector2(0.94f, 0.90f), Vector4.zero);
                }

                var plate = UiKit.IconPlate(btn.transform, "PlayIcon", "player-play", _p, _p.Accent);
                UiKit.Stretch(plate,
                    new Vector2(0.025f, 0.18f), new Vector2(0.115f, 0.82f), Vector4.zero);

                var notation = UiKit.Label(btn.transform, "Notation", alg.Notation, 19,
                    _p.Accent, TextAnchor.MiddleLeft);
                notation.fontStyle = FontStyle.Bold;
                UiKit.Stretch((RectTransform)notation.transform,
                    new Vector2(0.15f, 0.08f), new Vector2(0.94f, 0.50f), Vector4.zero);
            }
        }

        /// 현재 큐브 상태에서 다음 한 수를 돌려서 보여준다.
        public void ShowHint()
        {
            if (_renderer == null || _renderer.State == null) return;

            var hint = HintEngine.Next(_renderer.State);
            if (hint.IsSolved)
            {
                SetStatus("이미 다 맞췄습니다.", success: true);
                return;
            }

            if (!hint.HasMove)
            {
                SetStatus(hint.Reason);
                return;
            }

            SetStatus($"{hint.Notation}  ·  {hint.Reason}");
            _player.Play(hint.Notation);
        }

        void PlayAlgorithm(Algorithm alg)
        {
            SetStatus($"{alg.Name}  ·  {alg.When}");
            _player.Play(alg.Notation);
        }

        void ShowPage()
        {
            _body.text = _lesson.Steps[_page];
            _pageLabel.text = $"{_page + 1} / {_lesson.Steps.Length}";
            _prev.interactable = _page > 0;
            _next.interactable = _page < _lesson.Steps.Length - 1;
        }

        void TurnPage(int delta)
        {
            int next = Mathf.Clamp(_page + delta, 0, _lesson.Steps.Length - 1);
            if (next == _page) return;
            _page = next;
            ShowPage();
        }

        /// 단계 직전 상태를 만든다. 사용자가 맞추면 통과 판정을 한다.
        public void Practice()
        {
            ResetCube();
            _rotator.ApplyInstant(MoveNotation.Parse(_lesson.PracticeSetup, 3));

            InPractice = true;
            SetStatus("직접 맞춰 보세요.");
            RefreshPracticeLayout();
        }

        void OnMoveApplied(Move m)
        {
            if (!InPractice || _renderer.State == null) return;
            if (!StageChecker.Passed(_renderer.State, Stage)) return;

            InPractice = false;
            LearnProgress.MarkDone(Stage);
            string message = Stage < StageChecker.LastStage
                ? $"통과했습니다. {Stage + 1}단계가 열렸습니다."
                : "큐브를 다 맞췄습니다.";
            SetStatus(message, success: true);
            StagePassed?.Invoke(Stage);
        }

        void SetStatus(string text, bool success = false)
        {
            if (_statusCard == null || _status == null) return;

            bool visible = !string.IsNullOrEmpty(text);
            _statusCard.gameObject.SetActive(visible);
            if (!visible) return;

            _status.text = text;
            _status.color = success ? _p.Success : _p.Accent;
            _statusCard.GetComponent<Image>().color = success ? _p.AccentSoft : _p.Surface;
            if (_statusInfoIcon != null) _statusInfoIcon.gameObject.SetActive(!success);
            if (_statusSuccessIcon != null) _statusSuccessIcon.gameObject.SetActive(success);
        }

        // 연습 화면과 큐브 부품을 공유하므로 화면이 보이는 동안에만 구독한다.
        void OnDisable()
        {
            if (_rotator != null) _rotator.MoveApplied -= OnMoveApplied;
        }

        void OnEnable()
        {
            if (_rotator == null) return;
            _rotator.MoveApplied -= OnMoveApplied;
            _rotator.MoveApplied += OnMoveApplied;
        }

        void OnDestroy()
        {
            if (_rotator != null) _rotator.MoveApplied -= OnMoveApplied;
        }
    }
}
