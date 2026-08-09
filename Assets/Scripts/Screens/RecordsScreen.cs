using UnityEngine;
using UnityEngine.UI;

namespace Cube.App
{
    /// 크기별 기록 목록과 최고·ao5·ao12를 보여준다.
    public sealed class RecordsScreen : MonoBehaviour
    {
        SessionStore _store;
        Palette _p;

        Text _stats;
        Text _bestValue;
        Text _ao5Value;
        Text _ao12Value;
        Text _listCaption;
        Text _clearWarningText;

        RectTransform _listContent;
        ScrollRect _recordList;
        GameObject _clearWarning;
        Button _clear;
        Button[] _tabs;

        int _size = 3;
        bool _clearArmed;

        // A destructive confirmation must never survive leaving this screen.
        // Otherwise the first tap after returning could delete the session.
        void OnDisable() => _clearArmed = false;

        public void Build(RectTransform parent, SessionStore store, System.Action onBack)
        {
            _store = store;
            _p = ThemeService.Current;
            transform.SetParent(parent, false);

            var root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            UiKit.Stretch(root, Vector2.zero, Vector2.one, Vector4.zero);

            UiKit.ScreenHeader(transform, "기록", _p, onBack);
            BuildSizeSelector();
            BuildMetrics();
            BuildRecordList();
            BuildClearAction();

            Show(AppSettings.CubeSize);
        }

        void BuildSizeSelector()
        {
            var tabs = UiKit.Card(transform, "Tabs", _p);
            UiKit.Stretch(tabs,
                new Vector2(0.055f, 0.825f), new Vector2(0.945f, 0.885f), Vector4.zero);
            UiKit.AddSoftOutline(tabs.GetComponent<Image>(), _p.Border, 1f);

            _tabs = new Button[3];
            for (int i = 0; i < _tabs.Length; i++)
            {
                int size = i + 2;
                var button = UiKit.Button(tabs, $"Tab{size}", $"{size}×{size}", _p,
                    () => Show(size), ButtonVariant.Segment);
                var rt = (RectTransform)button.transform;
                rt.anchorMin = new Vector2(i / 3f, 0f);
                rt.anchorMax = new Vector2((i + 1) / 3f, 1f);
                rt.offsetMin = new Vector2(3f, 3f);
                rt.offsetMax = new Vector2(-3f, -3f);
                button.image.sprite = UiKit.RoundedTight;

                var label = button.transform.Find("Label").GetComponent<Text>();
                label.fontSize = 25;
                label.fontStyle = FontStyle.Bold;
                _tabs[i] = button;
            }
        }

        void BuildMetrics()
        {
            _stats = UiKit.Label(transform, "Stats", "", 22,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            _stats.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)_stats.transform,
                new Vector2(0.06f, 0.79f), new Vector2(0.94f, 0.823f), Vector4.zero);

            _bestValue = MetricCard("BestMetric", "최고", "sparkles", 0.055f, 0.34f);
            _ao5Value = MetricCard("Ao5Metric", "ao5", "clock", 0.357f, 0.642f);
            _ao12Value = MetricCard("Ao12Metric", "ao12", "chart-bar", 0.659f, 0.945f);
        }

        Text MetricCard(string name, string labelText, string iconName, float xMin, float xMax)
        {
            var card = UiKit.Card(transform, name, _p, raised: true);
            UiKit.Stretch(card,
                new Vector2(xMin, 0.685f), new Vector2(xMax, 0.785f), Vector4.zero);
            UiKit.AddSoftOutline(card.GetComponent<Image>(), _p.Border, 0.9f);

            var label = UiKit.Label(card, "Label", labelText, 19,
                _p.TextSecondary, TextAnchor.MiddleLeft);
            label.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)label.transform,
                new Vector2(0.08f, 0.52f), new Vector2(0.70f, 0.90f), Vector4.zero);

            var value = UiKit.Label(card, "Value", "—", 30,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            value.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)value.transform,
                new Vector2(0.08f, 0.10f), new Vector2(0.94f, 0.58f), Vector4.zero);

            var plate = UiKit.IconPlate(card, "IconPlate", iconName, _p, _p.Accent);
            UiKit.Stretch(plate,
                new Vector2(0.72f, 0.58f), new Vector2(0.91f, 0.86f), Vector4.zero);
            return value;
        }

        void BuildRecordList()
        {
            var title = UiKit.Label(transform, "ListTitle", "최근 기록", UiMetrics.SectionTitle,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)title.transform,
                new Vector2(0.06f, 0.64f), new Vector2(0.60f, 0.68f), Vector4.zero);

            _listCaption = UiKit.Label(transform, "ListCaption", "", 20,
                _p.TextSecondary, TextAnchor.MiddleRight);
            UiKit.Stretch((RectTransform)_listCaption.transform,
                new Vector2(0.60f, 0.64f), new Vector2(0.94f, 0.68f), Vector4.zero);

            _recordList = UiKit.ScrollList(transform, "List", _p, out _listContent,
                spacing: 12f, padding: 2f);
            UiKit.Stretch((RectTransform)_recordList.transform,
                new Vector2(0.055f, 0.215f), new Vector2(0.945f, 0.637f), Vector4.zero);
        }

        void BuildClearAction()
        {
            var warning = UiKit.Card(transform, "ClearWarning", _p, raised: true);
            _clearWarning = warning.gameObject;
            UiKit.Stretch(warning,
                new Vector2(0.055f, 0.135f), new Vector2(0.945f, 0.202f), Vector4.zero);
            UiKit.AddSoftOutline(warning.GetComponent<Image>(), _p.Warning, 1.2f);

            var plate = UiKit.IconPlate(warning, "WarningIcon", "lock", _p, _p.Warning);
            UiKit.Stretch(plate,
                new Vector2(0.035f, 0.20f), new Vector2(0.145f, 0.80f), Vector4.zero);

            _clearWarningText = UiKit.Label(warning, "WarningText", "", 20,
                _p.Warning, TextAnchor.MiddleLeft);
            _clearWarningText.fontStyle = FontStyle.Bold;
            UiKit.Wrap(_clearWarningText);
            UiKit.Stretch((RectTransform)_clearWarningText.transform,
                new Vector2(0.18f, 0.10f), new Vector2(0.96f, 0.90f), Vector4.zero);
            _clearWarning.SetActive(false);

            _clear = UiKit.Button(transform, "Clear", "이 세션 기록 지우기", _p,
                ConfirmClear, ButtonVariant.Secondary);
            UiKit.Stretch((RectTransform)_clear.transform,
                new Vector2(0.055f, 0.05f), new Vector2(0.945f, 0.12f), Vector4.zero);
            _clear.transform.Find("Label").GetComponent<Text>().fontStyle = FontStyle.Bold;
        }

        /// 두 번 눌러야 지워진다. 확인 창을 따로 만들지 않고 같은 버튼으로 되묻는다.
        void ConfirmClear()
        {
            if (!_clearArmed)
            {
                _clearArmed = true;
                Show(_size);
                return;
            }

            _store.ClearSession(_size);
            _store.Save();
            _clearArmed = false;
            Show(_size);
        }

        public void Show(int cubeSize)
        {
            if (_size != cubeSize) _clearArmed = false;
            _size = cubeSize;
            _p = ThemeService.Current;

            if (_tabs != null)
                for (int i = 0; i < _tabs.Length; i++)
                    UiKit.StyleButton(_tabs[i], _p,
                        i + 2 == _size ? ButtonVariant.SegmentSelected : ButtonVariant.Segment);

            var records = _store.Records(cubeSize);
            double? best = SessionStats.Best(records);
            double? ao5 = SessionStats.Average(records, 5);
            double? ao12 = SessionStats.Average(records, 12);

            _stats.text = $"{cubeSize}×{cubeSize} · 기록 {records.Count}개";
            _bestValue.text = MetricValue(best);
            _ao5Value.text = MetricValue(ao5);
            _ao12Value.text = MetricValue(ao12);
            _listCaption.text = records.Count == 0
                ? "새 기록을 기다리는 중"
                : $"최근 {Mathf.Min(records.Count, 30)}개";

            _clearWarning.SetActive(_clearArmed);
            if (_clearArmed)
                _clearWarningText.text =
                    $"삭제 준비됨 · 한 번 더 누르면 {cubeSize}×{cubeSize} 기록 {records.Count}개를 모두 지워요.";

            UiKit.StyleButton(_clear, _p,
                _clearArmed ? ButtonVariant.Danger : ButtonVariant.Secondary);
            _clear.transform.Find("Label").GetComponent<Text>().text =
                _clearArmed ? "정말 모두 지우기" : "이 세션 기록 지우기";

            RebuildRecordRows(records);
        }

        static string MetricValue(double? value)
            => value.HasValue ? SessionStats.Format(value.Value) : "—";

        void RebuildRecordRows(System.Collections.Generic.IReadOnlyList<SolveRecord> records)
        {
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                DestroyImmediate(_listContent.GetChild(i).gameObject);

            if (records.Count == 0)
            {
                BuildEmptyState();
            }
            else
            {
                int shown = 0;
                for (int i = records.Count - 1; i >= 0 && shown < 30; i--, shown++)
                    BuildRecordRow(records[i], i + 1);
            }

            _recordList.verticalNormalizedPosition = 1f;
        }

        void BuildEmptyState()
        {
            var card = UiKit.Card(_listContent, "EmptyState", _p, raised: true);
            UiKit.SetLayoutHeight(card, 300f);
            UiKit.AddSoftOutline(card.GetComponent<Image>(), _p.Border, 0.9f);

            var plate = UiKit.IconPlate(card, "EmptyIcon", "chart-bar", _p, _p.Accent);
            UiKit.Stretch(plate,
                new Vector2(0.42f, 0.58f), new Vector2(0.58f, 0.86f), Vector4.zero);

            var title = UiKit.Label(card, "EmptyTitle", "아직 기록이 없어요", 28,
                _p.TextPrimary, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)title.transform,
                new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.58f), Vector4.zero);

            var body = UiKit.Label(card, "EmptyBody",
                "첫 연습을 마치면 최고 기록과 평균이\n여기에 차곡차곡 쌓여요.", 21,
                _p.TextSecondary, TextAnchor.UpperCenter);
            body.lineSpacing = 1.1f;
            UiKit.Wrap(body);
            UiKit.Stretch((RectTransform)body.transform,
                new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.36f), Vector4.zero);
        }

        void BuildRecordRow(SolveRecord record, int recordNumber)
        {
            var row = UiKit.Card(_listContent, $"Record_{recordNumber}", _p);
            UiKit.SetLayoutHeight(row, 94f);
            UiKit.AddSoftOutline(row.GetComponent<Image>(), _p.Border, 0.8f);

            var numberPlate = UiKit.Panel(row, "NumberPlate", _p.SurfaceMuted);
            var numberImage = numberPlate.GetComponent<Image>();
            numberImage.sprite = UiKit.RoundedPill;
            numberImage.type = Image.Type.Sliced;
            UiKit.Stretch(numberPlate,
                new Vector2(0.025f, 0.20f), new Vector2(0.13f, 0.80f), Vector4.zero);

            var number = UiKit.Label(numberPlate, "Number", $"#{recordNumber}", 19,
                _p.Accent, TextAnchor.MiddleCenter);
            number.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)number.transform, Vector2.zero, Vector2.one, Vector4.zero);

            var duration = UiKit.Label(row, "Duration", SessionStats.Format(record.DurationMs), 30,
                _p.TextPrimary, TextAnchor.MiddleLeft);
            duration.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)duration.transform,
                new Vector2(0.17f, 0.12f), new Vector2(0.67f, 0.88f), Vector4.zero);

            var moves = UiKit.Label(row, "Moves", $"{record.Moves}수", 22,
                _p.TextSecondary, TextAnchor.MiddleRight);
            moves.fontStyle = FontStyle.Bold;
            UiKit.Stretch((RectTransform)moves.transform,
                new Vector2(0.68f, 0.12f), new Vector2(0.95f, 0.88f), Vector4.zero);
        }
    }
}
