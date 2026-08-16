using System;
using System.Collections.Generic;

namespace Cube.Core
{
    /// <summary>
    /// 색상 큐브를 다 푼 뒤 남는 그림 큐브의 센터 방향만 맞춘다.
    ///
    /// 일반 3x3 풀이 상태에는 센터의 0/90/180/270도 방향이 들어 있지 않다.
    /// 따라서 색 풀이에는 HintEngine을 그대로 쓰고, 마지막에만 색 배치를 전혀
    /// 바꾸지 않는 picture-cube 공식을 조합한다. 사용자가 지나온 수를 되감지
    /// 않으므로 앞에서 몇 번을 돌렸든 이 단계의 길이는 센터 방향에만 좌우된다.
    /// </summary>
    public static class ArtworkCenterSolver
    {
        const int FaceCount = Faces.Count;
        const int StateCount = 1 << (FaceCount * 2); // 센터마다 2비트(0~3)

        readonly struct Operation
        {
            public readonly int DeltaKey;
            public readonly List<Move> Moves;

            public Operation(int deltaKey, List<Move> moves)
            {
                DeltaKey = deltaKey;
                Moves = moves;
            }
        }

        static Operation[] _operations;

        /// <param name="currentTurns">
        /// 각 면 센터가 완성 방향에서 시계 방향으로 돌아간 90도 횟수. U,D,F,B,L,R 순서다.
        /// </param>
        public static bool TryPlan(IReadOnlyList<int> currentTurns, out List<Move> moves)
        {
            moves = new List<Move>();
            if (currentTurns == null || currentTurns.Count != FaceCount) return false;

            int start = Encode(currentTurns);
            if (start == 0) return true;

            var operations = Operations();
            var distance = new int[StateCount];
            var visited = new bool[StateCount];
            var previousState = new int[StateCount];
            var previousOperation = new short[StateCount];
            for (int i = 0; i < StateCount; i++)
            {
                distance[i] = int.MaxValue;
                previousState[i] = -1;
                previousOperation[i] = -1;
            }
            distance[start] = 0;

            // 상태가 4096개뿐이라 별도 힙보다 단순 다익스트라가 작고 예측 가능하다.
            for (int iteration = 0; iteration < StateCount; iteration++)
            {
                int state = -1;
                int best = int.MaxValue;
                for (int i = 0; i < StateCount; i++)
                    if (!visited[i] && distance[i] < best)
                    {
                        best = distance[i];
                        state = i;
                    }

                if (state < 0) break;
                if (state == 0) break;
                visited[state] = true;

                for (short opIndex = 0; opIndex < operations.Length; opIndex++)
                {
                    var op = operations[opIndex];
                    int next = Add(state, op.DeltaKey);
                    int candidate = best + op.Moves.Count;
                    if (candidate >= distance[next]) continue;
                    distance[next] = candidate;
                    previousState[next] = state;
                    previousOperation[next] = opIndex;
                }
            }

            if (distance[0] == int.MaxValue) return false;

            var path = new List<int>();
            for (int state = 0; state != start; state = previousState[state])
            {
                int op = previousOperation[state];
                if (op < 0) return false;
                path.Add(op);
            }
            path.Reverse();

            foreach (int op in path) moves.AddRange(operations[op].Moves);
            moves = Reduce(moves);
            return true;
        }

        static Operation[] Operations()
        {
            if (_operations != null) return _operations;

            var result = new List<Operation>();

            // 한 센터를 180도 돌리는 12수 공식.
            var halfBase = MoveNotation.Parse(
                "U R L U2 R' L' U R L U2 R' L'", 3);
            for (int face = 0; face < FaceCount; face++)
            {
                Face side = FirstAdjacent((Face)face);
                AddUnique(result, Conjugate(halfBase, (Face)face, side));
            }

            // 인접한 두 센터를 서로 반대 방향으로 90도 돌리는 공식.
            // U에는 +90도, R에는 -90도가 적용된다.
            var pairSeed = MoveNotation.Parse(
                "R U' R U R U R U' R' U' R2", 3);
            var pairBase = new List<Move>(pairSeed.Count * 3);
            for (int i = 0; i < 3; i++) pairBase.AddRange(pairSeed);
            for (int a = 0; a < FaceCount; a++)
                for (int b = 0; b < FaceCount; b++)
                    if (a != b && AreAdjacent((Face)a, (Face)b))
                        AddUnique(result, Conjugate(pairBase, (Face)a, (Face)b));

            // 같은 효과를 더 짧게 만드는 18수 관계식과 그 역수도 후보에 넣는다.
            // 색상 상태에는 항등이지만 여러 센터 방향을 한 번에 정리한다.
            var shortSeed = MoveNotation.Parse("U2 L2 F' B' R L", 3);
            var shortBase = new List<Move>(shortSeed.Count * 3);
            for (int i = 0; i < 3; i++) shortBase.AddRange(shortSeed);
            for (int top = 0; top < FaceCount; top++)
                for (int right = 0; right < FaceCount; right++)
                {
                    if (top == right || !AreAdjacent((Face)top, (Face)right)) continue;
                    var transformed = Conjugate(shortBase, (Face)top, (Face)right);
                    AddUnique(result, transformed);
                    AddUnique(result, Inverse(transformed));
                }

            _operations = result.ToArray();
            return _operations;
        }

        static void AddUnique(List<Operation> operations, List<Move> moves)
        {
            moves = Reduce(moves);
            int delta = DeltaOf(moves);
            if (delta == 0) return;

            for (int i = 0; i < operations.Count; i++)
            {
                if (operations[i].DeltaKey != delta) continue;
                if (operations[i].Moves.Count <= moves.Count) return;
                operations.RemoveAt(i);
                break;
            }
            operations.Add(new Operation(delta, moves));
        }

        /// base U를 top으로, base R을 right로 보내는 정육면체 회전으로 공식을 켤레 변환한다.
        static List<Move> Conjugate(IReadOnlyList<Move> source, Face top, Face right)
        {
            SignedAxis x = Normal(right);
            SignedAxis y = Normal(top);
            if (x.Axis == y.Axis) throw new ArgumentException("top과 right는 인접 면이어야 한다");
            SignedAxis z = Cross(x, y);
            var basis = new[] { x, y, z };

            var result = new List<Move>(source.Count);
            foreach (var move in source)
            {
                SignedAxis mapped = basis[(int)move.Axis];
                int layer = move.Layer;
                int turns = move.Turns;
                if (mapped.Sign < 0)
                {
                    layer = 2 - layer;
                    turns = 4 - turns;
                }
                result.Add(new Move(mapped.Axis, layer, turns));
            }
            return result;
        }

        static int DeltaOf(IEnumerable<Move> moves)
        {
            int key = 0;
            foreach (var move in moves)
            {
                int face;
                int turns;
                if (move.Layer == 0)
                {
                    face = PositiveFace(move.Axis);
                    turns = move.Turns;
                }
                else if (move.Layer == 2)
                {
                    face = NegativeFace(move.Axis);
                    turns = 4 - move.Turns;
                }
                else continue;

                int old = (key >> (face * 2)) & 3;
                key = Replace(key, face, (old + turns) & 3);
            }
            return key;
        }

        static List<Move> Reduce(IEnumerable<Move> source)
        {
            var result = new List<Move>();
            foreach (var move in source)
            {
                int last = result.Count - 1;
                if (last >= 0 && result[last].Axis == move.Axis && result[last].Layer == move.Layer)
                {
                    int turns = (result[last].Turns + move.Turns) & 3;
                    result.RemoveAt(last);
                    if (turns != 0) result.Add(new Move(move.Axis, move.Layer, turns));
                }
                else result.Add(move);
            }
            return result;
        }

        static List<Move> Inverse(IReadOnlyList<Move> source)
        {
            var result = new List<Move>(source.Count);
            for (int i = source.Count - 1; i >= 0; i--) result.Add(source[i].Inverse);
            return result;
        }

        static int Encode(IReadOnlyList<int> turns)
        {
            int key = 0;
            for (int face = 0; face < FaceCount; face++)
                key |= (turns[face] & 3) << (face * 2);
            return key;
        }

        static int Add(int state, int delta)
        {
            int result = state;
            for (int face = 0; face < FaceCount; face++)
            {
                int value = (((state >> (face * 2)) & 3)
                           + ((delta >> (face * 2)) & 3)) & 3;
                result = Replace(result, face, value);
            }
            return result;
        }

        static int Replace(int key, int face, int value)
        {
            int shift = face * 2;
            return (key & ~(3 << shift)) | ((value & 3) << shift);
        }

        static bool AreAdjacent(Face a, Face b) => Normal(a).Axis != Normal(b).Axis;

        static Face FirstAdjacent(Face face)
        {
            for (int i = 0; i < FaceCount; i++)
                if (AreAdjacent(face, (Face)i)) return (Face)i;
            throw new InvalidOperationException("인접 면을 찾을 수 없다");
        }

        readonly struct SignedAxis
        {
            public readonly Axis Axis;
            public readonly int Sign;
            public SignedAxis(Axis axis, int sign) { Axis = axis; Sign = sign; }
        }

        static SignedAxis Normal(Face face)
        {
            switch (face)
            {
                case Face.U: return new SignedAxis(Axis.Y, 1);
                case Face.D: return new SignedAxis(Axis.Y, -1);
                case Face.F: return new SignedAxis(Axis.Z, 1);
                case Face.B: return new SignedAxis(Axis.Z, -1);
                case Face.L: return new SignedAxis(Axis.X, -1);
                case Face.R: return new SignedAxis(Axis.X, 1);
                default: throw new ArgumentOutOfRangeException(nameof(face));
            }
        }

        static SignedAxis Cross(SignedAxis a, SignedAxis b)
        {
            int sign = a.Sign * b.Sign;
            if (a.Axis == Axis.X && b.Axis == Axis.Y) return new SignedAxis(Axis.Z, sign);
            if (a.Axis == Axis.Y && b.Axis == Axis.Z) return new SignedAxis(Axis.X, sign);
            if (a.Axis == Axis.Z && b.Axis == Axis.X) return new SignedAxis(Axis.Y, sign);
            if (a.Axis == Axis.Y && b.Axis == Axis.X) return new SignedAxis(Axis.Z, -sign);
            if (a.Axis == Axis.Z && b.Axis == Axis.Y) return new SignedAxis(Axis.X, -sign);
            if (a.Axis == Axis.X && b.Axis == Axis.Z) return new SignedAxis(Axis.Y, -sign);
            throw new ArgumentException("나란한 축은 외적할 수 없다");
        }

        static int PositiveFace(Axis axis)
            => axis == Axis.X ? (int)Face.R : axis == Axis.Y ? (int)Face.U : (int)Face.F;

        static int NegativeFace(Axis axis)
            => axis == Axis.X ? (int)Face.L : axis == Axis.Y ? (int)Face.D : (int)Face.B;
    }
}
