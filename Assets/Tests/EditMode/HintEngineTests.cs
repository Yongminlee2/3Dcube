using System.Diagnostics;
using NUnit.Framework;
using Cube.Core;

namespace Cube.Core.Tests
{
    public class HintEngineTests
    {
        static CubeState Scrambled(int seed)
        {
            var c = CubeState.Solved(3);
            c.Apply(MoveNotation.Parse(Scrambler.Generate(3, new System.Random(seed)), 3));
            return c;
        }

        [Test]
        public void 완성_상태에는_둘_수가_없다()
        {
            var h = HintEngine.Next(CubeState.Solved(3));
            Assert.IsTrue(h.IsSolved);
            Assert.IsFalse(h.HasMove);
        }

        [Test]
        public void 힌트는_항상_이유를_말한다()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var h = HintEngine.Next(Scrambled(seed));
                Assert.IsNotEmpty(h.Reason, $"seed={seed}");
            }
        }

        [Test]
        public void 힌트가_주는_수는_읽을_수_있는_표기다()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var h = HintEngine.Next(Scrambled(seed));
                if (!h.HasMove) continue;
                Assert.DoesNotThrow(() => MoveNotation.Parse(h.Notation, 3), $"seed={seed}: '{h.Notation}'");
            }
        }

        [TestCase("U U'", "")]
        [TestCase("U U", "U2")]
        [TestCase("U U' U", "U")]
        [TestCase("U U U U", "")]
        [TestCase("R R' U U' F2 F2", "")]
        public void 의미없이_반복되는_같은면_회전을_축약한다(string input, string expected)
        {
            string simplified = HintEngine.SimplifyNotation(input);
            Assert.AreEqual(expected, simplified);

            var before = CubeState.Solved(3);
            before.Apply(MoveNotation.Parse(input, 3));
            var after = CubeState.Solved(3);
            after.Apply(MoveNotation.Parse(simplified, 3));
            Assert.IsTrue(before.SameAs(after), $"{input} -> {simplified}");
        }

        [Test]
        public void 생성된_힌트에는_바로_취소하거나_합칠_회전이_없다()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var cube = Scrambled(seed);
                for (int round = 0; round < 80 && !cube.IsSolved(); round++)
                {
                    var hint = HintEngine.Next(cube);
                    if (!hint.HasMove) break;

                    var moves = MoveNotation.Parse(hint.Notation, 3);
                    for (int i = 1; i < moves.Count; i++)
                    {
                        bool sameLayer = moves[i - 1].Axis == moves[i].Axis
                            && moves[i - 1].Layer == moves[i].Layer;
                        Assert.IsFalse(sameLayer,
                            $"seed={seed}: 축약되지 않은 힌트 '{hint.Notation}'");
                    }
                    cube.Apply(moves);
                }
            }
        }

        [Test]
        public void 힌트에는_버튼이_없는_y_전체회전이_나오지_않는다()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var cube = Scrambled(seed);
                for (int i = 0; i < 80 && !cube.IsSolved(); i++)
                {
                    var h = HintEngine.Next(cube);
                    if (!h.HasMove) break;
                    StringAssert.DoesNotContain("y", h.Notation, $"seed={seed}: {h.Notation}");
                    cube.Apply(MoveNotation.Parse(h.Notation, 3));
                }
            }
        }

        [Test]
        public void 힌트를_따라가면_실제로_풀린다()
        {
            // 힌트 로직 전체를 한 번에 덮는 시험이다.
            // 어느 단계에서든 힌트가 진척을 만들지 못하면 여기서 걸린다.
            const int MaxHints = 300;

            for (int seed = 0; seed < 8; seed++)
            {
                var cube = Scrambled(seed);
                int applied = 0;
                int lastStage = -1, stuckRounds = 0;

                while (!cube.IsSolved() && applied < MaxHints)
                {
                    var h = HintEngine.Next(cube);
                    Assert.IsTrue(h.HasMove,
                        $"seed={seed}: {StageChecker.CurrentStage(cube)}단계에서 둘 수를 못 찾았다 — {h.Reason}");

                    cube.Apply(MoveNotation.Parse(h.Notation, 3));
                    applied++;

                    int stage = StageChecker.CurrentStage(cube);
                    stuckRounds = stage == lastStage ? stuckRounds + 1 : 0;
                    lastStage = stage;
                    Assert.Less(stuckRounds, 60, $"seed={seed}: {stage}단계에서 제자리걸음이다");
                }

                Assert.IsTrue(cube.IsSolved(), $"seed={seed}: {applied}번 만에도 못 풀었다");
            }
        }

        [Test]
        public void 힌트는_앞_단계를_깨뜨리지_않는다()
        {
            for (int seed = 0; seed < 5; seed++)
            {
                var cube = Scrambled(seed);
                for (int i = 0; i < 120 && !cube.IsSolved(); i++)
                {
                    int before = StageChecker.CurrentStage(cube);
                    var h = HintEngine.Next(cube);
                    if (!h.HasMove) break;

                    cube.Apply(MoveNotation.Parse(h.Notation, 3));
                    int after = StageChecker.CurrentStage(cube);

                    // 마지막 층 공식은 경우를 바꾸느라 일시적으로 같은 단계에 머물 수 있다.
                    // 다만 이미 끝낸 앞 단계로 되돌아가면 안 된다.
                    if (before >= 3)
                        Assert.GreaterOrEqual(after, 3, $"seed={seed}: 아래 두 층이 깨졌다 — {h.Notation}");
                }
            }
        }

        [Test]
        public void 한_수_힌트가_너무_오래_걸리지_않는다()
        {
            var sw = Stopwatch.StartNew();
            for (int seed = 0; seed < 5; seed++) HintEngine.Next(Scrambled(seed));
            sw.Stop();

            Assert.Less(sw.ElapsedMilliseconds, 3000,
                $"힌트 다섯 번에 {sw.ElapsedMilliseconds}ms 걸렸다. 폰에서는 더 느리다");
        }

        [Test]
        public void 진척도는_영에서_넷_사이다()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var c = Scrambled(seed);
                for (int stage = 1; stage <= 3; stage++)
                {
                    int p = HintEngine.Progress(c, stage);
                    Assert.GreaterOrEqual(p, 0);
                    Assert.LessOrEqual(p, 4);
                }
            }
        }

        [Test]
        public void 세칸_큐브가_아니면_예외를_던진다()
        {
            Assert.Throws<System.ArgumentException>(() => HintEngine.Next(CubeState.Solved(2)));
        }
    }
}
