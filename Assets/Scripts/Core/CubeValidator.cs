using System;
using System.Collections.Generic;

namespace Cube.Core
{
    /// 유효성 검사 결과. 실패하면 왜 실패했는지까지 알려준다 —
    /// "맞출 수 없습니다"만으로는 사용자가 어디를 고쳐야 할지 모른다.
    public readonly struct ValidationResult
    {
        public bool IsValid { get; }
        public string Reason { get; }

        ValidationResult(bool ok, string reason) { IsValid = ok; Reason = reason; }

        public static readonly ValidationResult Ok = new ValidationResult(true, "");
        public static ValidationResult Fail(string reason) => new ValidationResult(false, reason);

        public override string ToString() => IsValid ? "유효함" : $"유효하지 않음: {Reason}";
    }

    /// 임의의 칸 색 배열이 실제로 조립 가능한 3×3 큐브인지 판정한다.
    ///
    /// 사용자가 손으로 넣은 색에는 오타가 있을 수 있고, 그대로 솔버에 넣으면
    /// 영원히 답을 못 찾는다. 특히 비틀림·뒤집힘·순열 홀짝은 눈으로 알 수 없다.
    public static class CubeValidator
    {
        // 모서리 여덟 자리. 각 자리의 세 칸을 (면, 행, 열)로 적는다.
        // 순서가 중요하다 — 첫 칸이 U나 D를 향하는 칸이어야 비틀림을 셀 수 있다.
        static readonly (Face f, int r, int c)[][] CornerSlots =
        {
            new[] { (Face.U, 0, 0), (Face.L, 0, 0), (Face.B, 0, 2) },   // 왼쪽 뒤 위
            new[] { (Face.U, 0, 2), (Face.B, 0, 0), (Face.R, 0, 2) },   // 오른쪽 뒤 위
            new[] { (Face.U, 2, 2), (Face.R, 0, 0), (Face.F, 0, 2) },   // 오른쪽 앞 위
            new[] { (Face.U, 2, 0), (Face.F, 0, 0), (Face.L, 0, 2) },   // 왼쪽 앞 위
            new[] { (Face.D, 0, 0), (Face.L, 2, 2), (Face.F, 2, 0) },   // 왼쪽 앞 아래
            new[] { (Face.D, 0, 2), (Face.F, 2, 2), (Face.R, 2, 0) },   // 오른쪽 앞 아래
            new[] { (Face.D, 2, 2), (Face.R, 2, 2), (Face.B, 2, 0) },   // 오른쪽 뒤 아래
            new[] { (Face.D, 2, 0), (Face.B, 2, 2), (Face.L, 2, 0) },   // 왼쪽 뒤 아래
        };

        // 엣지 열두 자리. 첫 칸이 U/D를 향하면 그쪽, 아니면 F/B를 향하는 칸을 앞에 둔다.
        static readonly (Face f, int r, int c)[][] EdgeSlots =
        {
            new[] { (Face.U, 0, 1), (Face.B, 0, 1) },
            new[] { (Face.U, 1, 2), (Face.R, 0, 1) },
            new[] { (Face.U, 2, 1), (Face.F, 0, 1) },
            new[] { (Face.U, 1, 0), (Face.L, 0, 1) },
            new[] { (Face.D, 0, 1), (Face.F, 2, 1) },
            new[] { (Face.D, 1, 2), (Face.R, 2, 1) },
            new[] { (Face.D, 2, 1), (Face.B, 2, 1) },
            new[] { (Face.D, 1, 0), (Face.L, 2, 1) },
            new[] { (Face.F, 1, 0), (Face.L, 1, 2) },
            new[] { (Face.F, 1, 2), (Face.R, 1, 0) },
            new[] { (Face.B, 1, 0), (Face.R, 1, 2) },
            new[] { (Face.B, 1, 2), (Face.L, 1, 0) },
        };

        public static ValidationResult Validate(CubeState s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.N != 3) throw new ArgumentException("3x3만 검사한다", nameof(s));

            var r = CheckColorCounts(s);            if (!r.IsValid) return r;
            r = CheckCenters(s);                    if (!r.IsValid) return r;

            r = ReadCorners(s, out int[] cornerPiece, out int[] cornerTwist);
            if (!r.IsValid) return r;

            r = ReadEdges(s, out int[] edgePiece, out int[] edgeFlip);
            if (!r.IsValid) return r;

            r = CheckTwist(cornerTwist);            if (!r.IsValid) return r;
            r = CheckFlip(edgeFlip);                if (!r.IsValid) return r;
            return CheckParity(cornerPiece, edgePiece);
        }

        static ValidationResult CheckColorCounts(CubeState s)
        {
            var counts = new int[6];
            foreach (byte v in s.Facelets)
            {
                if (v > 5) return ValidationResult.Fail($"색 번호 {v}는 없는 색이다");
                counts[v]++;
            }
            for (int c = 0; c < 6; c++)
                if (counts[c] != 9)
                    return ValidationResult.Fail($"{Name(c)} 칸이 {counts[c]}개다. 각 색은 9개여야 한다");
            return ValidationResult.Ok;
        }

        static ValidationResult CheckCenters(CubeState s)
        {
            var seen = new HashSet<byte>();
            foreach (Face f in Faces.All)
                if (!seen.Add(s.Get(f, 1, 1)))
                    return ValidationResult.Fail("가운데 칸에 같은 색이 두 번 나온다");
            return ValidationResult.Ok;
        }

        /// 각 모서리 자리가 어떤 조각인지와 몇 번 비틀렸는지 읽는다.
        static ValidationResult ReadCorners(CubeState s, out int[] piece, out int[] twist)
        {
            piece = new int[8];
            twist = new int[8];

            // 완성 상태의 각 자리 색 조합이 곧 "조각의 정체"다.
            var solved = CubeState.Solved(3);
            var used = new bool[8];

            for (int slot = 0; slot < 8; slot++)
            {
                var cells = CornerSlots[slot];
                var colors = new byte[3];
                for (int i = 0; i < 3; i++) colors[i] = s.Get(cells[i].f, cells[i].r, cells[i].c);

                bool found = false;
                for (int candidate = 0; candidate < 8 && !found; candidate++)
                {
                    var target = CornerSlots[candidate];
                    var want = new byte[3];
                    for (int i = 0; i < 3; i++) want[i] = solved.Get(target[i].f, target[i].r, target[i].c);

                    // 세 칸은 돌아가 있을 수 있다. 세 번 돌려 보며 맞는 방향을 찾는다.
                    for (int t = 0; t < 3; t++)
                        if (colors[t] == want[0] && colors[(t + 1) % 3] == want[1] && colors[(t + 2) % 3] == want[2])
                        {
                            if (used[candidate])
                                return ValidationResult.Fail($"같은 모서리 조각이 두 번 나온다 ({Triple(want)})");
                            used[candidate] = true;
                            piece[slot] = candidate;
                            twist[slot] = t;
                            found = true;
                            break;
                        }
                }

                if (!found)
                    return ValidationResult.Fail($"실제 큐브에 없는 모서리다 ({Triple(colors)})");
            }
            return ValidationResult.Ok;
        }

        static ValidationResult ReadEdges(CubeState s, out int[] piece, out int[] flip)
        {
            piece = new int[12];
            flip = new int[12];

            var solved = CubeState.Solved(3);
            var used = new bool[12];

            for (int slot = 0; slot < 12; slot++)
            {
                var cells = EdgeSlots[slot];
                byte a = s.Get(cells[0].f, cells[0].r, cells[0].c);
                byte b = s.Get(cells[1].f, cells[1].r, cells[1].c);

                bool found = false;
                for (int candidate = 0; candidate < 12 && !found; candidate++)
                {
                    var target = EdgeSlots[candidate];
                    byte wa = solved.Get(target[0].f, target[0].r, target[0].c);
                    byte wb = solved.Get(target[1].f, target[1].r, target[1].c);

                    int f = -1;
                    if (a == wa && b == wb) f = 0;
                    else if (a == wb && b == wa) f = 1;
                    if (f < 0) continue;

                    if (used[candidate])
                        return ValidationResult.Fail($"같은 엣지 조각이 두 번 나온다 ({Name(wa)}·{Name(wb)})");
                    used[candidate] = true;
                    piece[slot] = candidate;
                    flip[slot] = f;
                    found = true;
                }

                if (!found)
                    return ValidationResult.Fail($"실제 큐브에 없는 엣지다 ({Name(a)}·{Name(b)})");
            }
            return ValidationResult.Ok;
        }

        static ValidationResult CheckTwist(int[] twist)
        {
            int sum = 0;
            foreach (int t in twist) sum += t;
            return sum % 3 == 0
                ? ValidationResult.Ok
                : ValidationResult.Fail("모서리 하나가 돌아간 채로 끼워져 있다. 눈으로는 알기 어려운 상태다");
        }

        static ValidationResult CheckFlip(int[] flip)
        {
            int sum = 0;
            foreach (int f in flip) sum += f;
            return sum % 2 == 0
                ? ValidationResult.Ok
                : ValidationResult.Fail("엣지 하나가 뒤집힌 채로 끼워져 있다. 눈으로는 알기 어려운 상태다");
        }

        static ValidationResult CheckParity(int[] cornerPiece, int[] edgePiece)
        {
            return PermutationParity(cornerPiece) == PermutationParity(edgePiece)
                ? ValidationResult.Ok
                : ValidationResult.Fail("조각 두 개가 서로 자리를 바꾼 채로 끼워져 있다");
        }

        /// 순열을 자리바꿈 횟수의 홀짝으로 나타낸다.
        static int PermutationParity(int[] p)
        {
            var a = (int[])p.Clone();
            int swaps = 0;
            for (int i = 0; i < a.Length; i++)
                while (a[i] != i)
                {
                    int j = a[i];
                    (a[i], a[j]) = (a[j], a[i]);
                    swaps++;
                }
            return swaps % 2;
        }

        static string Name(int color)
        {
            switch (color)
            {
                case 0: return "위";
                case 1: return "아래";
                case 2: return "앞";
                case 3: return "뒤";
                case 4: return "왼쪽";
                case 5: return "오른쪽";
                default: return $"?{color}";
            }
        }

        static string Triple(byte[] c) => $"{Name(c[0])}·{Name(c[1])}·{Name(c[2])}";
    }
}
