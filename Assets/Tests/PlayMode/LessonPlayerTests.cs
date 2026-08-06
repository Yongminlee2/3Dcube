using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cube.Core;
using Cube.App;

namespace Cube.App.Tests
{
    public class LessonPlayerTests
    {
        const string Sexy = "R U R' U'";

        GameObject _go;
        CubeRenderer _renderer;
        LayerRotator _rotator;
        TouchController _touch;
        LessonPlayer _player;

        [SetUp]
        public void SetUp()
        {
            ThemeService.Init();
            AppSettings.AnimationMs = 0;
            _go = new GameObject("Cube");
            _renderer = _go.AddComponent<CubeRenderer>();
            _rotator = _go.AddComponent<LayerRotator>();
            _touch = _go.AddComponent<TouchController>();   // Init을 안 하면 Update가 바로 빠져나간다
            _player = _go.AddComponent<LessonPlayer>();

            _renderer.Build(CubeState.Solved(3));
            _rotator.Init(_renderer);
            _player.Init(_renderer, _rotator, _touch);
        }

        [TearDown]
        public void TearDown()
        {
            AppSettings.AnimationMs = 120;
            if (_go != null) Object.DestroyImmediate(_go);
        }

        IEnumerator WaitDone()
        {
            float t = 0f;
            while (_player.IsPlaying && t < 5f) { t += Time.deltaTime; yield return null; }
            Assert.IsFalse(_player.IsPlaying, "시연이 끝나지 않았다");
        }

        [UnityTest]
        public IEnumerator 재생하면_공식을_적용한_결과와_같아진다()
        {
            var expected = CubeState.Solved(3);
            expected.Apply(MoveNotation.Parse(Sexy, 3));

            _player.Play(Sexy);
            yield return WaitDone();

            Assert.IsTrue(expected.SameAs(_renderer.State));
        }

        [UnityTest]
        public IEnumerator 되돌리면_시연_전으로_돌아간다()
        {
            _player.Play("R U R' U' F R U R' U' F'");
            yield return WaitDone();
            Assert.IsFalse(_renderer.State.IsSolved());

            _player.Rewind();
            yield return WaitDone();
            Assert.IsTrue(_renderer.State.IsSolved(), "되돌렸는데 원래 상태가 아니다");
        }

        [UnityTest]
        public IEnumerator 한_수씩_보면_그만큼만_진행된다()
        {
            _player.Load(Sexy);
            Assert.AreEqual(4, _player.SequenceLength);
            Assert.AreEqual(0, _player.PlayedCount);

            _player.StepOnce();
            yield return WaitDone();
            Assert.AreEqual(1, _player.PlayedCount);

            var afterOne = CubeState.Solved(3);
            afterOne.Apply(MoveNotation.Parse("R", 3));
            Assert.IsTrue(afterOne.SameAs(_renderer.State), "첫 수만 적용돼 있어야 한다");

            while (_player.HasMoreSteps)
            {
                _player.StepOnce();
                yield return WaitDone();
            }

            var all = CubeState.Solved(3);
            all.Apply(MoveNotation.Parse(Sexy, 3));
            Assert.IsTrue(all.SameAs(_renderer.State));
            Assert.AreEqual(4, _player.PlayedCount);
        }

        [UnityTest]
        public IEnumerator 한_수씩_본_뒤에도_되돌릴_수_있다()
        {
            _player.Load(Sexy);
            _player.StepOnce();
            yield return WaitDone();
            _player.StepOnce();
            yield return WaitDone();

            _player.Rewind();
            yield return WaitDone();
            Assert.IsTrue(_renderer.State.IsSolved());
        }

        [UnityTest]
        public IEnumerator 시연_중에는_손가락_입력이_막히고_끝나면_풀린다()
        {
            Assert.IsTrue(_touch.Enabled, "처음에는 열려 있어야 한다");

            _player.Play(Sexy);
            Assert.IsFalse(_touch.Enabled, "시연 중에는 막혀야 한다");

            yield return WaitDone();
            Assert.IsTrue(_touch.Enabled, "끝났으면 다시 열려야 한다");
        }

        [UnityTest]
        public IEnumerator 끝나면_알림이_온다()
        {
            int calls = 0;
            _player.Finished += () => calls++;

            _player.Play(Sexy);
            yield return WaitDone();
            Assert.AreEqual(1, calls);

            _player.Rewind();
            yield return WaitDone();
            Assert.AreEqual(2, calls);
        }

        [UnityTest]
        public IEnumerator 코스의_모든_공식을_시연하고_되돌릴_수_있다()
        {
            foreach (var lesson in LessonData.Lessons)
                foreach (var alg in lesson.Algorithms)
                {
                    _renderer.Build(CubeState.Solved(3));
                    _rotator.Init(_renderer);

                    _player.Play(alg.Notation);
                    yield return WaitDone();
                    Assert.IsFalse(_renderer.State.IsSolved(), $"'{alg.Name}'이 아무것도 안 바꿨다");

                    _player.Rewind();
                    yield return WaitDone();
                    Assert.IsTrue(_renderer.State.IsSolved(), $"'{alg.Name}' 되돌리기가 실패했다");
                }
        }

        [UnityTest]
        public IEnumerator 되돌릴_것이_없으면_아무_일도_없다()
        {
            _player.Rewind();
            yield return null;
            Assert.IsTrue(_renderer.State.IsSolved());
            Assert.IsFalse(_player.IsPlaying);
        }
    }
}
