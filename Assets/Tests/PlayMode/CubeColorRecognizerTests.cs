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

        [Test]
        public void LandscapeCameraUsesCenteredSquareCrop()
        {
            Rect crop = CubeColorRecognizer.CenterSquareCrop(1280, 720);

            Assert.That(crop.x, Is.EqualTo(0.21875f).Within(0.0001f));
            Assert.That(crop.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(crop.width, Is.EqualTo(0.5625f).Within(0.0001f));
            Assert.That(crop.height, Is.EqualTo(1f).Within(0.0001f));

            Vector2 center = CubeColorRecognizer.ApplyCrop(new Vector2(0.5f, 0.5f), crop);
            Assert.That(center.x, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(center.y, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void PortraitCameraUsesCenteredSquareCrop()
        {
            Rect crop = CubeColorRecognizer.CenterSquareCrop(720, 1280);

            Assert.That(crop.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(crop.y, Is.EqualTo(0.21875f).Within(0.0001f));
            Assert.That(crop.width, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(crop.height, Is.EqualTo(0.5625f).Within(0.0001f));
        }

        [Test]
        public void BalancedRecognitionSeparatesRedAndOrangeUnderWarmLighting()
        {
            var expected = CubeState.Solved(3);
            expected.Apply(MoveNotation.Parse("R U F2 L' D B R2 U'", 3));
            var samples = SamplesFor(expected);
            Color warmShadowOrange = Color.Lerp(Centers[(int)Face.L], Centers[(int)Face.R], 0.28f);

            for (int face = 0; face < 6; face++)
                for (int cell = 0; cell < 9; cell++)
                {
                    if (cell == 4) continue;
                    byte expectedColor = expected.Get((Face)face, cell / 3, cell % 3);
                    if (expectedColor == (byte)Face.R) samples[face][cell] = warmShadowOrange;
                }

            var capturedCenters = new Color[6];
            for (int face = 0; face < 6; face++) capturedCenters[face] = samples[face][4];
            Assert.AreEqual((byte)Face.L,
                CubeColorRecognizer.NearestCenter(warmShadowOrange, capturedCenters),
                "An isolated warm/dark orange sample is intentionally ambiguous with red");

            CubeState recognized = CubeColorRecognizer.BuildState(samples);
            Assert.IsTrue(expected.SameAs(recognized),
                "Whole-cube balancing should still recover all red and orange stickers");

            var counts = new int[6];
            foreach (byte color in recognized.Facelets) counts[color]++;
            for (int color = 0; color < 6; color++) Assert.AreEqual(9, counts[color]);
        }

        [Test]
        public void PhysicalReferenceColorsDoNotDependOnVisualSkin()
        {
            Color[] references = CubeColorRecognizer.PhysicalReferenceColors();
            Assert.AreEqual(6, references.Length);
            Assert.AreEqual((byte)Face.L,
                CubeColorRecognizer.NearestCenter(new Color(0.82f, 0.05f, 0.04f), references));
            Assert.AreEqual((byte)Face.R,
                CubeColorRecognizer.NearestCenter(new Color(0.98f, 0.29f, 0.02f), references));
        }

        [Test]
        public void PhysicalCenterColorIdentifiesTheFaceBeforeCapture()
        {
            Color[] references = CubeColorRecognizer.PhysicalReferenceColors();
            for (int face = 0; face < 6; face++)
                Assert.AreEqual((Face)face, CubeColorRecognizer.DetectPhysicalFace(references[face]));

            Assert.AreEqual(Face.L,
                CubeColorRecognizer.DetectPhysicalFace(new Color(0.78f, 0.04f, 0.03f)));
            Assert.AreEqual(Face.R,
                CubeColorRecognizer.DetectPhysicalFace(new Color(0.96f, 0.27f, 0.01f)));
            Assert.AreEqual(Face.F,
                CubeColorRecognizer.DetectPhysicalFace(new Color(0.07f, 0.22f, 0.10f)),
                "A dim green centre must not be mistaken for white");
            Assert.AreEqual(Face.D,
                CubeColorRecognizer.DetectPhysicalFace(new Color(0.48f, 0.47f, 0.46f)));
        }

        [Test]
        public void FrameStabilityIgnoresExposureButRejectsSceneChange()
        {
            var steady = new Color[9];
            var darker = new Color[9];
            var changed = new Color[9];
            for (int i = 0; i < 9; i++)
            {
                steady[i] = new Color(0.90f, 0.18f, 0.03f);
                darker[i] = new Color(0.68f, 0.136f, 0.022f);
                changed[i] = new Color(0.03f, 0.25f, 0.82f);
            }

            Assert.Less(CubeColorRecognizer.FrameDifference(steady, darker), 0.055f);
            Assert.Greater(CubeColorRecognizer.FrameDifference(steady, changed), 0.055f);
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
