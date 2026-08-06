using UnityEngine;
using UnityEngine.UI;

namespace Cube.App
{
    /// 크기별 기록 목록과 최고·ao5·ao12를 보여준다.
    public sealed class RecordsScreen : MonoBehaviour
    {
        SessionStore _store;
        Text _stats, _list;
        int _size = 3;
        bool _clearArmed;

        Button[] _tabs;

        public void Build(RectTransform parent, SessionStore store, System.Action onBack)
        {
            _store = store;
            var p = ThemeService.Current;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            var title = UiKit.Label(transform, "Title", "기록", 48, p.TextPrimary, TextAnchor.MiddleLeft);
            UiKit.Stretch((RectTransform)title.transform, new Vector2(0f, 0.90f), new Vector2(1f, 0.97f), new Vector4(48, 0, 48, 0));

            var tabs = UiKit.Panel(transform, "Tabs", new Color(0, 0, 0, 0));
            UiKit.Stretch(tabs, new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.89f), Vector4.zero);
            _tabs = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int size = i + 2;
                var btn = UiKit.Button(tabs, $"Tab{size}", $"{size}×{size}", p, () => Show(size));
                var rt = (RectTransform)btn.transform;
                rt.anchorMin = new Vector2(i / 3f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 3f, 1f);
                rt.offsetMin = new Vector2(4f, 0f);
                rt.offsetMax = new Vector2(-4f, 0f);
                _tabs[i] = btn;
            }

            _stats = UiKit.Label(transform, "Stats", "", 30, p.Accent, TextAnchor.UpperLeft);
            UiKit.Stretch((RectTransform)_stats.transform, new Vector2(0f, 0.71f), new Vector2(1f, 0.81f), new Vector4(48, 0, 48, 0));

            _list = UiKit.Label(transform, "List", "", 26, p.TextSecondary, TextAnchor.UpperLeft);
            UiKit.Stretch((RectTransform)_list.transform, new Vector2(0f, 0.12f), new Vector2(1f, 0.70f), new Vector4(48, 0, 48, 0));

            var clear = UiKit.Button(transform, "Clear", "이 세션 기록 지우기", p, ConfirmClear);
            UiKit.Stretch((RectTransform)clear.transform, new Vector2(0.04f, 0.03f), new Vector2(0.58f, 0.10f), Vector4.zero);

            // 돌아가기가 없어서 안드로이드 뒤로가기 키로만 나갈 수 있었다.
            var back = UiKit.Button(transform, "Back", "돌아가기", p, () => onBack?.Invoke());
            UiKit.Stretch((RectTransform)back.transform, new Vector2(0.62f, 0.03f), new Vector2(0.96f, 0.10f), Vector4.zero);

            Show(AppSettings.CubeSize);
        }

        /// 두 번 눌러야 지워진다. 확인 창을 따로 만들지 않고 같은 버튼으로 되묻는다.
        void ConfirmClear()
        {
            if (!_clearArmed) { _clearArmed = true; Show(_size); return; }
            _store.ClearSession(_size);
            _store.Save();
            _clearArmed = false;
            Show(_size);
        }

        public void Show(int cubeSize)
        {
            if (_size != cubeSize) _clearArmed = false;
            _size = cubeSize;

            var palette = ThemeService.Current;
            if (_tabs != null)
                for (int i = 0; i < _tabs.Length; i++)
                    _tabs[i].image.color = (i + 2 == _size) ? palette.Accent : palette.Surface;

            var records = _store.Records(cubeSize);

            double? best = SessionStats.Best(records);
            double? ao5 = SessionStats.Average(records, 5);
            double? ao12 = SessionStats.Average(records, 12);

            string line = $"기록 {records.Count}개";
            if (best.HasValue) line += $"   최고 {SessionStats.Format(best.Value)}";
            if (ao5.HasValue) line += $"   ao5 {SessionStats.Format(ao5.Value)}";
            if (ao12.HasValue) line += $"   ao12 {SessionStats.Format(ao12.Value)}";
            if (_clearArmed) line += "\n한 번 더 누르면 지워집니다";
            _stats.text = line;

            var sb = new System.Text.StringBuilder();
            for (int i = records.Count - 1; i >= 0 && i > records.Count - 30; i--)
                sb.AppendLine($"{i + 1,3}.  {SessionStats.Format(records[i].DurationMs),8}   {records[i].Moves}수");
            _list.text = records.Count == 0 ? "아직 기록이 없습니다." : sb.ToString();
        }
    }
}
