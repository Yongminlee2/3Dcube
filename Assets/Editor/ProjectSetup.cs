using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CubeEditor
{
    public static class ProjectSetup
    {
        // -executeMethod CubeEditor.ProjectSetup.Configure 로 호출한다.
        public static void Configure()
        {
            ConfigureRendering();
            ConfigurePlayer();
            ConfigureAndroidTools();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectSetup] 완료");
        }

        // -executeMethod CubeEditor.ProjectSetup.CreateAssets 로 호출한다.
        public static void CreateAssets()
        {
            CreatePalettes();
            CreateTouchSettings();
            CreateCubieMaterial();
            CreateScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectSetup] 팔레트와 씬 생성 완료");
        }

        static void ConfigureRendering()
        {
            Directory.CreateDirectory("Assets/Settings");
            const string rendererPath = "Assets/Settings/UniversalRenderer.asset";
            const string pipelinePath = "Assets/Settings/UniversalRenderPipelineAsset.asset";

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, rendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "ymlee";

            // 폰 홈 화면과 스토어에 뜨는 이름.
            PlayerSettings.productName = "큐브 연습장";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.ymlee.cube");
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Unity 6.3이 지원하는 최소값이 25다. 24는 폐기 예정 경고가 난다.
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        }

        /// 이 PC의 Unity는 C:\Users\사용자\... 에 깔려 있고, 그 한글 경로가
        /// 안드로이드 빌드를 두 군데서 깨뜨린다.
        ///
        /// 1) IL2CPP의 clang은 자기 실행 파일 경로에서 내장 헤더 폴더를 찾는데
        ///    한글이 섞이면 그 계산이 실패한다. stddef.h를 못 찾아 NDK 시스템 헤더가
        ///    전부 "unknown type name 'size_t'"로 무너진다.
        /// 2) Gradle이 만드는 prefab_command.bat은 UTF-8로 쓰이는데 cmd는
        ///    시스템 코드페이지로 읽는다. 한글이 깨져 JDK 경로를 UNC로 오인하고
        ///    "네트워크 경로를 찾지 못했습니다"로 죽는다.
        ///
        /// 그래서 도구 경로를 전부 ASCII 정션으로 가리킨다.
        /// 정션은 아래 명령으로 만든다 (PowerShell):
        ///   New-Item -ItemType Junction -Path C:\workAndroid\ndk-ascii -Target "<Unity>\PlaybackEngines\AndroidPlayer\NDK"
        ///
        /// 임시 폴더(TEMP)와 Gradle 홈도 ASCII여야 하는데 그건 빌드를 띄우는 쪽에서
        /// 환경 변수로 넘긴다. BuildScript.BuildApk 주석을 볼 것.
        static readonly (string label, string path, System.Action<string> apply)[] AsciiTools =
        {
            ("NDK",    @"C:\workAndroid\ndk-ascii",
                p => UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = p),
            ("SDK",    @"C:\workAndroid\sdk-ascii",
                p => UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = p),
            ("JDK",    @"C:\workAndroid\jdk-ascii",
                p => UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath = p),
            ("Gradle", @"C:\workAndroid\gradle-tool-ascii",
                p => UnityEditor.Android.AndroidExternalToolsSettings.gradlePath = p),
        };

        static void ConfigureAndroidTools()
        {
            foreach (var (label, path, apply) in AsciiTools)
            {
                if (!Directory.Exists(path))
                {
                    Debug.LogWarning($"[ProjectSetup] ASCII {label} 정션이 없다: {path}");
                    continue;
                }
                apply(path);
                Debug.Log($"[ProjectSetup] Android {label} -> {path}");
            }
        }

        static Color Hex(string s)
        {
            ColorUtility.TryParseHtmlString(s, out Color c);
            return c;
        }

        static void CreatePalettes()
        {
            Directory.CreateDirectory("Assets/Resources");

            var dark = ScriptableObject.CreateInstance<Cube.App.Palette>();
            dark.Background    = Hex("#0D0E11");
            dark.Surface       = Hex("#16181D");
            dark.TextPrimary   = Hex("#E8E9EC");
            dark.TextSecondary = Hex("#6D7078");
            dark.Border        = Hex("#2A2D34");
            dark.Accent        = Hex("#4C8DFF");
            dark.CubeBody      = Hex("#0A0A0A");
            dark.StickerColors = StandardStickers();
            WriteAsset(dark, "Assets/Resources/DarkPalette.asset");

            var light = ScriptableObject.CreateInstance<Cube.App.Palette>();
            light.Background    = Hex("#F4F5F7");
            light.Surface       = Hex("#FFFFFF");
            light.TextPrimary   = Hex("#1C2030");
            light.TextSecondary = Hex("#8B90A0");
            light.Border        = Hex("#DFE2E8");
            light.Accent        = Hex("#2F6BE0");
            light.CubeBody      = Hex("#111111");
            light.StickerColors = StandardStickers();
            WriteAsset(light, "Assets/Resources/LightPalette.asset");
        }

        /// 큐비에 쓸 머티리얼을 애셋으로 만들어 Resources에 둔다.
        ///
        /// 런타임에 Shader.Find로 찾으면 빌드에서 null이 나온다. 어떤 애셋도
        /// URP Lit을 참조하지 않으면 빌드에서 통째로 잘려나가기 때문이다.
        /// 에디터에서는 멀쩡히 돌아서 테스트로는 절대 안 잡힌다 — 실기기에서만 터진다.
        static void CreateCubieMaterial()
        {
            Directory.CreateDirectory("Assets/Resources");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[ProjectSetup] URP Lit 셰이더를 찾을 수 없다. URP 설정을 확인할 것");
                return;
            }
            WriteAsset(new Material(shader), "Assets/Resources/CubieMaterial.mat");
        }

        static void CreateTouchSettings()
        {
            Directory.CreateDirectory("Assets/Resources");
            var s = ScriptableObject.CreateInstance<Cube.App.TouchInputSettings>();
            WriteAsset(s, "Assets/Resources/TouchInputSettings.asset");
        }

        // 면 번호 순서: U, D, F, B, L, R
        //
        // 흰색을 D에 둔다. 초보자 강의는 예외 없이 흰 십자를 바닥에서 시작하고,
        // 표준 공식(Sune, T-perm 등)은 마지막 층이 U라고 가정하기 때문이다.
        // 흰색을 아래로 뒤집으면 좌우가 맞바뀌므로 L이 빨강, R이 주황이 된다.
        static Color[] StandardStickers() => new[]
        {
            Hex("#F5D000"),  // U 노랑
            Hex("#F2F2F2"),  // D 흰색
            Hex("#00A24A"),  // F 초록
            Hex("#0A5FD6"),  // B 파랑
            Hex("#E02020"),  // L 빨강
            Hex("#FF7A00"),  // R 주황
        };

        static void WriteAsset(Object obj, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
        }

        static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            const string path = "Assets/Scenes/Main.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("AppBootstrap");
            go.AddComponent<Cube.App.AppBootstrap>();
            EditorSceneManager.SaveScene(scene, path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
        }
    }
}
