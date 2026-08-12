using System;
using UnityEngine;
using UnityEngine.UI;
using Cube.Core;

namespace Cube.App
{
    /// U/D/L/R/F/B 버튼. 스와이프와 같은 Move를 만들어 같은 경로로 흘려보낸다.
    public sealed class NotationPad : MonoBehaviour
    {
        static readonly string[] Letters = { "U", "D", "L", "R", "F", "B" };

        int _n;
        Action<Move> _onMove;
        Button _primeButton, _doubleButton, _wideButton;
        Palette _palette;

        public bool Prime { get; set; }
        public bool Double { get; set; }
        public bool Wide { get; set; }

        /// **자기 자신의 RectTransform 안에** 버튼을 깐다.
        /// 부모로 옮기거나 자기 앵커를 건드리지 않는다 — 이 컴포넌트는 대개
        /// 이미 자리를 잡아 둔 오브젝트에 붙기 때문이다. 예전에 여기서
        /// 앵커를 전체 화면으로 덮어써서 패드가 화면을 통째로 가렸다.
        public void Build(RectTransform area, int n, Palette p, Action<Move> onMove)
        {
            _n = n; _onMove = onMove; _palette = p;

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            if (area != null && area != root) { transform.SetParent(area, false); UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero); }

            int columns = Letters.Length;
            int faceRows = n >= 4 ? 2 : 1;     // 4칸 큐브는 안쪽 층 버튼이 한 줄 더 붙는다
            int rows = faceRows + 1;           // 마지막 줄은 반시계·2회·넓은 수 토글

            for (int r = 0; r < faceRows; r++)
                for (int c = 0; c < columns; c++)
                {
                    string label = r == 0 ? Letters[c] : "2" + Letters[c];
                    var btn = UiKit.Button(transform, $"Pad_{label}", label, p, null, ButtonVariant.Segment);
                    string captured = label;
                    btn.onClick.AddListener(() => Fire(captured));
                    PlaceCell((RectTransform)btn.transform, c, c + 1, columns, r, rows);
                }

            _primeButton = UiKit.Button(transform, "Pad_Prime", "반시계", p, TogglePrime, ButtonVariant.Segment);
            _doubleButton = UiKit.Button(transform, "Pad_Double", "2회", p, ToggleDouble, ButtonVariant.Segment);
            _wideButton = UiKit.Button(transform, "Pad_Wide", "넓은 수", p, ToggleWide, ButtonVariant.Segment);
            PlaceCell((RectTransform)_primeButton.transform, 0, 2, columns, faceRows, rows);
            PlaceCell((RectTransform)_doubleButton.transform, 2, 4, columns, faceRows, rows);
            PlaceCell((RectTransform)_wideButton.transform, 4, 6, columns, faceRows, rows);
            RefreshToggleColors();
        }

        static void PlaceCell(RectTransform rt, int colFrom, int colTo, int columns, int row, int rows)
        {
            rt.anchorMin = new Vector2(colFrom / (float)columns, 1f - (row + 1) / (float)rows);
            rt.anchorMax = new Vector2(colTo / (float)columns, 1f - row / (float)rows);
            rt.offsetMin = new Vector2(3f, 3f);
            rt.offsetMax = new Vector2(-3f, -3f);
        }

        void TogglePrime()
        {
            Prime = !Prime;
            if (Prime) Double = false;
            RefreshToggleColors();
        }

        void ToggleDouble()
        {
            Double = !Double;
            if (Double) Prime = false;
            RefreshToggleColors();
        }

        void ToggleWide() { Wide = !Wide; RefreshToggleColors(); }

        void RefreshToggleColors()
        {
            if (_primeButton != null)
                UiKit.StyleButton(_primeButton, _palette,
                    Prime ? ButtonVariant.SegmentSelected : ButtonVariant.Segment);
            if (_doubleButton != null)
                UiKit.StyleButton(_doubleButton, _palette,
                    Double ? ButtonVariant.SegmentSelected : ButtonVariant.Segment);
            if (_wideButton != null)
                UiKit.StyleButton(_wideButton, _palette,
                    Wide ? ButtonVariant.SegmentSelected : ButtonVariant.Segment);
        }

        /// 테스트와 화면 양쪽에서 쓰는 입구. 버튼 하나를 누른 것과 같다.
        public void Press(string label)
        {
            Fire(label);
        }

        void Fire(string label)
        {
            string token = label;
            if (Wide && !label.StartsWith("2")) token += "w";
            if (Prime) token += "'";
            else if (Double) token += "2";

            foreach (var m in MoveNotation.ParseToken(token, _n)) _onMove?.Invoke(m);

            if (Prime || Double)
            {
                Prime = false;
                Double = false;
                RefreshToggleColors();
            }
        }
    }
}
