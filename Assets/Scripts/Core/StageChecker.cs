using System;

namespace Cube.Core
{
    /// "지금 상태가 이 단계를 통과했는가"만 판정한다. 풀이 탐색은 하지 않는다.
    /// 판정은 전부 센터 색 기준이라 큐브가 통째로 돌아가 있어도 결과가 같다.
    public static class StageChecker
    {
        public const int LastStage = 7;

        static readonly Face[] Sides = { Face.F, Face.R, Face.B, Face.L };

        static byte Center(CubeState s, Face f) => s.Get(f, 1, 1);

        /// 통과한 마지막 단계. 완성이면 LastStage, 아무것도 못 했으면 0.
        public static int CurrentStage(CubeState s)
        {
            for (int stage = 1; stage <= LastStage; stage++)
                if (!Passed(s, stage)) return stage - 1;
            return LastStage;
        }

        public static bool Passed(CubeState s, int stage)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (stage < 1 || stage > LastStage) throw new ArgumentOutOfRangeException(nameof(stage));
            if (s.N != 3) throw new ArgumentException("학습 코스는 3x3만 다룬다", nameof(s));

            // 단계는 누적된다. 앞 단계가 깨져 있으면 뒤 단계도 통과가 아니다.
            for (int i = 1; i <= stage; i++)
                if (!Only(s, i)) return false;
            return true;
        }

        static bool Only(CubeState s, int stage)
        {
            switch (stage)
            {
                case 1: return BottomCross(s);
                case 2: return FirstLayer(s);
                case 3: return MiddleLayer(s);
                case 4: return TopCross(s);
                case 5: return TopFace(s);
                case 6: return TopCornersPlaced(s);
                case 7: return s.IsSolved();
                default: throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }

        /// 1단계 — 아래 십자. 아래 면 십자가 한 색이고, 그 옆면 짝이 각 센터와 맞는다.
        static bool BottomCross(CubeState s)
        {
            byte d = Center(s, Face.D);
            if (s.Get(Face.D, 0, 1) != d || s.Get(Face.D, 1, 0) != d ||
                s.Get(Face.D, 1, 2) != d || s.Get(Face.D, 2, 1) != d) return false;

            // 네 방향 모두 짝 칸이 (2,1)로 떨어진다. 좌표계에서 나오는 성질이다.
            foreach (var f in Sides)
                if (s.Get(f, 2, 1) != Center(s, f)) return false;
            return true;
        }

        /// 2단계 — 첫 층. 아래 면 전체가 한 색이고, 옆면 맨 아랫줄이 각 센터와 맞는다.
        static bool FirstLayer(CubeState s)
        {
            byte d = Center(s, Face.D);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    if (s.Get(Face.D, row, col) != d) return false;

            foreach (var f in Sides)
            {
                byte c = Center(s, f);
                for (int col = 0; col < 3; col++)
                    if (s.Get(f, 2, col) != c) return false;
            }
            return true;
        }

        /// 3단계 — 가운데 층. 옆면 가운뎃줄 양끝이 각 센터와 맞는다.
        static bool MiddleLayer(CubeState s)
        {
            foreach (var f in Sides)
            {
                byte c = Center(s, f);
                if (s.Get(f, 1, 0) != c || s.Get(f, 1, 2) != c) return false;
            }
            return true;
        }

        /// 4단계 — 위 십자. 모서리는 보지 않는다.
        static bool TopCross(CubeState s)
        {
            byte u = Center(s, Face.U);
            return s.Get(Face.U, 0, 1) == u && s.Get(Face.U, 1, 0) == u
                && s.Get(Face.U, 1, 2) == u && s.Get(Face.U, 2, 1) == u;
        }

        /// 5단계 — 위 면 전체가 한 색.
        static bool TopFace(CubeState s)
        {
            byte u = Center(s, Face.U);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    if (s.Get(Face.U, row, col) != u) return false;
            return true;
        }

        /// 6단계 — 위층 모서리가 제 자리. 위 면이 이미 한 색이므로
        /// 옆면 맨 윗줄 양끝이 센터와 맞으면 모서리가 제자리에 있다는 뜻이다.
        static bool TopCornersPlaced(CubeState s)
        {
            foreach (var f in Sides)
            {
                byte c = Center(s, f);
                if (s.Get(f, 0, 0) != c || s.Get(f, 0, 2) != c) return false;
            }
            return true;
        }
    }
}
