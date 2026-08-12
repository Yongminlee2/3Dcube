using NUnit.Framework;
using UnityEngine;
using Cube.Core;

namespace Cube.App.Tests
{
    public class CubeColorRecognizerTests
    {
        static readonly Color[] Centers =
        {
            new Color(0.96f, 0.78f, 0.02f),
            new Color(0.92f, 0.92f, 0.90f),
            new Color(0.03f, 0.62f, 0.24f),
            new Color(0.03f, 0.28f, 0.78f),
            new Color(0.88f, 0.06f, 0.05f),
            new Color(1.00f, 0.34f, 0.02f),
        };

        [Test]
        public void 여섯_가운데색을_기준으로_실제_큐브_배치를_복원한다()
        {
            var expected = CubeState.Solved(3);
            expected.Apply(MoveNotation.Parse("R U R' U' F2 L D B2", 3));

            var samples = SamplesFor(expected);
            var recognized = CubeColorRecognizer.BuildState(samples);

            Assert.IsTrue(expected.SameAs(recognized));
        }

        [Test]
        public void 화면_회전값에_따라_카메라_좌표를_바꾼다()
        {
            Vector2 uv = CubeColorRecognizer.PreviewToTextureUv(new Vector2(0.2f, 0.7f), 90, false);
            Assert.That(uv.x, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(uv.y, Is.EqualTo(0.2f).Within(0.0001f));

            Vector2 mirrored = CubeColorRecognizer.PreviewToTextureUv(new Vector2(0.2f, 0.7f), 90, true);
            Assert.That(mirrored.x, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(mirrored.y, Is.EqualTo(0.8f).Within(0.0001f));
        }

        public static Color[][] SamplesFor(CubeState state)
        {
            var samples = new Color[6][];
            for (int face = 0; face < 6; face++)
            {
                samples[face] = new Color[9];
                float light = 0.92f + face * 0.018f;
                for (int cell = 0; cell < 9; cell++)
                {
                    byte color = state.Get((Face)face, cell / 3, cell % 3);
                    Color source = Centers[color];
                    samples[face][cell] = new Color(
                        Mathf.Clamp01(source.r * light + (cell % 2 == 0 ? 0.008f : -0.006f)),
                        Mathf.Clamp01(source.g * light + (cell % 3 == 0 ? 0.006f : -0.004f)),
                        Mathf.Clamp01(source.b * light + (cell % 4 == 0 ? 0.007f : -0.003f)));
                }
            }
            return samples;
        }
    }
}
