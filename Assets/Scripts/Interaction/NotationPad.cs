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
        Button _primeButton, _wideButton;
        Palette _palette;

        public bool Prime { get; set; }
        public bool Wide { get; set; }

        public void Build(RectTransform parent, int n, Palette p, Action<Move> onMove)
        {
            _n = n; _onMove = onMove; _palette = p;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            // 4칸 큐브는 안쪽 층 버튼이 한 줄 더 붙는다.
            int columns = Letters.Length;
            int rows = n >= 4 ? 2 : 1;

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                {
                    int depth = r + 1;                  // 1 = 바깥층, 2 = 안쪽 층
                    string label = depth == 1 ? Letters[c] : "2" + Letters[c];
                    var btn = UiKit.Button(transform, $"Pad_{label}", label, p, null);
                    string captured = label;
                    btn.onClick.AddListener(() => Fire(captured));

                    var rt = (RectTransform)btn.transform;
                    rt.anchorMin = new Vector2(c / (float)columns, 1f - (r + 1) / (float)rows);
                    rt.anchorMax = new Vector2((c + 1) / (float)columns, 1f - r / (float)rows);
                    rt.offsetMin = new Vector2(3f, 3f);
                    rt.offsetMax = new Vector2(-3f, -3f);
                }

            _primeButton = UiKit.Button(transform, "Pad_Prime", "'", p, TogglePrime);
            _wideButton = UiKit.Button(transform, "Pad_Wide", "w", p, ToggleWide);
            PlaceToggle((RectTransform)_primeButton.transform, 0f);
            PlaceToggle((RectTransform)_wideButton.transform, 0.5f);
            _wideButton.gameObject.SetActive(n >= 4);
            RefreshToggleColors();
        }

        void PlaceToggle(RectTransform rt, float xMin)
        {
            rt.anchorMin = new Vector2(xMin, -0.85f);
            rt.anchorMax = new Vector2(xMin + 0.5f, -0.05f);
            rt.offsetMin = new Vector2(3f, 3f);
            rt.offsetMax = new Vector2(-3f, -3f);
        }

        void TogglePrime() { Prime = !Prime; RefreshToggleColors(); }
        void ToggleWide() { Wide = !Wide; RefreshToggleColors(); }

        void RefreshToggleColors()
        {
            if (_primeButton != null)
                _primeButton.image.color = Prime ? _palette.Accent : _palette.Surface;
            if (_wideButton != null)
                _wideButton.image.color = Wide ? _palette.Accent : _palette.Surface;
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

            foreach (var m in MoveNotation.ParseToken(token, _n)) _onMove?.Invoke(m);

            if (Prime) { Prime = false; RefreshToggleColors(); }   // 반시계는 한 번만 걸린다
        }
    }
}
