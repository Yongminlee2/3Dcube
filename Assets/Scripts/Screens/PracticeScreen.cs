using System;
using UnityEngine;
using UnityEngine.UI;
using Cube.Core;

namespace Cube.App
{
    /// 연습 화면. 큐브·전개도·버튼·타이머를 한자리에 모은다.
    public sealed class PracticeScreen : MonoBehaviour
    {
        public CubeRenderer Renderer { get; private set; }
        public TimerService Timer { get; private set; }
        public string CurrentScramble { get; private set; } = "";

        /// (걸린 ms, 스크램블, 회전 수)
        public event Action<double, string, int> Solved;

        LayerRotator _rotator;
        TouchController _touch;
        OrbitCamera _orbit;
        NetView _net;
        NotationPad _pad;
        GameObject _padRoot;
        GameObject _netCard;
        GameObject _netRoot;
        RectTransform _netCardRect, _netTitleRect, _netToggleRect;
        MoveHistory _history;
        Text _timerLabel, _scrambleLabel;
        Palette _p;

        // 스크램블 카드 바로 아래에 고정한 윗변. 접히면 얇은 띠만 남기고,
        // 펴지면 아래로 자란다 — 그래야 중간 큐브 자리를 건드리지 않는다.
        const float NetTop = 0.835f;
        const float NetExpandedBottom = 0.605f;
        const float NetCollapsedBottom = 0.795f;
        int _n;
        int _movesSinceScramble;
        bool _armed;                 // 섞은 뒤 아직 첫 수를 두지 않은 상태
        bool _suppressHistory;       // 되돌리기가 만든 무브를 기록에서 걸러낸다
        readonly System.Random _rng = new System.Random();

        public void Build(RectTransform parent, int cubeSize, Action onBack = null)
        {
            _n = cubeSize;
            var p = ThemeService.Current;
            _p = p;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            // 두 번 지어도 UI가 겹쳐 쌓이지 않게 한다.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var old = transform.GetChild(i).gameObject;
                old.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            }

            // --- 3D 쪽 ---
            var cubeRoot = AppBootstrap.Instance != null
                ? AppBootstrap.Instance.CubeRoot
                : new GameObject("CubeRoot").transform;

            Renderer = GetOrAdd<CubeRenderer>(cubeRoot.gameObject);
            Renderer.Build(CubeState.Solved(_n));

            _rotator = GetOrAdd<LayerRotator>(cubeRoot.gameObject);
            _rotator.Init(Renderer);
            _rotator.MoveApplied += OnMoveApplied;

            _orbit = GetOrAdd<OrbitCamera>(cubeRoot.gameObject);
            _orbit.Init(cubeRoot);

            var cam = AppBootstrap.Instance != null ? AppBootstrap.Instance.CubeCamera : Camera.main;
            if (cam != null)
            {
                _touch = GetOrAdd<TouchController>(cubeRoot.gameObject);
                _touch.Init(cam, Renderer, _rotator, _orbit);
            }

            _history = new MoveHistory();
            Timer = new TimerService();

            // --- UI 쪽 ---
            var back = UiKit.Button(transform, "Back", "", p,
                () => onBack?.Invoke(), ButtonVariant.Ghost);
            UiKit.Stretch((RectTransform)back.transform,
                new Vector2(0.03f, 0.91f), new Vector2(0.12f, 0.98f), Vector4.zero);
            var backIcon = UiKit.Icon(back.transform, "BackIcon", "arrow-left", p.TextPrimary);
            UiKit.Stretch((RectTransform)backIcon.transform,
                new Vector2(0.18f, 0.18f), new Vector2(0.82f, 0.82f), Vector4.zero);

            var context = UiKit.Label(transform, "Context", $"{_n}×{_n} 연습", 28,
                p.TextSecondary, TextAnchor.MiddleLeft);
            context.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)context.transform,
                new Vector2(0.14f, 0.915f), new Vector2(0.50f, 0.975f), Vector4.zero);

            _timerLabel = UiKit.Label(transform, "Timer", "0.00", UiMetrics.Display, p.TextPrimary, TextAnchor.MiddleRight);
            _timerLabel.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)_timerLabel.transform,
                new Vector2(0.50f, 0.91f), new Vector2(0.95f, 0.98f), Vector4.zero);

            var headerDivider = UiKit.Divider(transform, "HeaderDivider", p.Border);
            UiKit.Stretch(headerDivider, new Vector2(0f, 0.902f), new Vector2(1f, 0.903f), Vector4.zero);

            var scrambleCard = UiKit.Card(transform, "ScrambleCard", p, raised: true);
            UiKit.Stretch(scrambleCard, new Vector2(0.05f, 0.842f), new Vector2(0.95f, 0.895f), Vector4.zero);
            UiKit.AddSoftOutline(scrambleCard.GetComponent<Image>(), p.Border, 0.8f);
            var scrambleTag = UiKit.Label(scrambleCard, "Tag", "SCRAMBLE", UiMetrics.Micro, p.Accent, TextAnchor.MiddleLeft);
            scrambleTag.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)scrambleTag.transform,
                new Vector2(0.035f, 0.08f), new Vector2(0.22f, 0.92f), Vector4.zero);
            _scrambleLabel = UiKit.Label(scrambleCard, "Scramble", "섞기 버튼을 눌러 시작하세요", UiMetrics.Caption,
                p.TextSecondary, TextAnchor.MiddleLeft);
            _scrambleLabel.resizeTextForBestFit = true;
            _scrambleLabel.resizeTextMinSize = 15;
            _scrambleLabel.resizeTextMaxSize = UiMetrics.Caption;
            UiKit.Stretch((RectTransform)_scrambleLabel.transform,
                new Vector2(0.22f, 0.08f), new Vector2(0.965f, 0.92f), Vector4.zero);

            // 전개도는 스크램블 카드 바로 아래, 접힌 채로 시작한다. 큐브가 화면
            // 가운데를 넓게 쓰도록 — 자리를 차지한다는 의견이 있었다.
            var netCard = UiKit.Card(transform, "NetCard", p);
            _netCard = netCard.gameObject;
            _netCardRect = netCard;
            UiKit.Stretch(netCard, new Vector2(0.10f, NetExpandedBottom), new Vector2(0.90f, NetTop), Vector4.zero);
            UiKit.AddSoftOutline(netCard.GetComponent<Image>(), p.Border, 0.8f);
            var netTitle = UiKit.Label(netCard, "NetTitle", "전개도", UiMetrics.Caption,
                p.TextSecondary, TextAnchor.MiddleLeft);
            netTitle.fontStyle = FontStyle.Bold;
            _netTitleRect = (RectTransform)netTitle.transform;
            UiKit.Stretch(_netTitleRect,
                new Vector2(0.055f, 0.84f), new Vector2(0.35f, 0.98f), Vector4.zero);
            var netRoot = UiKit.Panel(netCard, "Net", new Color(0, 0, 0, 0));
            _netRoot = netRoot.gameObject;
            UiKit.Stretch(netRoot, new Vector2(0.055f, 0.05f), new Vector2(0.945f, 0.84f), Vector4.zero);
            var aspect = netRoot.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 4f / 3f;
            _net = netRoot.gameObject.AddComponent<NetView>();
            _net.Build(_n);
            _net.Refresh(Renderer.State);

            // 전개도를 접을 수 있게 한다. 화면이 좁으면 거슬린다는 의견이 있었다.
            _netToggle = UiKit.Button(netCard, "NetToggle", "", p, ToggleNet, ButtonVariant.Ghost);
            _netToggleRect = (RectTransform)_netToggle.transform;
            UiKit.Stretch(_netToggleRect,
                new Vector2(0.64f, 0.84f), new Vector2(0.96f, 0.98f), Vector4.zero);
            var toggleLabel = _netToggle.GetComponentInChildren<Text>();
            toggleLabel.fontSize = UiMetrics.Caption;
            toggleLabel.color = ThemeService.Current.TextSecondary;
            _net.Expanded = AppSettings.ShowNet;
            RefreshNetToggle();

            var padRoot = UiKit.Panel(transform, "Pad", new Color(0, 0, 0, 0));
            UiKit.Stretch(padRoot, new Vector2(0.03f, 0.135f), new Vector2(0.97f, 0.255f), Vector4.zero);
            _padRoot = padRoot.gameObject;
            _pad = padRoot.gameObject.AddComponent<NotationPad>();
            _pad.Build(padRoot, _n, p, ApplyMove);
            _padRoot.SetActive(AppSettings.ShowPad);

            // 힌트를 누르면 여기에 다음 수와 이유가 뜨고, 다시 누르면 그대로 둬 준다.
            _hintButton = UiKit.Button(transform, "Hint", "", p, FollowHint, ButtonVariant.Card);
            UiKit.Stretch((RectTransform)_hintButton.transform,
                new Vector2(0.05f, 0.095f), new Vector2(0.95f, 0.128f), Vector4.zero);
            var hintLabel = _hintButton.GetComponentInChildren<Text>();
            hintLabel.alignment = TextAnchor.MiddleLeft;
            hintLabel.fontSize = UiMetrics.SectionTitle;
            UiKit.Stretch((RectTransform)hintLabel.transform, Vector2.zero, Vector2.one, new Vector4(20, 0, 20, 0));
            _hintButton.gameObject.SetActive(false);

            var bar = UiKit.Panel(transform, "Bar", new Color(0, 0, 0, 0));
            UiKit.Stretch(bar, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.09f), Vector4.zero);
            MakeBarButton(bar, "섞기", 0f, 0.235f, Scramble, p, ButtonVariant.Primary);
            MakeBarButton(bar, "힌트", 0.255f, 0.235f, ShowHint, p, ButtonVariant.Secondary);
            MakeBarButton(bar, "되돌리기", 0.51f, 0.235f, Undo, p, ButtonVariant.Secondary);
            MakeBarButton(bar, "초기화", 0.765f, 0.235f, ResetCube, p, ButtonVariant.Secondary);
        }

        Button _hintButton;
        Button _netToggle;
        Hint _hint;

        void ToggleNet()
        {
            AppSettings.ShowNet = !AppSettings.ShowNet;
            _net.Expanded = AppSettings.ShowNet;
            RefreshNetToggle();
        }

        void RefreshNetToggle()
        {
            if (_netToggle == null) return;
            bool expanded = AppSettings.ShowNet;
            _netToggle.GetComponentInChildren<Text>().text = expanded ? "접기" : "펴기";
            if (_netCard != null) _netCard.SetActive(true);
            if (_netRoot != null) _netRoot.SetActive(expanded);

            if (_netCardRect != null)
                UiKit.Stretch(_netCardRect,
                    new Vector2(0.10f, expanded ? NetExpandedBottom : NetCollapsedBottom),
                    new Vector2(0.90f, NetTop), Vector4.zero);
            if (_netTitleRect != null)
                UiKit.Stretch(_netTitleRect,
                    new Vector2(0.055f, expanded ? 0.84f : 0f),
                    new Vector2(0.35f, expanded ? 0.98f : 1f), Vector4.zero);
            if (_netToggleRect != null)
                UiKit.Stretch(_netToggleRect,
                    new Vector2(0.64f, expanded ? 0.84f : 0f),
                    new Vector2(0.96f, expanded ? 0.98f : 1f), Vector4.zero);
        }

        /// 다음 수와 이유를 보여주고, 봐야 할 조각을 전개도에 강조한다.
        public void ShowHint()
        {
            if (_n != 3)
            {
                SetHintText("힌트는 3×3에서만 됩니다.");
                _hintButton.interactable = false;
                return;
            }

            _hint = HintEngine.Next(Renderer.State);
            _net.ClearHighlights();

            if (_hint.IsSolved)
            {
                SetHintText(_hint.Reason);
                _hintButton.interactable = false;
            }
            else
            {
                foreach (var (face, row, col) in HintEngine.PendingCells(Renderer.State, _hint.Stage))
                    _net.SetHighlight(face, row, col, ThemeService.Current.Accent);

                SetHintText(_hint.HasMove
                    ? $"▶  {_hint.Notation}      {_hint.Reason}"
                    : _hint.Reason);
                _hintButton.interactable = _hint.HasMove;
            }
            _net.Refresh(Renderer.State);
        }

        /// 힌트 줄을 누르면 그 수를 대신 둬 준다.
        public void FollowHint()
        {
            if (!_hint.HasMove) return;
            _rotator.EnqueueRange(MoveNotation.Parse(_hint.Notation, _n));
            _hint = default;
            _net.ClearHighlights();
            _net.Refresh(Renderer.State);
            _hintButton.gameObject.SetActive(false);
        }

        void SetHintText(string text)
        {
            _hintButton.gameObject.SetActive(true);
            _hintButton.GetComponentInChildren<Text>().text = text;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        void MakeBarButton(RectTransform bar, string label, float xMin, float width, Action action,
                           Palette p, ButtonVariant variant)
        {
            var btn = UiKit.Button(bar, $"Bar_{label}", label, p, () => action(), variant);
            btn.GetComponentInChildren<Text>().fontSize = 25;
            btn.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMin + width, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // 큐브 부품은 화면들이 공유한다. 숨어 있는 동안에도 구독이 살아 있으면
        // 학습 화면에서 돌린 수가 연습 기록으로 새어 들어간다.
        void OnEnable()
        {
            if (_rotator == null) return;
            _rotator.MoveApplied -= OnMoveApplied;
            _rotator.MoveApplied += OnMoveApplied;
        }

        void OnDisable()
        {
            if (_rotator != null) _rotator.MoveApplied -= OnMoveApplied;
        }

        void OnDestroy()
        {
            if (_rotator != null) _rotator.MoveApplied -= OnMoveApplied;
        }

        public void ApplyMove(Move m) => _rotator.Enqueue(m);

        void OnMoveApplied(Move m)
        {
            // 되돌리기가 만든 무브는 기록에도 수 세기에도 넣지 않는다.
            if (_suppressHistory) { _net.Refresh(Renderer.State); return; }

            _history.Push(m);
            _movesSinceScramble++;

            if (_armed) { Timer.BeginSolve(); _armed = false; }

            _net.Refresh(Renderer.State);

            if (Timer.Phase == TimerPhase.Running && Renderer.State.IsSolved())
            {
                Timer.Stop();
                Solved?.Invoke(Timer.ElapsedMs, CurrentScramble, _movesSinceScramble);
            }
        }

        public void Scramble()
        {
            ResetCube();
            CurrentScramble = Scrambler.Generate(_n, _rng);
            _scrambleLabel.text = CurrentScramble;

            // 섞는 동안에는 타이머가 반응하지 않아야 한다.
            _armed = false;
            _suppressHistory = true;
            _rotator.ApplyInstant(MoveNotation.Parse(CurrentScramble, _n));
            _suppressHistory = false;

            _history.Clear();
            _movesSinceScramble = 0;
            _net.Refresh(Renderer.State);

            Timer.Reset();
            if (AppSettings.Inspection) Timer.BeginInspection();
            _armed = true;
        }

        public void Undo()
        {
            if (!_history.CanUndo) return;
            var inverse = _history.Undo();

            // Enqueue가 MoveApplied를 그 자리에서 쏘기 때문에 플래그로 막을 수 있다.
            _suppressHistory = true;
            _rotator.Enqueue(inverse);
            _suppressHistory = false;
            _movesSinceScramble = Mathf.Max(0, _movesSinceScramble - 1);
        }

        /// 밖에서 만든 상태를 그대로 실어 준다. 실물 큐브 색을 넣고 힌트를 받는 경로다.
        public void LoadState(CubeState state)
        {
            if (state == null || state.N != _n) return;
            ResetCube();
            _rotator.FinishAllImmediately();
            Renderer.Build(state);
            _rotator.Init(Renderer);
            _net.Refresh(Renderer.State);
        }

        public void ResetCube()
        {
            _rotator.FinishAllImmediately();
            Renderer.Build(CubeState.Solved(_n));
            _rotator.Init(Renderer);
            if (_touch != null)
                _touch.Init(AppBootstrap.Instance != null ? AppBootstrap.Instance.CubeCamera : Camera.main,
                            Renderer, _rotator, _orbit);

            _history.Clear();
            _movesSinceScramble = 0;
            CurrentScramble = "";
            if (_scrambleLabel != null) _scrambleLabel.text = "섞기 버튼을 눌러 시작하세요";
            _armed = false;
            Timer.Reset();

            // 큐브가 바뀌면 들고 있던 힌트는 더 이상 맞지 않는다.
            _hint = default;
            if (_hintButton != null) _hintButton.gameObject.SetActive(false);
            if (_net != null) { _net.ClearHighlights(); _net.Refresh(Renderer.State); }
        }

        void Update()
        {
            if (_timerLabel == null) return;
            switch (Timer.Phase)
            {
                case TimerPhase.Inspection:
                    double left = Timer.InspectionRemainingMs / 1000d;
                    _timerLabel.text = left >= 0d ? $"{left:F0}" : $"+{-left:F0}";
                    _timerLabel.color = _p != null ? _p.Warning : Color.yellow;
                    break;
                case TimerPhase.Running:
                    _timerLabel.color = _p != null ? _p.TextPrimary : Color.white;
                    _timerLabel.text = $"{Timer.ElapsedMs / 1000d:F2}";
                    break;
                case TimerPhase.Stopped:
                    _timerLabel.text = $"{Timer.ElapsedMs / 1000d:F2}";
                    _timerLabel.color = _p != null ? _p.Success : Color.green;
                    break;
                default:
                    _timerLabel.text = "0.00";
                    _timerLabel.color = _p != null ? _p.TextPrimary : Color.white;
                    break;
            }
        }
    }
}
