using NUnit.Framework;
using Cube.Core;

namespace Cube.Core.Tests
{
    public class MoveNotationTests
    {
        [TestCase("R", "오른쪽 면을 시계 방향으로 한 칸")]
        [TestCase("U'", "위쪽 면을 반시계 방향으로 한 칸")]
        [TestCase("F2", "앞면을 반 바퀴")]
        public void 첫_수를_사람이_읽는_설명으로_바꾼다(string notation, string expected)
        {
            Assert.AreEqual(expected, MoveNotation.DescribeFirst(notation + " L D"));
        }

        [Test]
        public void 기본_여섯_면이_규정된_무브로_바뀐다()
        {
            Assert.AreEqual(new Move(Axis.X, 0, 1), MoveNotation.ParseToken("R", 3)[0]);
            Assert.AreEqual(new Move(Axis.X, 2, 3), MoveNotation.ParseToken("L", 3)[0]);
            Assert.AreEqual(new Move(Axis.Y, 0, 1), MoveNotation.ParseToken("U", 3)[0]);
            Assert.AreEqual(new Move(Axis.Y, 2, 3), MoveNotation.ParseToken("D", 3)[0]);
            Assert.AreEqual(new Move(Axis.Z, 0, 1), MoveNotation.ParseToken("F", 3)[0]);
            Assert.AreEqual(new Move(Axis.Z, 2, 3), MoveNotation.ParseToken("B", 3)[0]);
        }

        [Test]
        public void 수식어가_회전량을_바꾼다()
        {
            Assert.AreEqual(new Move(Axis.X, 0, 3), MoveNotation.ParseToken("R'", 3)[0]);
            Assert.AreEqual(new Move(Axis.X, 0, 2), MoveNotation.ParseToken("R2", 3)[0]);
            Assert.AreEqual(new Move(Axis.X, 2, 1), MoveNotation.ParseToken("L'", 3)[0]);
        }

        [Test]
        public void 넓은수는_층_두_개를_만든다()
        {
            var wide = MoveNotation.ParseToken("Rw", 4);
            Assert.AreEqual(2, wide.Count);
            Assert.AreEqual(new Move(Axis.X, 0, 1), wide[0]);
            Assert.AreEqual(new Move(Axis.X, 1, 1), wide[1]);

            var lower = MoveNotation.ParseToken("r", 4);
            CollectionAssert.AreEqual(wide, lower);
        }

        [Test]
        public void 깊이_지정은_층_하나만_고른다()
        {
            var slice = MoveNotation.ParseToken("2R", 4);
            Assert.AreEqual(1, slice.Count);
            Assert.AreEqual(new Move(Axis.X, 1, 1), slice[0]);
        }

        [Test]
        public void 깊이와_넓은수를_함께_쓰면_그만큼_쌓인다()
        {
            var wide3 = MoveNotation.ParseToken("3Rw", 4);
            Assert.AreEqual(3, wide3.Count);
            Assert.AreEqual(new Move(Axis.X, 0, 1), wide3[0]);
            Assert.AreEqual(new Move(Axis.X, 1, 1), wide3[1]);
            Assert.AreEqual(new Move(Axis.X, 2, 1), wide3[2]);
        }

        [Test]
        public void 반대편_면의_깊이는_반대쪽에서_센다()
        {
            var lw = MoveNotation.ParseToken("Lw", 4);
            Assert.AreEqual(2, lw.Count);
            Assert.AreEqual(new Move(Axis.X, 3, 3), lw[0]);
            Assert.AreEqual(new Move(Axis.X, 2, 3), lw[1]);
        }

        [Test]
        public void 여러_토큰을_공백으로_나눠_읽는다()
        {
            var moves = MoveNotation.Parse("R U R' U'", 3);
            Assert.AreEqual(4, moves.Count);
            Assert.AreEqual(new Move(Axis.X, 0, 1), moves[0]);
            Assert.AreEqual(new Move(Axis.Y, 0, 1), moves[1]);
            Assert.AreEqual(new Move(Axis.X, 0, 3), moves[2]);
            Assert.AreEqual(new Move(Axis.Y, 0, 3), moves[3]);
        }

        [Test]
        public void 무브를_문자열로_바꿨다_읽으면_그대로다()
        {
            var rng = new System.Random(7);
            foreach (int n in new[] { 2, 3, 4 })
                for (int i = 0; i < 200; i++)
                {
                    var m = new Move((Axis)rng.Next(3), rng.Next(n), rng.Next(1, 4));
                    string text = MoveNotation.Format(m, n);
                    var back = MoveNotation.ParseToken(text, n);
                    Assert.AreEqual(1, back.Count, $"'{text}'");
                    Assert.AreEqual(m, back[0], $"'{text}' n={n}");
                }
        }

        [Test]
        public void 정규형은_바깥층을_면_문자로_적는다()
        {
            Assert.AreEqual("R", MoveNotation.Format(new Move(Axis.X, 0, 1), 3));
            Assert.AreEqual("R2", MoveNotation.Format(new Move(Axis.X, 0, 2), 3));
            Assert.AreEqual("R'", MoveNotation.Format(new Move(Axis.X, 0, 3), 3));
            Assert.AreEqual("L", MoveNotation.Format(new Move(Axis.X, 2, 3), 3));
            Assert.AreEqual("2R", MoveNotation.Format(new Move(Axis.X, 1, 1), 3));
        }

        [Test]
        public void 잘못된_토큰은_예외를_던진다()
        {
            Assert.Throws<System.FormatException>(() => MoveNotation.ParseToken("Q", 3));
            Assert.Throws<System.FormatException>(() => MoveNotation.ParseToken("R3", 3));
            Assert.Throws<System.FormatException>(() => MoveNotation.ParseToken("", 3));
            Assert.Throws<System.FormatException>(() => MoveNotation.ParseToken("5R", 3));
        }

        [Test]
        public void 알려진_시퀀스는_여섯_번_반복하면_제자리다()
        {
            var c = CubeState.Solved(3);
            var sexy = MoveNotation.Parse("R U R' U'", 3);
            for (int i = 0; i < 6; i++) c.Apply(sexy);
            Assert.IsTrue(c.IsSolved());
        }

        [Test]
        public void 알려진_시퀀스는_여섯_번이_되기_전에는_제자리가_아니다()
        {
            var c = CubeState.Solved(3);
            var sexy = MoveNotation.Parse("R U R' U'", 3);
            for (int i = 1; i < 6; i++)
            {
                c.Apply(sexy);
                Assert.IsFalse(c.IsSolved(), $"{i}번째에서 이미 풀렸다");
            }
        }
    }
}
