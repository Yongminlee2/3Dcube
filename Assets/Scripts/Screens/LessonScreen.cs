using System;
using UnityEngine;
using UnityEngine.UI;
using Cube.Core;

namespace Cube.App
{
    /// 한 단계 화면. 설명 문단 + 공식 카드 + 연습.
    public sealed class LessonScreen : MonoBehaviour
    {
        public int Stage { get; private set; }
        public bool InPractice { get; private set; }
        /// 방금 이 단계를 통과했을 때.
        public event Action<int> StagePassed;

        Palette _p;
        CubeRenderer _renderer;
        LayerRotator _rotator;
        TouchController _touch;
        LessonPlayer _player;

        Text _title, _body, _pageLabel, _status;
        Button _prev, _next, _practice, _rewind;
        Transform _algRoot;
        Lesson _lesson;
        int _page;

        public void Build(RectTransform parent, Action onBack)
        {
            _p = ThemeService.Current;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            AttachCube();

            _title = UiKit.Label(transform, "Title", "", 40, _p.TextPrimary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)_title.transform, new Vector2(0f, 0.91f), new Vector2(1f, 0.97f), new Vector4(48, 0, 48, 0));

            _body = UiKit.Label(transform, "Body", "", 30, _p.TextPrimary, TextAnchor.UpperLeft);
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Stretch((RectTransform)_body.transform, new Vector2(0f, 0.42f), new Vector2(1f, 0.56f), new Vector4(48, 0, 48, 0));

            _pageLabel = UiKit.Label(transform, "Page", "", 24, _p.TextSecondary);
            UiKit.Stretch((RectTransform)_pageLabel.transform, new Vector2(0.35f, 0.355f), new Vector2(0.65f, 0.41f), Vector4.zero);

            _prev = UiKit.Button(transform, "Prev", "이전", _p, () => TurnPage(-1));
            UiKit.Stretch((RectTransform)_prev.transform, new Vector2(0.06f, 0.345f), new Vector2(0.33f, 0.415f), Vector4.zero);

            _next = UiKit.Button(transform, "Next", "다음", _p, () => TurnPage(1));
            UiKit.Stretch((RectTransform)_next.transform, new Vector2(0.67f, 0.345f), new Vector2(0.94f, 0.415f), Vector4.zero);

            var algPanel = UiKit.Panel(transform, "Algorithms", new Color(0, 0, 0, 0));
            UiKit.Stretch(algPanel, new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.335f), Vector4.zero);
            _algRoot = algPanel;

            _status = UiKit.Label(transform, "Status", "", 26, _p.Accent);
            UiKit.Stretch((RectTransform)_status.transform, new Vector2(0f, 0.145f), new Vector2(1f, 0.195f), Vector4.zero);

            _practice = UiKit.Button(transform, "Practice", "연습하기", _p, Practice);
            UiKit.Stretch((RectTransform)_practice.transform, new Vector2(0.06f, 0.03f), new Vector2(0.48f, 0.115f), Vector4.zero);

            _rewind = UiKit.Button(transform, "Rewind", "큐브 되돌리기", _p, ResetCube);
            UiKit.Stretch((RectTransform)_rewind.transform, new Vector2(0.52f, 0.03f), new Vector2(0.94f, 0.115f), Vector4.zero);

            var back = UiKit.Button(transform, "Back", "목록", _p, () => onBack?.Invoke());
            UiKit.Stretch((RectTransform)back.transform, new Vector2(0.72f, 0.905f), new Vector2(0.96f, 0.965f), Vector4.zero);
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

        /// 이 단계를 연다. 큐브는 완성 상태에서 시작한다.
        public void Open(int stage)
        {
            Stage = stage;
            _lesson = LessonData.Get(stage);
            _page = 0;
            InPractice = false;

            ResetCube();
            _title.text = $"{stage}. {_lesson.Title}";
            _status.text = "";
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
            // Init을 불러야 기본 시점이 잡힌다. 안 부르면 큐브가 정면만 보여
            // 세 면 중 하나밖에 안 보인다 — 배우는 화면에서는 치명적이다.
            orbit.Init(_renderer.transform);
            if (cam != null) _touch.Init(cam, _renderer, _rotator, orbit);
            _player.Init(_renderer, _rotator, _touch);

            InPractice = false;
            if (_status != null) _status.text = "";
        }

        void BuildAlgorithmCards()
        {
            for (int i = _algRoot.childCount - 1; i >= 0; i--)
            {
                var child = _algRoot.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            var algs = _lesson.Algorithms;
            if (algs.Length == 0)
            {
                var none = UiKit.Label(_algRoot, "NoAlg", "이 단계는 공식 없이 눈으로 찾습니다", 26,
                    _p.TextSecondary, TextAnchor.MiddleCenter);
                UiKit.Stretch((RectTransform)none.transform, Vector2.zero, Vector2.one, Vector4.zero);
                return;
            }

            for (int i = 0; i < algs.Length; i++)
            {
                var alg = algs[i];
                var btn = UiKit.Button(_algRoot, $"Alg{i}", "", _p, () => PlayAlgorithm(alg));
                var rt = (RectTransform)btn.transform;
                rt.anchorMin = new Vector2(0f, 1f - (i + 1f) / algs.Length);
                rt.anchorMax = new Vector2(1f, 1f - i / (float)algs.Length);
                rt.offsetMin = new Vector2(0f, 3f);
                rt.offsetMax = new Vector2(0f, -3f);

                var label = btn.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 28;
                label.text = $"▶  {alg.Notation}      {alg.Name}";
                UiKit.Stretch((RectTransform)label.transform, Vector2.zero, Vector2.one, new Vector4(24, 0, 24, 0));
            }
        }

        void PlayAlgorithm(Algorithm alg)
        {
            _status.text = $"{alg.Name} — {alg.When}";
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

        /// 이 단계 직전 상태를 만들어 준다. 사용자가 맞추면 통과 판정이 뜬다.
        public void Practice()
        {
            ResetCube();
            _rotator.ApplyInstant(MoveNotation.Parse(_lesson.PracticeSetup, 3));

            InPractice = true;
            _status.text = "직접 맞춰 보세요.";
        }

        void OnMoveApplied(Move m)
        {
            if (!InPractice || _renderer.State == null) return;
            if (!StageChecker.Passed(_renderer.State, Stage)) return;

            InPractice = false;
            LearnProgress.MarkDone(Stage);
            _status.text = Stage < StageChecker.LastStage
                ? $"통과했습니다. {Stage + 1}단계가 열렸습니다."
                : "큐브를 다 맞췄습니다.";
            StagePassed?.Invoke(Stage);
        }

        // 연습 화면과 큐브 부품을 공유하므로, 숨어 있는 동안에는 구독을 끊는다.
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
