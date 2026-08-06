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
        MoveHistory _history;
        Text _timerLabel, _scrambleLabel;
        int _n;
        int _movesSinceScramble;
        bool _armed;                 // 섞은 뒤 아직 첫 수를 두지 않은 상태
        bool _suppressHistory;       // 되돌리기가 만든 무브를 기록에서 걸러낸다
        readonly System.Random _rng = new System.Random();

        public void Build(RectTransform parent, int cubeSize)
        {
            _n = cubeSize;
            var p = ThemeService.Current;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

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
            _timerLabel = UiKit.Label(transform, "Timer", "0.00", 72, p.TextPrimary, TextAnchor.MiddleRight);
            UiKit.Stretch((RectTransform)_timerLabel.transform, new Vector2(0.5f, 0.90f), new Vector2(1f, 0.98f), new Vector4(0, 0, 40, 0));

            _scrambleLabel = UiKit.Label(transform, "Scramble", "", 26, p.TextSecondary, TextAnchor.UpperLeft);
            UiKit.Stretch((RectTransform)_scrambleLabel.transform, new Vector2(0f, 0.83f), new Vector2(1f, 0.90f), new Vector4(40, 0, 40, 0));

            var netRoot = UiKit.Panel(transform, "Net", new Color(0, 0, 0, 0));
            UiKit.Stretch(netRoot, new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.42f), Vector4.zero);
            _net = netRoot.gameObject.AddComponent<NetView>();
            _net.Build(_n);
            _net.Refresh(Renderer.State);

            var padRoot = UiKit.Panel(transform, "Pad", new Color(0, 0, 0, 0));
            UiKit.Stretch(padRoot, new Vector2(0.03f, 0.13f), new Vector2(0.97f, 0.20f), Vector4.zero);
            _padRoot = padRoot.gameObject;
            _pad = padRoot.gameObject.AddComponent<NotationPad>();
            _pad.Build(padRoot, _n, p, ApplyMove);
            _padRoot.SetActive(AppSettings.ShowPad);

            var bar = UiKit.Panel(transform, "Bar", new Color(0, 0, 0, 0));
            UiKit.Stretch(bar, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.09f), Vector4.zero);
            MakeBarButton(bar, "섞기", 0f, Scramble, p);
            MakeBarButton(bar, "되돌리기", 0.34f, Undo, p);
            MakeBarButton(bar, "초기화", 0.68f, ResetCube, p);
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        void MakeBarButton(RectTransform bar, string label, float xMin, Action action, Palette p)
        {
            var btn = UiKit.Button(bar, $"Bar_{label}", label, p, () => action());
            var rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMin + 0.32f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
            _rotator.EnqueueRange(MoveNotation.Parse(CurrentScramble, _n));
            _rotator.FinishAllImmediately();
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
            if (_scrambleLabel != null) _scrambleLabel.text = "";
            _armed = false;
            Timer.Reset();
            if (_net != null) _net.Refresh(Renderer.State);
        }

        void Update()
        {
            if (_timerLabel == null) return;
            switch (Timer.Phase)
            {
                case TimerPhase.Inspection:
                    double left = Timer.InspectionRemainingMs / 1000d;
                    _timerLabel.text = left >= 0d ? $"{left:F0}" : $"+{-left:F0}";
                    break;
                case TimerPhase.Running:
                case TimerPhase.Stopped:
                    _timerLabel.text = $"{Timer.ElapsedMs / 1000d:F2}";
                    break;
                default:
                    _timerLabel.text = "0.00";
                    break;
            }
        }
    }
}
