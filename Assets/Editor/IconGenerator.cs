using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CubeEditor
{
    /// 앱 아이콘을 코드로 그린다.
    ///
    /// 이미지 파일을 밖에서 가져오지 않는다 — 출처와 라이선스를 따질 것이 없고,
    /// 색을 바꾸고 싶으면 여기 숫자만 고치면 된다.
    public static class IconGenerator
    {
        const string IconPath = "Assets/Art/AppIcon.png";
        const int Size = 1024;

        // 앱 색과 맞춘다. 아래 흰색·위 노랑·앞 초록·오른쪽 주황.
        static readonly Color Background = new Color32(0x0D, 0x0E, 0x11, 0xFF);
        static readonly Color Body       = new Color32(0x0A, 0x0A, 0x0A, 0xFF);
        static readonly Color TopColor   = new Color32(0xF5, 0xD0, 0x00, 0xFF);
        static readonly Color LeftColor  = new Color32(0x00, 0xA2, 0x4A, 0xFF);
        static readonly Color RightColor = new Color32(0xFF, 0x7A, 0x00, 0xFF);

        // -executeMethod CubeEditor.IconGenerator.Generate 로 호출한다.
        public static void Generate()
        {
            Directory.CreateDirectory("Assets/Art");

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var px = new Color[Size * Size];
            for (int i = 0; i < px.Length; i++) px[i] = Background;

            DrawCube(px);

            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(IconPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            var icons = new[] { icon, icon, icon, icon, icon, icon };
            PlayerSettings.SetIcons(NamedBuildTarget.Android, icons, IconKind.Application);

            // 적응형 아이콘(둥근 배경에 앞 레이어를 얹는 형식)은 넣지 않는다.
            // 안 넣으면 안드로이드가 이 아이콘을 그대로 쓴다. 첫 출시에는 충분하고,
            // 나중에 다듬을 때 앞·뒤 레이어를 따로 그려 붙이면 된다.

            AssetDatabase.SaveAssets();
            Debug.Log($"[IconGenerator] 아이콘 생성과 지정 완료: {IconPath}");
        }

        /// 정육면체를 비스듬히 본 모습을 세 개의 마름모로 그린다.
        /// 윗면 하나, 왼쪽 면 하나, 오른쪽 면 하나.
        static void DrawCube(Color[] px)
        {
            float cx = Size * 0.5f;
            float cy = Size * 0.5f;
            float r = Size * 0.34f;          // 큐브 반지름
            float half = r * 0.5f;

            // 마름모 세 개의 꼭짓점 (화면 좌표, y는 위가 큰 값)
            var top = new Vector2(cx, cy + r);
            var left = new Vector2(cx - r * 0.866f, cy + half);
            var right = new Vector2(cx + r * 0.866f, cy + half);
            var bottom = new Vector2(cx, cy - r);
            var lowLeft = new Vector2(cx - r * 0.866f, cy - half);
            var lowRight = new Vector2(cx + r * 0.866f, cy - half);
            var center = new Vector2(cx, cy);

            FillQuad(px, top, right, center, left, TopColor, 3);
            FillQuad(px, left, center, bottom, lowLeft, LeftColor, 3);
            FillQuad(px, center, right, lowRight, bottom, RightColor, 3);
        }

        /// 사각형을 채우고, 3×3 칸이 보이도록 안쪽에 격자 선을 남긴다.
        static void FillQuad(Color[] px, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, int cells)
        {
            for (int v = 0; v < cells; v++)
                for (int u = 0; u < cells; u++)
                {
                    // 사각형을 uv로 나눠 각 칸의 네 꼭짓점을 구한다.
                    float u0 = u / (float)cells, u1 = (u + 1f) / cells;
                    float v0 = v / (float)cells, v1 = (v + 1f) / cells;
                    const float Gap = 0.035f;
                    u0 += Gap / cells; u1 -= Gap / cells;
                    v0 += Gap / cells; v1 -= Gap / cells;

                    FillTriangleQuad(px,
                        Bilinear(a, b, c, d, u0, v0), Bilinear(a, b, c, d, u1, v0),
                        Bilinear(a, b, c, d, u1, v1), Bilinear(a, b, c, d, u0, v1), color);
                }
        }

        static Vector2 Bilinear(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float u, float v)
            => Vector2.Lerp(Vector2.Lerp(a, b, u), Vector2.Lerp(d, c, u), v);

        static void FillTriangleQuad(Color[] px, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            float minX = Mathf.Min(Mathf.Min(a.x, b.x), Mathf.Min(c.x, d.x));
            float maxX = Mathf.Max(Mathf.Max(a.x, b.x), Mathf.Max(c.x, d.x));
            float minY = Mathf.Min(Mathf.Min(a.y, b.y), Mathf.Min(c.y, d.y));
            float maxY = Mathf.Max(Mathf.Max(a.y, b.y), Mathf.Max(c.y, d.y));

            for (int y = Mathf.Max(0, (int)minY); y <= Mathf.Min(Size - 1, (int)maxY); y++)
                for (int x = Mathf.Max(0, (int)minX); x <= Mathf.Min(Size - 1, (int)maxX); x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    if (InTriangle(p, a, b, c) || InTriangle(p, a, c, d))
                        px[y * Size + x] = color;
                }
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        static float Cross(Vector2 p, Vector2 a, Vector2 b)
            => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }
}
