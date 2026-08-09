using System;
using UnityEngine;
using UnityEngine.UI;
using Cube.Core;

namespace Cube.App
{
    /// 실물 큐브의 색을 손으로 넣는 화면.
    ///
    /// 카메라 스캔을 쓰지 않는다 — 조명과 색보정 문제가 앱의 나머지 전부보다 크다.
    /// 전개도에서 칸을 눌러 색을 고른다.
    public sealed class ColorInputScreen : MonoBehaviour
    {
        static readonly (Face face, int col, int row)[] Layout =
        {
            (Face.U, 1, 0),
            (Face.L, 0, 1), (Face.F, 1, 1), (Face.R, 2, 1), (Face.B, 3, 1),
            (Face.D, 1, 2),
        };

        static readonly string[] FaceNames = { "위", "아래", "앞", "뒤", "왼쪽", "오른쪽" };

        public CubeState Current { get; private set; }
        public byte SelectedColor { get; private set; }

        Palette _p;
        Image[] _cells;          // face * 9 + row * 3 + col
        Image[] _faceBackgrounds;
        Button[] _swatches;
        Text[] _swatchLabels;
        Image[] _swatchChecks;
        Outline[] _swatchOutlines;

        Text _status;
        Text _statusHint;
        Image _statusIcon;
        Outline _statusOutline;
        Action<CubeState> _onAccept;

        public void Build(RectTransform parent, Action<CubeState> onAccept, Action onBack)
        {
            _p = ThemeService.Current;
            _onAccept = onAccept;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            Current = CubeState.Solved(3);
            SelectedColor = (byte)Face.U;

            UiKit.ScreenHeader(transform, "실물 큐브 넣기", _p, onBack);
            BuildCoachCard();
            BuildNet();
            BuildSwatches();
            BuildStatusCard();
            BuildActions();

            RefreshCells();
            RefreshSwatches();
            ClearStatus();

            SkinService.Changed -= OnSkinChanged;
            SkinService.Changed += OnSkinChanged;
        }

        void OnDestroy() => SkinService.Changed -= OnSkinChanged;

        void BuildCoachCard()
        {
            var guide = UiKit.Card(transform, "Guide", _p);
            UiKit.Stretch(guide,
                new Vector2(0.055f, 0.815f), new Vector2(0.945f, 0.89f), Vector4.zero);
            UiKit.AddSoftOutline(guide.GetComponent<Image>(), _p.Border, 1f);

            var plate = UiKit.IconPlate(guide, "CoachIcon", "hand-click", _p, _p.Accent);
            UiKit.Stretch(plate,
                new Vector2(0.035f, 0.20f), new Vector2(0.145f, 0.80f), Vector4.zero);

            var title = UiKit.Label(guide, "GuideTitle", "색을 골라 큐브를 칠해요", 23,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)title.transform,
                new Vector2(0.18f, 0.48f), new Vector2(0.96f, 0.90f), Vector4.zero);

            var body = UiKit.Label(guide, "GuideText",
                "아래 색을 선택한 뒤 전개도의 칸을 눌러 주세요.", 18,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Wrap(body);
            UiKit.Stretch((RectTransform)body.transform,
                new Vector2(0.18f, 0.08f), new Vector2(0.96f, 0.52f), Vector4.zero);
        }

        // 이 화면은 앱을 켤 때 한 번만 만들어지고 그 뒤로는 숨었다 보였다만 한다.
        // 다른 화면(스킨 고르기)에서 바꾼 색이 여기 남아 있는 전개도에는
        // 저절로 반영되지 않으므로 직접 구독해서 갱신한다.
        void OnSkinChanged(Skin skin)
        {
            var s = SkinService.Current;
            for (int f = 0; f < Faces.Count; f++)
                if (_faceBackgrounds[f] != null) _faceBackgrounds[f].color = s.CubeBody;
            RefreshCells();
            RefreshSwatches();
        }

        void BuildNet()
        {
            // 화면 비율을 고려해 실제 표시 크기가 약 4:3이 되도록 잡는다.
            // Net/Face_*/row_col 계층은 Paint와 테스트가 쓰던 구조 그대로 유지한다.
            var netRoot = UiKit.Card(transform, "Net", _p, raised: true);
            UiKit.Stretch(netRoot,
                new Vector2(0.055f, 0.425f), new Vector2(0.945f, 0.802f), Vector4.zero);
            UiKit.AddSoftOutline(netRoot.GetComponent<Image>(), _p.Border, 1f);
            UiKit.AddSoftShadow(netRoot.GetComponent<Image>(), _p.Shadow, 5f);

            _cells = new Image[6 * 9];
            _faceBackgrounds = new Image[Faces.Count];
            var skin = SkinService.Current;
            foreach (var (face, fc, fr) in Layout)
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
                        var btn = UiKit.Button(faceRoot, $"{row}_{col}", "", _p, null);
                        btn.onClick.AddListener(() => Paint(face, r, c));
                        btn.image.sprite = UiKit.RoundedSmall;
                        btn.image.type = Image.Type.Sliced;

                        var rt = (RectTransform)btn.transform;
                        rt.anchorMin = new Vector2(col / 3f, 1f - (row + 1) / 3f);
                        rt.anchorMax = new Vector2((col + 1) / 3f, 1f - row / 3f);
                        rt.offsetMin = new Vector2(2f, 2f);
                        rt.offsetMax = new Vector2(-2f, -2f);

                        // 가운데 칸은 색 기준이라 바꿀 수 없다.
                        if (row == 1 && col == 1)
                        {
                            btn.interactable = false;
                            var colors = btn.colors;
                            colors.disabledColor = Color.white;
                            btn.colors = colors;
                        }

                        _cells[(int)face * 9 + row * 3 + col] = btn.image;
                    }
            }
        }

        void BuildSwatches()
        {
            var title = UiKit.Label(transform, "SwatchTitle", "색상 고르기", UiMetrics.SectionTitle,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)title.transform,
                new Vector2(0.06f, 0.392f), new Vector2(0.94f, 0.424f), Vector4.zero);

            var row = UiKit.Panel(transform, "Swatches", new Color(0, 0, 0, 0));
            UiKit.Stretch(row,
                new Vector2(0.055f, 0.315f), new Vector2(0.945f, 0.39f), Vector4.zero);

            _swatches = new Button[6];
            _swatchLabels = new Text[6];
            _swatchChecks = new Image[6];
            _swatchOutlines = new Outline[6];
            for (int i = 0; i < _swatches.Length; i++)
            {
                byte color = (byte)i;
                var btn = UiKit.Button(row, $"Color{i}", FaceNames[i], _p,
                    () => SelectColor(color), ButtonVariant.Secondary);
                btn.image.sprite = UiKit.RoundedTight;

                var rt = (RectTransform)btn.transform;
                rt.anchorMin = new Vector2(i / 6f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 6f, 1f);
                rt.offsetMin = new Vector2(4f, 0f);
                rt.offsetMax = new Vector2(-4f, 0f);

                // Button에는 아이콘도 들어가므로 라벨을 이름으로 직접 잡는다.
                // 하위 계층 전체를 검색하면 구조가 확장될 때 다른 텍스트를 잡을 수 있다.
                var label = btn.transform.Find("Label").GetComponent<Text>();
                label.fontSize = 18;
                label.fontStyle = FontStyle.Bold;
                UiKit.Stretch((RectTransform)label.transform,
                    new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.64f), Vector4.zero);

                var check = UiKit.Icon(btn.transform, "SelectedIcon", "check", Color.white);
                UiKit.Stretch((RectTransform)check.transform,
                    new Vector2(0.38f, 0.66f), new Vector2(0.62f, 0.92f), Vector4.zero);

                UiKit.AddSoftOutline(btn.image, _p.Border, 1f);
                _swatches[i] = btn;
                _swatchLabels[i] = label;
                _swatchChecks[i] = check;
                _swatchOutlines[i] = btn.image.GetComponent<Outline>();
            }
        }

        void BuildStatusCard()
        {
            var card = UiKit.Card(transform, "StatusCard", _p, raised: true);
            UiKit.Stretch(card,
                new Vector2(0.055f, 0.205f), new Vector2(0.945f, 0.295f), Vector4.zero);
            UiKit.AddSoftOutline(card.GetComponent<Image>(), _p.Border, 1f);
            _statusOutline = card.GetComponent<Outline>();

            var plate = UiKit.IconPlate(card, "StatusIconPlate", "lock", _p, _p.Accent);
            UiKit.Stretch(plate,
                new Vector2(0.035f, 0.24f), new Vector2(0.145f, 0.76f), Vector4.zero);
            _statusIcon = plate.Find("Icon").GetComponent<Image>();

            _statusHint = UiKit.Label(card, "StatusHint",
                "가운데 칸은 각 면의 기준색이라 고정되어 있어요.", 21,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Wrap(_statusHint);
            UiKit.Stretch((RectTransform)_statusHint.transform,
                new Vector2(0.18f, 0.12f), new Vector2(0.96f, 0.88f), Vector4.zero);

            _status = UiKit.Label(card, "Status", "", 21,
                _p.Warning, TextAnchor.MiddleLeft);
            _status.fontStyle = FontStyle.Bold;
            _status.lineSpacing = 1.05f;
            UiKit.Wrap(_status);
            UiKit.Stretch((RectTransform)_status.transform,
                new Vector2(0.18f, 0.08f), new Vector2(0.96f, 0.92f), Vector4.zero);
        }

        void BuildActions()
        {
            var accept = UiKit.Button(transform, "Accept", "이 큐브로 시작", _p,
                () => TryAccept(out _), ButtonVariant.Primary);
            UiKit.Stretch((RectTransform)accept.transform,
                new Vector2(0.055f, 0.11f), new Vector2(0.945f, 0.185f), Vector4.zero);
            var acceptLabel = accept.transform.Find("Label").GetComponent<Text>();
            acceptLabel.fontSize = 31;
            acceptLabel.fontStyle = FontStyle.Bold;

            var reset = UiKit.Button(transform, "Reset", "처음부터", _p,
                ResetToSolved, ButtonVariant.Secondary);
            UiKit.Stretch((RectTransform)reset.transform,
                new Vector2(0.055f, 0.035f), new Vector2(0.945f, 0.095f), Vector4.zero);
            reset.transform.Find("Label").GetComponent<Text>().fontStyle = FontStyle.Bold;
        }

        public void SelectColor(byte color)
        {
            if (color > 5) return;
            SelectedColor = color;
            RefreshSwatches();
        }

        /// 칸 하나에 지금 고른 색을 칠한다. 가운데 칸은 바뀌지 않는다.
        public void Paint(Face face, int row, int col)
        {
            if (row == 1 && col == 1) return;
            Current.Facelets[Current.IndexOf(face, row, col)] = SelectedColor;
            RefreshCells();
            ClearStatus();
        }

        public void ResetToSolved()
        {
            Current = CubeState.Solved(3);
            RefreshCells();
            ClearStatus();
        }

        /// 넣은 배치가 실제로 맞출 수 있는 큐브인지 보고, 맞으면 넘긴다.
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
            _onAccept?.Invoke(Current.Clone());
            return true;
        }

        void ClearStatus()
        {
            if (_status == null) return;
            _status.text = "";
            _status.gameObject.SetActive(false);
            _statusHint.gameObject.SetActive(true);
            _statusIcon.color = _p.Accent;
            _statusOutline.effectColor = _p.Border;
            _statusOutline.effectDistance = new Vector2(1f, -1f);
        }

        void ShowStatusError(string reason)
        {
            _status.text = $"이대로는 맞출 수 없습니다.\n{reason}";
            _status.gameObject.SetActive(true);
            _statusHint.gameObject.SetActive(false);
            _statusIcon.color = _p.Warning;
            _statusOutline.effectColor = _p.Warning;
            _statusOutline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        void RefreshCells()
        {
            var skin = SkinService.Current;
            for (int f = 0; f < 6; f++)
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                        _cells[f * 9 + row * 3 + col].color =
                            skin.StickerColors[Current.Get((Face)f, row, col)];
        }

        void RefreshSwatches()
        {
            var skin = SkinService.Current;
            for (int i = 0; i < _swatches.Length; i++)
            {
                var swatch = skin.StickerColors[i];
                _swatches[i].image.color = swatch;

                // 색 위에 글자와 실제 체크 아이콘을 얹으므로 밝기에 따라 대비를 맞춘다.
                float luminance = 0.299f * swatch.r + 0.587f * swatch.g + 0.114f * swatch.b;
                var contrast = luminance > 0.55f
                    ? new Color(0.06f, 0.06f, 0.07f)
                    : Color.white;
                bool selected = i == SelectedColor;

                _swatchLabels[i].color = contrast;
                _swatchLabels[i].text = FaceNames[i];
                _swatchChecks[i].color = contrast;
                _swatchChecks[i].gameObject.SetActive(selected);
                _swatchOutlines[i].effectColor = selected ? contrast : _p.Border;
                _swatchOutlines[i].effectDistance = selected
                    ? new Vector2(2f, -2f)
                    : new Vector2(1f, -1f);
            }
        }
    }
}
