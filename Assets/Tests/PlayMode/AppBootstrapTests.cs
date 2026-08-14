using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cube.App;

namespace Cube.App.Tests
{
    public class AppBootstrapTests
    {
        GameObject _go;

        [SetUp]
        public void SetUp()
        {
            CubeProgressStore.ClearAll();
            // 부트스트랩이 진짜 기록 파일을 건드리지 않게 한다.
            AppBootstrap.StorePathOverride =
                System.IO.Path.Combine(Application.temporaryCachePath, "boot-test.json");
            _go = new GameObject("AppBootstrap");
        }

        [TearDown]
        public void TearDown()
        {
            AppBootstrap.StorePathOverride = null;
            if (_go != null) Object.DestroyImmediate(_go);
            CubeProgressStore.ClearAll();
        }

        [UnityTest]
        public IEnumerator 부트스트랩이_카메라와_캔버스와_큐브뿌리를_만든다()
        {
            var boot = _go.AddComponent<AppBootstrap>();
            yield return null;

            Assert.IsNotNull(boot.CubeCamera, "카메라가 없다");
            Assert.IsNotNull(boot.UiCanvas, "캔버스가 없다");
            Assert.IsNotNull(boot.CubeRoot, "큐브 뿌리가 없다");
            Assert.AreSame(boot, AppBootstrap.Instance);
            Assert.GreaterOrEqual(AudioService.RealisticMoveClipCount, 3,
                "CC0 실제 큐브 회전음이 Resources에서 로드되지 않았다");
            Assert.GreaterOrEqual(AudioService.CuteMoveClipCount, 3,
                "말랑 팝 회전음이 생성되지 않았다");
        }

        [UnityTest]
        public IEnumerator 카메라_배경색이_팔레트를_따른다()
        {
            var boot = _go.AddComponent<AppBootstrap>();
            yield return null;

            ThemeService.Apply(dark: true);
            yield return null;
            Assert.AreEqual(ThemeService.Current.Background, boot.CubeCamera.backgroundColor);

            ThemeService.Apply(dark: false);
            yield return null;
            Assert.AreEqual(ThemeService.Current.Background, boot.CubeCamera.backgroundColor);

            ThemeService.Apply(dark: true);
        }

        [Test]
        public void 스킨에는_스티커_색이_여섯_개_있다()
        {
            SkinService.Init();
            Assert.IsNotNull(SkinService.Current, "스킨을 못 불러왔다. Resources에 애셋이 있는지 확인할 것");
            Assert.AreEqual(6, SkinService.Current.StickerColors.Length);
        }

        [Test]
        public void 설정값은_저장되고_다시_읽힌다()
        {
            AppSettings.CubeSize = 4;
            AppSettings.AnimationMs = 60;
            AppSettings.Inspection = true;
            AppSettings.BackgroundMusic = false;
            AppSettings.CubeSound = CubeSoundMode.Realistic;
            Assert.AreEqual(4, AppSettings.CubeSize);
            Assert.AreEqual(60, AppSettings.AnimationMs);
            Assert.IsTrue(AppSettings.Inspection);
            Assert.IsFalse(AppSettings.BackgroundMusic);
            Assert.AreEqual(CubeSoundMode.Realistic, AppSettings.CubeSound);
            Assert.IsTrue(AppSettings.SoundEffects);

            AppSettings.CubeSound = CubeSoundMode.Cute;
            Assert.AreEqual(CubeSoundMode.Cute, AppSettings.CubeSound);
            Assert.IsTrue(AppSettings.SoundEffects);

            AppSettings.SoundEffects = false;
            Assert.AreEqual(CubeSoundMode.Off, AppSettings.CubeSound);
            Assert.IsFalse(AppSettings.SoundEffects);

            AppSettings.CubeSize = 3;
            AppSettings.AnimationMs = 120;
            AppSettings.Inspection = false;
            AppSettings.BackgroundMusic = true;
            AppSettings.SoundEffects = true;
            Assert.AreEqual(CubeSoundMode.Classic, AppSettings.CubeSound);
        }
    }
}
