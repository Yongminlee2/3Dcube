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
        Button[] _swatches;
        Text _status;
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

            var title = UiKit.Label(transform, "Title", "실물 큐브 넣기", 40, _p.TextPrimary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)title.transform, new Vector2(0f, 0.91f), new Vector2(1f, 0.97f), new Vector4(48, 0, 48, 0));

            var guide = UiKit.Label(transform, "Guide",
                "색을 고른 뒤 칸을 누르세요. 가운데 칸은 바꿀 수 없습니다.", 24, _p.TextSecondary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)guide.transform, new Vector2(0f, 0.865f), new Vector2(1f, 0.905f), new Vector4(48, 0, 48, 0));

            BuildNet();
            BuildSwatches();

            _status = UiKit.Label(transform, "Status", "", 26, _p.Accent, TextAnchor.UpperLeft);
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Stretch((RectTransform)_status.transform, new Vector2(0f, 0.13f), new Vector2(1f, 0.23f), new Vector4(48, 0, 48, 0));

            var accept = UiKit.Button(transform, "Accept", "이 큐브로 시작", _p, () => TryAccept(out _));
            UiKit.Stretch((RectTransform)accept.transform, new Vector2(0.06f, 0.015f), new Vector2(0.48f, 0.085f), Vector4.zero);

            var reset = UiKit.Button(transform, "Reset", "처음부터", _p, ResetToSolved);
            UiKit.Stretch((RectTransform)reset.transform, new Vector2(0.52f, 0.015f), new Vector2(0.72f, 0.085f), Vector4.zero);

            var back = UiKit.Button(transform, "Back", "돌아가기", _p, () => onBack?.Invoke());
            UiKit.Stretch((RectTransform)back.transform, new Vector2(0.76f, 0.015f), new Vector2(0.94f, 0.085f), Vector4.zero);

            RefreshCells();
            RefreshSwatches();
        }

        void BuildNet()
        {
            var netRoot = UiKit.Panel(transform, "Net", new Color(0, 0, 0, 0));
            UiKit.Stretch(netRoot, new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.855f), Vector4.zero);

            _cells = new Image[6 * 9];
            foreach (var (face, fc, fr) in Layout)
            {
                var faceRoot = UiKit.Panel(netRoot, $"Face_{face}", _p.CubeBody);
                faceRoot.anchorMin = new Vector2(fc / 4f, 1f - (fr + 1) / 3f);
                faceRoot.anchorMax = new Vector2((fc + 1) / 4f, 1f - fr / 3f);
                faceRoot.offsetMin = new Vector2(3f, 3f);
                faceRoot.offsetMax = new Vector2(-3f, -3f);

                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        int r = row, c = col;
                        var btn = UiKit.Button(faceRoot, $"{row}_{col}", "", _p, null);
                        btn.onClick.AddListener(() => Paint(face, r, c));

                        var rt = (RectTransform)btn.transform;
                        rt.anchorMin = new Vector2(col / 3f, 1f - (row + 1) / 3f);
                        rt.anchorMax = new Vector2((col + 1) / 3f, 1f - row / 3f);
                        rt.offsetMin = new Vector2(1f, 1f);
                        rt.offsetMax = new Vector2(-1f, -1f);

                        // 가운데 칸은 색 기준이라 바꿀 수 없다.
                        if (row == 1 && col == 1) btn.interactable = false;

                        _cells[(int)face * 9 + row * 3 + col] = btn.image;
                    }
            }
        }

        void BuildSwatches()
        {
            var row = UiKit.Panel(transform, "Swatches", new Color(0, 0, 0, 0));
            UiKit.Stretch(row, new Vector2(0.05f, 0.245f), new Vector2(0.95f, 0.325f), Vector4.zero);

            _swatches = new Button[6];
            for (int i = 0; i < 6; i++)
            {
                byte color = (byte)i;
                var btn = UiKit.Button(row, $"Color{i}", FaceNames[i], _p, () => SelectColor(color));
                btn.GetComponentInChildren<Text>().fontSize = 20;
                var rt = (RectTransform)btn.transform;
                rt.anchorMin = new Vector2(i / 6f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 6f, 1f);
                rt.offsetMin = new Vector2(3f, 0f);
                rt.offsetMax = new Vector2(-3f, 0f);
                _swatches[i] = btn;
            }
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
            if (_status != null) _status.text = "";
        }

        public void ResetToSolved()
        {
            Current = CubeState.Solved(3);
            RefreshCells();
            if (_status != null) _status.text = "";
        }

        /// 넣은 배치가 실제로 맞출 수 있는 큐브인지 보고, 맞으면 넘긴다.
        public bool TryAccept(out string error)
        {
            var result = CubeValidator.Validate(Current);
            error = result.Reason;

            if (!result.IsValid)
            {
                if (_status != null) _status.text = $"이대로는 맞출 수 없습니다.\n{result.Reason}";
                return false;
            }

            if (_status != null) _status.text = "";
            _onAccept?.Invoke(Current.Clone());
            return true;
        }

        void RefreshCells()
        {
            var p = ThemeService.Current;
            for (int f = 0; f < 6; f++)
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                        _cells[f * 9 + row * 3 + col].color =
                            p.StickerColors[Current.Get((Face)f, row, col)];
        }

        void RefreshSwatches()
        {
            var p = ThemeService.Current;
            for (int i = 0; i < _swatches.Length; i++)
            {
                _swatches[i].image.color = p.StickerColors[i];
                var label = _swatches[i].GetComponentInChildren<Text>();
                label.color = i == SelectedColor ? p.TextPrimary : p.CubeBody;
                label.text = i == SelectedColor ? $"◉ {FaceNames[i]}" : FaceNames[i];
            }
        }
    }
}
