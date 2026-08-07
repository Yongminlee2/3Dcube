using UnityEngine;
using UnityEngine.UI;
using Cube.Core;

namespace Cube.App
{
    /// 6면을 2D 십자 전개도로 그린다. 배치는 L·F·R·B 가로줄에 U가 위, D가 아래로 고정이다.
    public sealed class NetView : MonoBehaviour
    {
        // 전개도에서 각 면이 놓이는 칸 (열, 행)
        static readonly (Face face, int col, int row)[] Layout =
        {
            (Face.U, 1, 0),
            (Face.L, 0, 1), (Face.F, 1, 1), (Face.R, 2, 1), (Face.B, 3, 1),
            (Face.D, 1, 2),
        };

        const int Cols = 4, Rows = 3;

        Image[] _cells;
        bool[] _highlighted;
        int _n;

        public bool Expanded
        {
            get => gameObject.activeSelf;
            set => gameObject.SetActive(value);
        }

        public Image CellAt(Face f, int row, int col) => _cells[((int)f * _n + row) * _n + col];

        public void Build(int n)
        {
            if (GetComponent<RectTransform>() == null) gameObject.AddComponent<RectTransform>();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            _n = n;
            _cells = new Image[Faces.Count * n * n];
            _highlighted = new bool[_cells.Length];

            var p = ThemeService.Current;

            foreach (var (face, fc, fr) in Layout)
            {
                var faceRoot = UiKit.Panel(transform, $"Face_{face}", p.CubeBody);
                // 전개도 전체를 4x3 격자로 나누고 그 한 칸을 이 면이 차지한다.
                faceRoot.anchorMin = new Vector2(fc / (float)Cols, 1f - (fr + 1) / (float)Rows);
                faceRoot.anchorMax = new Vector2((fc + 1) / (float)Cols, 1f - fr / (float)Rows);
                faceRoot.offsetMin = new Vector2(3f, 3f);
                faceRoot.offsetMax = new Vector2(-3f, -3f);

                for (int row = 0; row < n; row++)
                    for (int col = 0; col < n; col++)
                    {
                        var cell = UiKit.Cell(faceRoot, $"{row}_{col}", p.StickerColors[(int)face]);
                        cell.sprite = UiKit.RoundedSmall; cell.type = Image.Type.Sliced;
                        var crt = (RectTransform)cell.transform;
                        crt.anchorMin = new Vector2(col / (float)n, 1f - (row + 1) / (float)n);
                        crt.anchorMax = new Vector2((col + 1) / (float)n, 1f - row / (float)n);
                        crt.offsetMin = new Vector2(1f, 1f);
                        crt.offsetMax = new Vector2(-1f, -1f);
                        _cells[((int)face * n + row) * n + col] = cell;
                    }
            }
        }

        /// 상태를 읽어 칸 색을 갱신한다. 강조가 걸린 칸은 건드리지 않는다.
        public void Refresh(CubeState state)
        {
            if (_cells == null || state == null || state.N != _n) return;
            var p = ThemeService.Current;
            for (int f = 0; f < Faces.Count; f++)
                for (int row = 0; row < _n; row++)
                    for (int col = 0; col < _n; col++)
                    {
                        int i = (f * _n + row) * _n + col;
                        if (_highlighted[i]) continue;
                        _cells[i].color = p.StickerColors[state.Get((Face)f, row, col)];
                    }
        }

        /// color가 null이면 강조를 푼다. Phase C의 힌트가 쓸 자리다.
        public void SetHighlight(Face f, int row, int col, Color? color)
        {
            int i = ((int)f * _n + row) * _n + col;
            _highlighted[i] = color.HasValue;
            if (color.HasValue) _cells[i].color = color.Value;
        }

        public void ClearHighlights()
        {
            if (_highlighted == null) return;
            for (int i = 0; i < _highlighted.Length; i++) _highlighted[i] = false;
        }
    }
}
