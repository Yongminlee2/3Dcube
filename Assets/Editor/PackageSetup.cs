using System;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace CubeEditor
{
    public static class PackageSetup
    {
        // -executeMethod CubeEditor.PackageSetup.AddPackages 로 호출한다.
        public static void AddPackages()
        {
            Add("com.unity.render-pipelines.universal");
            Add("com.unity.test-framework");
            Add("com.unity.ugui");
            Debug.Log("[PackageSetup] 패키지 설치 완료");
        }

        static void Add(string id)
        {
            AddRequest req = Client.Add(id);
            // 배치 모드에서는 에디터 루프가 돌지 않으므로 요청이 끝날 때까지 직접 기다린다.
            while (!req.IsCompleted) Thread.Sleep(100);
            if (req.Status != StatusCode.Success)
                throw new Exception($"{id} 설치 실패: {req.Error?.message}");
            Debug.Log($"[PackageSetup] {id} -> {req.Result.version}");
        }
    }
}
