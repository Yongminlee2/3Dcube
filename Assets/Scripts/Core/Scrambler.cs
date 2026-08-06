using System;
using System.Text;

namespace Cube.Core
{
    /// 완성 상태에서 무작위로 돌려서 섞는다.
    /// 돌리기만 하므로 "풀 수 없는 배치"가 원리적으로 생기지 않는다.
    public static class Scrambler
    {
        static readonly string[] TwoFaces  = { "R", "U", "F" };
        static readonly string[] SixFaces  = { "R", "L", "U", "D", "F", "B" };
        static readonly string[] WideFaces = { "Rw", "Lw", "Uw", "Dw", "Fw", "Bw" };
        static readonly string[] Suffixes  = { "", "'", "2" };

        public static int DefaultLength(int n)
        {
            switch (n)
            {
                case 2: return 11;
                case 3: return 20;
                case 4: return 45;
                default: return 20;
            }
        }

        public static string Generate(int n, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            string[] faces = n == 2 ? TwoFaces : SixFaces;
            bool allowWide = n >= 4;
            int length = DefaultLength(n);

            var sb = new StringBuilder();
            Axis? prevAxis = null;

            for (int i = 0; i < length; i++)
            {
                string token;
                Axis axis;
                while (true)
                {
                    int f = rng.Next(faces.Length);
                    // 4칸 큐브에서는 넓은수를 3분의 1 확률로 섞는다.
                    bool wide = allowWide && rng.Next(3) == 0;
                    string baseToken = wide ? WideFaces[f] : faces[f];
                    token = baseToken + Suffixes[rng.Next(Suffixes.Length)];
                    axis = MoveNotation.ParseToken(token, n)[0].Axis;
                    if (prevAxis != axis) break;
                }

                if (sb.Length > 0) sb.Append(' ');
                sb.Append(token);
                prevAxis = axis;
            }

            return sb.ToString();
        }
    }
}
