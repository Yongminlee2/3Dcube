using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
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

        /// 화면에 나오지 않는 한국어. 여기 있는 것만 번역 검사에서 뺀다.
        ///
        /// 목록으로 두는 이유가 있다. 새로 한국어 문장을 넣고 번역을 깜빡하면
        /// 테스트가 실패하고, 그때 "화면에 안 나오는 것"이라고 판단했다면
        /// 여기에 직접 적어야 한다. 조용히 지나가지 않게 하려는 것이다.
        static readonly HashSet<string> NotOnScreen = new HashSet<string>
        {
            // 개발자에게만 보이는 예외 메시지
            "Assets/Resources/CubieMaterial.mat이 없다. ProjectSetup.CreateAssets를 돌릴 것",
            "Assets/Resources/Skins에 스킨 에셋이 없다. ProjectSetup.CreateAssets를 돌릴 것",
            // 디버깅용 ToString
            "유효함", "유효하지 않음: {Reason}",
            // 문장을 조립하는 조각. 완성된 문장이 카탈로그에 있다
            "오른쪽 면", "왼쪽 면", "위쪽 면", "아래쪽 면", "앞면", "뒷면",
            "{face}을 반 바퀴", "{face}을 반시계 방향으로 한 칸", "{face}을 시계 방향으로 한 칸",
            // 유니티 인스펙터 툴팁. 두 줄로 이어 써서 Tooltip( 검사에 안 걸린다
            "손이 자꾸 헛돌면 올리고, 잘 안 돌면 내린다",
        };

        /// 숫자나 이름이 끼어드는 문장은 키로 곧바로 찾을 수 없다.
        /// 실행 중에는 TranslatePatterns가 되읽으므로 대표값으로 따로 확인한다.
        static bool IsInterpolated(string s) => s.Contains("{");

        static string StripComments(string code)
        {
            code = Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline);
            var sb = new StringBuilder();
            foreach (string raw in code.Split('\n'))
            {
                string line = raw;
                int i = line.IndexOf("//");
                while (i >= 0)
                {
                    int quotes = 0;
                    for (int k = 0; k < i; k++) if (line[k] == '"') quotes++;
                    if (quotes % 2 == 0) { line = line.Substring(0, i); break; }
                    i = line.IndexOf("//", i + 2);
                }
                sb.AppendLine(line);
            }
            return sb.ToString();
        }

        /// 소스에 박힌 한국어 문자열이 전부 번역되는지 본다.
        ///
        /// 앞선 테스트들은 LessonData와 카탈로그만 봤다. 그래서 화면이 직접
        /// 조립하는 문장 — 힌트를 눌러야 나오는 안내 같은 것 — 을 놓쳤고,
        /// 실기기에서 영어로 바꿨을 때 한국어가 그대로 나왔다.
        [Test]
        public void 소스에_박힌_한국어가_전부_번역된다()
        {
            string root = Path.Combine(Application.dataPath, "Scripts");
            var literal = new Regex("\\$?@?\"((?:[^\"\\\\\n]|\\\\.)*)\"");
            var skipLine = new Regex(@"Debug\.Log|throw new|Exception\(|Tooltip\(|nameof\(");
            var objectName = new Regex(@"^(Row_|Menu_|Face_|Alg\d|C\d|Skin_|Bar_|Pad_)");

            var missing = new StringBuilder();
            var seen = new HashSet<string>();

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == "LocalizationService.cs") continue;

                foreach (string line in StripComments(File.ReadAllText(file)).Split('\n'))
                {
                    if (skipLine.IsMatch(line)) continue;
                    foreach (Match m in literal.Matches(line))
                    {
                        string s = m.Groups[1].Value.Replace("\\n", "\n").Replace("\\\"", "\"");
                        if (!HasKorean(s) || objectName.IsMatch(s)) continue;
                        if (NotOnScreen.Contains(s) || IsInterpolated(s)) continue;
                        if (!seen.Add(s)) continue;

                        foreach (string locale in Locales)
                        {
                            LocalizationService.SetLanguage(locale);
                            if (HasKorean(LocalizationService.T(s)))
                            {
                                missing.AppendLine($"[{locale}] {Path.GetFileName(file)}: {s}");
                                break;   // 한 언어만 알려도 충분하다
                            }
                        }
                    }
                }
            }

            Assert.IsEmpty(missing.ToString(),
                "화면에 나올 수 있는 한국어인데 번역이 없다.\n"
                + "번역을 넣거나, 화면에 안 나오는 것이면 NotOnScreen에 적을 것:\n" + missing);
        }

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
