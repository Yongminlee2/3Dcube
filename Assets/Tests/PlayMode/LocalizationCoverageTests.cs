using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Cube.Core;
using Cube.App;

namespace Cube.App.Tests
{
    /// 번역이 빠진 채로 출시되는 것을 막는다.
    ///
    /// 이 앱은 한국어 원문을 그대로 키로 쓴다. 그래서 카탈로그에 없는 문장은
    /// 조용히 한국어 그대로 화면에 남고, 영어 폴백에만 있는 문장은 일본어를
    /// 골라도 영어가 남는다. 둘 다 눈으로 보기 전에는 알 수 없어서 테스트로 잡는다.
    public class LocalizationCoverageTests
    {
        static readonly string[] Locales =
        {
            "en", "ja", "zh-CN", "zh-TW", "zh-HK", "es", "fr",
            "de", "pt", "ru", "vi", "id", "th",
        };

        static bool HasKorean(string s)
        {
            for (int i = 0; i < s.Length; i++)
                if (s[i] >= '가' && s[i] <= '힣') return true;
            return false;
        }

        [TearDown]
        public void TearDown() => LocalizationService.SetLanguage("ko");

        /// 화면에서 실제로 읽게 되는 학습 콘텐츠 전부.
        /// 새 단계나 공식을 추가하고 번역을 빠뜨리면 여기서 걸린다.
        static IEnumerable<string> LessonCopy()
        {
            for (int stage = 1; stage <= StageChecker.LastStage; stage++)
            {
                var lesson = LessonData.Get(stage);
                yield return lesson.Title;
                foreach (var step in lesson.Steps) yield return step;
                foreach (var alg in lesson.Algorithms)
                {
                    yield return alg.Name;
                    yield return alg.When;
                }
            }
            foreach (var alg in LessonData.Library)
            {
                yield return alg.Name;
                yield return alg.When;
            }
        }

        /// 스킨 이름은 .asset 파일에 들어 있어 소스 코드만 훑어서는 놓친다.
        /// 실제로 일본어로 바꿨을 때 "Classic"이 영어로 남아 있던 자리다.
        [Test]
        public void 스킨_이름은_모든_언어에서_한글이_남지_않는다()
        {
            SkinService.Init();
            var skins = SkinService.All;
            Assert.Greater(skins.Length, 0, "스킨을 불러오지 못했다");

            var bad = new StringBuilder();
            foreach (string locale in Locales)
            {
                LocalizationService.SetLanguage(locale);
                foreach (var skin in skins)
                {
                    string translated = LocalizationService.T(skin.DisplayName);
                    if (HasKorean(translated))
                        bad.AppendLine($"[{locale}] {skin.name}: {skin.DisplayName}");
                }
            }

            Assert.IsEmpty(bad.ToString(), "스킨 이름 번역이 없다:\n" + bad);
        }

        [Test]
        public void 학습_콘텐츠는_모든_언어에서_한글이_남지_않는다()
        {
            var missing = new StringBuilder();
            foreach (string locale in Locales)
            {
                LocalizationService.SetLanguage(locale);
                foreach (string source in LessonCopy())
                {
                    if (string.IsNullOrWhiteSpace(source)) continue;
                    string translated = LocalizationService.T(source);
                    if (HasKorean(translated))
                        missing.AppendLine($"[{locale}] {source}");
                }
            }

            Assert.IsEmpty(missing.ToString(),
                "번역이 없어 한국어가 그대로 나오는 문장이 있다:\n" + missing);
        }

        [Test]
        public void 카탈로그에_등록된_문장은_모든_언어에서_한글이_남지_않는다()
        {
            // 카탈로그 키 자체를 되짚어 본다. 어느 한 칸을 비워 두거나 한국어를
            // 그대로 복사해 두면(번역을 깜빡하면) 여기서 걸린다.
            var keys = new List<string>(LocalizationService.CatalogKeysForTests);
            Assert.Greater(keys.Count, 100, "카탈로그를 읽지 못했다");

            var bad = new StringBuilder();
            foreach (string locale in Locales)
            {
                LocalizationService.SetLanguage(locale);
                foreach (string key in keys)
                {
                    if (!HasKorean(key)) continue;   // 고유명사 등은 건너뛴다
                    string translated = LocalizationService.T(key);
                    if (HasKorean(translated)) bad.AppendLine($"[{locale}] {key}");
                }
            }

            Assert.IsEmpty(bad.ToString(), "번역 칸이 비었거나 한국어가 남아 있다:\n" + bad);
        }

        [Test]
        public void 언어를_바꾸면_실제로_그_언어가_나온다()
        {
            LocalizationService.SetLanguage("ja");
            Assert.AreEqual("設定", LocalizationService.T("설정"));

            LocalizationService.SetLanguage("de");
            Assert.AreEqual("Einstellungen", LocalizationService.T("설정"));

            LocalizationService.SetLanguage("ko");
            Assert.AreEqual("설정", LocalizationService.T("설정"));
        }

        /// 숫자가 끼어드는 문장은 카탈로그 키로 곧바로 찾을 수 없어 패턴으로 되읽는다.
        /// 그 경로가 살아 있는지 대표값으로 확인한다.
        [Test]
        public void 숫자가_끼어든_문장도_번역된다()
        {
            LocalizationService.SetLanguage("de");
            foreach (string sample in new[]
            {
                "3×3 연습",
                "0 / 7 완료",
                "3×3 · 기록 5개",
                "1단계 · 흰 십자",
                "1/6  위면 촬영 · 가운데 노란색",
            })
            {
                string translated = LocalizationService.T(sample);
                Assert.IsFalse(HasKorean(translated),
                    $"'{sample}' 가 번역되지 않았다 -> '{translated}'");
            }
        }
    }
}
