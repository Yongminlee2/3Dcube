using System.Collections.Generic;
using NUnit.Framework;
using Cube.Core;

namespace Cube.Core.Tests
{
    public class LessonDataTests
    {
        [Test]
        public void 일곱_단계가_빠짐없이_하나씩_있다()
        {
            Assert.AreEqual(StageChecker.LastStage, LessonData.Lessons.Count);
            for (int stage = 1; stage <= StageChecker.LastStage; stage++)
                Assert.AreEqual(stage, LessonData.Get(stage).Stage);
        }

        [Test]
        public void 모든_단계에_제목과_설명이_있다()
        {
            foreach (var l in LessonData.Lessons)
            {
                Assert.IsNotEmpty(l.Title, $"{l.Stage}단계 제목");
                Assert.Greater(l.Steps.Length, 0, $"{l.Stage}단계 설명");
                foreach (var s in l.Steps)
                    Assert.IsNotEmpty(s?.Trim(), $"{l.Stage}단계에 빈 문단이 있다");
            }
        }

        [Test]
        public void 모든_공식_표기가_삼칸_큐브로_읽힌다()
        {
            foreach (var a in AllAlgorithms())
            {
                var moves = MoveNotation.Parse(a.Notation, 3);
                Assert.Greater(moves.Count, 0, $"'{a.Name}' 표기가 비었다: {a.Notation}");
                Assert.IsNotEmpty(a.When?.Trim(), $"'{a.Name}' 설명이 없다");
            }
        }

        [Test]
        public void 어떤_공식도_아무_일도_하지_않는_공식이_아니다()
        {
            foreach (var a in AllAlgorithms())
            {
                var c = CubeState.Solved(3);
                c.Apply(MoveNotation.Parse(a.Notation, 3));
                Assert.IsFalse(c.IsSolved(), $"'{a.Name}'이 아무것도 바꾸지 않는다: {a.Notation}");
            }
        }

        [Test]
        public void 마지막_층_공식은_아래_두_층을_건드리지_않는다()
        {
            // 4단계부터는 아래 두 층이 끝난 상태에서 쓰는 공식이다.
            // 아래를 건드리면 배우는 사람이 앞 단계를 다시 해야 한다.
            for (int stage = 4; stage <= 7; stage++)
                foreach (var a in LessonData.Get(stage).Algorithms)
                {
                    var c = CubeState.Solved(3);
                    c.Apply(MoveNotation.Parse(a.Notation, 3));
                    Assert.IsTrue(StageChecker.Passed(c, 3),
                        $"{stage}단계 '{a.Name}'이 아래 두 층을 망가뜨린다: {a.Notation}");
                }
        }

        [Test]
        public void 가운데_층_공식은_첫_층을_건드리지_않는다()
        {
            foreach (var a in LessonData.Get(3).Algorithms)
            {
                var c = CubeState.Solved(3);
                c.Apply(MoveNotation.Parse(a.Notation, 3));
                Assert.IsTrue(StageChecker.Passed(c, 2),
                    $"'{a.Name}'이 첫 층을 망가뜨린다: {a.Notation}");
            }
        }

        [Test]
        public void 연습_준비는_앞_단계까지만_통과한_상태를_만든다()
        {
            // 이 코스에서 가장 틀리기 쉬운 부분이다. 준비 시퀀스를 눈대중으로 고르면
            // 연습 화면이 엉뚱한 상태에서 시작한다.
            foreach (var l in LessonData.Lessons)
            {
                var c = CubeState.Solved(3);
                c.Apply(MoveNotation.Parse(l.PracticeSetup, 3));
                Assert.AreEqual(l.Stage - 1, StageChecker.CurrentStage(c),
                    $"{l.Stage}단계 연습 준비가 {l.Stage - 1}단계 상태를 만들지 않는다: {l.PracticeSetup}");
            }
        }

        [Test]
        public void 모서리_공식은_왼쪽_앞_모서리를_제자리에_남긴다()
        {
            // 설명 글이 "제자리인 모서리를 왼쪽 앞에 두라"고 말한다.
            // 실제로 고정되는 자리가 다르면 배우는 사람이 헤맨다.
            var c = CubeState.Solved(3);
            c.Apply(MoveNotation.Parse(LessonData.Get(6).Algorithms[0].Notation, 3));

            var intact = new List<string>();
            if (Same(c, Face.U, 2, 0, Face.F, 0, 0, Face.L, 0, 2)) intact.Add("왼쪽 앞");
            if (Same(c, Face.U, 2, 2, Face.F, 0, 2, Face.R, 0, 0)) intact.Add("오른쪽 앞");
            if (Same(c, Face.U, 0, 2, Face.R, 0, 2, Face.B, 0, 0)) intact.Add("오른쪽 뒤");
            if (Same(c, Face.U, 0, 0, Face.B, 0, 2, Face.L, 0, 0)) intact.Add("왼쪽 뒤");

            Assert.AreEqual(1, intact.Count,
                $"세 모서리만 도는 공식이어야 한다. 그대로인 자리: [{string.Join(", ", intact)}]");
            Assert.AreEqual("왼쪽 앞", intact[0],
                $"설명 글과 다른 자리가 고정된다. 실제: {intact[0]}");
        }

        static bool Same(CubeState c, Face a, int ar, int ac, Face b, int br, int bc, Face d, int dr, int dc)
            => c.Get(a, ar, ac) == c.Get(a, 1, 1)
            && c.Get(b, br, bc) == c.Get(b, 1, 1)
            && c.Get(d, dr, dc) == c.Get(d, 1, 1);

        [Test]
        public void 라이브러리_공식_이름이_겹치지_않는다()
        {
            var seen = new HashSet<string>();
            foreach (var a in LessonData.Library)
                Assert.IsTrue(seen.Add(a.Name), $"이름이 겹친다: {a.Name}");
        }

        [Test]
        public void 코스에_나온_공식은_라이브러리에도_있다()
        {
            var library = new HashSet<string>();
            foreach (var a in LessonData.Library) library.Add(a.Notation);

            foreach (var l in LessonData.Lessons)
                foreach (var a in l.Algorithms)
                    Assert.IsTrue(library.Contains(a.Notation),
                        $"{l.Stage}단계 '{a.Name}'이 라이브러리에 없다: {a.Notation}");
        }

        [Test]
        public void 범위_밖_단계를_찾으면_예외를_던진다()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => LessonData.Get(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => LessonData.Get(8));
        }

        static IEnumerable<Algorithm> AllAlgorithms()
        {
            foreach (var l in LessonData.Lessons)
                foreach (var a in l.Algorithms) yield return a;
            foreach (var a in LessonData.Library) yield return a;
        }
    }
}
