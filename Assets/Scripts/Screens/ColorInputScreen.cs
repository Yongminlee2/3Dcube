using System;
using UnityEngine;
using UnityEngine.UI;
using Cube.Core;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Cube.App
{
    /// <summary>
    /// 실물 3×3 큐브의 여섯 면을 카메라로 읽고, 필요하면 직접 고친 뒤
    /// 같은 상태를 연습 화면의 3D 큐브로 넘긴다.
    /// </summary>
    public sealed class ColorInputScreen : MonoBehaviour
    {
        static readonly Face[] CaptureOrder =
        {
            Face.U, Face.F, Face.R, Face.B, Face.L, Face.D,
        };

        static readonly (Face face, int col, int row)[] NetLayout =
        {
            (Face.U, 1, 0),
            (Face.L, 0, 1), (Face.F, 1, 1), (Face.R, 2, 1), (Face.B, 3, 1),
            (Face.D, 1, 2),
        };

        static readonly string[] FaceNames = { "위", "아래", "앞", "뒤", "왼쪽", "오른쪽" };
        static readonly string[] CaptureNames = { "위", "앞", "오른쪽", "뒤", "왼쪽", "아래" };

        public CubeState Current { get; private set; }
        public byte SelectedColor { get; private set; }
        public int CapturedFaceCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _captured.Length; i++) if (_captured[i]) count++;
                return count;
            }
        }

        Palette _p;
        Action<CubeState> _onAccept;
        Action _onBack;
        Text _headerTitle;

        readonly Color[][] _samplesByFace = new Color[6][];
        readonly bool[] _captured = new bool[6];
        Color[] _liveSamples;
        int _captureSlot;
        bool _built;
        bool _scanMode = true;

        RectTransform _scanRoot;
        RawImage _cameraPreview;
        Text _cameraMessage;
        Image[] _liveCells;
        Text _progress;
        Text _instruction;
        Button[] _faceButtons;
        Image[,] _facePreviewCells;
        Outline[] _faceOutlines;
        Text _scanStatusTitle;
        Text _scanStatusBody;
        Image _scanStatusIcon;
        Button _primaryScanButton;
        Text _primaryScanLabel;
        Button _editButton;

        RectTransform _editorRoot;
        Image[] _cells;
        Image[] _faceBackgrounds;
        Button[] _swatches;
        Text[] _swatchLabels;
        Image[] _swatchChecks;
        Outline[] _swatchOutlines;
        Text _editStatus;
        Text _editStatusHint;
        Image _editStatusIcon;
        Outline _editStatusOutline;

        WebCamTexture _camera;
        float _nextLiveSampleAt;
        int _lastRotation = -1;
        bool _lastMirrored;
#if UNITY_ANDROID && !UNITY_EDITOR
        PermissionCallbacks _permissionCallbacks;
#endif

        public void Build(RectTransform parent, Action<CubeState> onAccept, Action onBack)
        {
            _p = ThemeService.Current;
            _onAccept = onAccept;
            _onBack = onBack;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            Current = CubeState.Solved(3);
            SelectedColor = (byte)Face.U;
            _captureSlot = 0;
            _scanMode = true;

            _headerTitle = UiKit.ScreenHeader(transform, "큐브 자동 인식", _p, Back);
            BuildScanUi();
            BuildEditorUi();

            RefreshCells();
            RefreshSwatches();
            RefreshScanUi();
            ClearStatus();
            ShowScanMode();

            SkinService.Changed -= OnSkinChanged;
            SkinService.Changed += OnSkinChanged;
            _built = true;
        }

        void Back()
        {
            StopCamera();
            _onBack?.Invoke();
        }

        void OnEnable()
        {
            if (_built && _scanMode) TryStartCamera();
        }

        void OnDisable() => StopCamera();

        void OnDestroy()
        {
            StopCamera();
            SkinService.Changed -= OnSkinChanged;
        }

        void BuildScanUi()
        {
            _scanRoot = UiKit.Panel(transform, "ScanMode", new Color(0, 0, 0, 0));
            UiKit.Stretch(_scanRoot, Vector2.zero, new Vector2(1f, 0.902f), Vector4.zero);

            _progress = UiKit.Label(_scanRoot, "Progress", "0 / 6면", 31,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            _progress.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)_progress.transform,
                new Vector2(0.06f, 0.925f), new Vector2(0.34f, 0.985f), Vector4.zero);

            _instruction = UiKit.Label(_scanRoot, "Instruction", "", UiMetrics.Caption,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            _instruction.fontStyle = FontStyle.Bold;
            _instruction.resizeTextForBestFit = true;
            _instruction.resizeTextMinSize = 15;
            _instruction.resizeTextMaxSize = UiMetrics.Caption;
            UiKit.Stretch((RectTransform)_instruction.transform,
                new Vector2(0.30f, 0.915f), new Vector2(0.80f, 0.988f), Vector4.zero);

            var orientation = UiKit.Artwork(_scanRoot, "OrientationCube", "UiArt/Size3Cube");
            UiKit.Stretch((RectTransform)orientation.transform,
                new Vector2(0.81f, 0.91f), new Vector2(0.95f, 0.995f), Vector4.zero);

            var cameraCard = UiKit.Card(_scanRoot, "CameraCard", _p, raised: true);
            UiKit.Stretch(cameraCard,
                new Vector2(0.055f, 0.445f), new Vector2(0.945f, 0.905f), Vector4.zero);
            UiKit.AddSoftOutline(cameraCard.GetComponent<Image>(), _p.Border, 1f);
            cameraCard.gameObject.AddComponent<RectMask2D>();

            var previewGo = new GameObject("CameraPreview", typeof(RectTransform), typeof(RawImage));
            previewGo.transform.SetParent(cameraCard, false);
            _cameraPreview = previewGo.GetComponent<RawImage>();
            _cameraPreview.color = Color.white;
            _cameraPreview.raycastTarget = false;
            UiKit.Stretch((RectTransform)previewGo.transform, Vector2.zero, Vector2.one, Vector4.zero);

            var shade = UiKit.Panel(cameraCard, "CameraShade", new Color(0f, 0f, 0f, 0.08f));
            UiKit.Stretch(shade, Vector2.zero, Vector2.one, Vector4.zero);

            _cameraMessage = UiKit.Label(cameraCard, "CameraMessage", "카메라 준비 중…",
                UiMetrics.Body, _p.TextSecondary, TextAnchor.MiddleCenter);
            _cameraMessage.fontStyle = FontStyle.Bold;
            UiKit.Wrap(_cameraMessage);
            UiKit.Stretch((RectTransform)_cameraMessage.transform,
                new Vector2(0.10f, 0.42f), new Vector2(0.90f, 0.58f), Vector4.zero);

            var overlay = UiKit.Panel(cameraCard, "DetectionGrid", new Color(0, 0, 0, 0));
            UiKit.Stretch(overlay,
                new Vector2(0.20f, 0.13f), new Vector2(0.80f, 0.87f), Vector4.zero);
            _liveCells = new Image[9];
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    int index = row * 3 + col;
                    var cell = UiKit.Cell(overlay, $"Detect_{row}_{col}", Color.clear);
                    cell.sprite = UiKit.RoundedTight;
                    cell.type = Image.Type.Sliced;
                    AddDetectionFrame(cell.transform);
                    var rt = (RectTransform)cell.transform;
                    rt.anchorMin = new Vector2(col / 3f, 1f - (row + 1) / 3f);
                    rt.anchorMax = new Vector2((col + 1) / 3f, 1f - row / 3f);
                    rt.offsetMin = new Vector2(5f, 5f);
                    rt.offsetMax = new Vector2(-5f, -5f);

                    var dot = UiKit.Cell(cell.transform, "ColorDot", Color.white);
                    dot.sprite = UiKit.RoundedPill;
                    dot.type = Image.Type.Sliced;
                    UiKit.Stretch((RectTransform)dot.transform,
                        new Vector2(0.43f, 0.43f), new Vector2(0.57f, 0.57f), Vector4.zero);
                    _liveCells[index] = dot;
                }

            var strip = UiKit.Card(_scanRoot, "CapturedFaces", _p);
            UiKit.Stretch(strip,
                new Vector2(0.055f, 0.295f), new Vector2(0.945f, 0.425f), Vector4.zero);
            UiKit.AddSoftOutline(strip.GetComponent<Image>(), _p.Border, 0.8f);
            var stripTitle = UiKit.Label(strip, "StripTitle", "촬영된 면", UiMetrics.Micro,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            stripTitle.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)stripTitle.transform,
                new Vector2(0.035f, 0.76f), new Vector2(0.40f, 0.96f), Vector4.zero);

            var rowRoot = UiKit.Panel(strip, "FaceRow", new Color(0, 0, 0, 0));
            UiKit.Stretch(rowRoot,
                new Vector2(0.025f, 0.06f), new Vector2(0.975f, 0.76f), Vector4.zero);
            _faceButtons = new Button[6];
            _faceOutlines = new Outline[6];
            _facePreviewCells = new Image[6, 9];
            for (int slot = 0; slot < 6; slot++)
            {
                int capturedSlot = slot;
                var button = UiKit.Button(rowRoot, $"Captured_{CaptureNames[slot]}", "", _p,
                    () => SelectCaptureSlot(capturedSlot), ButtonVariant.Segment);
                button.image.sprite = UiKit.RoundedTight;
                UiKit.AddSoftOutline(button.image, _p.Border, 1f);
                var rt = (RectTransform)button.transform;
                rt.anchorMin = new Vector2(slot / 6f, 0f);
                rt.anchorMax = new Vector2((slot + 1) / 6f, 1f);
                rt.offsetMin = new Vector2(5f, 0f);
                rt.offsetMax = new Vector2(-5f, 0f);

                var mini = UiKit.Panel(button.transform, "MiniFace", new Color(0, 0, 0, 0));
                UiKit.Stretch(mini, new Vector2(0.18f, 0.30f), new Vector2(0.82f, 0.96f), Vector4.zero);
                for (int cell = 0; cell < 9; cell++)
                {
                    int r = cell / 3;
                    int c = cell % 3;
                    var image = UiKit.Cell(mini, $"Cell_{r}_{c}", _p.SurfaceMuted);
                    image.sprite = UiKit.RoundedSmall;
                    image.type = Image.Type.Sliced;
                    var cellRt = (RectTransform)image.transform;
                    cellRt.anchorMin = new Vector2(c / 3f, 1f - (r + 1) / 3f);
                    cellRt.anchorMax = new Vector2((c + 1) / 3f, 1f - r / 3f);
                    cellRt.offsetMin = new Vector2(1.5f, 1.5f);
                    cellRt.offsetMax = new Vector2(-1.5f, -1.5f);
                    _facePreviewCells[slot, cell] = image;
                }

                var label = button.transform.Find("Label").GetComponent<Text>();
                label.text = CaptureNames[slot];
                label.fontSize = 16;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.LowerCenter;
                UiKit.Stretch((RectTransform)label.transform,
                    new Vector2(0f, 0.01f), new Vector2(1f, 0.30f), Vector4.zero);

                _faceButtons[slot] = button;
                _faceOutlines[slot] = button.image.GetComponent<Outline>();
            }

            var status = UiKit.Card(_scanRoot, "RecognitionStatus", _p, raised: true);
            UiKit.Stretch(status,
                new Vector2(0.055f, 0.19f), new Vector2(0.945f, 0.275f), Vector4.zero);
            UiKit.AddSoftOutline(status.GetComponent<Image>(), _p.Border, 1f);
            var plate = UiKit.IconPlate(status, "StatusIconPlate", "check", _p, _p.Success);
            UiKit.Stretch(plate,
                new Vector2(0.035f, 0.22f), new Vector2(0.145f, 0.78f), Vector4.zero);
            _scanStatusIcon = plate.Find("Icon").GetComponent<Image>();

            _scanStatusTitle = UiKit.Label(status, "StatusTitle", "촬영을 시작해 주세요", 23,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            _scanStatusTitle.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)_scanStatusTitle.transform,
                new Vector2(0.18f, 0.45f), new Vector2(0.96f, 0.88f), Vector4.zero);
            _scanStatusBody = UiKit.Label(status, "StatusBody", "안내된 순서대로 여섯 면을 촬영합니다.",
                17, _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)_scanStatusBody.transform,
                new Vector2(0.18f, 0.10f), new Vector2(0.96f, 0.50f), Vector4.zero);

            _primaryScanButton = UiKit.Button(_scanRoot, "CaptureOrStart", "다음 면 촬영", _p,
                CaptureOrAccept, ButtonVariant.Primary);
            UiKit.Stretch((RectTransform)_primaryScanButton.transform,
                new Vector2(0.055f, 0.085f), new Vector2(0.945f, 0.165f), Vector4.zero);
            _primaryScanLabel = _primaryScanButton.transform.Find("Label").GetComponent<Text>();
            _primaryScanLabel.fontSize = 31;
            _primaryScanLabel.fontStyle = FontStyle.Bold;

            _editButton = UiKit.Button(_scanRoot, "EditColors", "색상 수정", _p,
                ShowEditorMode, ButtonVariant.Ghost);
            UiKit.Stretch((RectTransform)_editButton.transform,
                new Vector2(0.055f, 0.015f), new Vector2(0.945f, 0.07f), Vector4.zero);
            _editButton.transform.Find("Label").GetComponent<Text>().fontStyle = FontStyle.Bold;
        }

        static void AddDetectionFrame(Transform parent)
        {
            Color color = new Color(1f, 1f, 1f, 0.88f);
            var top = UiKit.Panel(parent, "Top", color);
            UiKit.Stretch(top, new Vector2(0f, 0.982f), Vector2.one, Vector4.zero);
            var bottom = UiKit.Panel(parent, "Bottom", color);
            UiKit.Stretch(bottom, Vector2.zero, new Vector2(1f, 0.018f), Vector4.zero);
            var left = UiKit.Panel(parent, "Left", color);
            UiKit.Stretch(left, Vector2.zero, new Vector2(0.018f, 1f), Vector4.zero);
            var right = UiKit.Panel(parent, "Right", color);
            UiKit.Stretch(right, new Vector2(0.982f, 0f), Vector2.one, Vector4.zero);
        }

        void BuildEditorUi()
        {
            _editorRoot = UiKit.Panel(transform, "EditMode", new Color(0, 0, 0, 0));
            UiKit.Stretch(_editorRoot, Vector2.zero, new Vector2(1f, 0.902f), Vector4.zero);

            var guide = UiKit.Card(_editorRoot, "Guide", _p);
            UiKit.Stretch(guide,
                new Vector2(0.055f, 0.84f), new Vector2(0.945f, 0.98f), Vector4.zero);
            UiKit.AddSoftOutline(guide.GetComponent<Image>(), _p.Border, 1f);
            var plate = UiKit.IconPlate(guide, "CoachIcon", "hand-click", _p, _p.Accent);
            UiKit.Stretch(plate,
                new Vector2(0.035f, 0.24f), new Vector2(0.145f, 0.76f), Vector4.zero);
            var title = UiKit.Label(guide, "GuideTitle", "잘못 읽힌 칸만 고쳐 주세요", 23,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)title.transform,
                new Vector2(0.18f, 0.49f), new Vector2(0.96f, 0.90f), Vector4.zero);
            var body = UiKit.Label(guide, "GuideText",
                "아래에서 색을 고른 뒤 전개도의 칸을 누르면 바뀝니다.", 18,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)body.transform,
                new Vector2(0.18f, 0.10f), new Vector2(0.96f, 0.52f), Vector4.zero);

            BuildNet();
            BuildSwatches();
            BuildEditStatus();

            var accept = UiKit.Button(_editorRoot, "Accept", "3D 큐브로 시작", _p,
                () => TryAccept(out _), ButtonVariant.Primary);
            UiKit.Stretch((RectTransform)accept.transform,
                new Vector2(0.055f, 0.085f), new Vector2(0.945f, 0.165f), Vector4.zero);
            var acceptLabel = accept.transform.Find("Label").GetComponent<Text>();
            acceptLabel.fontSize = 31;
            acceptLabel.fontStyle = FontStyle.Bold;

            var backToCamera = UiKit.Button(_editorRoot, "BackToCamera", "촬영 화면으로", _p,
                ShowScanMode, ButtonVariant.Secondary);
            UiKit.Stretch((RectTransform)backToCamera.transform,
                new Vector2(0.055f, 0.015f), new Vector2(0.62f, 0.07f), Vector4.zero);
            backToCamera.transform.Find("Label").GetComponent<Text>().fontStyle = FontStyle.Bold;

            var reset = UiKit.Button(_editorRoot, "Reset", "다시 촬영", _p,
                ResetToSolved, ButtonVariant.Ghost);
            UiKit.Stretch((RectTransform)reset.transform,
                new Vector2(0.64f, 0.015f), new Vector2(0.945f, 0.07f), Vector4.zero);
            reset.transform.Find("Label").GetComponent<Text>().fontStyle = FontStyle.Bold;
        }

        void BuildNet()
        {
            var netRoot = UiKit.Card(_editorRoot, "Net", _p, raised: true);
            UiKit.Stretch(netRoot,
                new Vector2(0.055f, 0.43f), new Vector2(0.945f, 0.825f), Vector4.zero);
            UiKit.AddSoftOutline(netRoot.GetComponent<Image>(), _p.Border, 1f);
            UiKit.AddSoftShadow(netRoot.GetComponent<Image>(), _p.Shadow, 5f);

            _cells = new Image[6 * 9];
            _faceBackgrounds = new Image[6];
            var skin = SkinService.Current;
            foreach (var (face, fc, fr) in NetLayout)
            {
                var faceRoot = UiKit.Panel(netRoot, $"Face_{face}", skin.CubeBody);
                var faceImage = faceRoot.GetComponent<Image>();
                faceImage.sprite = UiKit.RoundedSmall;
                faceImage.type = Image.Type.Sliced;
                _faceBackgrounds[(int)face] = faceImage;

                faceRoot.anchorMin = new Vector2(fc / 4f, 1f - (fr + 1) / 3f);
                faceRoot.anchorMax = new Vector2((fc + 1) / 4f, 1f - fr / 3f);
                faceRoot.offsetMin = new Vector2(5f, 5f);
                faceRoot.offsetMax = new Vector2(-5f, -5f);

                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        int r = row, c = col;
                        var button = UiKit.Button(faceRoot, $"{row}_{col}", "", _p, null);
                        button.onClick.AddListener(() => Paint(face, r, c));
                        button.image.sprite = UiKit.RoundedSmall;
                        button.image.type = Image.Type.Sliced;
                        var rt = (RectTransform)button.transform;
                        rt.anchorMin = new Vector2(col / 3f, 1f - (row + 1) / 3f);
                        rt.anchorMax = new Vector2((col + 1) / 3f, 1f - row / 3f);
                        rt.offsetMin = new Vector2(2f, 2f);
                        rt.offsetMax = new Vector2(-2f, -2f);
                        if (row == 1 && col == 1)
                        {
                            button.interactable = false;
                            var colors = button.colors;
                            colors.disabledColor = Color.white;
                            button.colors = colors;
                        }
                        _cells[(int)face * 9 + row * 3 + col] = button.image;
                    }
            }
        }

        void BuildSwatches()
        {
            var title = UiKit.Label(_editorRoot, "SwatchTitle", "색상 고르기", UiMetrics.SectionTitle,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)title.transform,
                new Vector2(0.06f, 0.397f), new Vector2(0.94f, 0.427f), Vector4.zero);

            var row = UiKit.Panel(_editorRoot, "Swatches", new Color(0, 0, 0, 0));
            UiKit.Stretch(row,
                new Vector2(0.055f, 0.315f), new Vector2(0.945f, 0.39f), Vector4.zero);

            _swatches = new Button[6];
            _swatchLabels = new Text[6];
            _swatchChecks = new Image[6];
            _swatchOutlines = new Outline[6];
            for (int i = 0; i < 6; i++)
            {
                byte color = (byte)i;
                var button = UiKit.Button(row, $"Color{i}", FaceNames[i], _p,
                    () => SelectColor(color), ButtonVariant.Secondary);
                button.image.sprite = UiKit.RoundedTight;
                var rt = (RectTransform)button.transform;
                rt.anchorMin = new Vector2(i / 6f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 6f, 1f);
                rt.offsetMin = new Vector2(4f, 0f);
                rt.offsetMax = new Vector2(-4f, 0f);

                var label = button.transform.Find("Label").GetComponent<Text>();
                label.fontSize = 18;
                label.fontStyle = FontStyle.Bold;
                UiKit.Stretch((RectTransform)label.transform,
                    new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.64f), Vector4.zero);
                var check = UiKit.Icon(button.transform, "SelectedIcon", "check", Color.white);
                UiKit.Stretch((RectTransform)check.transform,
                    new Vector2(0.38f, 0.66f), new Vector2(0.62f, 0.92f), Vector4.zero);
                UiKit.AddSoftOutline(button.image, _p.Border, 1f);

                _swatches[i] = button;
                _swatchLabels[i] = label;
                _swatchChecks[i] = check;
                _swatchOutlines[i] = button.image.GetComponent<Outline>();
            }
        }

        void BuildEditStatus()
        {
            var card = UiKit.Card(_editorRoot, "StatusCard", _p, raised: true);
            UiKit.Stretch(card,
                new Vector2(0.055f, 0.19f), new Vector2(0.945f, 0.295f), Vector4.zero);
            UiKit.AddSoftOutline(card.GetComponent<Image>(), _p.Border, 1f);
            _editStatusOutline = card.GetComponent<Outline>();
            var plate = UiKit.IconPlate(card, "StatusIconPlate", "lock", _p, _p.Accent);
            UiKit.Stretch(plate,
                new Vector2(0.035f, 0.24f), new Vector2(0.145f, 0.76f), Vector4.zero);
            _editStatusIcon = plate.Find("Icon").GetComponent<Image>();

            _editStatusHint = UiKit.Label(card, "StatusHint",
                "가운데 칸은 각 면의 기준색이라 고정되어 있습니다.", 21,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Wrap(_editStatusHint);
            UiKit.Stretch((RectTransform)_editStatusHint.transform,
                new Vector2(0.18f, 0.12f), new Vector2(0.96f, 0.88f), Vector4.zero);
            _editStatus = UiKit.Label(card, "Status", "", 21,
                _p.Warning, TextAnchor.MiddleLeft);
            _editStatus.fontStyle = FontStyle.Bold;
            UiKit.Wrap(_editStatus);
            UiKit.Stretch((RectTransform)_editStatus.transform,
                new Vector2(0.18f, 0.08f), new Vector2(0.96f, 0.92f), Vector4.zero);
        }

        void ShowScanMode()
        {
            _scanMode = true;
            if (_headerTitle != null) _headerTitle.text = "큐브 자동 인식";
            if (_scanRoot != null) _scanRoot.gameObject.SetActive(true);
            if (_editorRoot != null) _editorRoot.gameObject.SetActive(false);
            RefreshScanUi();
            TryStartCamera();
        }

        void ShowEditorMode()
        {
            _scanMode = false;
            StopCamera();
            if (_headerTitle != null) _headerTitle.text = "색상 수정";
            if (_scanRoot != null) _scanRoot.gameObject.SetActive(false);
            if (_editorRoot != null) _editorRoot.gameObject.SetActive(true);
            RefreshCells();
            RefreshSwatches();
            ClearStatus();
        }

        void SelectCaptureSlot(int slot)
        {
            if (slot < 0 || slot >= CaptureOrder.Length) return;
            _captureSlot = slot;
            _liveSamples = null;
            RefreshScanUi();
        }

        void CaptureOrAccept()
        {
            if (CapturedFaceCount == 6)
            {
                if (!TryAccept(out string error))
                {
                    ShowEditorMode();
                    ShowStatusError(error);
                }
                return;
            }
            CaptureCurrentFace();
        }

        void CaptureCurrentFace()
        {
            if (_liveSamples == null || _liveSamples.Length != 9)
            {
                _scanStatusTitle.text = "카메라가 아직 준비되지 않았어요";
                _scanStatusBody.text = "큐브 한 면을 격자 안에 맞춘 뒤 잠시 기다려 주세요.";
                _scanStatusTitle.color = _p.Warning;
                _scanStatusIcon.color = _p.Warning;
                return;
            }

            ApplyScannedFace(CaptureOrder[_captureSlot], _liveSamples);
            int next = NextUncapturedSlot(_captureSlot + 1);
            if (next >= 0) _captureSlot = next;
            RefreshScanUi();
        }

        /// 테스트와 향후 네이티브 카메라 연동에서도 같은 인식 경로를 쓰는 입구다.
        public void ApplyScannedFace(Face face, Color[] samples)
        {
            if (samples == null || samples.Length != 9) return;
            _samplesByFace[(int)face] = (Color[])samples.Clone();
            _captured[(int)face] = true;
            RebuildRecognizedState();
            RefreshCells();
            RefreshScanUi();
        }

        int NextUncapturedSlot(int start)
        {
            for (int offset = 0; offset < CaptureOrder.Length; offset++)
            {
                int slot = (start + offset) % CaptureOrder.Length;
                if (!_captured[(int)CaptureOrder[slot]]) return slot;
            }
            return -1;
        }

        void RebuildRecognizedState()
        {
            var centers = (Color[])SkinService.Current.StickerColors.Clone();
            for (int face = 0; face < 6; face++)
                if (_captured[face] && _samplesByFace[face] != null)
                    centers[face] = _samplesByFace[face][4];
            Current = CubeColorRecognizer.BuildState(_samplesByFace, centers);
        }

        void RefreshScanUi()
        {
            if (_progress == null) return;
            int count = CapturedFaceCount;
            _progress.text = $"{count} / 6면";
            _instruction.text = CaptureInstruction(CaptureOrder[_captureSlot]);

            var skin = SkinService.Current;
            for (int slot = 0; slot < 6; slot++)
            {
                Face face = CaptureOrder[slot];
                bool done = _captured[(int)face];
                for (int cell = 0; cell < 9; cell++)
                {
                    Color color = done
                        ? skin.StickerColors[Current.Facelets[Current.IndexOf(face, cell / 3, cell % 3)]]
                        : _p.SurfaceMuted;
                    _facePreviewCells[slot, cell].color = color;
                }
                _faceOutlines[slot].effectColor = slot == _captureSlot ? _p.Accent : _p.Border;
                _faceOutlines[slot].effectDistance = slot == _captureSlot
                    ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }

            _scanStatusTitle.color = count == 6 ? _p.Success : _p.TextPrimary;
            _scanStatusIcon.color = count == 6 ? _p.Success : _p.Accent;
            if (count == 6)
            {
                float confidence = RecognitionConfidence();
                _scanStatusTitle.text = "색상 인식 완료";
                _scanStatusBody.text = $"54칸을 읽었습니다 · 신뢰도 {Mathf.RoundToInt(confidence * 100f)}%";
                _primaryScanLabel.text = "3D 큐브로 시작";
            }
            else if (count > 0)
            {
                _scanStatusTitle.text = $"{count}개 면을 저장했습니다";
                _scanStatusBody.text = "위의 작은 면을 누르면 다시 촬영할 수 있습니다.";
                _primaryScanLabel.text = _captured[(int)CaptureOrder[_captureSlot]]
                    ? "이 면 다시 촬영" : "다음 면 촬영";
            }
            else
            {
                _scanStatusTitle.text = "촬영을 시작해 주세요";
                _scanStatusBody.text = "안내된 방향을 유지하고 큐브 한 면을 격자에 맞춰 주세요.";
                _primaryScanLabel.text = "다음 면 촬영";
            }
            _editButton.interactable = count > 0;
        }

        float RecognitionConfidence()
        {
            if (CapturedFaceCount != 6) return 0f;
            var centers = new Color[6];
            for (int face = 0; face < 6; face++) centers[face] = _samplesByFace[face][4];
            float sum = 0f;
            int count = 0;
            for (int face = 0; face < 6; face++)
                for (int cell = 0; cell < 9; cell++)
                {
                    sum += CubeColorRecognizer.Confidence(_samplesByFace[face][cell], centers);
                    count++;
                }
            return count > 0 ? sum / count : 0f;
        }

        static string CaptureInstruction(Face face)
        {
            switch (face)
            {
                case Face.U: return "노란 윗면 · 파란 면이 위쪽";
                case Face.F: return "초록 앞면 · 노란 면이 위쪽";
                case Face.R: return "주황 오른쪽 면 · 노란 면이 위쪽";
                case Face.B: return "파란 뒷면 · 노란 면이 위쪽";
                case Face.L: return "빨간 왼쪽 면 · 노란 면이 위쪽";
                default: return "흰 아래면 · 초록 면이 위쪽";
            }
        }

        public void SelectColor(byte color)
        {
            if (color > 5) return;
            SelectedColor = color;
            RefreshSwatches();
        }

        public void Paint(Face face, int row, int col)
        {
            if (row == 1 && col == 1) return;
            Current.Facelets[Current.IndexOf(face, row, col)] = SelectedColor;
            RefreshCells();
            RefreshScanUi();
            ClearStatus();
        }

        public void ResetToSolved()
        {
            Current = CubeState.Solved(3);
            SelectedColor = (byte)Face.U;
            for (int face = 0; face < 6; face++)
            {
                _samplesByFace[face] = null;
                _captured[face] = false;
            }
            _captureSlot = 0;
            _liveSamples = null;
            RefreshCells();
            RefreshSwatches();
            RefreshScanUi();
            ClearStatus();
            ShowScanMode();
        }

        public bool TryAccept(out string error)
        {
            var result = CubeValidator.Validate(Current);
            error = result.Reason;
            if (!result.IsValid)
            {
                ShowStatusError(result.Reason);
                return false;
            }
            ClearStatus();
            StopCamera();
            _onAccept?.Invoke(Current.Clone());
            return true;
        }

        void ClearStatus()
        {
            if (_editStatus == null) return;
            _editStatus.text = "";
            _editStatus.gameObject.SetActive(false);
            _editStatusHint.gameObject.SetActive(true);
            _editStatusIcon.color = _p.Accent;
            _editStatusOutline.effectColor = _p.Border;
            _editStatusOutline.effectDistance = new Vector2(1f, -1f);
        }

        void ShowStatusError(string reason)
        {
            _editStatus.text = $"이대로는 맞출 수 없습니다.\n{reason}";
            _editStatus.gameObject.SetActive(true);
            _editStatusHint.gameObject.SetActive(false);
            _editStatusIcon.color = _p.Warning;
            _editStatusOutline.effectColor = _p.Warning;
            _editStatusOutline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        void RefreshCells()
        {
            if (_cells == null || Current == null) return;
            var skin = SkinService.Current;
            for (int face = 0; face < 6; face++)
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                        _cells[face * 9 + row * 3 + col].color =
                            skin.StickerColors[Current.Get((Face)face, row, col)];
        }

        void RefreshSwatches()
        {
            if (_swatches == null) return;
            var skin = SkinService.Current;
            for (int i = 0; i < 6; i++)
            {
                Color swatch = skin.StickerColors[i];
                _swatches[i].image.color = swatch;
                float luminance = 0.299f * swatch.r + 0.587f * swatch.g + 0.114f * swatch.b;
                Color contrast = luminance > 0.55f ? new Color(0.06f, 0.06f, 0.07f) : Color.white;
                bool selected = i == SelectedColor;
                _swatchLabels[i].color = contrast;
                _swatchLabels[i].text = FaceNames[i];
                _swatchChecks[i].color = contrast;
                _swatchChecks[i].gameObject.SetActive(selected);
                _swatchOutlines[i].effectColor = selected ? contrast : _p.Border;
                _swatchOutlines[i].effectDistance = selected
                    ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }
        }

        void OnSkinChanged(Skin skin)
        {
            if (_faceBackgrounds != null)
                for (int face = 0; face < _faceBackgrounds.Length; face++)
                    if (_faceBackgrounds[face] != null) _faceBackgrounds[face].color = skin.CubeBody;
            RefreshCells();
            RefreshSwatches();
            RefreshScanUi();
        }

        void TryStartCamera()
        {
            if (!_built || !_scanMode || !gameObject.activeInHierarchy || _camera != null) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                _permissionCallbacks = new PermissionCallbacks();
                _permissionCallbacks.PermissionGranted += _ => StartCameraDevice();
                _permissionCallbacks.PermissionDenied += _ => ShowCameraUnavailable(
                    "카메라 권한이 필요합니다. 휴대폰 설정에서 권한을 허용해 주세요.");
                _permissionCallbacks.PermissionDeniedAndDontAskAgain += _ => ShowCameraUnavailable(
                    "카메라 권한이 꺼져 있습니다. 휴대폰 설정에서 직접 허용해 주세요.");
                Permission.RequestUserPermission(Permission.Camera, _permissionCallbacks);
                _cameraMessage.text = "카메라 권한을 확인하고 있습니다…";
                _cameraMessage.gameObject.SetActive(true);
                return;
            }
#endif

            if (!Application.isMobilePlatform)
            {
                ShowCameraUnavailable("카메라 촬영은 연결된 휴대폰에서 사용할 수 있습니다.");
                return;
            }
            StartCameraDevice();
        }

        void StartCameraDevice()
        {
            if (!gameObject.activeInHierarchy || !_scanMode || _camera != null) return;
            var devices = WebCamTexture.devices;
            if (devices == null || devices.Length == 0)
            {
                ShowCameraUnavailable("사용 가능한 카메라를 찾지 못했습니다.");
                return;
            }

            int selected = 0;
            for (int i = 0; i < devices.Length; i++)
                if (!devices[i].isFrontFacing) { selected = i; break; }

            _camera = new WebCamTexture(devices[selected].name, 1280, 720, 30);
            _cameraPreview.texture = _camera;
            _cameraPreview.color = Color.white;
            _camera.Play();
            _cameraMessage.text = "카메라 준비 중…";
            _cameraMessage.gameObject.SetActive(true);
            _nextLiveSampleAt = 0f;
        }

        void ShowCameraUnavailable(string message)
        {
            if (_cameraMessage == null) return;
            _cameraMessage.text = message;
            _cameraMessage.gameObject.SetActive(true);
        }

        void StopCamera()
        {
            if (_camera == null) return;
            if (_camera.isPlaying) _camera.Stop();
            if (_cameraPreview != null) _cameraPreview.texture = null;
            Destroy(_camera);
            _camera = null;
            _liveSamples = null;
            _lastRotation = -1;
        }

        void Update()
        {
            if (_camera == null || !_camera.isPlaying || !_camera.didUpdateThisFrame) return;
            if (_camera.width <= 16 || _camera.height <= 16) return;

            ConfigureCameraPreview();
            if (Time.unscaledTime < _nextLiveSampleAt) return;
            _nextLiveSampleAt = Time.unscaledTime + 0.12f;
            _liveSamples = SampleGrid();
            if (_liveSamples == null) return;

            _cameraMessage.gameObject.SetActive(false);
            for (int i = 0; i < 9; i++)
            {
                _liveCells[i].color = _liveSamples[i];
            }
        }

        void ConfigureCameraPreview()
        {
            int rotation = _camera.videoRotationAngle;
            bool mirrored = _camera.videoVerticallyMirrored;
            if (rotation == _lastRotation && mirrored == _lastMirrored) return;
            _lastRotation = rotation;
            _lastMirrored = mirrored;
            _cameraPreview.rectTransform.localEulerAngles = new Vector3(0f, 0f, -rotation);
            _cameraPreview.uvRect = mirrored ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
        }

        Color[] SampleGrid()
        {
            try
            {
                var samples = new Color[9];
                const float first = 0.30f;
                const float step = 0.20f;
                const float patch = 0.012f;
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        Vector2 previewUv = new Vector2(first + col * step, 1f - (first + row * step));
                        Color sum = Color.black;
                        int count = 0;
                        for (int py = -2; py <= 2; py++)
                            for (int px = -2; px <= 2; px++)
                            {
                                Vector2 uv = previewUv + new Vector2(px * patch, py * patch);
                                uv = CubeColorRecognizer.PreviewToTextureUv(
                                    uv, _camera.videoRotationAngle, _camera.videoVerticallyMirrored);
                                int x = Mathf.Clamp(Mathf.RoundToInt(uv.x * (_camera.width - 1)),
                                    0, _camera.width - 1);
                                int y = Mathf.Clamp(Mathf.RoundToInt(uv.y * (_camera.height - 1)),
                                    0, _camera.height - 1);
                                sum += _camera.GetPixel(x, y);
                                count++;
                            }
                        samples[row * 3 + col] = sum / count;
                    }
                return samples;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[CubeCamera] 색상 표본을 읽지 못했습니다: {exception.Message}");
                return null;
            }
        }
    }
}
