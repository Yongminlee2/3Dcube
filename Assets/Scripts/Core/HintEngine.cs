using System;
using System.Collections.Generic;

namespace Cube.Core
{
    /// 다음에 둘 수와 그 이유.
    public readonly struct Hint
    {
        /// 이 힌트가 진행시키려는 단계 (1~7). 완성이면 0.
        public int Stage { get; }
        /// 둘 수의 노테이션. 빈 문자열이면 둘 수가 없다.
        public string Notation { get; }
        /// 왜 이 수를 두는지 한 줄 설명.
        public string Reason { get; }

        public bool HasMove => !string.IsNullOrEmpty(Notation);
        public bool IsSolved => Stage == 0;

        public Hint(int stage, string notation, string reason)
        {
            Stage = stage; Notation = notation; Reason = reason;
        }

        public static readonly Hint Solved = new Hint(0, "", "이미 다 맞췄습니다.");
    }

    /// 지금 상태에서 다음 한 수를 알려준다.
    ///
    /// 최단 풀이를 내지 않는다. 20수짜리 최적 해답은 왜 그 수를 두는지 설명할 수 없고,
    /// 배우는 사람에게 아무것도 가르치지 못한다. 대신 학습 모드가 가르친 LBL을 따라간다.
    ///
    /// 마지막 층(4~7단계)은 경우가 유한하므로 탐색 없이 판정한다.
    /// 앞 단계(1~3)는 조각 단위 탐색으로 찾는다.
    public static class HintEngine
    {
        /// 앞 단계에서 한 조각을 옮기는 데 허용하는 최대 수. 이보다 길면 포기하고
        /// 거친 안내만 준다. 사람이 손으로 푸는 길이는 대개 이 안에 들어온다.
        public const int MaxSearchDepth = 7;

        public static Hint Next(CubeState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.N != 3) throw new ArgumentException("힌트는 3x3만 낸다", nameof(state));

            if (state.IsSolved()) return Hint.Solved;

            int done = StageChecker.CurrentStage(state);
            int target = done + 1;
            if (target > StageChecker.LastStage) return Hint.Solved;

            return target >= 4 ? LastLayerHint(state, target) : EarlyStageHint(state, target);
        }

        // ---------- 마지막 층: 경우 인식 ----------

        /// 마지막 층은 "위층을 돌려 자세를 맞추고 공식을 쓴다"를 몇 번 반복하면 끝난다.
        /// 그래서 탐색 알파벳을 {U, U2, U', 그 단계 공식} 네 개로 줄인다.
        /// 네 개뿐이라 깊이 8까지 봐도 6만 가지가 안 된다.
        ///
        /// 자세를 맞추지 않고 같은 공식만 반복하면 영원히 수렴하지 않는다.
        /// 처음 구현이 정확히 그랬고, "5단계에서 제자리걸음" 테스트가 잡아냈다.
        const int LastLayerDepth = 8;

        static Hint LastLayerHint(CubeState state, int target)
        {
            var lesson = LessonData.Get(target);
            string alg = lesson.Algorithms[0].Notation;

            // 앞 단계와 같은 알파벳을 쓴다 — 위층 돌리기, 큐브 돌리기, 그 단계 공식.
            // 큐브 돌리기를 빼면 안 된다. 예를 들어 6단계 공식은 왼쪽 앞 모서리를
            // 고정하므로, 다른 모서리를 겨냥하려면 큐브를 돌려야 한다.
            // 처음에 이걸 빼먹어서 "마지막 층에서 제자리걸음" 테스트가 걸렸다.
            var alphabet = WithSetups(lesson.Algorithms);

            var tokens = new List<string>();
            var work = state.Clone();

            for (int depth = 1; depth <= LastLayerDepth; depth++)
                if (SearchToStage(work, target, alphabet, depth, tokens))
                {
                    tokens.Reverse();
                    return BuildLastLayerHint(target, lesson.Title, tokens, alg);
                }

            return new Hint(target, alg, $"{lesson.Title} — 공식을 쓴 뒤 다시 살펴보세요.");
        }

        static bool SearchToStage(CubeState s, int target, Token[] alphabet, int depth, List<string> tokens)
        {
            if (StageChecker.CurrentStage(s) >= target) return true;
            if (depth == 0) return false;

            foreach (var token in alphabet)
            {
                s.Apply(token.Moves);
                if (SearchToStage(s, target, alphabet, depth - 1, tokens))
                {
                    tokens.Add(token.Name);
                    Undo(s, token.Moves);
                    return true;
                }
                Undo(s, token.Moves);
            }
            return false;
        }

        static void Undo(CubeState s, List<Move> moves)
        {
            for (int i = moves.Count - 1; i >= 0; i--) s.Apply(moves[i].Inverse);
        }

        /// 찾은 순서를 통째로 준다.
        ///
        /// 처음에는 첫 공식까지만 잘라서 줬다. 한 걸음씩이 배우기에 낫다고 봤는데,
        /// 같은 길이의 다른 해답이 여럿일 때 앞부분만 적용하면 다음 탐색이 엉뚱한
        /// 해답을 골라 진척이 상쇄된다. "마지막 층에서 제자리걸음" 테스트가 잡아냈다.
        /// 어차피 사용자가 배운 형태 그대로("위층 돌리고 공식")라 읽는 데 무리가 없다.
        static Hint BuildLastLayerHint(int target, string title, List<string> tokens, string alg)
        {
            int algCount = 0;
            foreach (var t in tokens) if (t == alg) algCount++;

            string reason;
            if (algCount == 0) reason = $"{title} — 위층만 돌리면 맞습니다.";
            else if (algCount == 1) reason = $"{title} — 자세를 맞춘 뒤 공식을 한 번 씁니다.";
            else reason = $"{title} — 공식을 {algCount}번 써야 하는 경우입니다.";

            return new Hint(target, string.Join(" ", tokens), reason);
        }

        // ---------- 앞 단계: 조각 단위 탐색 ----------

        static readonly Move[] AllMoves = BuildMoves();

        static Move[] BuildMoves()
        {
            var list = new List<Move>();
            foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
                for (int layer = 0; layer < 3; layer += 2)      // 바깥 두 층만. 가운데 층은 센터를 흔든다
                    for (int turns = 1; turns <= 3; turns++)
                        list.Add(new Move(axis, layer, turns));
            return list.ToArray();
        }

        /// 탐색 알파벳 한 조각. 이름은 사람이 읽을 표기, 무브는 실제로 적용할 것.
        readonly struct Token
        {
            public readonly string Name;
            public readonly List<Move> Moves;
            public Token(string name, List<Move> moves) { Name = name; Moves = moves; }
        }

        static Token[] _stage1, _stage2, _stage3;

        /// 단계마다 탐색 알파벳이 다르다.
        ///
        /// 18개 수를 자유롭게 탐색하면 깊이 6에서 노드가 천만 단위라 폰에서 못 쓴다.
        /// 대신 **우리가 가르친 방법과 같은 알파벳**으로 좁힌다 — 위층 돌리기,
        /// 큐브 돌리기, 그리고 그 단계의 공식. 토큰이 일고여덟 개뿐이라 깊이 8도 싸고,
        /// 찾은 답이 곧 사용자가 배운 그대로라 설명도 된다.
        ///
        /// 1단계(십자)만 공식이 없어서 낱개 수로 찾는다. 대신 깊이를 얕게 잡는다.
        static Token[] Alphabet(int stage)
        {
            switch (stage)
            {
                case 1:
                    if (_stage1 == null)
                    {
                        var list = new List<Token>();
                        foreach (var m in AllMoves)
                            list.Add(new Token(MoveNotation.Format(m, 3), new List<Move> { m }));
                        _stage1 = list.ToArray();
                    }
                    return _stage1;

                case 2:
                    return _stage2 ?? (_stage2 = WithSetups(LessonData.Get(2).Algorithms));

                default:
                    return _stage3 ?? (_stage3 = WithSetups(LessonData.Get(3).Algorithms));
            }
        }

        static Token[] WithSetups(Algorithm[] algorithms)
        {
            var list = new List<Token>();
            foreach (var t in new[] { "U", "U2", "U'", "y", "y2", "y'" })
                list.Add(new Token(t, MoveNotation.ParseToken(t, 3)));
            foreach (var a in algorithms)
                list.Add(new Token(a.Notation, MoveNotation.Parse(a.Notation, 3)));
            return list.ToArray();
        }

        static int DepthFor(int stage) => stage == 1 ? 5 : 8;

        /// 조각 하나를 더 맞추는 가장 짧은 길을 반복 심화로 찾는다.
        /// 이미 맞춘 것을 깨뜨리지 않는 조건도 함께 건다 — 사람이 푸는 방식과 같다.
        static Hint EarlyStageHint(CubeState state, int target)
        {
            var lesson = LessonData.Get(target);
            int now = Progress(state, target);
            var alphabet = Alphabet(target);

            var work = state.Clone();
            for (int depth = 1; depth <= DepthFor(target); depth++)
            {
                var path = new List<string>(depth);
                if (Search(work, target, now, alphabet, depth, path))
                {
                    path.Reverse();
                    return new Hint(target, string.Join(" ", path),
                        $"{lesson.Title} — 조각 하나를 더 맞춥니다. ({now + 1}/4)");
                }
            }

            return new Hint(target, "", $"{lesson.Title} — {Guidance(target)}");
        }

        /// 찾으면 path에 역순으로 쌓아 돌려준다.
        ///
        /// 상태를 복제하지 않고 적용했다가 되돌린다. 매 노드에서 배열을 새로 만들면
        /// 노드 수가 조금만 늘어도 감당이 안 된다.
        static bool Search(CubeState state, int target, int baseline, Token[] alphabet,
                           int depth, List<string> path)
        {
            if (depth == 0) return false;

            foreach (var token in alphabet)
            {
                state.Apply(token.Moves);

                if (Improves(state, target, baseline) ||
                    Search(state, target, baseline, alphabet, depth - 1, path))
                {
                    path.Add(token.Name);
                    Undo(state, token.Moves);
                    return true;
                }

                Undo(state, token.Moves);
            }
            return false;
        }

        /// 앞 단계를 깨뜨리지 않으면서 조각을 하나 더 맞췄는가.
        static bool Improves(CubeState s, int target, int baseline)
        {
            if (target > 1 && !StageChecker.Passed(s, target - 1)) return false;
            return Progress(s, target) > baseline;
        }

        /// 그 단계에서 몇 조각이나 제자리인지 (0~4).
        public static int Progress(CubeState s, int stage)
        {
            switch (stage)
            {
                case 1: return CountBottomCrossEdges(s);
                case 2: return CountBottomCorners(s);
                case 3: return CountMiddleEdges(s);
                default: return StageChecker.Passed(s, stage) ? 4 : 0;
            }
        }

        static byte C(CubeState s, Face f) => s.Get(f, 1, 1);

        static int CountBottomCrossEdges(CubeState s)
        {
            int n = 0;
            if (s.Get(Face.D, 0, 1) == C(s, Face.D) && s.Get(Face.F, 2, 1) == C(s, Face.F)) n++;
            if (s.Get(Face.D, 1, 2) == C(s, Face.D) && s.Get(Face.R, 2, 1) == C(s, Face.R)) n++;
            if (s.Get(Face.D, 2, 1) == C(s, Face.D) && s.Get(Face.B, 2, 1) == C(s, Face.B)) n++;
            if (s.Get(Face.D, 1, 0) == C(s, Face.D) && s.Get(Face.L, 2, 1) == C(s, Face.L)) n++;
            return n;
        }

        static int CountBottomCorners(CubeState s)
        {
            int n = 0;
            if (Ok(s, Face.D, 0, 0, Face.F, 2, 0, Face.L, 2, 2)) n++;
            if (Ok(s, Face.D, 0, 2, Face.F, 2, 2, Face.R, 2, 0)) n++;
            if (Ok(s, Face.D, 2, 2, Face.R, 2, 2, Face.B, 2, 0)) n++;
            if (Ok(s, Face.D, 2, 0, Face.B, 2, 2, Face.L, 2, 0)) n++;
            return n;
        }

        static int CountMiddleEdges(CubeState s)
        {
            int n = 0;
            if (s.Get(Face.F, 1, 0) == C(s, Face.F) && s.Get(Face.L, 1, 2) == C(s, Face.L)) n++;
            if (s.Get(Face.F, 1, 2) == C(s, Face.F) && s.Get(Face.R, 1, 0) == C(s, Face.R)) n++;
            if (s.Get(Face.B, 1, 0) == C(s, Face.B) && s.Get(Face.R, 1, 2) == C(s, Face.R)) n++;
            if (s.Get(Face.B, 1, 2) == C(s, Face.B) && s.Get(Face.L, 1, 0) == C(s, Face.L)) n++;
            return n;
        }

        static bool Ok(CubeState s, Face a, int ar, int ac, Face b, int br, int bc, Face d, int dr, int dc)
            => s.Get(a, ar, ac) == C(s, a) && s.Get(b, br, bc) == C(s, b) && s.Get(d, dr, dc) == C(s, d);

        /// 탐색으로 못 찾을 만큼 먼 상태에서 주는 거친 안내.
        static string Guidance(int target)
        {
            switch (target)
            {
                case 1: return "아래 면에 흰 십자를 만들 조각을 위층으로 올린 뒤 자리를 맞추고 내리세요.";
                case 2: return "흰색이 들어간 모서리를 위층으로 빼낸 뒤 들어갈 자리 위에 놓고 공식을 쓰세요.";
                case 3: return "노란색이 없는 조각을 위층에서 찾아 앞면 색을 맞춘 뒤 공식을 쓰세요.";
                default: return "다음 단계 설명을 다시 읽어 보세요.";
            }
        }
    }
}
