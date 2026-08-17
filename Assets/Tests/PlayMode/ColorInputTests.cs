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
            CubeProgressStore.ClearAll();
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
            CubeProgressStore.ClearAll();
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

            _router.Go(ScreenId.Home);
            yield return null;
            _router.StartPractice(3);
            yield return null;
            Assert.IsTrue(real.SameAs(_router.Practice.Renderer.State),
                "촬영한 큐브도 다시 들어왔을 때 이어져야 한다");
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

        /// 미리보기는 찍힌 색이 아니라 「앱이 무슨 색으로 읽었는지」를 보여 준다.
        /// 원본 표본을 그대로 띄우면 조명 탓에 위 방향 안내의 기준색과 달라 보여,
        /// 제대로 읽힌 건지 사람이 판단할 수가 없었다.
        [Test]
        public void CapturedFacePreviewShowsTheRecognizedColor()
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
            Color[] references = CubeColorRecognizer.PhysicalReferenceColors();

            // 어느 칸이든 실물 큐브의 여섯 색 중 하나여야 한다.
            for (int cell = 0; cell < 9; cell++)
            {
                Color shown = previews[1, cell].color;
                bool known = false;
                foreach (Color reference in references)
                    if (Mathf.Abs(reference.r - shown.r) < 0.001f
                        && Mathf.Abs(reference.g - shown.g) < 0.001f
                        && Mathf.Abs(reference.b - shown.b) < 0.001f) known = true;
                Assert.IsTrue(known, $"{cell}번 칸에 큐브 색이 아닌 색이 떴다: {shown}");
            }

            // 뚜렷한 두 칸은 어느 색으로 읽혀야 하는지 정해져 있다.
            AssertColor(references[(int)Face.L], previews[1, 0].color);   // 선명한 빨강
            AssertColor(references[(int)Face.U], previews[1, 4].color);   // 가운데 노랑
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

            // 방향 안내는 글이 아니라 색 칩으로 보여 준다. 앞면을 찍는 동안
            // 둘레 네 면과 등 뒤 면에 와야 하는 색이 그대로 떠 있어야 한다.
            var expected = new[]
            {
                ("위", Face.U, "노란색"),
                ("아래", Face.D, "흰색"),
                ("왼쪽", Face.L, "빨간색"),
                ("오른쪽", Face.R, "주황색"),
                ("뒤", Face.B, "파란색"),
            };
            var reference = CubeColorRecognizer.PhysicalReferenceColors();

            for (int i = 0; i < expected.Length; i++)
            {
                var (label, face, colorName) = expected[i];
                var column = _input.transform.Find($"ScanMode/OrientationGuide/Guide_{i}");
                Assert.IsNotNull(column, $"{label} 칸이 없다");

                var direction = column.Find("Direction").GetComponent<UnityEngine.UI.Text>();
                Assert.AreEqual(label, direction.text);

                var chip = column.Find("Chip").GetComponent<UnityEngine.UI.Image>();
                TestColors.AssertSame(reference[(int)face], chip.color,
                    $"{label}에 뜬 색이 {face}면 색이 아니다");

                var name = column.Find("Chip/Name").GetComponent<UnityEngine.UI.Text>();
                Assert.AreEqual(colorName, name.text,
                    "색만으로 구분하지 않도록 칩 안에 색 이름도 적어야 한다");
            }
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
