using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CubeEditor
{
    public static class BuildScript
    {
        /// 이 PC에서는 반드시 아래 환경 변수를 준 채로 띄워야 한다.
        /// 한글 사용자 폴더가 Gradle과 prefab 도구를 깨뜨리기 때문이다.
        ///
        ///   TEMP=C:/workAndroid/tmp-ascii
        ///   TMP=C:/workAndroid/tmp-ascii
        ///   GRADLE_USER_HOME=C:/workAndroid/gradle-home-ascii
        ///
        /// 도구 경로(NDK/SDK/JDK/Gradle) 쪽은 ProjectSetup.Configure가 맡는다.

        /// 폰에 꽂아 확인할 때 쓴다. 디버그 키로 서명되므로 스토어에는 올릴 수 없다.
        /// -executeMethod CubeEditor.BuildScript.BuildApk
        public static void BuildApk() => Build("Build/cube.apk", release: false);

        /// 스토어에 올릴 것. 신규 앱은 APK를 받지 않고 AAB만 받는다.
        /// 릴리스 키로 서명하므로 keystore.properties가 있어야 한다.
        /// -executeMethod CubeEditor.BuildScript.BuildAab
        public static void BuildAab() => Build("Build/cube.aab", release: true);

        static void Build(string outputPath, bool release)
        {
            WarnIfNonAsciiTempPath();
            Directory.CreateDirectory("Build");

            EditorUserBuildSettings.buildAppBundle = release;

            if (release)
            {
                if (!ApplySigning())
                {
                    Debug.LogError("[BuildScript] 서명 설정을 읽지 못해 중단한다. "
                                 + "디버그 키로 서명된 것을 스토어에 올리면 거부된다.");
                    EditorApplication.Exit(1);
                    return;
                }
            }
            else
            {
                // 릴리스 빌드가 켜 둔 설정을 되돌린다.
                //
                // useCustomKeystore는 ProjectSettings에 저장되지만 비밀번호는
                // 저장되지 않는다(그래야 저장소에 새지 않는다). 그래서 AAB를 한 번
                // 만들고 나면 다음 APK 빌드가 "Unable to sign the application;
                // please provide passwords!"로 즉시 죽는다.
                PlayerSettings.Android.useCustomKeystore = false;
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[BuildScript] {s.result} / {outputPath} / {s.totalSize} bytes / {s.totalTime}");

            if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// keystore.properties를 읽어 릴리스 서명을 건다.
        ///
        /// 이 파일과 .jks는 .gitignore에 있어 저장소에 올라가지 않는다.
        /// 다른 앱들과 같은 업로드 키를 쓴다 — 키를 잃어버리면 그 패키지는
        /// 영원히 업데이트를 올릴 수 없으므로 백업을 반드시 유지할 것.
        ///
        /// 값은 로그에 남기지 않는다. 배치 빌드 로그가 그대로 파일로 남기 때문이다.
        static bool ApplySigning()
        {
            const string propsPath = "keystore.properties";
            if (!File.Exists(propsPath))
            {
                Debug.LogError($"[BuildScript] {propsPath}가 없다. "
                             + "다른 앱에서 쓰는 것을 프로젝트 루트에 복사할 것.");
                return false;
            }

            var props = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string raw in File.ReadAllLines(propsPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                props[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }

            foreach (string key in new[] { "storeFile", "storePassword", "keyAlias", "keyPassword" })
                if (!props.TryGetValue(key, out string v) || string.IsNullOrEmpty(v))
                {
                    Debug.LogError($"[BuildScript] {propsPath}에 {key}가 비어 있다.");
                    return false;
                }

            string storeFile = Path.GetFullPath(props["storeFile"]);
            if (!File.Exists(storeFile))
            {
                Debug.LogError($"[BuildScript] 키스토어 파일이 없다: {storeFile}");
                return false;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = storeFile;
            PlayerSettings.Android.keystorePass = props["storePassword"];
            PlayerSettings.Android.keyaliasName = props["keyAlias"];
            PlayerSettings.Android.keyaliasPass = props["keyPassword"];

            // 파일 이름과 별칭까지만 알린다. 비밀번호는 찍지 않는다.
            Debug.Log($"[BuildScript] 릴리스 서명 적용: {Path.GetFileName(storeFile)} "
                    + $"(alias {props["keyAlias"]})");
            return true;
        }

        /// 임시 폴더에 ASCII가 아닌 글자가 있으면 Gradle의 prefab 단계가
        /// "네트워크 경로를 찾지 못했습니다"로 죽는다. 원인 찾기가 어려우니 미리 알린다.
        static void WarnIfNonAsciiTempPath()
        {
            string temp = Path.GetTempPath();
            foreach (char c in temp)
                if (c > 127)
                {
                    Debug.LogWarning($"[BuildScript] 임시 폴더에 ASCII가 아닌 글자가 있다: {temp}\n" +
                                     "TEMP와 TMP를 C:/workAndroid/tmp-ascii 로 준 채 빌드할 것.");
                    return;
                }
        }
    }
}
