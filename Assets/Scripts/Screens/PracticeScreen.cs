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
        GameObject _hintCard;
        Text _hintNotation, _hintExplanation;
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
            _scrambleLabel = UiKit.Label(scrambleCard, "Scramble",
                "섞기 버튼으로 시작 · 두 손가락으로 시점 조절", UiMetrics.Caption,
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

            BuildHintCard(p);

            var padRoot = UiKit.Panel(transform, "Pad", new Color(0, 0, 0, 0));
            UiKit.Stretch(padRoot, new Vector2(0.045f, 0.11f), new Vector2(0.955f, 0.235f), Vector4.zero);
            _padRoot = padRoot.gameObject;
            _pad = padRoot.gameObject.AddComponent<NotationPad>();
            _pad.Build(padRoot, _n, p, ApplyMove);
            _padRoot.SetActive(AppSettings.ShowPad);

            var bar = UiKit.Card(transform, "Bar", p, raised: true);
            UiKit.Stretch(bar, new Vector2(0.045f, 0.018f), new Vector2(0.955f, 0.095f), Vector4.zero);
            UiKit.AddSoftOutline(bar.GetComponent<Image>(), p.Border, 1f);
            MakeBarButton(bar, "섞기", "shuffle", 0, Scramble, p, true, false);
            MakeBarButton(bar, "힌트", "lightbulb", 1, ShowHint, p, false, false);
            MakeBarButton(bar, "되돌리기", "undo", 2, Undo, p, false, false);
            MakeBarButton(bar, "초기화", "restart", 3, ResetCube, p, false, true);
        }

        Button _netToggle;
        Hint _hint;

        void BuildHintCard(Palette p)
        {
            var card = UiKit.Card(transform, "Hint", p, raised: true);
            _hintCard = card.gameObject;
            UiKit.Stretch(card,
                new Vector2(0.045f, 0.245f), new Vector2(0.955f, 0.335f), Vector4.zero);
            UiKit.AddSoftOutline(card.GetComponent<Image>(), p.Border, 1f);

            var tag = UiKit.Label(card, "HintTag", "힌트", UiMetrics.Micro,
                p.Accent, TextAnchor.MiddleLeft);
            tag.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)tag.transform,
                new Vector2(0.035f, 0.52f), new Vector2(0.26f, 0.90f), Vector4.zero);

            _hintNotation = UiKit.Label(card, "HintNotation", "직접 조작",
                28, p.TextPrimary, TextAnchor.MiddleLeft);
            _hintNotation.fontStyle = FontStyle.Bold;
            _hintNotation.resizeTextForBestFit = true;
            _hintNotation.resizeTextMinSize = 17;
            _hintNotation.resizeTextMaxSize = 28;
            UiKit.Stretch((RectTransform)_hintNotation.transform,
                new Vector2(0.035f, 0.10f), new Vector2(0.29f, 0.56f), Vector4.zero);

            var divider = UiKit.Divider(card, "HintDivider", p.Border);
            UiKit.Stretch(divider,
                new Vector2(0.315f, 0.18f), new Vector2(0.317f, 0.82f), Vector4.zero);

            _hintExplanation = UiKit.Label(card, "HintExplanation",
                "힌트를 누르면 다음 동작을 설명해 드려요. 큐브는 자동으로 움직이지 않습니다.",
                20, p.TextSecondary, TextAnchor.MiddleLeft);
            _hintExplanation.resizeTextForBestFit = true;
            _hintExplanation.resizeTextMinSize = 15;
            _hintExplanation.resizeTextMaxSize = 20;
            UiKit.Wrap(_hintExplanation);
            UiKit.Stretch((RectTransform)_hintExplanation.transform,
                new Vector2(0.35f, 0.12f), new Vector2(0.965f, 0.88f), Vector4.zero);
        }

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
                SetHint("안내", "힌트는 3×3에서만 됩니다.");
                return;
            }

            _hint = HintEngine.Next(Renderer.State);
            _net.ClearHighlights();

            if (_hint.IsSolved)
            {
                SetHint("완료", _hint.Reason);
            }
            else
            {
                foreach (var (face, row, col) in HintEngine.PendingCells(Renderer.State, _hint.Stage))
                    _net.SetHighlight(face, row, col, ThemeService.Current.Accent);

                if (_hint.HasMove)
                    SetHint(_hint.Notation,
                        $"{MoveNotation.DescribeFirst(_hint.Notation)} · {_hint.Reason}");
                else
                    SetHint("안내", _hint.Reason);
            }
            _net.Refresh(Renderer.State);
        }

        /// 예전 공개 API를 유지하되 힌트는 설명 전용이다.
        /// 직접 호출해도 큐브 상태를 바꾸지 않는다.
        public void FollowHint()
        {
            if (_hint.HasMove) ShowHint();
        }

        void SetHint(string notation, string explanation)
        {
            if (_hintCard != null) _hintCard.SetActive(true);
            if (_hintNotation != null) _hintNotation.text = notation;
            if (_hintExplanation != null) _hintExplanation.text = explanation;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        void MakeBarButton(RectTransform bar, string label, string iconName, int index, Action action,
                           Palette p, bool primary, bool danger)
        {
            var btn = UiKit.Button(bar, $"Bar_{label}", label, p, () => action(),
                primary ? ButtonVariant.Primary : ButtonVariant.Ghost);
            var rt = (RectTransform)btn.transform;
            float xMin = index / 4f;
            rt.anchorMin = new Vector2(xMin, 0.06f);
            rt.anchorMax = new Vector2(xMin + 0.25f, 0.94f);
            rt.offsetMin = new Vector2(5f, 0f);
            rt.offsetMax = new Vector2(-5f, 0f);
            btn.image.sprite = UiKit.RoundedTight;

            var text = btn.transform.Find("Label").GetComponent<Text>();
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.LowerCenter;
            UiKit.Stretch((RectTransform)text.transform,
                new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.44f), Vector4.zero);

            Color color = danger
                ? new Color(1f, 0.34f, 0.38f, 1f)
                : primary ? p.TextOnAccent : p.TextPrimary;
            text.color = color;
            var icon = UiKit.Icon(btn.transform, "Icon", iconName, color);
            UiKit.Stretch((RectTransform)icon.transform,
                new Vector2(0.39f, 0.49f), new Vector2(0.61f, 0.86f), Vector4.zero);

            if (index > 0)
            {
                var divider = UiKit.Divider(bar, $"Divider_{index}", p.Border);
                UiKit.Stretch(divider,
                    new Vector2(xMin, 0.20f), new Vector2(xMin + 0.0015f, 0.80f), Vector4.zero);
                divider.SetAsFirstSibling();
            }
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
            if (_scrambleLabel != null)
                _scrambleLabel.text = "섞기 버튼으로 시작 · 두 손가락으로 시점 조절";
            _armed = false;
            Timer.Reset();

            // 큐브가 바뀌면 들고 있던 힌트는 더 이상 맞지 않는다.
            _hint = default;
            SetHint("직접 조작",
                "힌트를 누르면 다음 동작을 설명해 드려요. 큐브는 자동으로 움직이지 않습니다.");
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
