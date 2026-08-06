using NUnit.Framework;
using Cube.Core;

namespace Cube.Core.Tests
{
    public class CubeValidatorTests
    {
        static CubeState Solved() => CubeState.Solved(3);

        static CubeState Scrambled(int seed)
        {
            var c = Solved();
            c.Apply(MoveNotation.Parse(Scrambler.Generate(3, new System.Random(seed)), 3));
            return c;
        }

        static void Swap(CubeState c, Face fa, int ra, int ca, Face fb, int rb, int cb)
        {
            int ia = c.IndexOf(fa, ra, ca), ib = c.IndexOf(fb, rb, cb);
            (c.Facelets[ia], c.Facelets[ib]) = (c.Facelets[ib], c.Facelets[ia]);
        }

        [Test]
        public void 완성_상태는_유효하다()
        {
            var r = CubeValidator.Validate(Solved());
            Assert.IsTrue(r.IsValid, r.Reason);
        }

        [Test]
        public void 어떻게_섞어도_항상_유효하다()
        {
            for (int seed = 0; seed < 60; seed++)
            {
                var r = CubeValidator.Validate(Scrambled(seed));
                Assert.IsTrue(r.IsValid, $"seed={seed}: {r.Reason}");
            }
        }

        [Test]
        public void 색_개수가_틀리면_거부한다()
        {
            var c = Solved();
            c.Facelets[c.IndexOf(Face.U, 0, 0)] = c.Get(Face.D, 0, 0);   // 위 칸 하나를 아래 색으로
            var r = CubeValidator.Validate(c);
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("9개", r.Reason);
        }

        [Test]
        public void 가운데_색이_겹치면_거부한다()
        {
            var c = Solved();
            // 센터 두 개를 맞바꾸면 개수는 그대로지만 가운데가 겹치지는 않는다.
            // 개수를 유지한 채 가운데만 겹치게 하려면 두 색을 통째로 맞바꾼다.
            for (int i = 0; i < c.Facelets.Length; i++)
                if (c.Facelets[i] == (byte)Face.R) c.Facelets[i] = (byte)Face.L;
            var r = CubeValidator.Validate(c);
            Assert.IsFalse(r.IsValid);
        }

        [Test]
        public void 모서리_하나를_비틀면_거부한다()
        {
            // 왼쪽 앞 위 모서리의 세 칸을 제자리에서 돌린다.
            var c = Solved();
            byte u = c.Get(Face.U, 2, 0), f = c.Get(Face.F, 0, 0), l = c.Get(Face.L, 0, 2);
            c.Facelets[c.IndexOf(Face.U, 2, 0)] = f;
            c.Facelets[c.IndexOf(Face.F, 0, 0)] = l;
            c.Facelets[c.IndexOf(Face.L, 0, 2)] = u;

            var r = CubeValidator.Validate(c);
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("모서리", r.Reason);
        }

        [Test]
        public void 엣지_하나를_뒤집으면_거부한다()
        {
            var c = Solved();
            Swap(c, Face.U, 2, 1, Face.F, 0, 1);   // 위-앞 엣지의 두 칸을 맞바꾼다

            var r = CubeValidator.Validate(c);
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("엣지", r.Reason);
        }

        [Test]
        public void 엣지_두_개만_맞바꾸면_거부한다()
        {
            // 조각 자체를 통째로 맞바꾼다. 개수·비틀림·뒤집힘은 멀쩡하고 순열 홀짝만 깨진다.
            var c = Solved();
            Swap(c, Face.U, 2, 1, Face.U, 0, 1);   // 위-앞 엣지 ↔ 위-뒤 엣지
            Swap(c, Face.F, 0, 1, Face.B, 0, 1);

            var r = CubeValidator.Validate(c);
            Assert.IsFalse(r.IsValid);
            StringAssert.Contains("자리를 바꾼", r.Reason);
        }

        [Test]
        public void 세_조각을_돌리는_것은_유효하다()
        {
            // 3-순환은 짝순열이라 실제로 만들 수 있는 상태다. 거부하면 안 된다.
            var c = Solved();
            c.Apply(MoveNotation.Parse("R U' R U R U R U' R' U' R2", 3));   // 엣지 3-순환
            var r = CubeValidator.Validate(c);
            Assert.IsTrue(r.IsValid, r.Reason);
        }

        [Test]
        public void 없는_색_번호는_거부한다()
        {
            var c = Solved();
            c.Facelets[0] = 9;
            var r = CubeValidator.Validate(c);
            Assert.IsFalse(r.IsValid);
        }

        [Test]
        public void 세칸_큐브가_아니면_예외를_던진다()
        {
            Assert.Throws<System.ArgumentException>(() => CubeValidator.Validate(CubeState.Solved(2)));
            Assert.Throws<System.ArgumentException>(() => CubeValidator.Validate(CubeState.Solved(4)));
        }

        [Test]
        public void 같은_색_두_칸을_맞바꾸면_아무_일도_아니다()
        {
            // 같은 면의 모서리 칸 두 개는 완성 상태에서 같은 색이다. 바꿔도 그대로다.
            var c = Solved();
            Swap(c, Face.U, 0, 0, Face.U, 0, 2);
            Assert.IsTrue(CubeValidator.Validate(c).IsValid);
        }

        [Test]
        public void 거부할_때는_반드시_이유를_말한다()
        {
            var c = Solved();
            Swap(c, Face.U, 0, 0, Face.F, 0, 0);   // 서로 다른 색이라 실제로 깨진다
            var r = CubeValidator.Validate(c);
            Assert.IsFalse(r.IsValid);
            Assert.IsNotEmpty(r.Reason, "왜 거부됐는지 알려주지 않으면 사용자가 고칠 수 없다");
        }
    }
}
