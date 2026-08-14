using System;
using UnityEngine;
using Cube.Core;

namespace Cube.App
{
    /// <summary>
    /// 카메라에서 얻은 여섯 면의 RGB 표본을 큐브의 여섯 색으로 바꾼다.
    /// 각 면의 가운데 스티커가 그 면의 색 기준이므로 조명 색이 조금 달라도
    /// 같은 촬영 세션 안에서 서로 비교할 수 있다.
    /// </summary>
    public static class CubeColorRecognizer
    {
        public const int FaceCount = 6;
        public const int SamplesPerFace = 9;

        static readonly Color[] PhysicalCubeCenters =
        {
            new Color(0.96f, 0.78f, 0.02f), // U: yellow
            new Color(0.92f, 0.92f, 0.90f), // D: white
            new Color(0.03f, 0.62f, 0.24f), // F: green
            new Color(0.03f, 0.28f, 0.78f), // B: blue
            new Color(0.88f, 0.06f, 0.05f), // L: red
            new Color(1.00f, 0.34f, 0.02f), // R: orange
        };

        public static Color[] PhysicalReferenceColors()
            => (Color[])PhysicalCubeCenters.Clone();

        public static Face DetectPhysicalFace(Color centerSample)
        {
            Color.RGBToHSV(centerSample, out _, out float saturation, out _);
            if (saturation < 0.16f) return Face.D;

            // A dim green/blue sticker can be close to grey in Lab space. Once visible chroma is
            // present it cannot be the white centre, so compare only the five coloured centres.
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int face = 0; face < PhysicalCubeCenters.Length; face++)
            {
                if (face == (int)Face.D) continue;
                float distance = ColorDistance(centerSample, PhysicalCubeCenters[face]);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = face;
            }
            return (Face)best;
        }

        public static CubeState BuildState(Color[][] faceSamples)
        {
            ValidateSamples(faceSamples);

            var centers = new Color[FaceCount];
            for (int face = 0; face < FaceCount; face++) centers[face] = faceSamples[face][4];
            return BuildState(faceSamples, centers);
        }

        public static CubeState BuildState(Color[][] faceSamples, Color[] centers)
        {
            if (centers == null || centers.Length != FaceCount)
                throw new ArgumentException("가운데 색은 여섯 개여야 합니다.", nameof(centers));
            if (faceSamples == null || faceSamples.Length != FaceCount)
                throw new ArgumentException("면 표본은 여섯 개여야 합니다.", nameof(faceSamples));

            if (HasAllFaces(faceSamples))
                return BuildBalancedState(faceSamples, centers);

            var state = CubeState.Solved(3);
            for (int face = 0; face < FaceCount; face++)
            {
                var samples = faceSamples[face];
                if (samples == null) continue;
                if (samples.Length != SamplesPerFace)
                    throw new ArgumentException("한 면에는 아홉 개의 색 표본이 필요합니다.", nameof(faceSamples));

                for (int cell = 0; cell < SamplesPerFace; cell++)
                {
                    int row = cell / 3;
                    int col = cell % 3;
                    byte color = cell == 4 ? (byte)face : NearestCenter(samples[cell], centers);
                    state.Facelets[state.IndexOf((Face)face, row, col)] = color;
                }
            }
            return state;
        }

        public static byte NearestCenter(Color sample, Color[] centers)
        {
            if (centers == null || centers.Length != FaceCount)
                throw new ArgumentException("가운데 색은 여섯 개여야 합니다.", nameof(centers));

            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < centers.Length; i++)
            {
                float distance = ColorDistance(sample, centers[i]);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return (byte)best;
        }

        /// <summary>가장 가까운 색과 두 번째 색의 간격으로 0~1 신뢰도를 낸다.</summary>
        public static float Confidence(Color sample, Color[] centers)
        {
            if (centers == null || centers.Length != FaceCount) return 0f;
            float first = float.MaxValue;
            float second = float.MaxValue;
            for (int i = 0; i < centers.Length; i++)
            {
                float distance = Mathf.Sqrt(ColorDistance(sample, centers[i]));
                if (distance < first) { second = first; first = distance; }
                else if (distance < second) second = distance;
            }
            if (second <= 0.001f || second == float.MaxValue) return 0f;
            return Mathf.Clamp01((second - first) / second);
        }

        static bool HasAllFaces(Color[][] samples)
        {
            for (int face = 0; face < FaceCount; face++)
                if (samples[face] == null || samples[face].Length != SamplesPerFace)
                    return false;
            return true;
        }

        /// <summary>
        /// A physical 3x3 cube always contains exactly nine stickers of each colour. Assign all
        /// non-centre stickers together so similar red/orange samples cannot collapse into one class.
        /// </summary>
        static CubeState BuildBalancedState(Color[][] samples, Color[] centers)
        {
            const int movableStickerCount = FaceCount * (SamplesPerFace - 1);
            var sampleColors = new Color[movableStickerCount];
            var stateIndices = new int[movableStickerCount];
            var state = CubeState.Solved(3);
            int item = 0;

            for (int face = 0; face < FaceCount; face++)
                for (int cell = 0; cell < SamplesPerFace; cell++)
                {
                    int row = cell / 3;
                    int col = cell % 3;
                    int stateIndex = state.IndexOf((Face)face, row, col);
                    if (cell == 4)
                    {
                        state.Facelets[stateIndex] = (byte)face;
                        continue;
                    }

                    sampleColors[item] = samples[face][cell];
                    stateIndices[item] = stateIndex;
                    item++;
                }

            var costs = new float[movableStickerCount, movableStickerCount];
            for (int sample = 0; sample < movableStickerCount; sample++)
                for (int slot = 0; slot < movableStickerCount; slot++)
                    costs[sample, slot] = ColorDistance(sampleColors[sample], centers[slot / 8]);

            int[] assignment = MinimumCostAssignment(costs);
            for (int sample = 0; sample < movableStickerCount; sample++)
                state.Facelets[stateIndices[sample]] = (byte)(assignment[sample] / 8);
            return state;
        }

        // Hungarian assignment, O(n^3). n is fixed at 48, so this is inexpensive at capture time.
        static int[] MinimumCostAssignment(float[,] costs)
        {
            int n = costs.GetLength(0);
            var rowPotential = new float[n + 1];
            var columnPotential = new float[n + 1];
            var matchedRow = new int[n + 1];
            var previousColumn = new int[n + 1];

            for (int row = 1; row <= n; row++)
            {
                matchedRow[0] = row;
                int column0 = 0;
                var minimum = new float[n + 1];
                var used = new bool[n + 1];
                for (int column = 1; column <= n; column++) minimum[column] = float.MaxValue;

                do
                {
                    used[column0] = true;
                    int row0 = matchedRow[column0];
                    float delta = float.MaxValue;
                    int column1 = 0;
                    for (int column = 1; column <= n; column++)
                    {
                        if (used[column]) continue;
                        float current = costs[row0 - 1, column - 1]
                            - rowPotential[row0] - columnPotential[column];
                        if (current < minimum[column])
                        {
                            minimum[column] = current;
                            previousColumn[column] = column0;
                        }
                        if (minimum[column] < delta)
                        {
                            delta = minimum[column];
                            column1 = column;
                        }
                    }

                    for (int column = 0; column <= n; column++)
                    {
                        if (used[column])
                        {
                            rowPotential[matchedRow[column]] += delta;
                            columnPotential[column] -= delta;
                        }
                        else if (column > 0) minimum[column] -= delta;
                    }
                    column0 = column1;
                }
                while (matchedRow[column0] != 0);

                do
                {
                    int column1 = previousColumn[column0];
                    matchedRow[column0] = matchedRow[column1];
                    column0 = column1;
                }
                while (column0 != 0);
            }

            var assignment = new int[n];
            for (int column = 1; column <= n; column++)
                assignment[matchedRow[column] - 1] = column - 1;
            return assignment;
        }

        static float ColorDistance(Color sample, Color center)
        {
            Vector3 labDelta = ToLab(sample) - ToLab(center);
            // Sticker hue/chroma is more stable than brightness under phone auto-exposure.
            float distance = labDelta.x * labDelta.x * 0.18f
                + labDelta.y * labDelta.y + labDelta.z * labDelta.z;

            Color.RGBToHSV(sample, out float sampleHue, out float sampleSaturation, out _);
            Color.RGBToHSV(center, out float centerHue, out float centerSaturation, out _);
            float chroma = Mathf.Min(sampleSaturation, centerSaturation);
            if (chroma > 0.25f)
            {
                float hueTurns = Mathf.Abs(sampleHue - centerHue);
                hueTurns = Mathf.Min(hueTurns, 1f - hueTurns);
                float hueDegrees = hueTurns * 360f;
                distance += hueDegrees * hueDegrees * chroma * 1.15f;
            }
            return distance;
        }

        /// <summary>
        /// Measures scene change while largely ignoring uniform exposure changes. Camera capture
        /// is accepted only after several consecutive frames stay below the stability threshold.
        /// </summary>
        public static float FrameDifference(Color[] first, Color[] second)
        {
            if (first == null || second == null || first.Length != second.Length || first.Length == 0)
                return float.MaxValue;

            float total = 0f;
            for (int i = 0; i < first.Length; i++)
            {
                Color a = first[i];
                Color b = second[i];
                float aSum = Mathf.Max(0.0001f, a.r + a.g + a.b);
                float bSum = Mathf.Max(0.0001f, b.r + b.g + b.b);
                Vector3 aChromaticity = new Vector3(a.r / aSum, a.g / aSum, a.b / aSum);
                Vector3 bChromaticity = new Vector3(b.r / bSum, b.g / bSum, b.b / bSum);
                float aValue = Mathf.Max(a.r, Mathf.Max(a.g, a.b));
                float bValue = Mathf.Max(b.r, Mathf.Max(b.g, b.b));
                total += Vector3.Distance(aChromaticity, bChromaticity)
                    + Mathf.Abs(aValue - bValue) * 0.12f;
            }
            return total / first.Length;
        }

        /// <summary>
        /// 화면에 똑바로 보이는 미리보기 좌표를 WebCamTexture 원본 좌표로 바꾼다.
        /// </summary>
        public static Vector2 PreviewToTextureUv(Vector2 previewUv, int rotationAngle,
                                                  bool verticallyMirrored)
        {
            int angle = ((rotationAngle % 360) + 360) % 360;
            Vector2 uv;
            switch (angle)
            {
                case 90:  uv = new Vector2(1f - previewUv.y, previewUv.x); break;
                case 180: uv = new Vector2(1f - previewUv.x, 1f - previewUv.y); break;
                case 270: uv = new Vector2(previewUv.y, 1f - previewUv.x); break;
                default:  uv = previewUv; break;
            }
            if (verticallyMirrored) uv.y = 1f - uv.y;
            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);
            return uv;
        }

        /// <summary>카메라 원본 가운데에서 가장 큰 정사각형을 잘라 내는 UV 영역.</summary>
        public static Rect CenterSquareCrop(int width, int height)
        {
            if (width <= 0 || height <= 0) return new Rect(0f, 0f, 1f, 1f);
            if (width == height) return new Rect(0f, 0f, 1f, 1f);
            if (width > height)
            {
                float normalizedWidth = height / (float)width;
                return new Rect((1f - normalizedWidth) * 0.5f, 0f, normalizedWidth, 1f);
            }

            float normalizedHeight = width / (float)height;
            return new Rect(0f, (1f - normalizedHeight) * 0.5f, 1f, normalizedHeight);
        }

        /// <summary>미리보기의 0~1 좌표를 가운데 정사각형 크롭 안의 원본 UV로 바꾼다.</summary>
        public static Vector2 ApplyCrop(Vector2 uv, Rect crop)
            => new Vector2(
                Mathf.Clamp01(crop.x + uv.x * crop.width),
                Mathf.Clamp01(crop.y + uv.y * crop.height));

        static void ValidateSamples(Color[][] samples)
        {
            if (samples == null || samples.Length != FaceCount)
                throw new ArgumentException("면 표본은 여섯 개여야 합니다.", nameof(samples));
            for (int face = 0; face < FaceCount; face++)
                if (samples[face] == null || samples[face].Length != SamplesPerFace)
                    throw new ArgumentException("각 면에 아홉 개의 색 표본이 필요합니다.", nameof(samples));
        }

        // sRGB -> CIE Lab. RGB 거리보다 흰색/노란색과 빨강/주황을 안정적으로 나눈다.
        static Vector3 ToLab(Color color)
        {
            float r = Linearize(color.r);
            float g = Linearize(color.g);
            float b = Linearize(color.b);

            float x = (r * 0.4124564f + g * 0.3575761f + b * 0.1804375f) / 0.95047f;
            float y =  r * 0.2126729f + g * 0.7151522f + b * 0.0721750f;
            float z = (r * 0.0193339f + g * 0.1191920f + b * 0.9503041f) / 1.08883f;

            x = Pivot(x); y = Pivot(y); z = Pivot(z);
            return new Vector3(116f * y - 16f, 500f * (x - y), 200f * (y - z));
        }

        static float Linearize(float value)
            => value <= 0.04045f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);

        static float Pivot(float value)
            => value > 0.008856f ? Mathf.Pow(value, 1f / 3f) : 7.787f * value + 16f / 116f;
    }
}
