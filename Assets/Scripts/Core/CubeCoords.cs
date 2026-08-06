using System;

namespace Cube.Core
{
    /// 큐브 표면의 칸 하나. pos는 큐비의 격자 좌표(0..N-1), normal은 그 칸이 바라보는 방향.
    public readonly struct FaceletPoint
    {
        public readonly int X, Y, Z;
        public readonly int NX, NY, NZ;

        public FaceletPoint(int x, int y, int z, int nx, int ny, int nz)
        {
            X = x; Y = y; Z = z; NX = nx; NY = ny; NZ = nz;
        }
    }

    /// 칸 좌표계의 규칙을 담는 유일한 자리. 무브도 상태도 알지 못한다.
    ///
    /// 각 면을 바깥에서 정면으로 볼 때 좌상단이 (row 0, col 0)이고,
    /// 위쪽에 오는 면은 U면이면 B, D면이면 F, 나머지 네 면이면 U다.
    public static class CubeCoords
    {
        public static FaceletPoint ToPoint(Face face, int row, int col, int n)
        {
            int m = n - 1;
            switch (face)
            {
                case Face.U: return new FaceletPoint(col,     m,         row,     0,  1,  0);
                case Face.D: return new FaceletPoint(col,     0,         m - row, 0, -1,  0);
                case Face.F: return new FaceletPoint(col,     m - row,   m,       0,  0,  1);
                case Face.B: return new FaceletPoint(m - col, m - row,   0,       0,  0, -1);
                case Face.L: return new FaceletPoint(0,       m - row,   col,    -1,  0,  0);
                case Face.R: return new FaceletPoint(m,       m - row,   m - col, 1,  0,  0);
                default: throw new ArgumentOutOfRangeException(nameof(face));
            }
        }

        public static void ToFacelet(in FaceletPoint p, int n, out Face face, out int row, out int col)
        {
            int m = n - 1;
            if (p.NY == 1)       { face = Face.U; row = p.Z;       col = p.X; }
            else if (p.NY == -1) { face = Face.D; row = m - p.Z;   col = p.X; }
            else if (p.NZ == 1)  { face = Face.F; row = m - p.Y;   col = p.X; }
            else if (p.NZ == -1) { face = Face.B; row = m - p.Y;   col = m - p.X; }
            else if (p.NX == -1) { face = Face.L; row = m - p.Y;   col = p.Z; }
            else if (p.NX == 1)  { face = Face.R; row = m - p.Y;   col = m - p.Z; }
            else throw new ArgumentException("법선이 축 방향이 아니다");
        }

        /// 축의 양의 방향에서 원점을 볼 때 시계방향으로 90° 돌린다.
        public static FaceletPoint RotateCW(in FaceletPoint p, Axis axis, int n)
        {
            RotateGridCW(p.X, p.Y, p.Z, axis, n, out int rx, out int ry, out int rz);
            RotateNormalCW(p.NX, p.NY, p.NZ, axis, out int rnx, out int rny, out int rnz);
            return new FaceletPoint(rx, ry, rz, rnx, rny, rnz);
        }

        /// 격자 좌표만 90° 돌린다. 법선은 다루지 않는다.
        public static void RotateGridCW(int x, int y, int z, Axis axis, int n,
                                        out int rx, out int ry, out int rz)
        {
            int m = n - 1;
            // 중심이 반정수가 되는 짝수 N을 피하려고 좌표를 2배로 늘려서 회전한다.
            int dx = 2 * x - m, dy = 2 * y - m, dz = 2 * z - m;
            int ax, ay, az;
            switch (axis)
            {
                case Axis.X: ax =  dx; ay =  dz; az = -dy; break;
                case Axis.Y: ax = -dz; ay =  dy; az =  dx; break;
                case Axis.Z: ax =  dy; ay = -dx; az =  dz; break;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
            rx = (ax + m) / 2; ry = (ay + m) / 2; rz = (az + m) / 2;
        }

        /// 방향 벡터만 90° 돌린다.
        public static void RotateNormalCW(int nx, int ny, int nz, Axis axis,
                                          out int rx, out int ry, out int rz)
        {
            switch (axis)
            {
                case Axis.X: rx =  nx; ry =  nz; rz = -ny; break;
                case Axis.Y: rx = -nz; ry =  ny; rz =  nx; break;
                case Axis.Z: rx =  ny; ry = -nx; rz =  nz; break;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        public static int Component(in FaceletPoint p, Axis axis)
            => axis == Axis.X ? p.X : axis == Axis.Y ? p.Y : p.Z;
    }
}
