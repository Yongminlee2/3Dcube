using NUnit.Framework;
using Cube.Core;

namespace Cube.Core.Tests
{
    public class ScramblerTests
    {
        static readonly int[] Sizes = { 2, 3, 4 };

        [Test]
        public void 정해진_길이만큼_토큰을_만든다()
        {
            foreach (int n in Sizes)
            {
                string s = Scrambler.Generate(n, new System.Random(1));
                int tokens = s.Split(' ').Length;
                Assert.AreEqual(Scrambler.DefaultLength(n), tokens, $"n={n}: {s}");
            }
        }

        [Test]
        public void 섞은_뒤_거꾸로_풀면_완성된다()
        {
            foreach (int n in Sizes)
                for (int seed = 0; seed < 20; seed++)
                {
                    string s = Scrambler.Generate(n, new System.Random(seed));
                    var moves = MoveNotation.Parse(s, n);
                    var c = CubeState.Solved(n);
                    c.Apply(moves);
                    for (int i = moves.Count - 1; i >= 0; i--) c.Apply(moves[i].Inverse);
                    Assert.IsTrue(c.IsSolved(), $"n={n} seed={seed}: {s}");
                }
        }

        [Test]
        public void 섞으면_완성이_아니다()
        {
            foreach (int n in Sizes)
                for (int seed = 0; seed < 20; seed++)
                {
                    string s = Scrambler.Generate(n, new System.Random(seed));
                    var c = CubeState.Solved(n);
                    c.Apply(MoveNotation.Parse(s, n));
                    Assert.IsFalse(c.IsSolved(), $"n={n} seed={seed}: {s}");
                }
        }

        [Test]
        public void 같은_축이_연달아_나오지_않는다()
        {
            foreach (int n in Sizes)
                for (int seed = 0; seed < 20; seed++)
                {
                    string s = Scrambler.Generate(n, new System.Random(seed));
                    var tokens = s.Split(' ');
                    Axis? prev = null;
                    foreach (var t in tokens)
                    {
                        Axis axis = MoveNotation.ParseToken(t, n)[0].Axis;
                        Assert.AreNotEqual(prev, axis, $"n={n} seed={seed}: {s}");
                        prev = axis;
                    }
                }
        }

        [Test]
        public void 두칸_큐브는_세_면만_쓴다()
        {
            string s = Scrambler.Generate(2, new System.Random(5));
            foreach (var t in s.Split(' '))
                Assert.IsTrue(t[0] == 'R' || t[0] == 'U' || t[0] == 'F', $"허용되지 않은 토큰: {t} ({s})");
        }

        [Test]
        public void 네칸_큐브는_넓은수를_섞어_쓴다()
        {
            string s = Scrambler.Generate(4, new System.Random(3));
            Assert.IsTrue(s.Contains("w"), $"넓은수가 하나도 없다: {s}");
        }

        [Test]
        public void 씨앗이_같으면_결과도_같다()
        {
            foreach (int n in Sizes)
                Assert.AreEqual(Scrambler.Generate(n, new System.Random(42)),
                                Scrambler.Generate(n, new System.Random(42)));
        }

        [Test]
        public void 되돌리기는_역무브를_돌려준다()
        {
            var h = new MoveHistory();
            var m = new Move(Axis.X, 0, 1);
            h.Push(m);
            Assert.IsTrue(h.CanUndo);
            Assert.AreEqual(m.Inverse, h.Undo());
            Assert.IsFalse(h.CanUndo);
            Assert.IsTrue(h.CanRedo);
            Assert.AreEqual(m, h.Redo());
        }

        [Test]
        public void 되돌린_뒤_새로_쌓으면_다시하기가_사라진다()
        {
            var h = new MoveHistory();
            h.Push(new Move(Axis.X, 0, 1));
            h.Undo();
            Assert.IsTrue(h.CanRedo);
            h.Push(new Move(Axis.Y, 0, 1));
            Assert.IsFalse(h.CanRedo);
            Assert.AreEqual(1, h.Count);
        }

        [Test]
        public void 되돌리기와_실제_상태가_맞아떨어진다()
        {
            var rng = new System.Random(11);
            var c = CubeState.Solved(3);
            var h = new MoveHistory();
            for (int i = 0; i < 25; i++)
            {
                var m = new Move((Axis)rng.Next(3), rng.Next(3), rng.Next(1, 4));
                c.Apply(m); h.Push(m);
            }
            while (h.CanUndo) c.Apply(h.Undo());
            Assert.IsTrue(c.IsSolved());
        }
    }
}
