using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Cube.Core;
using Cube.App;

namespace Cube.App.Tests
{
    public class PracticeScreenTests
    {
        GameObject _boot;
        GameObject _root;
        PracticeScreen _screen;

        [SetUp]
        public void SetUp()
        {
            CubeProgressStore.ClearAll();
            ThemeService.Init();
            AppSettings.AnimationMs = 0;          // 테스트에서는 애니메이션을 끈다
            AppBootstrap.StorePathOverride =
                System.IO.Path.Combine(Application.temporaryCachePath, "practice-test.json");
            _boot = new GameObject("AppBootstrap");
            _boot.AddComponent<AppBootstrap>();
            _root = new GameObject("Root", typeof(RectTransform), typeof(Canvas));
            _screen = new GameObject("Practice").AddComponent<PracticeScreen>();
            _screen.Build((RectTransform)_root.transform, 3);
        }

        [TearDown]
        public void TearDown()
        {
            AppSettings.AnimationMs = 120;
            AppBootstrap.StorePathOverride = null;
            if (_screen != null) Object.DestroyImmediate(_screen.gameObject);
            if (_root != null) Object.DestroyImmediate(_root);
            if (_boot != null) Object.DestroyImmediate(_boot);
            CubeProgressStore.ClearAll();
        }

        [UnityTest]
        public IEnumerator 섞으면_완성이_아니고_스크램블이_남는다()
        {
            _screen.Scramble();
            yield return null;
            Assert.IsFalse(_screen.Renderer.State.IsSolved());
            Assert.IsNotEmpty(_screen.CurrentScramble);
        }

        [UnityTest]
        public IEnumerator 거꾸로_풀면_완성_신호가_온다()
        {
            double ms = -1d; string scramble = null; int moves = -1;
            _screen.Solved += (m, s, c) => { ms = m; scramble = s; moves = c; };

            _screen.Scramble();
            yield return null;

            var applied = MoveNotation.Parse(_screen.CurrentScramble, 3);
            for (int i = applied.Count - 1; i >= 0; i--)
                _screen.ApplyMove(applied[i].Inverse);
            yield return null;

            Assert.IsTrue(_screen.Renderer.State.IsSolved());
            Assert.GreaterOrEqual(ms, 0d, "완성 신호가 오지 않았다");
            Assert.IsNotEmpty(scramble);
            Assert.AreEqual(applied.Count, moves);
        }

        [UnityTest]
        public IEnumerator 섞기는_회전_수에_들어가지_않는다()
        {
            int moves = -1;
            _screen.Solved += (m, s, c) => moves = c;

            _screen.Scramble();
            yield return null;
            var applied = MoveNotation.Parse(_screen.CurrentScramble, 3);
            for (int i = applied.Count - 1; i >= 0; i--)
                _screen.ApplyMove(applied[i].Inverse);
            yield return null;

            // 섞기 20수 + 되돌리기 20수가 아니라, 사용자가 둔 20수만 세야 한다
            Assert.AreEqual(applied.Count, moves);
        }

        [UnityTest]
        public IEnumerator 되돌리기가_한_수를_물린다()
        {
            _screen.ResetCube();
            _screen.ApplyMove(new Move(Axis.X, 0, 1));
            yield return null;
            Assert.IsFalse(_screen.Renderer.State.IsSolved());

            _screen.Undo();
            yield return null;
            Assert.IsTrue(_screen.Renderer.State.IsSolved());
        }

        [UnityTest]
        public IEnumerator 초기화하면_완성_상태로_돌아간다()
        {
            _screen.Scramble();
            yield return null;
            _screen.ResetCube();
            yield return null;
            Assert.IsTrue(_screen.Renderer.State.IsSolved());
            Assert.IsEmpty(_screen.CurrentScramble);
        }

        [UnityTest]
        public IEnumerator 뒤로갔다_다시_열면_맞추던_상태를_이어간다()
        {
            _screen.Scramble();
            yield return null;
            var expected = _screen.Renderer.State.Clone();
            string scramble = _screen.CurrentScramble;

            Object.DestroyImmediate(_screen.gameObject);
            _screen = new GameObject("PracticeReloaded").AddComponent<PracticeScreen>();
            _screen.Build((RectTransform)_root.transform, 3);
            yield return null;

            Assert.IsTrue(expected.SameAs(_screen.Renderer.State));
            Assert.AreEqual(scramble, _screen.CurrentScramble);
        }

        [UnityTest]
        public IEnumerator 촬영한_실물큐브도_다시_열면_그대로_이어간다()
        {
            var photographed = CubeState.Solved(3);
            photographed.Apply(new Move(Axis.X, 0, 1));
            photographed.Apply(new Move(Axis.Z, 2, 3));
            _screen.LoadState(photographed);
            yield return null;

            Object.DestroyImmediate(_screen.gameObject);
            _screen = new GameObject("RealCubeReloaded").AddComponent<PracticeScreen>();
            _screen.Build((RectTransform)_root.transform, 3);
            yield return null;

            Assert.IsTrue(photographed.SameAs(_screen.Renderer.State));
        }

        [UnityTest]
        public IEnumerator 하단_섞기와_초기화는_확인한_뒤에만_실행된다()
        {
            var shuffle = _screen.transform.Find("Bar/Bar_섞기").GetComponent<Button>();
            var overlay = _screen.transform.Find("ConfirmOverlay").gameObject;

            shuffle.onClick.Invoke();
            yield return null;
            Assert.IsTrue(overlay.activeSelf);
            Assert.IsTrue(_screen.Renderer.State.IsSolved(), "확인 전에는 섞이면 안 된다");

            overlay.transform.Find("ConfirmCard/ConfirmCancel").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(overlay.activeSelf);
            Assert.IsTrue(_screen.Renderer.State.IsSolved());

            shuffle.onClick.Invoke();
            overlay.transform.Find("ConfirmCard/ConfirmAccept").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsFalse(_screen.Renderer.State.IsSolved());
            var scrambled = _screen.Renderer.State.Clone();

            var reset = _screen.transform.Find("Bar/Bar_초기화").GetComponent<Button>();
            reset.onClick.Invoke();
            yield return null;
            Assert.IsTrue(scrambled.SameAs(_screen.Renderer.State), "확인 전에는 초기화되면 안 된다");

            overlay.transform.Find("ConfirmCard/ConfirmAccept").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.IsTrue(_screen.Renderer.State.IsSolved());
        }

        [UnityTest]
        public IEnumerator 인스펙션을_켜면_섞은_뒤_카운트다운이_돈다()
        {
            AppSettings.Inspection = true;
            _screen.Scramble();
            yield return null;
            Assert.AreEqual(TimerPhase.Inspection, _screen.Timer.Phase);

            _screen.ApplyMove(new Move(Axis.X, 0, 1));
            yield return null;
            Assert.AreEqual(TimerPhase.Running, _screen.Timer.Phase);

            AppSettings.Inspection = false;
        }
    }
}
