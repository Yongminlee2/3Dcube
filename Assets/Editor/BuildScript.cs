using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CubeEditor
{
    public static class BuildScript
    {
        /// -executeMethod CubeEditor.BuildScript.BuildApk 로 호출한다.
        ///
        /// 이 PC에서는 반드시 아래 환경 변수를 준 채로 띄워야 한다.
        /// 한글 사용자 폴더가 Gradle과 prefab 도구를 깨뜨리기 때문이다.
        ///
        ///   TEMP=C:/workAndroid/tmp-ascii
        ///   TMP=C:/workAndroid/tmp-ascii
        ///   GRADLE_USER_HOME=C:/workAndroid/gradle-home-ascii
        ///
        /// 도구 경로(NDK/SDK/JDK/Gradle) 쪽은 ProjectSetup.Configure가 맡는다.
        public static void BuildApk()
        {
            WarnIfNonAsciiTempPath();

            Directory.CreateDirectory("Build");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = "Build/cube.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[BuildScript] {s.result} / {s.totalSize} bytes / {s.totalTime}");

            if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
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
