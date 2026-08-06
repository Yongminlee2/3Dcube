using System;
using System.Collections.Generic;
using System.Text;

namespace Cube.Core
{
    /// 사람이 읽는 노테이션과 Move 사이를 오간다. 상태를 알지 못한다.
    public static class MoveNotation
    {
        // 면 문자 -> (축, 축의 양의 방향 끝인가)
        static bool TryFace(char c, out Axis axis, out bool positiveEnd)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'R': axis = Axis.X; positiveEnd = true;  return true;
                case 'L': axis = Axis.X; positiveEnd = false; return true;
                case 'U': axis = Axis.Y; positiveEnd = true;  return true;
                case 'D': axis = Axis.Y; positiveEnd = false; return true;
                case 'F': axis = Axis.Z; positiveEnd = true;  return true;
                case 'B': axis = Axis.Z; positiveEnd = false; return true;
                default: axis = Axis.X; positiveEnd = false; return false;
            }
        }

        public static List<Move> Parse(string text, int n)
        {
            var result = new List<Move>();
            if (string.IsNullOrWhiteSpace(text)) return result;
            foreach (var token in text.Split(new[] { ' ', '\t', '\n', '\r' },
                                             StringSplitOptions.RemoveEmptyEntries))
                result.AddRange(ParseToken(token, n));
            return result;
        }

        public static List<Move> ParseToken(string token, int n)
        {
            if (string.IsNullOrEmpty(token)) throw new FormatException("빈 토큰");

            int i = 0;
            int depth = 0;
            while (i < token.Length && char.IsDigit(token[i]))
                depth = depth * 10 + (token[i++] - '0');
            bool explicitDepth = depth > 0;
            if (!explicitDepth) depth = 1;

            if (i >= token.Length) throw new FormatException($"면 문자가 없다: '{token}'");
            char faceChar = token[i];
            if (!TryFace(faceChar, out Axis axis, out bool positiveEnd))
                throw new FormatException($"모르는 면 문자: '{token}'");
            bool wide = char.IsLower(faceChar);
            i++;

            if (i < token.Length && (token[i] == 'w' || token[i] == 'W')) { wide = true; i++; }

            int turns = positiveEnd ? 1 : 3;
            if (i < token.Length)
            {
                if (token[i] == '\'') { turns = 4 - turns; i++; }
                else if (token[i] == '2') { turns = 2; i++; }
                else throw new FormatException($"모르는 수식어: '{token}'");
            }
            if (i != token.Length) throw new FormatException($"남는 글자가 있다: '{token}'");

            // 넓은수인데 깊이를 안 적었으면 두 층이다.
            int count = wide ? (explicitDepth ? depth : 2) : 1;
            int firstFromFace = wide ? 0 : depth - 1;

            if (firstFromFace + count > n)
                throw new FormatException($"'{token}'는 {n}칸 큐브에 들어가지 않는다");

            var moves = new List<Move>(count);
            for (int k = 0; k < count; k++)
            {
                int fromFace = firstFromFace + k;
                int layer = positiveEnd ? fromFace : n - 1 - fromFace;
                moves.Add(new Move(axis, layer, turns));
            }
            return moves;
        }

        public static string Format(Move m, int n)
        {
            char letter;
            int turns = m.Turns;
            string prefix = "";

            if (m.Layer == 0)
            {
                letter = PositiveLetter(m.Axis);
            }
            else if (m.Layer == n - 1)
            {
                letter = NegativeLetter(m.Axis);
                turns = 4 - turns;
            }
            else
            {
                letter = PositiveLetter(m.Axis);
                prefix = (m.Layer + 1).ToString();
            }

            string suffix = turns == 1 ? "" : turns == 2 ? "2" : "'";
            return prefix + letter + suffix;
        }

        public static string Format(IEnumerable<Move> moves, int n)
        {
            var sb = new StringBuilder();
            foreach (var m in moves)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(Format(m, n));
            }
            return sb.ToString();
        }

        static char PositiveLetter(Axis a) => a == Axis.X ? 'R' : a == Axis.Y ? 'U' : 'F';
        static char NegativeLetter(Axis a) => a == Axis.X ? 'L' : a == Axis.Y ? 'D' : 'B';
    }
}
