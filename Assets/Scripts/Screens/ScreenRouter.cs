using UnityEngine;
using Cube.Core;

namespace Cube.App
{
    public enum ScreenId { Home, Practice, Records, Settings, Learn, Lesson, Library, ColorInput, Skins }

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
        public SkinScreen Skins { get; private set; }
        public SessionStore Store { get; private set; }

        HomeScreen _home;
        SettingsScreen _settings;
        RectTransform _canvasRect;
        RectTransform _screenRoot;
        ScreenId _skinReturn = ScreenId.Settings;

        public void Build(Canvas canvas, SessionStore store)
        {
            Store = store;
            _canvasRect = (RectTransform)canvas.transform;

            var safe = new GameObject("SafeAreaRoot", typeof(RectTransform));
            safe.transform.SetParent(_canvasRect, false);
            _screenRoot = (RectTransform)safe.transform;
            UiKit.Stretch(_screenRoot, Vector2.zero, Vector2.one, Vector4.zero);
            safe.AddComponent<SafeAreaFitter>();

            _home = new GameObject("HomeScreen").AddComponent<HomeScreen>();
            _home.Build(_screenRoot, StartPractice, () => Go(ScreenId.Learn),
                        () => Go(ScreenId.ColorInput),
                        () => Go(ScreenId.Records), () => OpenSkins(ScreenId.Home),
                        () => Go(ScreenId.Settings));

            Records = new GameObject("RecordsScreen").AddComponent<RecordsScreen>();
            Records.Build(_screenRoot, store, () => Go(ScreenId.Home));

            _settings = new GameObject("SettingsScreen").AddComponent<SettingsScreen>();
            _settings.Build(_screenRoot, () => Go(ScreenId.Home), () => OpenSkins(ScreenId.Settings));

            Learn = new GameObject("LearnScreen").AddComponent<LearnScreen>();
            Learn.Build(_screenRoot, OpenLesson, () => Go(ScreenId.Library), () => Go(ScreenId.Home));

            Lesson = new GameObject("LessonScreen").AddComponent<LessonScreen>();
            Lesson.Build(_screenRoot, () => Go(ScreenId.Learn), OpenLesson);

            Library = new GameObject("AlgorithmScreen").AddComponent<AlgorithmScreen>();
            Library.Build(_screenRoot, () => Go(ScreenId.Learn));

            ColorInput = new GameObject("ColorInputScreen").AddComponent<ColorInputScreen>();
            ColorInput.Build(_screenRoot, AcceptRealCube, () => Go(ScreenId.Home));

            Skins = new GameObject("SkinScreen").AddComponent<SkinScreen>();
            Skins.Build(_screenRoot, () => Go(_skinReturn));

            // 화면을 만드는 도중에 무엇이 터지더라도 전부 겹쳐 보이지는 않게 한다.
            // 실기기에서 셰이더 예외로 Build가 중단됐을 때 정확히 그렇게 됐다.
            HideAll();
            Go(ScreenId.Home);

            ThemeService.Changed -= OnThemeChanged;
            ThemeService.Changed += OnThemeChanged;
        }

        void OnDestroy() { ThemeService.Changed -= OnThemeChanged; }

        /// 테마가 바뀌면 화면을 다시 짓는다.
        ///
        /// 색은 만들 때 한 번 박히므로, 팔레트만 갈아 끼우면 배경만 밝아지고
        /// 버튼은 어두운 채로 남아 글자가 보이지 않는다. 실기기에서 그랬다.
        void OnThemeChanged(Palette p)
        {
            if (_canvasRect == null) return;

            var wasOn = Current;
            var store = Store;

            for (int i = _canvasRect.childCount - 1; i >= 0; i--)
            {
                var child = _canvasRect.GetChild(i).gameObject;
                child.transform.SetParent(null, false);
                Destroy(child);
            }

            Practice = null;
            Build(_canvasRect.GetComponent<Canvas>(), store);

            // 연습·단계 화면의 진행 상태는 저장했지만, 설정에서 테마를 바꾸는
            // 흐름이므로 설정 화면에 남겨 갑작스러운 복원 전환을 피한다.
            Go(wasOn == ScreenId.Practice || wasOn == ScreenId.Lesson ? ScreenId.Settings : wasOn);
        }

        /// 학습 홈에서 단계를 고르면 부른다.
        public void OpenLesson(int stage)
        {
            if (Current == ScreenId.Practice)
            {
                Practice?.SaveProgress();
                Go(ScreenId.Learn);
            }
            else if (Current == ScreenId.Lesson)
            {
                Lesson?.SaveProgress();
                Go(ScreenId.Learn);
            }
            Lesson.Open(stage);
            Go(ScreenId.Lesson);
        }

        void OpenSkins(ScreenId returnTo)
        {
            _skinReturn = returnTo;
            Go(ScreenId.Skins);
        }

        /// 홈에서 부르고, 테스트도 이 입구로 연습 화면을 연다.
        public void StartPractice(int cubeSize)
        {
            if (Current == ScreenId.Lesson)
            {
                Lesson?.SaveProgress();
                Go(ScreenId.Home);
            }
            if (Current == ScreenId.Practice) Practice?.SaveProgress();
            if (Practice != null)
            {
                Practice.Solved -= OnSolved;
                DestroyImmediate(Practice.gameObject);
            }

            Practice = new GameObject("PracticeScreen").AddComponent<PracticeScreen>();
            Practice.Build(_screenRoot, cubeSize, () => Go(ScreenId.Home));
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

        void HideAll()
        {
            foreach (var go in new[]
            {
                _home != null ? _home.gameObject : null,
                Practice != null ? Practice.gameObject : null,
                Records != null ? Records.gameObject : null,
                _settings != null ? _settings.gameObject : null,
                Learn != null ? Learn.gameObject : null,
                Lesson != null ? Lesson.gameObject : null,
                Library != null ? Library.gameObject : null,
                ColorInput != null ? ColorInput.gameObject : null,
                Skins != null ? Skins.gameObject : null,
            })
                if (go != null) go.SetActive(false);
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
            if (Skins != null) Skins.gameObject.SetActive(id == ScreenId.Skins);

            // 큐브는 큐브를 쓰는 화면에서만 보인다. 스킨 고르기도 포함이다 —
            // 큐브 자체가 미리보기라서 뒤에 보여야 고른 게 바로 눈에 들어온다.
            bool cubeVisible = id == ScreenId.Practice || id == ScreenId.Lesson
                             || id == ScreenId.Library || id == ScreenId.Skins;
            if (AppBootstrap.Instance != null && AppBootstrap.Instance.CubeRoot != null)
            {
                var cubeRoot = AppBootstrap.Instance.CubeRoot.gameObject;
                // Unity stops coroutines when CubeRoot is disabled. Finish the shared
                // rotator first so no stale coroutine handle or half-turned pivot can
                // leak into Practice, Lesson, Library, or the Skin preview later.
                if (!cubeVisible && cubeRoot.activeSelf)
                    cubeRoot.GetComponent<LayerRotator>()?.FinishAllImmediately();
                cubeRoot.SetActive(cubeVisible);
                switch (id)
                {
                    case ScreenId.Lesson:
                        // 배우기에서도 일반 연습과 같은 크기와 중심을 쓴다.
                        // 설명 묶음은 LessonScreen에서 화면 하단으로 내린다.
                        AppBootstrap.Instance.SetCubePresentation(1f, 0.04f);
                        break;
                    case ScreenId.Library:
                        AppBootstrap.Instance.SetCubePresentation(0.52f, 0.28f);
                        break;
                    case ScreenId.Skins:
                        // The safe-area preview card is centred around viewport y=0.67. Keep the
                        // shared 3D cube on that same centre instead of floating above it.
                        AppBootstrap.Instance.SetCubePresentation(0.60f, 0.17f);
                        break;
                    default:
                        // 전개도가 기본으로 접혀 스크램블 카드 밑에 얇은 띠만 남으므로,
                        // 큐브는 화면 중앙에 가깝게 둔다. 0.18은 전개도가 가운데 카드로
                        // 자리 잡던 예전 레이아웃 값이라 지금은 너무 위로 밀린다.
                        AppBootstrap.Instance.SetCubePresentation(1f, 0.04f);
                        break;
                }
            }

            if (id == ScreenId.Records) Records.Show(AppSettings.CubeSize);
            if (id == ScreenId.Home) _home.RefreshContinueState();
            if (id == ScreenId.Settings) _settings.RefreshLabels();
            if (id == ScreenId.Learn) Learn.Refresh();   // 진도가 바뀌었을 수 있다
            if (id == ScreenId.Library) Library.ResetCube();
            if (id == ScreenId.Skins) Skins.Refresh();
        }

        void Update()
        {
            // 안드로이드 뒤로가기: 연습·기록·설정에서는 홈으로, 홈에서는 앱을 닫는다.
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (Current == ScreenId.Home) Application.Quit();
            else if (Current == ScreenId.Lesson || Current == ScreenId.Library) Go(ScreenId.Learn);
            else if (Current == ScreenId.Skins) Go(_skinReturn);
            else Go(ScreenId.Home);
        }
    }
}
