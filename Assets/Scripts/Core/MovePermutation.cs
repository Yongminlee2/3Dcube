using System.Collections.Generic;

namespace Cube.Core
{
    /// 회전 하나가 칸을 어디로 보내는지만 계산한다.
    /// 상태를 바꾸는 쪽(CubeState)과 그림을 옮기는 쪽(CubeRenderer)이
    /// 같은 표를 보게 해서 둘이 어긋날 여지를 없앤다.
    public static class MovePermutation
    {
        static readonly Dictionary<(int, Axis, int, int), int[]> Cache
            = new Dictionary<(int, Axis, int, int), int[]>();

        /// result[i] < 0 이면 그 칸은 움직이지 않는다.
        /// result[i] >= 0 이면 i번 칸의 내용이 result[i]번 자리로 간다.
        public static int[] For(Move m, int n)
        {
            var key = (n, m.Axis, m.Layer, m.Turns);
            if (Cache.TryGetValue(key, out var cached)) return cached;

            int target = n - 1 - m.Layer;
            var perm = new int[Faces.Count * n * n];
            for (int i = 0; i < perm.Length; i++) perm[i] = -1;

            for (int f = 0; f < Faces.Count; f++)
                for (int row = 0; row < n; row++)
                    for (int col = 0; col < n; col++)
                    {
                        var p = CubeCoords.ToPoint((Face)f, row, col, n);
                        if (CubeCoords.Component(p, m.Axis) != target) continue;

                        for (int t = 0; t < m.Turns; t++) p = CubeCoords.RotateCW(p, m.Axis, n);
                        CubeCoords.ToFacelet(p, n, out Face nf, out int nr, out int nc);

                        perm[(f * n + row) * n + col] = ((int)nf * n + nr) * n + nc;
                    }

            Cache[key] = perm;
            return perm;
        }
    }
}
