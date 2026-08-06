using UnityEngine;
using Cube.Core;

namespace Cube.App
{
    public enum ScreenId { Home, Practice, Records, Settings, Learn, Lesson, Library, ColorInput }

    /// 씬을 새로 읽지 않고 패널만 켜고 끈다. 큐브를 다시 만들지 않아도 되고 전환이 즉각적이다.
    public sealed class ScreenRouter : MonoBehaviour
    {
        public ScreenId Current { get; private set; } = ScreenId.Home;
        public PracticeScreen Practice { get; private set; }
        public RecordsScreen Records { get; private set; }
        public LearnScreen Learn { get; private set; }
        public LessonScreen Lesson { get; private set; }
        public AlgorithmScreen Library { get; private set; }
        public ColorInputScreen ColorInput { get; private set; }
        public SessionStore Store { get; private set; }

        HomeScreen _home;
        SettingsScreen _settings;
        RectTransform _canvasRect;

        public void Build(Canvas canvas, SessionStore store)
        {
            Store = store;
            _canvasRect = (RectTransform)canvas.transform;

            _home = new GameObject("HomeScreen").AddComponent<HomeScreen>();
            _home.Build(_canvasRect, StartPractice, () => Go(ScreenId.Learn),
                        () => Go(ScreenId.ColorInput),
                        () => Go(ScreenId.Records), () => Go(ScreenId.Settings));

            Records = new GameObject("RecordsScreen").AddComponent<RecordsScreen>();
            Records.Build(_canvasRect, store);

            _settings = new GameObject("SettingsScreen").AddComponent<SettingsScreen>();
            _settings.Build(_canvasRect, () => Go(ScreenId.Home));

            Learn = new GameObject("LearnScreen").AddComponent<LearnScreen>();
            Learn.Build(_canvasRect, OpenLesson, () => Go(ScreenId.Library), () => Go(ScreenId.Home));

            Lesson = new GameObject("LessonScreen").AddComponent<LessonScreen>();
            Lesson.Build(_canvasRect, () => Go(ScreenId.Learn));

            Library = new GameObject("AlgorithmScreen").AddComponent<AlgorithmScreen>();
            Library.Build(_canvasRect, () => Go(ScreenId.Learn));

            ColorInput = new GameObject("ColorInputScreen").AddComponent<ColorInputScreen>();
            ColorInput.Build(_canvasRect, AcceptRealCube, () => Go(ScreenId.Home));

            Go(ScreenId.Home);
        }

        /// 학습 홈에서 단계를 고르면 부른다.
        public void OpenLesson(int stage)
        {
            Lesson.Open(stage);
            Go(ScreenId.Lesson);
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

        /// 손으로 넣은 실물 큐브를 연습 화면에 실어 준다. 거기서 힌트를 받을 수 있다.
        void AcceptRealCube(CubeState state)
        {
            AppSettings.CubeSize = 3;
            StartPractice(3);
            Practice.LoadState(state);
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
            if (Learn != null) Learn.gameObject.SetActive(id == ScreenId.Learn);
            if (Lesson != null) Lesson.gameObject.SetActive(id == ScreenId.Lesson);
            if (Library != null) Library.gameObject.SetActive(id == ScreenId.Library);
            if (ColorInput != null) ColorInput.gameObject.SetActive(id == ScreenId.ColorInput);

            // 큐브는 큐브를 쓰는 화면에서만 보인다.
            bool cubeVisible = id == ScreenId.Practice || id == ScreenId.Lesson || id == ScreenId.Library;
            if (AppBootstrap.Instance != null && AppBootstrap.Instance.CubeRoot != null)
                AppBootstrap.Instance.CubeRoot.gameObject.SetActive(cubeVisible);

            if (id == ScreenId.Records) Records.Show(AppSettings.CubeSize);
            if (id == ScreenId.Settings) _settings.RefreshLabels();
            if (id == ScreenId.Learn) Learn.Refresh();   // 진도가 바뀌었을 수 있다
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
