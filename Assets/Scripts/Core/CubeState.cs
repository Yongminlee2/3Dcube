using System;
using System.Collections.Generic;

namespace Cube.Core
{
    /// 큐브의 상태. 칸마다 색 번호(0~5)를 담는다.
    /// 인덱스는 face * N * N + row * N + col 이다.
    ///
    /// 성능을 위해 가변 객체로 둔다. 사본이 필요하면 Clone()을 쓴다.
    public sealed class CubeState
    {
        public int N { get; }
        public byte[] Facelets { get; }

        CubeState(int n, byte[] facelets) { N = n; Facelets = facelets; }

        public static CubeState Solved(int n)
        {
            if (n < 2) throw new ArgumentOutOfRangeException(nameof(n));
            var f = new byte[Faces.Count * n * n];
            for (int face = 0; face < Faces.Count; face++)
                for (int i = 0; i < n * n; i++)
                    f[face * n * n + i] = (byte)face;
            return new CubeState(n, f);
        }

        public CubeState Clone() => new CubeState(N, (byte[])Facelets.Clone());

        public int IndexOf(Face face, int row, int col) => ((int)face * N + row) * N + col;

        public byte Get(Face face, int row, int col) => Facelets[IndexOf(face, row, col)];

        public bool IsSolved()
        {
            int per = N * N;
            for (int face = 0; face < Faces.Count; face++)
            {
                byte first = Facelets[face * per];
                for (int i = 1; i < per; i++)
                    if (Facelets[face * per + i] != first) return false;
            }
            return true;
        }

        public bool SameAs(CubeState other)
        {
            if (other == null || other.N != N) return false;
            for (int i = 0; i < Facelets.Length; i++)
                if (Facelets[i] != other.Facelets[i]) return false;
            return true;
        }

        public void Apply(IEnumerable<Move> moves)
        {
            foreach (var m in moves) Apply(m);
        }

        /// 회전 한 번을 적용한다.
        ///
        /// 층 안에 들어가는 칸을 전부 찾아 좌표째로 돌린 다음 다시 칸 번호로 되돌린다.
        /// 이 방식이면 바깥층(면 전체가 도는 경우)과 안쪽층(띠만 도는 경우)을
        /// 따로 처리할 필요가 없다.
        public void Apply(Move m)
        {
            if (m.Layer >= N) throw new ArgumentOutOfRangeException(nameof(m), $"층 {m.Layer}은(는) {N}칸 큐브에 없다");

            int target = N - 1 - m.Layer;
            var result = (byte[])Facelets.Clone();

            for (int f = 0; f < Faces.Count; f++)
                for (int row = 0; row < N; row++)
                    for (int col = 0; col < N; col++)
                    {
                        var p = CubeCoords.ToPoint((Face)f, row, col, N);
                        if (CubeCoords.Component(p, m.Axis) != target) continue;

                        for (int t = 0; t < m.Turns; t++) p = CubeCoords.RotateCW(p, m.Axis, N);

                        CubeCoords.ToFacelet(p, N, out Face nf, out int nr, out int nc);
                        result[IndexOf(nf, nr, nc)] = Facelets[IndexOf((Face)f, row, col)];
                    }

            Array.Copy(result, Facelets, Facelets.Length);
        }
    }
}
