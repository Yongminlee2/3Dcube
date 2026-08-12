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
        public IEnumerator 힌트와_안내카드를_눌러도_큐브는_움직이지_않는다()
        {
            var practice = _router.Practice;
            practice.Scramble();
            yield return null;

            var before = practice.Renderer.State.Clone();
            practice.ShowHint();
            practice.FollowHint();
            yield return null;

            Assert.IsTrue(before.SameAs(practice.Renderer.State),
                "힌트는 설명만 하고 큐브를 대신 돌리면 안 된다");
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
