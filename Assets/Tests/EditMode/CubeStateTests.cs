using System.Collections.Generic;
using NUnit.Framework;
using Cube.Core;

namespace Cube.Core.Tests
{
    public class CubeStateTests
    {
        static readonly int[] Sizes = { 2, 3, 4 };

        [Test]
        public void 갓_만든_큐브는_완성_상태다()
        {
            foreach (int n in Sizes)
                Assert.IsTrue(CubeState.Solved(n).IsSolved(), $"n={n}");
        }

        [Test]
        public void 한_수만_돌려도_완성이_아니다()
        {
            foreach (int n in Sizes)
            {
                var c = CubeState.Solved(n);
                c.Apply(new Move(Axis.X, 0, 1));
                Assert.IsFalse(c.IsSolved(), $"n={n}");
            }
        }

        [Test]
        public void 같은_층을_네_번_돌리면_제자리다()
        {
            foreach (int n in Sizes)
                foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
                    for (int layer = 0; layer < n; layer++)
                    {
                        var c = CubeState.Solved(n);
                        for (int i = 0; i < 4; i++) c.Apply(new Move(axis, layer, 1));
                        Assert.IsTrue(c.IsSolved(), $"n={n} axis={axis} layer={layer}");
                    }
        }

        [Test]
        public void 시퀀스와_역시퀀스는_서로_지운다()
        {
            var rng = new System.Random(1234);
            foreach (int n in Sizes)
            {
                var moves = new List<Move>();
                for (int i = 0; i < 30; i++)
                    moves.Add(new Move((Axis)rng.Next(3), rng.Next(n), rng.Next(1, 4)));

                var c = CubeState.Solved(n);
                c.Apply(moves);
                for (int i = moves.Count - 1; i >= 0; i--) c.Apply(moves[i].Inverse);
                Assert.IsTrue(c.IsSolved(), $"n={n}");
            }
        }

        [Test]
        public void 어떤_회전_뒤에도_색깔_개수는_변하지_않는다()
        {
            var rng = new System.Random(99);
            foreach (int n in Sizes)
            {
                var c = CubeState.Solved(n);
                for (int i = 0; i < 50; i++)
                    c.Apply(new Move((Axis)rng.Next(3), rng.Next(n), rng.Next(1, 4)));

                var counts = new int[6];
                foreach (byte v in c.Facelets) counts[v]++;
                for (int v = 0; v < 6; v++)
                    Assert.AreEqual(n * n, counts[v], $"n={n} 색 {v}");
            }
        }

        [Test]
        public void 두_번_돌리기는_한_번씩_두_번과_같다()
        {
            foreach (int n in Sizes)
            {
                var a = CubeState.Solved(n);
                a.Apply(new Move(Axis.Y, 0, 2));

                var b = CubeState.Solved(n);
                b.Apply(new Move(Axis.Y, 0, 1));
                b.Apply(new Move(Axis.Y, 0, 1));

                Assert.IsTrue(a.SameAs(b), $"n={n}");
            }
        }

        [Test]
        public void R_회전은_F면_오른쪽_줄을_U면_오른쪽_줄로_옮긴다()
        {
            var c = CubeState.Solved(3);
            byte fColor = c.Get(Face.F, 1, 2);
            c.Apply(new Move(Axis.X, 0, 1));       // R
            Assert.AreEqual(fColor, c.Get(Face.U, 1, 2));
        }

        [Test]
        public void 복제본은_원본과_따로_논다()
        {
            var a = CubeState.Solved(3);
            var b = a.Clone();
            b.Apply(new Move(Axis.X, 0, 1));
            Assert.IsTrue(a.IsSolved());
            Assert.IsFalse(b.IsSolved());
        }
    }
}
