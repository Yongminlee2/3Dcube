using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace CubeEditor
{
    /// 유니티가 자동으로 넣는 INTERNET 권한을 걷어낸다.
    ///
    /// 이 앱은 네트워크를 전혀 쓰지 않는다(UnityWebRequest도 소켓도 없다).
    /// 그런데도 유니티는 기본 템플릿에 android.permission.INTERNET을 넣어 두고,
    /// 그대로 두면 개인정보처리방침의 "인터넷 접근 권한이 없어서 데이터를 밖으로
    /// 보내는 것이 기술적으로 불가능하다"는 문장이 사실과 달라진다.
    /// 스토어 권한 목록에도 쓰지 않는 권한이 뜬다.
    ///
    /// Assets/Plugins/Android/AndroidManifest.xml을 직접 두는 방법도 있지만
    /// 그건 유니티가 만든 매니페스트를 통째로 대체해서 액티비티 선언까지
    /// 내가 관리해야 한다. 생성된 결과에서 그 줄만 지우는 편이 안전하다.
    public sealed class StripInternetPermission : IPostGenerateGradleAndroidProject
    {
        // LocalizedAndroidAppName이 100이다. 순서는 서로 무관하지만 뒤에 둔다.
        public int callbackOrder => 200;

        static readonly Regex InternetPermission = new Regex(
            @"[ \t]*<uses-permission\s+android:name=""android\.permission\.INTERNET""\s*/>\s*\r?\n?",
            RegexOptions.IgnoreCase);

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            string gradleRoot = Directory.GetParent(unityLibraryPath).FullName;
            int removed = 0;

            foreach (string manifest in Directory.GetFiles(
                         gradleRoot, "AndroidManifest.xml", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(manifest);
                if (!InternetPermission.IsMatch(text)) continue;
                File.WriteAllText(manifest, InternetPermission.Replace(text, ""));
                removed++;
            }

            Debug.Log(removed > 0
                ? $"[StripInternetPermission] INTERNET 권한을 매니페스트 {removed}개에서 제거했다"
                : "[StripInternetPermission] INTERNET 권한이 없다. 지울 것이 없었다");
        }
    }
}
