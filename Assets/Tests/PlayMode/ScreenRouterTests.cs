using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cube.App;

namespace Cube.App.Tests
{
    public class ScreenRouterTests
    {
        GameObject _boot;
        ScreenRouter _router;
        string _path;

        [SetUp]
        public void SetUp()
        {
            AppSettings.AnimationMs = 0;
            AppSettings.CubeSize = 3;
            _path = Path.Combine(Application.temporaryCachePath, "router-test.json");
            if (File.Exists(_path)) File.Delete(_path);

            // 부트스트랩이 진짜 저장 경로를 건드리지 않도록 미리 갈아끼운다.
            AppBootstrap.StorePathOverride = _path;
            _boot = new GameObject("AppBootstrap");
            _boot.AddComponent<AppBootstrap>();
            _router = AppBootstrap.Instance.Router;
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
        public IEnumerator 처음에는_홈이_보인다()
        {
            yield return null;
            Assert.AreEqual(ScreenId.Home, _router.Current);
            Assert.IsNull(_router.Practice, "연습 화면은 시작하기 전까지 만들지 않는다");
        }

        [UnityTest]
        public IEnumerator 화면을_옮기면_하나만_보인다()
        {
            _router.StartPractice(3);
            yield return null;
            Assert.AreEqual(ScreenId.Practice, _router.Current);
            Assert.IsTrue(_router.Practice.gameObject.activeSelf);

            _router.Go(ScreenId.Records);
            yield return null;
            Assert.AreEqual(ScreenId.Records, _router.Current);
            Assert.IsFalse(_router.Practice.gameObject.activeSelf);
            Assert.IsTrue(_router.Records.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator 큐브는_연습_화면에서만_보인다()
        {
            _router.StartPractice(3);
            yield return null;
            Assert.IsTrue(AppBootstrap.Instance.CubeRoot.gameObject.activeSelf);

            _router.Go(ScreenId.Home);
            yield return null;
            Assert.IsFalse(AppBootstrap.Instance.CubeRoot.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator 완성하면_기록이_쌓이고_파일로_남는다()
        {
            _router.StartPractice(3);
            yield return null;

            var practice = _router.Practice;
            practice.Scramble();
            yield return null;

            var applied = Cube.Core.MoveNotation.Parse(practice.CurrentScramble, 3);
            for (int i = applied.Count - 1; i >= 0; i--) practice.ApplyMove(applied[i].Inverse);
            yield return null;

            Assert.AreEqual(1, _router.Store.Records(3).Count);
            Assert.IsTrue(File.Exists(_path), "기록 파일이 만들어지지 않았다");

            var reloaded = new SessionStore(_path);
            reloaded.Load();
            Assert.AreEqual(1, reloaded.Records(3).Count);
        }

        [UnityTest]
        public IEnumerator 테마를_바꾸면_카메라_배경이_따라간다()
        {
            _router.Go(ScreenId.Settings);
            yield return null;

            ThemeService.Apply(false);
            yield return null;
            TestColors.AssertSame(ThemeService.Current.Background,
                                  AppBootstrap.Instance.CubeCamera.backgroundColor);

            ThemeService.Apply(true);
            yield return null;
            TestColors.AssertSame(ThemeService.Current.Background,
                                  AppBootstrap.Instance.CubeCamera.backgroundColor);
        }
    }
}
