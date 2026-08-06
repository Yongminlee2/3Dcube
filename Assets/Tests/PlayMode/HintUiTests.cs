using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cube.Core;
using Cube.App;

namespace Cube.App.Tests
{
    public class HintUiTests
    {
        GameObject _boot;
        ScreenRouter _router;
        string _path;

        [SetUp]
        public void SetUp()
        {
            AppSettings.AnimationMs = 0;
            AppSettings.CubeSize = 3;
            _path = Path.Combine(Application.temporaryCachePath, "hintui-test.json");
            if (File.Exists(_path)) File.Delete(_path);
            AppBootstrap.StorePathOverride = _path;

            _boot = new GameObject("AppBootstrap");
            _boot.AddComponent<AppBootstrap>();
            _router = AppBootstrap.Instance.Router;
            _router.StartPractice(3);
        }

        [TearDown]
        public void TearDown()
        {
            AppSettings.AnimationMs = 120;
            AppBootstrap.StorePathOverride = null;
            if (_boot != null) Object.DestroyImmediate(_boot);
            if (File.Exists(_path)) File.Delete(_path);
        }

        [UnityTest]
        public IEnumerator 힌트를_따라두면_실제로_진척이_생긴다()
        {
            var practice = _router.Practice;
            practice.Scramble();
            yield return null;

            int before = StageChecker.CurrentStage(practice.Renderer.State);

            // 힌트를 보고 따라두기를 몇 번 반복하면 단계가 올라가야 한다.
            for (int i = 0; i < 40; i++)
            {
                practice.ShowHint();
                practice.FollowHint();
                yield return null;
                if (StageChecker.CurrentStage(practice.Renderer.State) > before) break;
            }

            Assert.Greater(StageChecker.CurrentStage(practice.Renderer.State), before,
                "힌트를 마흔 번 따라뒀는데 단계가 그대로다");
        }

        [UnityTest]
        public IEnumerator 완성_상태에서는_따라둘_수가_없다()
        {
            var practice = _router.Practice;
            practice.ResetCube();
            yield return null;

            practice.ShowHint();
            practice.FollowHint();     // 아무 일도 없어야 한다
            yield return null;

            Assert.IsTrue(practice.Renderer.State.IsSolved());
        }

        [UnityTest]
        public IEnumerator 큐브를_다시_섞으면_들고_있던_힌트가_사라진다()
        {
            var practice = _router.Practice;
            practice.Scramble();
            yield return null;
            practice.ShowHint();

            practice.Scramble();       // 힌트가 가리키던 상태가 사라진다
            yield return null;

            var before = practice.Renderer.State.Clone();
            practice.FollowHint();     // 옛 힌트가 남아 있으면 여기서 큐브가 움직인다
            yield return null;

            Assert.IsTrue(before.SameAs(practice.Renderer.State), "섞은 뒤에도 옛 힌트가 살아 있다");
        }

        [UnityTest]
        public IEnumerator 네칸_큐브에서는_힌트를_주지_않는다()
        {
            _router.StartPractice(4);
            yield return null;

            var practice = _router.Practice;
            practice.Scramble();
            yield return null;

            var before = practice.Renderer.State.Clone();
            practice.ShowHint();
            practice.FollowHint();
            yield return null;

            Assert.IsTrue(before.SameAs(practice.Renderer.State), "4x4에서 힌트가 큐브를 움직였다");
        }
    }
}
