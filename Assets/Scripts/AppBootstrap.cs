using UnityEngine;

namespace Cube.App
{
    /// 씬에 있는 유일한 컴포넌트. 카메라·조명·캔버스·큐브 뿌리를 코드로 세운다.
    public sealed class AppBootstrap : MonoBehaviour
    {
        public static AppBootstrap Instance { get; private set; }

        /// 테스트가 진짜 저장 경로를 건드리지 않게 하는 통로. 평소에는 null이다.
        public static string StorePathOverride;

        public Camera CubeCamera { get; private set; }
        public Canvas UiCanvas { get; private set; }
        public Transform CubeRoot { get; private set; }
        public SessionStore Store { get; private set; }
        public ScreenRouter Router { get; private set; }

        void Awake()
        {
            Instance = this;
            ThemeService.Init();

            CubeCamera = BuildCamera();
            BuildLight();
            CubeRoot = new GameObject("CubeRoot").transform;
            CubeRoot.SetParent(transform, false);
            UiCanvas = BuildCanvas();

            ThemeService.Changed += OnThemeChanged;
            OnThemeChanged(ThemeService.Current);

            Store = new SessionStore(StorePathOverride);
            Store.Load();
            Router = gameObject.AddComponent<ScreenRouter>();
            Router.Build(UiCanvas, Store);
        }

        void OnDestroy()
        {
            ThemeService.Changed -= OnThemeChanged;
            if (Instance == this) Instance = null;
        }

        void OnThemeChanged(Palette p)
        {
            if (CubeCamera != null && p != null) CubeCamera.backgroundColor = p.Background;
        }

        Camera BuildCamera()
        {
            var go = new GameObject("CubeCamera");
            go.transform.SetParent(transform, false);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.orthographic = false;
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(0f, 0f, -8f);
            cam.transform.LookAt(Vector3.zero);
            go.tag = "MainCamera";
            return cam;
        }

        void BuildLight()
        {
            var go = new GameObject("KeyLight");
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(38f, -34f, 0f);
        }

        Canvas BuildCanvas()
        {
            var go = new GameObject("UiCanvas");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;   // 세로 고정이므로 높이에 맞춘다

            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            return canvas;
        }
    }
}
