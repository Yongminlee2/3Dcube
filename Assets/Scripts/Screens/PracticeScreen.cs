using System;
using System.Collections.Generic;
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
        GameObject _confirmOverlay;
        Text _confirmTitle, _confirmMessage, _confirmAcceptLabel;
        Button _confirmAccept;
        Action _pendingConfirmedAction;
        Palette _p;

        // 스크램블 카드 바로 아래에 고정한 윗변. 접히면 얇은 띠만 남기고,
        // 펴지면 아래로 자란다 — 그래야 중간 큐브 자리를 건드리지 않는다.
        const float NetTop = 0.835f;
        const float NetExpandedBottom = 0.605f;
        const float NetCollapsedBottom = 0.795f;
        const float ContentLeft = 0.05f;
        const float ContentRight = 0.95f;
        int _n;
        int _movesSinceScramble;
        bool _armed;                 // 섞은 뒤 아직 첫 수를 두지 않은 상태
        bool _suppressHistory;       // 되돌리기가 만든 무브를 기록에서 걸러낸다
        bool _fromRealCube;
        bool _restoringProgress;
        readonly System.Random _rng = new System.Random();
        readonly Queue<Move> _hintPlan = new Queue<Move>();
        bool _hintPlanActive;
        const int MaxArtworkHintMoves = 12;

        public void Build(RectTransform parent, int cubeSize, Action onBack = null)
        {
            _n = cubeSize;
            var savedProgress = CubeProgressStore.LoadPractice(_n);
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
            _rotator = GetOrAdd<LayerRotator>(cubeRoot.gameObject);
            _rotator.MoveApplied -= OnMoveApplied;

            var savedState = savedProgress?.ToState();
            bool restoredPose = TryRestoreGeneratedPose(savedProgress, savedState);
            if (!restoredPose)
            {
                Renderer.Build(savedState ?? CubeState.Solved(_n));
                _rotator.Init(Renderer);
            }
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
            UiKit.Stretch(netCard,
                new Vector2(ContentLeft, NetExpandedBottom),
                new Vector2(ContentRight, NetTop), Vector4.zero);
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

            float padBottom = 0.087f;
            float padTop = _n >= 4 ? 0.205f : 0.155f;
            BuildHintCard(p, padTop + 0.012f, padTop + 0.094f);

            var padRoot = UiKit.Panel(transform, "Pad", new Color(0, 0, 0, 0));
            UiKit.Stretch(padRoot,
                new Vector2(ContentLeft, padBottom), new Vector2(ContentRight, padTop), Vector4.zero);
            _padRoot = padRoot.gameObject;
            _pad = padRoot.gameObject.AddComponent<NotationPad>();
            _pad.Build(padRoot, _n, p, ApplyMove);
            _padRoot.SetActive(AppSettings.ShowPad);

            var bar = UiKit.Card(transform, "Bar", p, raised: true);
            UiKit.Stretch(bar,
                new Vector2(ContentLeft, 0.018f), new Vector2(ContentRight, 0.075f), Vector4.zero);
            UiKit.AddSoftOutline(bar.GetComponent<Image>(), p.Border, 1f);
            MakeBarButton(bar, "섞기", "shuffle", 0, RequestScramble, p, true, false);
            MakeBarButton(bar, "힌트", "lightbulb", 1, ShowHint, p, false, false);
            MakeBarButton(bar, "되돌리기", "undo", 2, Undo, p, false, false);
            MakeBarButton(bar, "초기화", "restart", 3, RequestResetCube, p, false, true);

            BuildConfirmationDialog(p);
            RestoreProgress(savedProgress);
        }

        Button _netToggle;
        Hint _hint;

        void BuildHintCard(Palette p, float bottom, float top)
        {
            var card = UiKit.Card(transform, "Hint", p, raised: true);
            _hintCard = card.gameObject;
            UiKit.Stretch(card,
                new Vector2(ContentLeft, bottom), new Vector2(ContentRight, top), Vector4.zero);
            UiKit.AddSoftOutline(card.GetComponent<Image>(), p.Border, 1f);

            var tag = UiKit.Label(card, "HintTag", "힌트", 20,
                p.Accent, TextAnchor.MiddleLeft);
            tag.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)tag.transform,
                new Vector2(0.035f, 0.52f), new Vector2(0.26f, 0.90f), Vector4.zero);

            _hintNotation = UiKit.Label(card, "HintNotation", "직접 조작",
                30, p.TextPrimary, TextAnchor.MiddleLeft);
            _hintNotation.fontStyle = FontStyle.Bold;
            _hintNotation.resizeTextForBestFit = true;
            _hintNotation.resizeTextMinSize = 17;
            _hintNotation.resizeTextMaxSize = 30;
            UiKit.Stretch((RectTransform)_hintNotation.transform,
                new Vector2(0.035f, 0.10f), new Vector2(0.29f, 0.56f), Vector4.zero);

            var divider = UiKit.Divider(card, "HintDivider", p.Border);
            UiKit.Stretch(divider,
                new Vector2(0.315f, 0.18f), new Vector2(0.317f, 0.82f), Vector4.zero);

            _hintExplanation = UiKit.Label(card, "HintExplanation",
                "힌트를 누르면 다음 동작을 설명해 드려요. 큐브는 자동으로 움직이지 않습니다.",
                23, p.TextSecondary, TextAnchor.MiddleLeft);
            _hintExplanation.resizeTextForBestFit = true;
            _hintExplanation.resizeTextMinSize = 18;
            _hintExplanation.resizeTextMaxSize = 23;
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
                    new Vector2(ContentLeft, expanded ? NetExpandedBottom : NetCollapsedBottom),
                    new Vector2(ContentRight, NetTop), Vector4.zero);
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
                ClearHintPlan();
                SetHint("안내", "힌트는 3×3에서만 됩니다.");
                return;
            }

            if (Renderer.IsSolvedWithArtwork())
            {
                ClearHintPlan();
                _hint = Hint.Solved;
                _net.ClearHighlights();
                SetHint("완료", _hint.Reason);
                _net.Refresh(Renderer.State);
                return;
            }

            if (!_hintPlanActive || _hintPlan.Count == 0)
                PrepareHintPlan();

            _net.ClearHighlights();
            foreach (var (face, row, col) in HintEngine.PendingCells(Renderer.State, _hint.Stage))
                _net.SetHighlight(face, row, col, ThemeService.Current.Accent);

            if (_hintPlan.Count > 0) ShowPlannedSequence();
            else SetHint("안내", _hint.Reason);
            _net.Refresh(Renderer.State);
        }

        void PrepareHintPlan()
        {
            ClearHintPlan();

            if (TryPrepareArtworkHintPlan()) return;

            _hint = HintEngine.Next(Renderer.State);
            if (!_hint.HasMove) return;

            EnqueueHintMoves(MoveNotation.Parse(_hint.Notation, _n));
        }

        bool TryPrepareArtworkHintPlan()
        {
            if (_fromRealCube || string.IsNullOrEmpty(CurrentScramble)
                || SkinService.ArtworkLayout != SkinArtworkLayout.WholeFace)
                return false;

            var skin = SkinService.Current;
            if (skin == null || skin.StickerTextures == null
                || !Array.Exists(skin.StickerTextures, texture => texture != null))
                return false;

            List<Move> applied;
            try
            {
                applied = new List<Move>(MoveNotation.Parse(CurrentScramble, _n));
                foreach (var move in _history.Moves) applied.Add(move);
            }
            catch (FormatException)
            {
                return false;
            }

            var reduced = ReduceExactPath(applied);
            if (reduced.Count == 0) return false;

            var next = new List<Move>();
            for (int i = reduced.Count - 1; i >= 0 && next.Count < MaxArtworkHintMoves; i--)
                next.Add(reduced[i].Inverse);

            _hint = new Hint(7, MoveNotation.Format(next, _n),
                "그림 조각의 위치와 상하좌우 방향까지 정확히 맞추는 수열입니다.");
            EnqueueHintMoves(next);
            return _hintPlanActive;
        }

        void EnqueueHintMoves(IEnumerable<Move> moves)
        {
            foreach (var move in moves)
            {
                // 화면에 2회 버튼이 없으므로 U2 같은 반 바퀴는 같은 버튼 두 번으로 안내한다.
                if (move.Turns == 2)
                {
                    int quarterTurn = move.Layer == _n - 1 ? 3 : 1;
                    _hintPlan.Enqueue(new Move(move.Axis, move.Layer, quarterTurn));
                    _hintPlan.Enqueue(new Move(move.Axis, move.Layer, quarterTurn));
                }
                else _hintPlan.Enqueue(move);
            }

            _hintPlanActive = _hintPlan.Count > 0;
        }

        static List<Move> ReduceExactPath(IEnumerable<Move> moves)
        {
            var reduced = new List<Move>();
            foreach (var move in moves)
            {
                int lastIndex = reduced.Count - 1;
                if (lastIndex >= 0
                    && reduced[lastIndex].Axis == move.Axis
                    && reduced[lastIndex].Layer == move.Layer)
                {
                    int turns = (reduced[lastIndex].Turns + move.Turns) & 3;
                    if (turns == 0) reduced.RemoveAt(lastIndex);
                    else reduced[lastIndex] = new Move(move.Axis, move.Layer, turns);
                }
                else reduced.Add(move);
            }
            return reduced;
        }

        void ShowPlannedSequence()
        {
            if (_hintPlan.Count == 0) return;
            string notation = HintEngine.SimplifyNotation(MoveNotation.Format(_hintPlan, _n));
            SetHint(notation, $"왼쪽부터 순서대로 끝까지 실행하세요 · {_hint.Reason}\n"
                + "중간에 흐트러져 보여도 이 수식을 끝까지 계속하면 됩니다.");
        }

        void AdvanceHintPlan(Move applied)
        {
            if (!_hintPlanActive || _hintPlan.Count == 0) return;

            if (!_hintPlan.Peek().Equals(applied))
            {
                ClearHintPlan();
                _net.ClearHighlights();
                SetHint("경로 다시 계산",
                    "안내와 다른 동작이 들어왔어요. 현재 상태에서 힌트를 다시 눌러 주세요.");
                return;
            }

            _hintPlan.Dequeue();
            if (_hintPlan.Count > 0)
            {
                ShowPlannedSequence();
                return;
            }

            _hintPlanActive = false;
            SetHint("묶음 완료", "여기까지 잘 따라왔어요. 힌트를 눌러 다음 동작을 확인하세요.");
        }

        void ClearHintPlan()
        {
            _hintPlan.Clear();
            _hintPlanActive = false;
            _hint = default;
        }

        /// 예전 공개 API를 유지하되 힌트는 설명 전용이다.
        /// 직접 호출해도 큐브 상태를 바꾸지 않는다.
        public void FollowHint()
        {
            if (_n == 3) ShowHint();
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
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.LowerCenter;
            UiKit.Stretch((RectTransform)text.transform,
                new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.47f), Vector4.zero);

            Color color = danger
                ? new Color(1f, 0.34f, 0.38f, 1f)
                : primary ? p.TextOnAccent : p.TextPrimary;
            text.color = color;
            var icon = UiKit.Icon(btn.transform, "Icon", iconName, color);
            UiKit.Stretch((RectTransform)icon.transform,
                new Vector2(0.35f, 0.50f), new Vector2(0.65f, 0.88f), Vector4.zero);

            if (index > 0)
            {
                var divider = UiKit.Divider(bar, $"Divider_{index}", p.Border);
                UiKit.Stretch(divider,
                    new Vector2(xMin, 0.20f), new Vector2(xMin + 0.0015f, 0.80f), Vector4.zero);
                divider.SetAsFirstSibling();
            }
        }

        void BuildConfirmationDialog(Palette p)
        {
            var overlay = UiKit.Panel(transform, "ConfirmOverlay", new Color(0f, 0f, 0f, 0.72f));
            _confirmOverlay = overlay.gameObject;
            UiKit.Stretch(overlay, Vector2.zero, Vector2.one, Vector4.zero);

            // 카드 바깥을 누르면 취소된다.
            var dismiss = overlay.gameObject.AddComponent<Button>();
            dismiss.targetGraphic = overlay.GetComponent<Image>();
            dismiss.onClick.AddListener(CancelConfirmation);

            var card = UiKit.Card(overlay, "ConfirmCard", p, raised: true);
            UiKit.Stretch(card,
                new Vector2(0.075f, 0.365f), new Vector2(0.925f, 0.625f), Vector4.zero);
            UiKit.AddSoftOutline(card.GetComponent<Image>(), p.Border, 1f);

            // 카드 자체를 누른 클릭이 뒤의 취소 버튼까지 전달되지 않게 막는다.
            var blocker = card.gameObject.AddComponent<Button>();
            blocker.targetGraphic = card.GetComponent<Image>();

            var iconPlate = UiKit.IconPlate(card, "ConfirmIcon", "restart", p, p.Warning);
            UiKit.Stretch(iconPlate,
                new Vector2(0.055f, 0.67f), new Vector2(0.17f, 0.90f), Vector4.zero);

            _confirmTitle = UiKit.Label(card, "Title", "확인", 32,
                p.TextPrimary, TextAnchor.MiddleLeft);
            _confirmTitle.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)_confirmTitle.transform,
                new Vector2(0.20f, 0.66f), new Vector2(0.94f, 0.91f), Vector4.zero);

            _confirmMessage = UiKit.Label(card, "Message", "", 23,
                p.TextSecondary, TextAnchor.UpperLeft);
            UiKit.Wrap(_confirmMessage);
            UiKit.Stretch((RectTransform)_confirmMessage.transform,
                new Vector2(0.055f, 0.35f), new Vector2(0.945f, 0.64f), Vector4.zero);

            var cancel = UiKit.Button(card, "ConfirmCancel", "취소", p,
                CancelConfirmation, ButtonVariant.Secondary);
            UiKit.Stretch((RectTransform)cancel.transform,
                new Vector2(0.055f, 0.08f), new Vector2(0.47f, 0.30f), Vector4.zero);
            var cancelLabel = cancel.transform.Find("Label")?.GetComponent<Text>();
            if (cancelLabel != null) { cancelLabel.fontSize = 25; cancelLabel.fontStyle = FontStyle.Bold; }

            _confirmAccept = UiKit.Button(card, "ConfirmAccept", "계속", p,
                ConfirmPending, ButtonVariant.Primary);
            UiKit.Stretch((RectTransform)_confirmAccept.transform,
                new Vector2(0.53f, 0.08f), new Vector2(0.945f, 0.30f), Vector4.zero);
            _confirmAcceptLabel = _confirmAccept.transform.Find("Label")?.GetComponent<Text>();
            if (_confirmAcceptLabel != null)
            {
                _confirmAcceptLabel.fontSize = 25;
                _confirmAcceptLabel.fontStyle = FontStyle.Bold;
            }

            _confirmOverlay.SetActive(false);
        }

        void RequestScramble()
        {
            ShowConfirmation("새로 섞을까요?",
                "현재 맞추던 상태는 새 스크램블로 바뀝니다.",
                "섞기", Scramble, danger: false);
        }

        void RequestResetCube()
        {
            ShowConfirmation("처음 상태로 돌릴까요?",
                "현재 맞추던 큐브 상태와 진행 기록이 지워집니다.",
                "초기화", ResetCube, danger: true);
        }

        void ShowConfirmation(string title, string message, string acceptLabel,
                              Action confirmed, bool danger)
        {
            if (_confirmOverlay == null) return;
            _pendingConfirmedAction = confirmed;
            _confirmTitle.text = title;
            _confirmMessage.text = message;
            if (_confirmAcceptLabel != null) _confirmAcceptLabel.text = acceptLabel;
            UiKit.StyleButton(_confirmAccept, _p,
                danger ? ButtonVariant.Danger : ButtonVariant.Primary);
            _confirmOverlay.SetActive(true);
            _confirmOverlay.transform.SetAsLastSibling();
        }

        void CancelConfirmation()
        {
            _pendingConfirmedAction = null;
            if (_confirmOverlay != null) _confirmOverlay.SetActive(false);
        }

        void ConfirmPending()
        {
            var action = _pendingConfirmedAction;
            CancelConfirmation();
            action?.Invoke();
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
            SaveProgress();
            CancelConfirmation();
            if (_rotator != null) _rotator.MoveApplied -= OnMoveApplied;
        }

        void OnDestroy()
        {
            if (_rotator != null) _rotator.MoveApplied -= OnMoveApplied;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && gameObject.activeInHierarchy) SaveProgress();
        }

        public void ApplyMove(Move m) => _rotator.Enqueue(m);

        void OnMoveApplied(Move m)
        {
            // 되돌리기가 만든 무브는 기록에도 수 세기에도 넣지 않는다.
            if (_suppressHistory) { _net.Refresh(Renderer.State); return; }

            AdvanceHintPlan(m);

            _history.Push(m);
            _movesSinceScramble++;

            if (_armed) { Timer.BeginSolve(); _armed = false; }

            _net.Refresh(Renderer.State);

            if (Timer.Phase == TimerPhase.Running && Renderer.IsSolvedWithArtwork())
            {
                Timer.Stop();
                Solved?.Invoke(Timer.ElapsedMs, CurrentScramble, _movesSinceScramble);
                AudioService.PlaySuccess();
            }
            SaveProgress();
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
            _fromRealCube = false;
            SaveProgress();
        }

        public void Undo()
        {
            if (!_history.CanUndo) return;
            ClearHintPlan();
            var inverse = _history.Undo();

            // Enqueue가 MoveApplied를 그 자리에서 쏘기 때문에 플래그로 막을 수 있다.
            _suppressHistory = true;
            _rotator.Enqueue(inverse);
            _suppressHistory = false;
            _movesSinceScramble = Mathf.Max(0, _movesSinceScramble - 1);
            SaveProgress();
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
            CurrentScramble = "";
            _history.Clear();
            _movesSinceScramble = 0;
            _armed = false;
            _fromRealCube = true;
            Timer.Reset();
            if (_scrambleLabel != null)
                _scrambleLabel.text = "촬영한 실물 큐브를 이어서 풀고 있어요";
            SaveProgress();
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
            _fromRealCube = false;
            Timer.Reset();

            // 큐브가 바뀌면 들고 있던 힌트는 더 이상 맞지 않는다.
            ClearHintPlan();
            SetHint("직접 조작",
                "힌트를 누르면 다음 동작을 설명해 드려요. 큐브는 자동으로 움직이지 않습니다.");
            if (_net != null) { _net.ClearHighlights(); _net.Refresh(Renderer.State); }
            SaveProgress();
        }

        bool TryRestoreGeneratedPose(PracticeProgressSnapshot snapshot, CubeState savedState)
        {
            if (snapshot == null || savedState == null || snapshot.FromRealCube
                || string.IsNullOrEmpty(snapshot.Scramble))
                return false;

            try
            {
                var moves = new List<Move>(MoveNotation.Parse(snapshot.Scramble, _n));
                if (!string.IsNullOrEmpty(snapshot.HistoryNotation))
                    moves.AddRange(MoveNotation.Parse(snapshot.HistoryNotation, _n));

                // 그림이 완성된 자세에서 실제 수열을 다시 재생해야 센터를 포함한
                // 모든 조각의 0/90/180/270도 방향이 저장 전과 똑같이 돌아온다.
                Renderer.Build(CubeState.Solved(_n));
                _rotator.Init(Renderer);
                _rotator.ApplyInstant(moves);
                return Renderer.State.SameAs(savedState);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        void RestoreProgress(PracticeProgressSnapshot snapshot)
        {
            if (snapshot == null) return;
            _restoringProgress = true;
            try
            {
                CurrentScramble = snapshot.Scramble ?? "";
                _movesSinceScramble = Mathf.Max(0, snapshot.MovesSinceScramble);
                _armed = snapshot.Armed;
                _fromRealCube = snapshot.FromRealCube;

                _history.Clear();
                try
                {
                    foreach (var move in MoveNotation.Parse(snapshot.HistoryNotation, _n))
                        _history.Push(move);
                }
                catch (FormatException)
                {
                    _history.Clear();
                }

                var phase = (TimerPhase)Mathf.Clamp(snapshot.TimerPhase,
                    (int)TimerPhase.Idle, (int)TimerPhase.Stopped);
                Timer.Restore(phase, snapshot.TimerElapsedMs, snapshot.InspectionRemainingMs);

                if (_scrambleLabel != null)
                {
                    if (!string.IsNullOrEmpty(CurrentScramble))
                        _scrambleLabel.text = CurrentScramble;
                    else if (_fromRealCube)
                        _scrambleLabel.text = "촬영한 실물 큐브를 이어서 풀고 있어요";
                }
                _net?.Refresh(Renderer.State);
            }
            finally
            {
                _restoringProgress = false;
            }
        }

        public void SaveProgress()
        {
            if (_restoringProgress || Renderer == null || Renderer.State == null
                || Timer == null || _history == null || _n < 2) return;

            // 완성 상태는 이어 할 작업이 아니다. 다음 입장은 새 연습으로 연다.
            if (Renderer.IsSolvedWithArtwork())
            {
                CubeProgressStore.ClearPractice(_n);
                return;
            }

            CubeProgressStore.SavePractice(new PracticeProgressSnapshot
            {
                CubeSize = _n,
                FaceletsBase64 = CubeProgressStore.EncodeState(Renderer.State),
                Scramble = CurrentScramble ?? "",
                HistoryNotation = MoveNotation.Format(_history.Moves, _n),
                MovesSinceScramble = _movesSinceScramble,
                Armed = _armed,
                FromRealCube = _fromRealCube,
                TimerPhase = (int)Timer.Phase,
                TimerElapsedMs = Timer.ElapsedMs,
                InspectionRemainingMs = Timer.InspectionRemainingMs,
                ArtworkPending = Renderer.State.IsSolved() && !Renderer.IsSolvedWithArtwork(),
            });
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
