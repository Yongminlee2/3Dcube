using NUnit.Framework;
using Cube.Core;

namespace Cube.Core.Tests
{
    public class CubeCoordsTests
    {
        static readonly int[] Sizes = { 2, 3, 4 };

        [Test]
        public void 칸과_좌표는_서로_왕복한다()
        {
            foreach (int n in Sizes)
                for (int f = 0; f < 6; f++)
                    for (int row = 0; row < n; row++)
                        for (int col = 0; col < n; col++)
                        {
                            var p = CubeCoords.ToPoint((Face)f, row, col, n);
                            CubeCoords.ToFacelet(p, n, out Face f2, out int r2, out int c2);
                            Assert.AreEqual((Face)f, f2, $"n={n} face={f} row={row} col={col}");
                            Assert.AreEqual(row, r2, $"n={n} face={f} row={row} col={col}");
                            Assert.AreEqual(col, c2, $"n={n} face={f} row={row} col={col}");
                        }
        }

        [Test]
        public void 서로_다른_칸은_서로_다른_좌표를_갖는다()
        {
            foreach (int n in Sizes)
            {
                var seen = new System.Collections.Generic.HashSet<(int, int, int, int, int, int)>();
                for (int f = 0; f < 6; f++)
                    for (int row = 0; row < n; row++)
                        for (int col = 0; col < n; col++)
                        {
                            var p = CubeCoords.ToPoint((Face)f, row, col, n);
                            Assert.IsTrue(seen.Add((p.X, p.Y, p.Z, p.NX, p.NY, p.NZ)),
                                $"n={n} 에서 좌표가 겹쳤다: face={f} row={row} col={col}");
                        }
                Assert.AreEqual(6 * n * n, seen.Count);
            }
        }

        [Test]
        public void 네_번_돌리면_제자리로_돌아온다()
        {
            foreach (int n in Sizes)
                foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
                    for (int f = 0; f < 6; f++)
                        for (int row = 0; row < n; row++)
                            for (int col = 0; col < n; col++)
                            {
                                var start = CubeCoords.ToPoint((Face)f, row, col, n);
                                var p = start;
                                for (int i = 0; i < 4; i++) p = CubeCoords.RotateCW(p, axis, n);
                                Assert.AreEqual(start.X, p.X); Assert.AreEqual(start.Y, p.Y);
                                Assert.AreEqual(start.Z, p.Z); Assert.AreEqual(start.NX, p.NX);
                                Assert.AreEqual(start.NY, p.NY); Assert.AreEqual(start.NZ, p.NZ);
                            }
        }

        [Test]
        public void 각_면의_좌상단은_규정된_자리에_있다()
        {
            // n=3 기준. U의 (0,0)은 L·B 쪽 모서리, F의 (0,0)은 L·U 쪽 모서리.
            var u = CubeCoords.ToPoint(Face.U, 0, 0, 3);
            Assert.AreEqual((0, 2, 0), (u.X, u.Y, u.Z));
            var f = CubeCoords.ToPoint(Face.F, 0, 0, 3);
            Assert.AreEqual((0, 2, 2), (f.X, f.Y, f.Z));
            var r = CubeCoords.ToPoint(Face.R, 0, 0, 3);
            Assert.AreEqual((2, 2, 2), (r.X, r.Y, r.Z));
            var b = CubeCoords.ToPoint(Face.B, 0, 0, 3);
            Assert.AreEqual((2, 2, 0), (b.X, b.Y, b.Z));
        }

        [Test]
        public void X축_시계방향_회전은_F를_U로_보낸다()
        {
            // R 회전에서 F면 오른쪽 가운데 칸은 U면 오른쪽 가운데로 간다.
            var p = CubeCoords.ToPoint(Face.F, 1, 2, 3);
            var q = CubeCoords.RotateCW(p, Axis.X, 3);
            CubeCoords.ToFacelet(q, 3, out Face face, out int row, out int col);
            Assert.AreEqual(Face.U, face);
            Assert.AreEqual(1, row);
            Assert.AreEqual(2, col);
        }

        [Test]
        public void X축_시계방향_회전은_U를_B로_보낸다()
        {
            var p = CubeCoords.ToPoint(Face.U, 1, 2, 3);
            var q = CubeCoords.RotateCW(p, Axis.X, 3);
            CubeCoords.ToFacelet(q, 3, out Face face, out int row, out int col);
            Assert.AreEqual(Face.B, face);
            Assert.AreEqual(1, row);
            Assert.AreEqual(0, col);
        }
    }
}
