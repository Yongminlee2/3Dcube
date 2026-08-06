using NUnit.Framework;
using UnityEngine;
using Cube.Core;
using Cube.App;

namespace Cube.App.Tests
{
    public class SwipeResolverTests
    {
        static readonly Vector3Int X = new Vector3Int(1, 0, 0);
        static readonly Vector3Int Y = new Vector3Int(0, 1, 0);
        static readonly Vector3Int Z = new Vector3Int(0, 0, 1);

        [Test]
        public void 앞면_윗줄을_오른쪽으로_밀면_U프라임이_된다()
        {
            // F면(법선 +Z), 오른쪽으로 드래그. 화면에서 X 접선이 오른쪽, Y 접선이 위쪽.
            bool ok = SwipeResolver.Resolve(
                normal: Z, cubieCoord: new Vector3Int(1, 2, 2), n: 3,
                tangentA: X, screenA: new Vector2(1f, 0f),
                tangentB: Y, screenB: new Vector2(0f, 1f),
                drag: new Vector2(80f, 3f),
                out Move move, out float along);

            Assert.IsTrue(ok);
            Assert.AreEqual(new Move(Axis.Y, 0, 3), move);   // U'
            Assert.Greater(along, 0f);
        }

        [Test]
        public void 앞면_윗줄을_왼쪽으로_밀면_U가_된다()
        {
            bool ok = SwipeResolver.Resolve(
                normal: Z, cubieCoord: new Vector3Int(1, 2, 2), n: 3,
                tangentA: X, screenA: new Vector2(1f, 0f),
                tangentB: Y, screenB: new Vector2(0f, 1f),
                drag: new Vector2(-80f, 3f),
                out Move move, out float along);

            Assert.IsTrue(ok);
            Assert.AreEqual(new Move(Axis.Y, 0, 1), move);   // U
            Assert.Less(along, 0f);
        }

        [Test]
        public void 윗면_앞줄을_오른쪽으로_밀면_F가_된다()
        {
            bool ok = SwipeResolver.Resolve(
                normal: Y, cubieCoord: new Vector3Int(1, 2, 2), n: 3,
                tangentA: X, screenA: new Vector2(1f, 0f),
                tangentB: Z, screenB: new Vector2(0f, -1f),
                drag: new Vector2(70f, 0f),
                out Move move, out float along);

            Assert.IsTrue(ok);
            Assert.AreEqual(new Move(Axis.Z, 0, 1), move);   // F
        }

        [Test]
        public void 앞면을_위로_밀면_R축이_고른다()
        {
            bool ok = SwipeResolver.Resolve(
                normal: Z, cubieCoord: new Vector3Int(2, 1, 2), n: 3,
                tangentA: X, screenA: new Vector2(1f, 0f),
                tangentB: Y, screenB: new Vector2(0f, 1f),
                drag: new Vector2(2f, 90f),
                out Move move, out float along);

            Assert.IsTrue(ok);
            Assert.AreEqual(Axis.X, move.Axis);
            Assert.AreEqual(0, move.Layer);                  // x=2 이므로 R층
            Assert.AreEqual(1, move.Turns);                  // R
        }

        [Test]
        public void 네칸_큐브의_안쪽_층도_고를_수_있다()
        {
            bool ok = SwipeResolver.Resolve(
                normal: Z, cubieCoord: new Vector3Int(2, 1, 3), n: 4,
                tangentA: X, screenA: new Vector2(1f, 0f),
                tangentB: Y, screenB: new Vector2(0f, 1f),
                drag: new Vector2(1f, 90f),
                out Move move, out float along);

            Assert.IsTrue(ok);
            Assert.AreEqual(Axis.X, move.Axis);
            Assert.AreEqual(1, move.Layer);                  // x=2, n=4 -> layer 1
        }

        [Test]
        public void 드래그가_너무_작으면_판정하지_않는다()
        {
            bool ok = SwipeResolver.Resolve(
                normal: Z, cubieCoord: new Vector3Int(1, 1, 2), n: 3,
                tangentA: X, screenA: new Vector2(1f, 0f),
                tangentB: Y, screenB: new Vector2(0f, 1f),
                drag: Vector2.zero,
                out Move move, out float along);

            Assert.IsFalse(ok);
        }

        [Test]
        public void 판정한_무브를_실제로_적용하면_그_면이_돈다()
        {
            // 앞면 윗줄을 오른쪽으로 밀면 U'이고, U'을 적용하면 F 윗줄이 R 윗줄로 간다.
            SwipeResolver.Resolve(
                normal: Z, cubieCoord: new Vector3Int(1, 2, 2), n: 3,
                tangentA: X, screenA: new Vector2(1f, 0f),
                tangentB: Y, screenB: new Vector2(0f, 1f),
                drag: new Vector2(80f, 0f),
                out Move move, out _);

            var c = CubeState.Solved(3);
            byte front = c.Get(Face.F, 0, 1);
            c.Apply(move);
            Assert.AreEqual(front, c.Get(Face.R, 0, 1), "U'이면 앞면 윗줄이 오른쪽 면으로 가야 한다");
        }
    }
}
