using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CubeEditor
{
    /// Assets/Art/AppIcon.png에 선택한 최종 아이콘을 Android 앱 아이콘으로 지정한다.
    public static class IconGenerator
    {
        const string IconPath = "Assets/Art/AppIcon.png";

        // 기존 자동화 호환용 이름. 이제 이미지를 다시 그리지 않고 선택한 PNG를 적용한다.
        public static void Generate()
        {
            if (!File.Exists(IconPath))
                throw new FileNotFoundException("선택한 앱 아이콘이 없습니다.", IconPath);
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) throw new MissingReferenceException($"아이콘을 불러오지 못했습니다: {IconPath}");
            var icons = new[] { icon, icon, icon, icon, icon, icon };
            PlayerSettings.SetIcons(NamedBuildTarget.Android, icons, IconKind.Application);

            AssetDatabase.SaveAssets();
            Debug.Log($"[IconGenerator] 선택한 Android 앱 아이콘 적용 완료: {IconPath}");
        }
    }
}
