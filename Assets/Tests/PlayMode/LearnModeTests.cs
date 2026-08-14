using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cube.Core;
using Cube.App;

namespace Cube.App.Tests
{
    public class LearnModeTests
    {
        GameObject _boot;
        ScreenRouter _router;
        string _path;

        [SetUp]
        public void SetUp()
        {
            CubeProgressStore.ClearAll();
            AppSettings.AnimationMs = 0;
            AppSettings.CubeSize = 3;
            AppSettings.ShowPad = true;
            LearnProgress.Reset();

            _path = Path.Combine(Application.temporaryCachePath, "learn-test.json");
            if (File.Exists(_path)) File.Delete(_path);
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
            LearnProgress.Reset();
            if (_boot != null) Object.DestroyImmediate(_boot);
            if (File.Exists(_path)) File.Delete(_path);
            CubeProgressStore.ClearAll();
        }

        [Test]
        public void 처음에는_일단계만_열려_있다()
        {
            Assert.IsTrue(LearnProgress.IsUnlocked(1));
            for (int stage = 2; stage <= StageChecker.LastStage; stage++)
                Assert.IsFalse(LearnProgress.IsUnlocked(stage), $"{stage}단계가 열려 있다");
        }

        [Test]
        public void 단계를_마치면_다음_단계가_열린다()
        {
            LearnProgress.MarkDone(1);
            Assert.IsTrue(LearnProgress.IsDone(1));
            Assert.IsTrue(LearnProgress.IsUnlocked(2));
            Assert.IsFalse(LearnProgress.IsUnlocked(3));
        }

        [Test]
        public void 앞_단계를_다시_해도_진도가_깎이지_않는다()
        {
            LearnProgress.MarkDone(4);
            LearnProgress.MarkDone(2);
            Assert.AreEqual(4, LearnProgress.Completed);
        }

        [UnityTest]
        public IEnumerator 학습_화면으로_갈_수_있고_큐브가_보인다()
        {
            _router.Go(ScreenId.Learn);
            yield return null;
            Assert.AreEqual(ScreenId.Learn, _router.Current);
            Assert.IsFalse(AppBootstrap.Instance.CubeRoot.gameObject.activeSelf, "학습 홈에서는 큐브를 숨긴다");

            _router.OpenLesson(1);
            yield return null;
            Assert.AreEqual(ScreenId.Lesson, _router.Current);
            Assert.AreEqual(1, _router.Lesson.Stage);
            Assert.IsTrue(AppBootstrap.Instance.CubeRoot.gameObject.activeSelf, "단계 화면에서는 큐브를 보여준다");

            float lessonCubeCenter = AppBootstrap.Instance.CubeCamera
                .WorldToViewportPoint(AppBootstrap.Instance.CubeRoot.position).y;
            float lessonCubeScale = AppBootstrap.Instance.CubeRoot.localScale.x;
            Assert.That(lessonCubeCenter, Is.InRange(0.53f, 0.55f),
                "배우기 큐브 중심은 일반 연습과 같은 높이여야 한다");
            Assert.That(lessonCubeScale, Is.EqualTo(1f).Within(0.001f),
                "배우기 큐브는 일반 연습과 같은 크기여야 한다");

            _router.StartPractice(3);
            yield return null;
            float practiceCubeCenter = AppBootstrap.Instance.CubeCamera
                .WorldToViewportPoint(AppBootstrap.Instance.CubeRoot.position).y;
            Assert.That(AppBootstrap.Instance.CubeRoot.localScale.x,
                Is.EqualTo(lessonCubeScale).Within(0.001f));
            Assert.That(practiceCubeCenter, Is.EqualTo(lessonCubeCenter).Within(0.001f));

            var coach = _router.Lesson.transform.Find("ExplainGroup/CoachCard") as RectTransform;
            var pager = _router.Lesson.transform.Find("ExplainGroup/PagePill") as RectTransform;
            var algorithms = _router.Lesson.transform.Find("ExplainGroup/Algorithms") as RectTransform;
            Assert.IsNotNull(coach);
            Assert.IsNotNull(pager);
            Assert.IsNotNull(algorithms);
            Assert.LessOrEqual(coach.anchorMax.y, 0.35f,
                "코치 설명 카드는 화면 하단 묶음에 붙어야 한다");
            Assert.LessOrEqual(pager.anchorMax.y, 0.23f,
                "페이지 버튼도 설명 카드 바로 아래에 붙어야 한다");
            Assert.LessOrEqual(algorithms.anchorMax.y, 0.15f,
                "공식 설명도 하단 동작 버튼 가까이에 붙어야 한다");
            Assert.LessOrEqual(algorithms.anchorMin.y - 0.085f, 0.01f,
                "공식 카드와 하단 동작 버튼 사이에 큰 빈 공간이 없어야 한다");
            Assert.GreaterOrEqual(algorithms.anchorMax.y - algorithms.anchorMin.y, 0.05f,
                "공식 카드의 두 줄 설명이 잘리지 않을 높이가 필요하다");
        }

        [UnityTest]
        public IEnumerator 연습을_누르면_앞_단계까지만_통과한_상태가_된다()
        {
            for (int stage = 1; stage <= StageChecker.LastStage; stage++)
            {
                _router.OpenLesson(stage);
                yield return null;
                _router.Lesson.Practice();
                yield return null;

                Assert.AreEqual(stage - 1, StageChecker.CurrentStage(CubeStateNow()), $"{stage}단계 연습 시작 상태");
                Assert.IsTrue(_router.Lesson.InPractice);
            }
        }

        [UnityTest]
        public IEnumerator 연습에서_단계를_통과하면_진도가_올라간다()
        {
            int passedStage = 0;
            _router.Lesson.StagePassed += s => passedStage = s;

            // 연습 준비 시퀀스의 역순을 놓으면 완성 상태로 돌아간다.
            // 공식의 위수를 추측하지 않아도 되는 방식이다.
            LearnProgress.MarkDone(4);
            _router.OpenLesson(5);
            yield return null;
            _router.Lesson.Practice();
            yield return null;
            Assert.IsFalse(StageChecker.Passed(CubeStateNow(), 5));

            var rotator = AppBootstrap.Instance.CubeRoot.GetComponent<LayerRotator>();
            var setup = MoveNotation.Parse(LessonData.Get(5).PracticeSetup, 3);
            for (int i = setup.Count - 1; i >= 0; i--)
            {
                rotator.Enqueue(setup[i].Inverse);
                yield return null;
            }

            Assert.IsTrue(StageChecker.Passed(CubeStateNow(), 5), "역순을 놓으면 완성 상태로 돌아간다");
            Assert.AreEqual(5, passedStage, "통과 알림이 오지 않았다");
            Assert.IsTrue(LearnProgress.IsDone(5));
            Assert.IsTrue(LearnProgress.IsUnlocked(6));
        }

        [UnityTest]
        public IEnumerator 공식_라이브러리에서_시연할_수_있다()
        {
            _router.Go(ScreenId.Library);
            yield return null;
            Assert.AreEqual(ScreenId.Library, _router.Current);
            Assert.IsTrue(AppBootstrap.Instance.CubeRoot.gameObject.activeSelf);
            Assert.IsTrue(CubeStateNow().IsSolved(), "라이브러리는 완성 상태에서 시작한다");
        }

        [UnityTest]
        public IEnumerator 단계_힌트도_큐브를_대신_돌리지_않는다()
        {
            _router.OpenLesson(3);
            yield return null;
            _router.Lesson.Practice();
            yield return null;

            var before = CubeStateNow().Clone();
            _router.Lesson.ShowHint();
            yield return null;

            Assert.IsTrue(before.SameAs(CubeStateNow()),
                "단계 힌트는 설명만 하고 시연을 시작하면 안 된다");
        }

        [UnityTest]
        public IEnumerator LessonPracticeShowsNotationPadAndMovesCube()
        {
            _router.OpenLesson(1);
            yield return null;
            Assert.IsNotNull(_router.Lesson.Pad);
            Assert.IsFalse(_router.Lesson.Pad.gameObject.activeSelf);

            _router.Lesson.Practice();
            yield return null;
            Assert.IsTrue(_router.Lesson.Pad.gameObject.activeSelf);

            CubeState before = CubeStateNow().Clone();
            _router.Lesson.Pad.Press("R");
            yield return null;
            Assert.IsFalse(before.SameAs(CubeStateNow()));
        }

        [UnityTest]
        public IEnumerator 배우기_연습도_나갔다_들어오면_같은_상태에서_이어간다()
        {
            _router.OpenLesson(3);
            yield return null;
            _router.Lesson.Practice();
            yield return null;
            _router.Lesson.Pad.Press("R");
            yield return null;
            var expected = CubeStateNow().Clone();

            _router.Go(ScreenId.Learn);
            yield return null;
            _router.OpenLesson(3);
            yield return null;

            Assert.IsTrue(_router.Lesson.InPractice);
            Assert.IsTrue(expected.SameAs(CubeStateNow()));
            Assert.IsTrue(_router.Lesson.Pad.gameObject.activeSelf);
        }

        static CubeState CubeStateNow()
            => AppBootstrap.Instance.CubeRoot.GetComponent<CubeRenderer>().State;
    }
}
