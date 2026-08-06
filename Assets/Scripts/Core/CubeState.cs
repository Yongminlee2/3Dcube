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
        /// 층 안에 들어가는 칸이 어디로 가는지는 MovePermutation이 계산한다.
        /// 그림을 옮기는 쪽도 같은 표를 보므로 상태와 화면이 어긋날 여지가 없다.
        public void Apply(Move m)
        {
            if (m.Layer >= N) throw new ArgumentOutOfRangeException(nameof(m), $"층 {m.Layer}은(는) {N}칸 큐브에 없다");

            int[] perm = MovePermutation.For(m, N);
            var result = (byte[])Facelets.Clone();
            for (int i = 0; i < perm.Length; i++)
                if (perm[i] >= 0) result[perm[i]] = Facelets[i];
            Array.Copy(result, Facelets, Facelets.Length);
        }
    }
}
