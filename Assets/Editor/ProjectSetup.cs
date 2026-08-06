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
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ProjectSetup] 완료");
        }

        // -executeMethod CubeEditor.ProjectSetup.CreateAssets 로 호출한다.
        public static void CreateAssets()
        {
            CreatePalettes();
            CreateTouchSettings();
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
            PlayerSettings.productName = "3Dcube";
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

        static void CreateTouchSettings()
        {
            Directory.CreateDirectory("Assets/Resources");
            var s = ScriptableObject.CreateInstance<Cube.App.TouchInputSettings>();
            WriteAsset(s, "Assets/Resources/TouchInputSettings.asset");
        }

        // 면 번호 순서: U, D, F, B, L, R
        static Color[] StandardStickers() => new[]
        {
            Hex("#F2F2F2"), Hex("#F5D000"), Hex("#00A24A"),
            Hex("#0A5FD6"), Hex("#FF7A00"), Hex("#E02020"),
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
