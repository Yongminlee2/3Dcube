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
            LiftCube();
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

            // 카메라 rect로 위쪽만 그리게 하면 안 된다. 그러면 나머지 영역이
            // 아예 지워지지 않아 이전 화면이 유령처럼 남는다. 실기기에서 정확히 그랬다.
            // 화면 전체를 쓰고, 대신 큐브를 위로 띄워 아래를 UI에 내준다.
            cam.transform.position = new Vector3(0f, 0f, -CameraDistance(cam));
            cam.transform.rotation = Quaternion.identity;
            go.tag = "MainCamera";
            return cam;
        }

        /// 3칸 큐브의 대각선에 여유를 더한 값.
        const float CubeHalfExtent = 2.7f;

        /// 큐브 중심을 화면 중앙에서 위로 얼마나 올릴지 (보이는 높이에 대한 비율).
        const float CubeLiftRatio = 0.22f;

        static float ScreenAspect()
            => Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;

        /// 세로 화면에서는 가로로 보이는 폭이 세로보다 훨씬 좁다.
        /// 시야각은 세로 기준이라, 거리를 세로만 보고 잡으면 큐브가 좌우로 잘린다.
        static float CameraDistance(Camera cam)
        {
            float halfV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfH = halfV * ScreenAspect();
            float tighter = Mathf.Max(0.01f, Mathf.Min(halfV, halfH));
            return CubeHalfExtent / tighter;
        }

        /// 큐브를 화면 위쪽으로 올린다. 아래쪽은 전개도와 버튼이 쓴다.
        void LiftCube()
        {
            float halfV = Mathf.Tan(CubeCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float visibleHalfHeight = CameraDistance(CubeCamera) * halfV;
            CubeRoot.localPosition = new Vector3(0f, visibleHalfHeight * CubeLiftRatio * 2f, 0f);
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
