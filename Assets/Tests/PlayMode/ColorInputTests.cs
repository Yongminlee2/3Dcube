using System.Collections;
using System.IO;
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
        public IEnumerator 실은_큐브에서_힌트를_받을_수_있다()
        {
            var real = CubeState.Solved(3);
            real.Apply(MoveNotation.Parse("R U R' U' F2 L D", 3));

            _router.StartPractice(3);
            _router.Practice.LoadState(real);
            yield return null;

            int before = StageChecker.CurrentStage(_router.Practice.Renderer.State);
            for (int i = 0; i < 40; i++)
            {
                _router.Practice.ShowHint();
                _router.Practice.FollowHint();
                yield return null;
                if (StageChecker.CurrentStage(_router.Practice.Renderer.State) > before) break;
            }

            Assert.Greater(StageChecker.CurrentStage(_router.Practice.Renderer.State), before,
                "실은 큐브에서 힌트가 진척을 못 만든다");
        }

        [Test]
        public void 처음부터를_누르면_완성_상태로_돌아간다()
        {
            _input.SelectColor((byte)Face.R);
            _input.Paint(Face.U, 0, 0);
            _input.ResetToSolved();
            Assert.IsTrue(_input.Current.IsSolved());
        }
    }
}
