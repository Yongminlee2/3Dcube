using System.Collections.Generic;
using System.IO;
using System.Security;
using UnityEditor.Android;

namespace CubeEditor
{
    /// Adds locale-specific launcher labels to Unity's generated launcher module.
    /// The in-app language can be changed independently; Android chooses this label from
    /// the phone locale on the home screen and in system settings.
    public sealed class LocalizedAndroidAppName : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        static readonly IReadOnlyDictionary<string, string> Names =
            new Dictionary<string, string>
            {
                ["values-ko"] = "3D 큐브",
                ["values-en"] = "3D Cube",
                ["values-ja"] = "3Dキューブ",
                ["values-zh-rCN"] = "3D魔方",
                ["values-zh-rTW"] = "3D魔方",
                ["values-zh-rHK"] = "3D魔方",
                ["values-es"] = "Cubo 3D",
                ["values-fr"] = "Cube 3D",
                ["values-de"] = "3D-Würfel",
                ["values-pt"] = "Cubo 3D",
                ["values-ru"] = "3D-кубик",
                ["values-vi"] = "Khối 3D",
                ["values-in"] = "Kubus 3D",
                ["values-th"] = "ลูกบาศก์ 3D",
            };

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            string gradleRoot = Directory.GetParent(unityLibraryPath).FullName;
            string resourceRoot = Path.Combine(gradleRoot, "launcher", "src", "main", "res");
            foreach (var pair in Names)
            {
                string directory = Path.Combine(resourceRoot, pair.Key);
                Directory.CreateDirectory(directory);
                string escaped = SecurityElement.Escape(pair.Value);
                File.WriteAllText(Path.Combine(directory, "strings.xml"),
                    $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                    + $"<resources><string name=\"app_name\">{escaped}</string></resources>\n");
            }
        }
    }
}
