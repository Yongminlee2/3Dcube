using NUnit.Framework;
using Cube.Core;

namespace Cube.Core.Tests
{
    public class StageCheckerTests
    {
        static CubeState Solved() => CubeState.Solved(3);

        [Test]
        public void 완성_상태는_모든_단계를_통과한다()
        {
            var c = Solved();
            for (int s = 1; s <= StageChecker.LastStage; s++)
                Assert.IsTrue(StageChecker.Passed(c, s), $"{s}단계");
            Assert.AreEqual(StageChecker.LastStage, StageChecker.CurrentStage(c));
        }

        [Test]
        public void 섞은_큐브는_대개_첫_단계도_통과하지_못한다()
        {
            int failed = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                var c = Solved();
                c.Apply(MoveNotation.Parse(Scrambler.Generate(3, new System.Random(seed)), 3));
                if (!StageChecker.Passed(c, 1)) failed++;
            }
            Assert.Greater(failed, 25, "섞었는데 대부분 1단계를 통과했다면 판정이 헐겁다");
        }

        [Test]
        public void 큐브를_통째로_돌려도_판정이_같다()
        {
            // 전체 회전은 세 층을 한꺼번에 돌리는 것과 같다.
            for (int seed = 0; seed < 10; seed++)
            {
                var a = Solved();
                a.Apply(MoveNotation.Parse(Scrambler.Generate(3, new System.Random(seed)), 3));

                var b = a.Clone();
                for (int layer = 0; layer < 3; layer++) b.Apply(new Move(Axis.Y, layer, 1));

                Assert.AreEqual(StageChecker.CurrentStage(a), StageChecker.CurrentStage(b), $"seed={seed} (Y축 전체 회전)");

                var c = a.Clone();
                for (int layer = 0; layer < 3; layer++) c.Apply(new Move(Axis.X, layer, 1));

                Assert.AreEqual(StageChecker.CurrentStage(a), StageChecker.CurrentStage(c), $"seed={seed} (X축 전체 회전)");
            }
        }

        [Test]
        public void 단계는_누적된다()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 200; i++)
            {
                var c = Solved();
                int n = rng.Next(0, 12);
                for (int k = 0; k < n; k++)
                    c.Apply(new Move((Axis)rng.Next(3), rng.Next(3), rng.Next(1, 4)));

                int cur = StageChecker.CurrentStage(c);
                for (int s = 1; s <= cur; s++)
                    Assert.IsTrue(StageChecker.Passed(c, s), $"{cur}단계인데 {s}단계가 거짓이다");
            }
        }

        [Test]
        public void 마지막_층만_돌리면_세_단계까지_통과한다()
        {
            var c = Solved();
            c.Apply(MoveNotation.Parse("U", 3));
            Assert.IsTrue(StageChecker.Passed(c, 3), "아래 두 층은 그대로여야 한다");
            Assert.IsFalse(StageChecker.Passed(c, 7), "완성은 아니어야 한다");
        }

        [Test]
        public void 노란_십자_판정은_모서리를_보지_않는다()
        {
            // Sune는 위 면 모서리 방향만 바꾸고 십자는 유지한다.
            var c = Solved();
            c.Apply(MoveNotation.Parse("R U R' U R U2 R'", 3));
            Assert.IsTrue(StageChecker.Passed(c, 4), "십자는 남아 있어야 한다");
            Assert.IsFalse(StageChecker.Passed(c, 5), "위 면은 깨져 있어야 한다");
        }

        [Test]
        public void 아래_층을_건드리면_첫_단계가_깨진다()
        {
            var c = Solved();
            c.Apply(MoveNotation.Parse("D", 3));
            Assert.IsFalse(StageChecker.Passed(c, 1), "아래 십자의 옆면 색이 센터와 어긋나야 한다");
            Assert.AreEqual(0, StageChecker.CurrentStage(c));
        }

        [Test]
        public void 범위_밖_단계는_예외를_던진다()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => StageChecker.Passed(Solved(), 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => StageChecker.Passed(Solved(), 8));
        }

        [Test]
        public void 세칸_큐브가_아니면_예외를_던진다()
        {
            Assert.Throws<System.ArgumentException>(() => StageChecker.Passed(CubeState.Solved(2), 1));
            Assert.Throws<System.ArgumentException>(() => StageChecker.Passed(CubeState.Solved(4), 1));
        }
    }
}
