using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
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

        [UnityTest]
        public IEnumerator 연습_카드와_하단버튼은_같은_규격과_읽기좋은_글자를_쓴다()
        {
            yield return null;

            var root = _router.Practice.transform;
            var net = root.Find("NetCard") as RectTransform;
            var hint = root.Find("Hint") as RectTransform;
            var bar = root.Find("Bar") as RectTransform;
            Assert.NotNull(net);
            Assert.NotNull(hint);
            Assert.NotNull(bar);
            Assert.AreEqual(0.05f, net.anchorMin.x, 0.001f);
            Assert.AreEqual(0.95f, net.anchorMax.x, 0.001f);
            Assert.AreEqual(net.anchorMin.x, hint.anchorMin.x, 0.001f);
            Assert.AreEqual(net.anchorMax.x, hint.anchorMax.x, 0.001f);
            Assert.AreEqual(net.anchorMin.x, bar.anchorMin.x, 0.001f);
            Assert.AreEqual(net.anchorMax.x, bar.anchorMax.x, 0.001f);
            Assert.LessOrEqual(bar.anchorMax.y - bar.anchorMin.y, 0.06f,
                "하단 버튼 바가 글자보다 지나치게 크다");

            var pad = root.Find("Pad") as RectTransform;
            Assert.NotNull(pad);
            Assert.LessOrEqual(pad.anchorMax.y - pad.anchorMin.y, 0.075f,
                "3x3 노테이션 패드가 한 줄보다 크게 자리를 차지한다");
            Assert.IsNull(root.Find("Pad/Pad_Double"), "2회 토글은 제거되어야 한다");
            Assert.IsNull(root.Find("Pad/Pad_Wide"), "넓은 수 토글은 제거되어야 한다");
            Assert.NotNull(root.Find("Pad/Pad_Prime"), "반시계 입력은 남아 있어야 한다");

            var label = root.Find("Bar/Bar_섞기/Label")?.GetComponent<Text>();
            var explanation = root.Find("Hint/HintExplanation")?.GetComponent<Text>();
            Assert.NotNull(label);
            Assert.NotNull(explanation);
            Assert.GreaterOrEqual(label.fontSize, 24);
            Assert.GreaterOrEqual(explanation.resizeTextMaxSize, 23);
        }
    }
}
