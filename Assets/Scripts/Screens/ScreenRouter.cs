using UnityEngine;

namespace Cube.App
{
    public enum ScreenId { Home, Practice, Records, Settings }

    /// 씬을 새로 읽지 않고 패널만 켜고 끈다. 큐브를 다시 만들지 않아도 되고 전환이 즉각적이다.
    public sealed class ScreenRouter : MonoBehaviour
    {
        public ScreenId Current { get; private set; } = ScreenId.Home;
        public PracticeScreen Practice { get; private set; }
        public RecordsScreen Records { get; private set; }
        public SessionStore Store { get; private set; }

        HomeScreen _home;
        SettingsScreen _settings;
        RectTransform _canvasRect;

        public void Build(Canvas canvas, SessionStore store)
        {
            Store = store;
            _canvasRect = (RectTransform)canvas.transform;

            _home = new GameObject("HomeScreen").AddComponent<HomeScreen>();
            _home.Build(_canvasRect, StartPractice, () => Go(ScreenId.Records), () => Go(ScreenId.Settings));

            Records = new GameObject("RecordsScreen").AddComponent<RecordsScreen>();
            Records.Build(_canvasRect, store);

            _settings = new GameObject("SettingsScreen").AddComponent<SettingsScreen>();
            _settings.Build(_canvasRect, () => Go(ScreenId.Home));

            Go(ScreenId.Home);
        }

        /// 홈에서 부르고, 테스트도 이 입구로 연습 화면을 연다.
        public void StartPractice(int cubeSize)
        {
            if (Practice != null)
            {
                Practice.Solved -= OnSolved;
                DestroyImmediate(Practice.gameObject);
            }

            Practice = new GameObject("PracticeScreen").AddComponent<PracticeScreen>();
            Practice.Build(_canvasRect, cubeSize);
            Practice.Solved += OnSolved;
            Go(ScreenId.Practice);
        }

        void OnSolved(double ms, string scramble, int moves)
        {
            Store.Add(AppSettings.CubeSize, new SolveRecord
            {
                UnixMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                DurationMs = ms,
                Scramble = scramble,
                Moves = moves,
            });
            Store.Save();
        }

        public void Go(ScreenId id)
        {
            Current = id;
            if (_home != null) _home.gameObject.SetActive(id == ScreenId.Home);
            if (Practice != null) Practice.gameObject.SetActive(id == ScreenId.Practice);
            if (Records != null) Records.gameObject.SetActive(id == ScreenId.Records);
            if (_settings != null) _settings.gameObject.SetActive(id == ScreenId.Settings);

            // 큐브는 연습 화면에서만 보인다.
            if (AppBootstrap.Instance != null && AppBootstrap.Instance.CubeRoot != null)
                AppBootstrap.Instance.CubeRoot.gameObject.SetActive(id == ScreenId.Practice);

            if (id == ScreenId.Records) Records.Show(AppSettings.CubeSize);
            if (id == ScreenId.Settings) _settings.RefreshLabels();
        }

        void Update()
        {
            // 안드로이드 뒤로가기: 연습·기록·설정에서는 홈으로, 홈에서는 앱을 닫는다.
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (Current == ScreenId.Home) Application.Quit();
            else Go(ScreenId.Home);
        }
    }
}
