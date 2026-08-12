using System.IO;
using System.Linq;
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
            CreateSkins();
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
            PlayerSettings.productName = "3D 큐브";
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
            WriteAsset(dark, "Assets/Resources/DarkPalette.asset");

            var light = ScriptableObject.CreateInstance<Cube.App.Palette>();
            light.Background    = Hex("#F4F5F7");
            light.Surface       = Hex("#FFFFFF");
            light.TextPrimary   = Hex("#1C2030");
            light.TextSecondary = Hex("#8B90A0");
            light.Border        = Hex("#DFE2E8");
            light.Accent        = Hex("#2F6BE0");
            WriteAsset(light, "Assets/Resources/LightPalette.asset");
        }

        /// 큐브 겉모습(스티커 6색 + 몸통색) 다섯 종류. 다크/라이트 팔레트와 달리
        /// 화면 테마와 무관하게 설정에서 따로 고른다.
        ///
        /// 색만 바꾸고 재질(금속성 등)은 건드리지 않는다 — 방향광이 거의 평평해서
        /// 금속성을 올리면 반사가 없어 색이 탁한 회색으로 죽는다. 큐브는 색으로
        /// 푸는 물건이라 스킨이 바뀌어도 여섯 면이 서로 뚜렷이 구분돼야 한다.
        ///
        /// 배열 순서는 항상 U, D, F, B, L, R이고 D(아래)를 흰색 계열로 고정한다.
        /// 초보자 강의와 표준 공식(Sune, T-perm 등)이 흰 십자를 바닥에서 시작하고
        /// 마지막 층을 U로 가정하기 때문이다.
        static void CreateSkins()
        {
            Directory.CreateDirectory("Assets/Resources/Skins");

            WriteSkin("Skin_Classic", "클래식", "#0A0A0A", new[]
            {
                "#F5D000", "#F2F2F2", "#00A24A", "#0A5FD6", "#E02020", "#FF7A00",
            });

            WriteSkin("Skin_Pastel", "파스텔", "#2B2B33", new[]
            {
                "#FFE28A", "#FFFFFF", "#8FE3B0", "#90C2F5", "#FFA6A6", "#FFC48A",
            });

            WriteSkin("Skin_Vivid", "비비드", "#050505", new[]
            {
                "#FFD500", "#FFFFFF", "#00E676", "#2979FF", "#FF1744", "#FF6D00",
            });

            WriteSkin("Skin_Muted", "톤다운", "#1C1C1C", new[]
            {
                "#C9A227", "#E8E6DF", "#4C8C6B", "#3E6FA8", "#B4453A", "#C97A3D",
            });

            WriteSkin("Skin_Steel", "다크스틸", "#14161A", new[]
            {
                "#E0B84D", "#C9CDD3", "#2F9E6E", "#3A6FD8", "#D1453D", "#E08A3C",
            });
        }

        /// 면 번호 순서: U, D, F, B, L, R
        static void WriteSkin(string assetName, string displayName, string body, string[] stickers)
        {
            var skin = ScriptableObject.CreateInstance<Cube.App.Skin>();
            skin.DisplayName = displayName;
            skin.CubeBody = Hex(body);
            skin.StickerColors = stickers.Select(Hex).ToArray();
            WriteAsset(skin, $"Assets/Resources/Skins/{assetName}.asset");
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
