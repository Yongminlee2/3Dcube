using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Cube.App.Tests
{
    public sealed class LocalizationServiceTests
    {
        string _previousLanguage;

        [SetUp]
        public void SetUp()
        {
            _previousLanguage = AppSettings.LanguageCode;
            LocalizationService.Init();
        }

        [TearDown]
        public void TearDown()
        {
            LocalizationService.SetLanguage(_previousLanguage);
        }

        [Test]
        public void SupportsSameFourteenLocalesAsPiyakApps()
        {
            Assert.AreEqual(14, LocalizationService.Supported.Count);
            var codes = new HashSet<string>();
            foreach (var locale in LocalizationService.Supported)
            {
                Assert.IsTrue(codes.Add(locale.Code), locale.Code);
                Assert.IsFalse(string.IsNullOrWhiteSpace(locale.NativeName), locale.Code);
            }
            CollectionAssert.Contains(codes, "zh-TW");
            CollectionAssert.Contains(codes, "zh-HK");
        }

        [Test]
        public void SelectedLanguagePersistsAndTranslatesCoreNavigation()
        {
            LocalizationService.SetLanguage("es-MX");
            Assert.AreEqual("es", LocalizationService.CurrentCode);
            Assert.AreEqual("Ajustes", LocalizationService.T("설정"));
            Assert.AreEqual("Empezar práctica", LocalizationService.T("연습 시작"));
            Assert.AreEqual("es", AppSettings.LanguageCode);
        }

        [Test]
        public void TraditionalChineseRegionsRemainDistinct()
        {
            LocalizationService.SetLanguage("zh-TW");
            Assert.AreEqual("zh-TW", LocalizationService.CurrentCode);
            Assert.AreEqual("繁體中文（台灣）", LocalizationService.CurrentName);

            LocalizationService.SetLanguage("zh-HK");
            Assert.AreEqual("zh-HK", LocalizationService.CurrentCode);
            Assert.AreEqual("繁體中文（香港）", LocalizationService.CurrentName);
        }

        [Test]
        public void UnknownLocaleFallsBackToEnglish()
        {
            LocalizationService.SetLanguage("ar");
            Assert.AreEqual("en", LocalizationService.CurrentCode);
            Assert.AreEqual("Settings", LocalizationService.T("설정"));
        }

        [Test]
        public void SystemDefaultUsesEmptyPersistedOverride()
        {
            LocalizationService.SetLanguage("");
            Assert.IsTrue(LocalizationService.UsesSystemLanguage);
            Assert.AreEqual("", AppSettings.LanguageCode);
            Assert.IsFalse(string.IsNullOrWhiteSpace(LocalizationService.CurrentCode));
        }

        [Test]
        public void EveryLocaleTranslatesCoreScreenLabels()
        {
            string[] keys = { "설정", "연습 시작", "배우기", "기록", "큐브 자동 인식", "큐브 스킨", "힌트" };
            foreach (var locale in LocalizationService.Supported)
            {
                LocalizationService.SetLanguage(locale.Code);
                foreach (string key in keys)
                {
                    string value = LocalizationService.T(key);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"{locale.Code}: {key}");
                    if (locale.Code != "ko") Assert.AreNotEqual(key, value, $"{locale.Code}: {key}");
                }
            }
        }

        [Test]
        public void DetailedCoachCopyFallsBackToEnglish()
        {
            LocalizationService.SetLanguage("ja");
            Assert.AreEqual("Hold the cube with white on the bottom and keep this orientation.",
                LocalizationService.T("큐브를 흰 면이 아래로 가게 잡습니다. 앞으로 이 방향을 계속 유지합니다."));
        }
    }
}
