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

            Vector3 sampleLab = ToLab(sample);
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < centers.Length; i++)
            {
                float distance = (sampleLab - ToLab(centers[i])).sqrMagnitude;
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
            Vector3 sampleLab = ToLab(sample);
            float first = float.MaxValue;
            float second = float.MaxValue;
            for (int i = 0; i < centers.Length; i++)
            {
                float distance = Vector3.Distance(sampleLab, ToLab(centers[i]));
                if (distance < first) { second = first; first = distance; }
                else if (distance < second) second = distance;
            }
            if (second <= 0.001f || second == float.MaxValue) return 0f;
            return Mathf.Clamp01((second - first) / second);
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
