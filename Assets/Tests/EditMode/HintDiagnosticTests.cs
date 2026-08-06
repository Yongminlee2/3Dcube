using NUnit.Framework;
using Cube.Core;

namespace Cube.Core.Tests
{
    /// 힌트가 마지막 층에서 수렴하지 않는 원인을 좁히는 진단.
    /// 특정 스크램블 탓인지, 아니면 근본적으로 못 푸는 경우가 있는지 가른다.
    public class HintDiagnosticTests
    {
        static string CornerAlg => LessonData.Get(6).Algorithms[0].Notation;

        [Test]
        public void 최소_반례_모서리_공식_한_번_적용한_상태에서_해답을_찾는다()
        {
            // 완성 상태에 6단계 공식을 한 번 쓰면 모서리 셋만 돌아간 상태가 된다.
            // 여기서 6단계로 가는 길을 못 찾으면 문제는 스크램블이 아니라 근본이다.
            var c = CubeState.Solved(3);
            c.Apply(MoveNotation.Parse(CornerAlg, 3));
            Assert.AreEqual(5, StageChecker.CurrentStage(c), "준비 상태가 5단계여야 한다");

            bool found = HintEngine.TryFindStageSequence(c, 6, 8, out string seq, out int depth);
            Assert.IsTrue(found, $"6단계로 가는 길을 못 찾았다. 알파벳이 모자란다는 뜻이다.\n상태: {Dump(c)}");

            c.Apply(MoveNotation.Parse(seq, 3));
            Assert.IsTrue(StageChecker.Passed(c, 6), $"찾은 답 '{seq}'(깊이 {depth})을 적용해도 6단계가 아니다");
        }

        [Test]
        public void 모서리_공식을_두_번_적용한_상태에서도_해답을_찾는다()
        {
            var c = CubeState.Solved(3);
            c.Apply(MoveNotation.Parse(CornerAlg, 3));
            c.Apply(MoveNotation.Parse("U", 3));
            c.Apply(MoveNotation.Parse(CornerAlg, 3));
            c.Apply(MoveNotation.Parse("U'", 3));

            if (StageChecker.Passed(c, 6)) Assert.Pass("이미 6단계다");
            Assert.AreEqual(5, StageChecker.CurrentStage(c));

            bool found = HintEngine.TryFindStageSequence(c, 6, 8, out string seq, out int depth);
            Assert.IsTrue(found, $"6단계로 가는 길을 못 찾았다.\n상태: {Dump(c)}");

            c.Apply(MoveNotation.Parse(seq, 3));
            Assert.IsTrue(StageChecker.Passed(c, 6), $"'{seq}'(깊이 {depth})으로도 6단계가 아니다");
        }

        [Test]
        public void 실제로_막히는_상태를_찾아_보고한다()
        {
            // 힌트를 따라가다 처음 막히는 지점을 찾아 그 상태와 단계를 그대로 보고한다.
            for (int seed = 0; seed < 3; seed++)
            {
                var cube = CubeState.Solved(3);
                cube.Apply(MoveNotation.Parse(Scrambler.Generate(3, new System.Random(seed)), 3));

                int lastStage = -1, same = 0;
                for (int i = 0; i < 80 && !cube.IsSolved(); i++)
                {
                    int target = StageChecker.CurrentStage(cube) + 1;
                    var h = HintEngine.Next(cube);

                    if (!h.HasMove)
                        Assert.Fail($"seed={seed}: {target}단계에서 둘 수를 못 냈다 — {h.Reason}\n{Dump(cube)}");

                    cube.Apply(MoveNotation.Parse(h.Notation, 3));

                    int now = StageChecker.CurrentStage(cube);
                    same = now == lastStage ? same + 1 : 0;
                    lastStage = now;

                    if (same >= 8)
                    {
                        bool found = HintEngine.TryFindStageSequence(cube, now + 1, 8, out string seq, out int d);
                        Assert.Fail(
                            $"seed={seed}: {now}단계에서 막혔다 (목표 {now + 1}단계).\n" +
                            $"직접 탐색: {(found ? $"찾음 '{seq}' 깊이 {d}" : "깊이 8까지 못 찾음")}\n" +
                            $"마지막 힌트: '{h.Notation}' — {h.Reason}\n{Dump(cube)}");
                    }
                }
            }
            Assert.Pass("세 스크램블 모두 막히지 않았다");
        }

        static string Dump(CubeState c)
        {
            var sb = new System.Text.StringBuilder();
            foreach (Face f in Faces.All)
            {
                sb.Append(f).Append(": ");
                for (int r = 0; r < 3; r++)
                    for (int col = 0; col < 3; col++)
                        sb.Append(c.Get(f, r, col));
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
