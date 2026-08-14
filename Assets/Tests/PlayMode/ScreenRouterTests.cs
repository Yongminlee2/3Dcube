using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
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
        public IEnumerator 홈에서_큐브스킨을_바로_열고_홈으로_돌아온다()
        {
            yield return null;
            var home = AppBootstrap.Instance.UiCanvas.transform.Find("SafeAreaRoot/HomeScreen");
            var skinButton = home?.Find("Menu_스킨")?.GetComponent<Button>();
            Assert.IsNotNull(skinButton, "홈에서 스킨 메뉴를 찾을 수 없다");

            skinButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(ScreenId.Skins, _router.Current);

            Vector3 previewCenter = AppBootstrap.Instance.CubeCamera
                .WorldToViewportPoint(AppBootstrap.Instance.CubeRoot.position);
            Assert.That(previewCenter.x, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(previewCenter.y, Is.InRange(0.66f, 0.68f),
                "스킨 미리보기 큐브는 카드 정중앙에 있어야 한다");

            var back = _router.Skins.transform.Find("Back_큐브 스킨")?.GetComponent<Button>();
            Assert.IsNotNull(back);
            back.onClick.Invoke();
            yield return null;
            Assert.AreEqual(ScreenId.Home, _router.Current);
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

        [UnityTest]
        public IEnumerator 기록_삭제_확인은_화면을_나가면_취소된다()
        {
            _router.Store.Add(3, new SolveRecord { DurationMs = 1234d });
            _router.Go(ScreenId.Records);
            yield return null;

            Button clear = null;
            foreach (var button in _router.Records.GetComponentsInChildren<Button>(true))
                if (button.gameObject.name == "Clear") { clear = button; break; }
            Assert.IsNotNull(clear);

            clear.onClick.Invoke();
            yield return null;
            Assert.AreEqual(1, _router.Store.Records(3).Count);

            _router.Go(ScreenId.Home);
            yield return null;
            _router.Go(ScreenId.Records);
            yield return null;
            clear.onClick.Invoke();
            yield return null;

            Assert.AreEqual(1, _router.Store.Records(3).Count,
                "재진입 후 첫 탭은 삭제 확인만 다시 열어야 한다");
        }

        [UnityTest]
        public IEnumerator 회전_중_연습을_나가도_다음_연습에서_입력이_멈추지_않는다()
        {
            AppSettings.AnimationMs = 250;
            _router.StartPractice(3);
            yield return null;

            var rotator = AppBootstrap.Instance.CubeRoot.GetComponent<LayerRotator>();
            _router.Practice.ApplyMove(new Cube.Core.Move(Cube.Core.Axis.X, 2, 1));
            Assert.IsTrue(rotator.IsAnimating);

            _router.Go(ScreenId.Home);
            yield return null;
            Assert.IsFalse(rotator.IsAnimating,
                "CubeRoot를 숨기기 전에 회전 큐를 마무리해야 한다");

            _router.StartPractice(3);
            yield return null;
            _router.Practice.ApplyMove(new Cube.Core.Move(Cube.Core.Axis.Z, 2, 1));

            float elapsed = 0f;
            while (rotator.IsAnimating && elapsed < 2f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsFalse(rotator.IsAnimating,
                "재진입 후 시작한 회전이 끝나지 않았다");
        }
    }
}
