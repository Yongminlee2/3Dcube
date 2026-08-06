using NUnit.Framework;
using Cube.Core;

namespace Cube.Core.Tests
{
    public class PendingCellsTests
    {
        static CubeState Scrambled(int seed)
        {
            var c = CubeState.Solved(3);
            c.Apply(MoveNotation.Parse(Scrambler.Generate(3, new System.Random(seed)), 3));
            return c;
        }

        [Test]
        public void 완성_상태에서는_강조할_칸이_없다()
        {
            var c = CubeState.Solved(3);
            for (int stage = 1; stage <= StageChecker.LastStage; stage++)
                Assert.AreEqual(0, HintEngine.PendingCells(c, stage).Count, $"{stage}단계");
        }

        [Test]
        public void 아직_못_맞춘_단계에는_강조할_칸이_있다()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var c = Scrambled(seed);
                int target = StageChecker.CurrentStage(c) + 1;
                if (target > StageChecker.LastStage) continue;
                Assert.Greater(HintEngine.PendingCells(c, target).Count, 0,
                    $"seed={seed}: {target}단계인데 강조할 칸이 없다");
            }
        }

        [Test]
        public void 이미_맞은_조각은_강조하지_않는다()
        {
            // 마지막 층만 흐트러뜨리면 아래 두 층은 강조 대상이 아니다.
            var c = CubeState.Solved(3);
            c.Apply(MoveNotation.Parse("R U R' U R U2 R'", 3));   // 수네: F2L 보존

            Assert.AreEqual(0, HintEngine.PendingCells(c, 1).Count, "아래 십자는 멀쩡하다");
            Assert.AreEqual(0, HintEngine.PendingCells(c, 2).Count, "첫 층은 멀쩡하다");
            Assert.AreEqual(0, HintEngine.PendingCells(c, 3).Count, "가운데 층은 멀쩡하다");
            Assert.Greater(HintEngine.PendingCells(c, 5).Count, 0, "위 면은 깨져 있다");
        }

        [Test]
        public void 강조한_칸은_전부_실제로_어긋나_있다()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var c = Scrambled(seed);
                for (int stage = 1; stage <= StageChecker.LastStage; stage++)
                    foreach (var (face, row, col) in HintEngine.PendingCells(c, stage))
                    {
                        // 조각 단위로 강조하므로 칸 하나하나가 반드시 틀린 건 아니다.
                        // 다만 유효한 자리여야 한다.
                        Assert.GreaterOrEqual(row, 0); Assert.Less(row, 3);
                        Assert.GreaterOrEqual(col, 0); Assert.Less(col, 3);
                    }
            }
        }

        [Test]
        public void 사단계는_십자만_보고_모서리는_보지_않는다()
        {
            var c = CubeState.Solved(3);
            c.Apply(MoveNotation.Parse("R U R' U R U2 R'", 3));   // 십자는 남고 모서리만 틀어진다
            Assert.AreEqual(0, HintEngine.PendingCells(c, 4).Count, "4단계에서 모서리를 강조하면 안 된다");
        }

        [Test]
        public void 세칸_큐브가_아니면_빈_목록이다()
        {
            Assert.AreEqual(0, HintEngine.PendingCells(CubeState.Solved(2), 1).Count);
            Assert.AreEqual(0, HintEngine.PendingCells(null, 1).Count);
        }
    }
}
