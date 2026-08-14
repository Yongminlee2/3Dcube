using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cube.Core;
using Cube.App;

namespace Cube.App.Tests
{
    public class ColorInputTests
    {
        GameObject _boot;
        ScreenRouter _router;
        ColorInputScreen _input;
        string _path;

        [SetUp]
        public void SetUp()
        {
            AppSettings.AnimationMs = 0;
            AppSettings.CubeSize = 3;
            _path = Path.Combine(Application.temporaryCachePath, "colorinput-test.json");
            if (File.Exists(_path)) File.Delete(_path);
            AppBootstrap.StorePathOverride = _path;

            _boot = new GameObject("AppBootstrap");
            _boot.AddComponent<AppBootstrap>();
            _router = AppBootstrap.Instance.Router;
            _input = _router.ColorInput;
        }

        [TearDown]
        public void TearDown()
        {
            AppSettings.AnimationMs = 120;
            AppBootstrap.StorePathOverride = null;
            if (_boot != null) Object.DestroyImmediate(_boot);
            if (File.Exists(_path)) File.Delete(_path);
        }

        [Test]
        public void 처음에는_완성_상태에서_시작한다()
        {
            Assert.IsTrue(_input.Current.IsSolved());
        }

        [Test]
        public void 가운데_칸은_바꿀_수_없다()
        {
            byte before = _input.Current.Get(Face.U, 1, 1);
            _input.SelectColor((byte)Face.D);
            _input.Paint(Face.U, 1, 1);
            Assert.AreEqual(before, _input.Current.Get(Face.U, 1, 1), "가운데는 색 기준이라 고정이다");
        }

        [Test]
        public void 고른_색이_칸에_칠해진다()
        {
            _input.SelectColor((byte)Face.R);
            _input.Paint(Face.U, 0, 0);
            Assert.AreEqual((byte)Face.R, _input.Current.Get(Face.U, 0, 0));
        }

        [Test]
        public void 말이_안_되는_배치는_이유와_함께_거부된다()
        {
            _input.SelectColor((byte)Face.R);
            _input.Paint(Face.U, 0, 0);          // 색 개수가 깨진다

            Assert.IsFalse(_input.TryAccept(out string error));
            Assert.IsNotEmpty(error, "왜 거부됐는지 알려주지 않으면 고칠 수 없다");
        }

        [UnityTest]
        public IEnumerator 제대로_넣은_큐브는_연습_화면으로_실린다()
        {
            // 손으로 한 칸씩 넣는 대신, 실제로 가능한 배치를 통째로 채운다.
            var real = CubeState.Solved(3);
            real.Apply(MoveNotation.Parse("R U R' U' F2 L D", 3));

            _input.ResetToSolved();
            for (int f = 0; f < 6; f++)
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        if (row == 1 && col == 1) continue;
                        _input.SelectColor(real.Get((Face)f, row, col));
                        _input.Paint((Face)f, row, col);
                    }

            Assert.IsTrue(_input.TryAccept(out string error), $"거부됐다: {error}");
            yield return null;

            Assert.AreEqual(ScreenId.Practice, _router.Current, "연습 화면으로 넘어가야 한다");
            Assert.IsTrue(real.SameAs(_router.Practice.Renderer.State), "넣은 색 그대로 실려야 한다");
        }

        [UnityTest]
        public IEnumerator 실은_큐브에서도_힌트는_설명만_한다()
        {
            var real = CubeState.Solved(3);
            real.Apply(MoveNotation.Parse("R U R' U' F2 L D", 3));

            _router.StartPractice(3);
            _router.Practice.LoadState(real);
            yield return null;

            var before = _router.Practice.Renderer.State.Clone();
            _router.Practice.ShowHint();
            _router.Practice.FollowHint();
            yield return null;

            Assert.IsTrue(before.SameAs(_router.Practice.Renderer.State),
                "실은 큐브에서도 힌트가 상태를 바꾸면 안 된다");
        }

        [Test]
        public void 처음부터를_누르면_완성_상태로_돌아간다()
        {
            _input.SelectColor((byte)Face.R);
            _input.Paint(Face.U, 0, 0);
            _input.ResetToSolved();
            Assert.IsTrue(_input.Current.IsSolved());
        }

        [Test]
        public void 카메라로_읽은_여섯_면이_현재_큐브에_적용된다()
        {
            var expected = CubeState.Solved(3);
            expected.Apply(MoveNotation.Parse("R U R' U' F2 L D B2", 3));
            Color[][] samples = CubeColorRecognizerTests.SamplesFor(expected);

            for (int face = 0; face < 6; face++)
                _input.ApplyScannedFace((Face)face, samples[face]);

            Assert.AreEqual(6, _input.CapturedFaceCount);
            Assert.IsTrue(expected.SameAs(_input.Current));
        }

        [Test]
        public void CapturedFacePreviewPreservesRawSampleColors()
        {
            var samples = new[]
            {
                new Color(0.91f, 0.12f, 0.08f), new Color(0.95f, 0.42f, 0.06f), new Color(0.08f, 0.63f, 0.25f),
                new Color(0.12f, 0.31f, 0.88f), new Color(0.96f, 0.78f, 0.02f), new Color(0.91f, 0.79f, 0.08f),
                new Color(0.73f, 0.16f, 0.31f), new Color(0.22f, 0.54f, 0.69f), new Color(0.44f, 0.38f, 0.21f),
            };

            _input.ApplyScannedFace(Face.U, samples);

            var field = typeof(ColorInputScreen).GetField("_facePreviewCells",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var previews = (UnityEngine.UI.Image[,])field.GetValue(_input);
            AssertColor(samples[0], previews[1, 0].color);
            AssertColor(samples[4], previews[1, 4].color);
            AssertColor(samples[8], previews[1, 8].color);
        }

        [Test]
        public void WrongCenterWarningCanBeConfirmedWhenCameraMisreadsTheColor()
        {
            Color[] references = CubeColorRecognizer.PhysicalReferenceColors();
            var samples = new Color[9];
            for (int cell = 0; cell < samples.Length; cell++)
                samples[cell] = references[(int)Face.U];

            typeof(ColorInputScreen).GetField("_liveSamples",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_input, samples);
            typeof(ColorInputScreen).GetField("_sampleHistoryCount",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_input, 5);
            var capture = typeof(ColorInputScreen).GetMethod("CaptureCurrentFace",
                BindingFlags.Instance | BindingFlags.NonPublic);

            capture.Invoke(_input, null);
            Assert.AreEqual(0, _input.CapturedFaceCount);
            capture.Invoke(_input, null);
            Assert.AreEqual(1, _input.CapturedFaceCount);
        }

        static void AssertColor(Color expected, Color actual)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator CameraGuideAndDetectionGridAreSquare()
        {
            _router.Go(ScreenId.ColorInput);
            yield return null;
            Canvas.ForceUpdateCanvases();

            var card = _input.transform.Find("ScanMode/CameraSlot/CameraCard") as RectTransform;
            var grid = _input.transform.Find("ScanMode/CameraSlot/CameraCard/DetectionGrid") as RectTransform;
            Assert.IsNotNull(card);
            Assert.IsNotNull(grid);
            Assert.That(card.rect.width, Is.EqualTo(card.rect.height).Within(1f));
            Assert.That(grid.rect.width, Is.EqualTo(grid.rect.height).Within(1f));

            var fitter = card.GetComponent<UnityEngine.UI.AspectRatioFitter>();
            Assert.IsNotNull(fitter);
            Assert.That(fitter.aspectRatio, Is.EqualTo(1f).Within(0.0001f));

            var target = _input.transform.Find(
                "ScanMode/CameraSlot/CameraCard/TargetColorBanner/Label")
                .GetComponent<UnityEngine.UI.Text>();
            Assert.IsNotNull(target);
            Assert.That(target.fontSize, Is.GreaterThanOrEqualTo(21));
            StringAssert.Contains("앞면", target.text);
            StringAssert.Contains("초록색", target.text);

            var orientation = _input.transform.Find(
                "ScanMode/CameraSlot/CameraCard/OrientationGuide/Label")
                .GetComponent<UnityEngine.UI.Text>();
            Assert.IsNotNull(orientation);
            StringAssert.Contains("빨강", orientation.text);
            StringAssert.Contains("주황", orientation.text);
        }

        [Test]
        public void CaptureOrderStartsFromFrontAndKeepsLeftAndRightDistinct()
        {
            var orderField = typeof(ColorInputScreen).GetField("CaptureOrder",
                BindingFlags.Static | BindingFlags.NonPublic);
            var order = (Face[])orderField.GetValue(null);
            CollectionAssert.AreEqual(
                new[] { Face.F, Face.U, Face.D, Face.L, Face.R, Face.B }, order);
        }
    }
}
