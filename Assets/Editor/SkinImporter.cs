using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CubeEditor
{
    /// docs/skin-brief-for-codex.md 규격대로 도착한 이미지 스킨을 프로젝트에 붙인다.
    /// delivery/skins/<id>/{u,d,f,b,l,r}.png + meta.json을 읽어서
    /// 텍스처는 Assets/Resources/SkinTextures/<id>/에, 스킨 에셋은
    /// Assets/Resources/Skins/Skin_<Id>.asset에 만든다.
    public static class SkinImporter
    {
        static readonly string[] Faces = { "u", "d", "f", "b", "l", "r" };

        [Serializable]
        class FaceMeta
        {
            public string representativeHex;
            public string sourceUrl;
            public string license;
        }

        [Serializable]
        class FacesMeta
        {
            public FaceMeta u, d, f, b, l, r;

            public FaceMeta Get(string face) => face switch
            {
                "u" => u, "d" => d, "f" => f, "b" => b, "l" => l, "r" => r,
                _ => null,
            };
        }

        [Serializable]
        class SkinMeta
        {
            public string id;
            public string displayName;
            public string bodyColorHex;
            public bool characterArtwork;
            public string generationDisclosure;
            public FacesMeta faces;
        }

        // -executeMethod CubeEditor.SkinImporter.ImportAll 로 호출한다.
        public static void ImportAll()
        {
            string deliveryRoot = Path.Combine(Application.dataPath, "..", "delivery", "skins");
            deliveryRoot = Path.GetFullPath(deliveryRoot);

            if (!Directory.Exists(deliveryRoot))
            {
                Debug.LogWarning($"[SkinImporter] delivery 폴더가 없다: {deliveryRoot}");
                return;
            }

            int count = 0;
            foreach (var dir in Directory.GetDirectories(deliveryRoot))
            {
                string metaPath = Path.Combine(dir, "meta.json");
                if (!File.Exists(metaPath)) continue;
                ImportOne(dir, metaPath);
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[SkinImporter] {count}개 스킨을 붙였다");
        }

        static void ImportOne(string sourceDir, string metaPath)
        {
            var meta = JsonUtility.FromJson<SkinMeta>(File.ReadAllText(metaPath));
            if (meta == null || string.IsNullOrEmpty(meta.id))
            {
                Debug.LogError($"[SkinImporter] meta.json을 못 읽었다: {metaPath}");
                return;
            }

            string texDir = $"Assets/Resources/SkinTextures/{meta.id}";
            Directory.CreateDirectory(texDir);

            var textures = new Texture2D[6];
            var colors = new Color[6];

            for (int i = 0; i < Faces.Length; i++)
            {
                string face = Faces[i];
                string srcPng = Path.Combine(sourceDir, $"{face}.png");
                if (!File.Exists(srcPng))
                {
                    Debug.LogError($"[SkinImporter] {meta.id}: {face}.png이 없다");
                    return;
                }

                string destPng = $"{texDir}/{face}.png";
                File.Copy(srcPng, destPng, overwrite: true);
                AssetDatabase.ImportAsset(destPng, ImportAssetOptions.ForceUpdate);
                textures[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(destPng);

                var faceMeta = meta.faces?.Get(face);
                if (faceMeta == null || string.IsNullOrEmpty(faceMeta.representativeHex))
                {
                    Debug.LogError($"[SkinImporter] {meta.id}: {face} representativeHex가 없다");
                    return;
                }
                colors[i] = Hex(faceMeta.representativeHex);
            }

            var skin = ScriptableObject.CreateInstance<Cube.App.Skin>();
            skin.DisplayName = string.IsNullOrEmpty(meta.displayName) ? meta.id : meta.displayName;
            skin.CubeBody = Hex(meta.bodyColorHex);
            skin.CharacterArtwork = meta.characterArtwork;
            skin.StickerColors = colors;
            skin.StickerTextures = textures;

            string assetName = "Skin_" + char.ToUpperInvariant(meta.id[0]) + meta.id.Substring(1);
            string assetPath = $"Assets/Resources/Skins/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (existing != null) AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(skin, assetPath);

            Debug.Log($"[SkinImporter] {meta.id} -> {assetPath} ({meta.generationDisclosure})");
        }

        static Color Hex(string s)
        {
            ColorUtility.TryParseHtmlString(s, out Color c);
            return c;
        }
    }
}
