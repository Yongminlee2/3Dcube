using System.Collections.Generic;

namespace Cube.Core
{
    /// 공식 하나.
    public sealed class Algorithm
    {
        public string Name { get; }
        public string Notation { get; }
        public string When { get; }

        public Algorithm(string name, string notation, string when)
        {
            Name = name; Notation = notation; When = when;
        }
    }

    /// 코스 한 단계.
    public sealed class Lesson
    {
        public int Stage { get; }
        public string Title { get; }
        /// 설명 문단. 폰에서 한 번에 다 보여주지 않고 넘겨 본다.
        public string[] Steps { get; }
        public Algorithm[] Algorithms { get; }
        /// 완성 상태에 이걸 적용하면 "앞 단계까지만 통과한" 연습 상태가 된다.
        /// 그 성질은 테스트가 지킨다.
        public string PracticeSetup { get; }

        public Lesson(int stage, string title, string[] steps, Algorithm[] algorithms, string practiceSetup)
        {
            Stage = stage; Title = title; Steps = steps;
            Algorithms = algorithms; PracticeSetup = practiceSetup;
        }
    }

    /// 초보자용 LBL 7단계 코스.
    ///
    /// 내용을 코드 상수로 둔다. 외부 파일로 빼면 편집은 쉬워지지만 오타를 컴파일러가
    /// 못 잡는다. 일곱 단계뿐이라 상수가 낫다.
    ///
    /// 색 기준: 아래 흰색, 위 노란색, 앞 초록, 뒤 파랑, 왼쪽 빨강, 오른쪽 주황.
    public static class LessonData
    {
        public static IReadOnlyList<Lesson> Lessons => All;
        public static IReadOnlyList<Algorithm> Library => LibraryAlgorithms;

        public static Lesson Get(int stage)
        {
            foreach (var l in All) if (l.Stage == stage) return l;
            throw new System.ArgumentOutOfRangeException(nameof(stage));
        }

        static readonly Lesson[] All =
        {
            new Lesson(1, "흰 십자",
                new[]
                {
                    "큐브를 흰 면이 아래로 가게 잡습니다. 앞으로 이 방향을 계속 유지합니다.",
                    "아래 면에 흰색 십자를 만듭니다. 십자를 이루는 네 조각은 두 가지 색을 갖고 있습니다. 흰색은 아래를 향하고, 나머지 색은 옆면 가운데 색과 맞아야 합니다.",
                    "예를 들어 흰-초록 조각은 흰색이 아래, 초록색이 초록 가운데가 있는 면을 향하게 놓습니다.",
                    "이 단계는 공식 없이 눈으로 찾아 옮깁니다. 한 조각씩 위로 올린 뒤 자리를 맞추고 아래로 내리면 됩니다. 다른 조각을 망가뜨렸다면 되돌리고 다시 해보세요.",
                },
                new Algorithm[0],
                // 완성 상태를 통째로 섞는다. 1단계 연습은 처음부터 시작하는 것이다.
                "R U2 F' L D2 B R' U F2 D L2 B' R2 U' F D'"),

            new Lesson(2, "첫 층 완성",
                new[]
                {
                    "십자를 만들었으면 이제 아래 층 네 모서리를 채웁니다.",
                    "흰색이 들어간 모서리 조각을 찾아 위층으로 올리고, 그 조각이 들어갈 자리 바로 위에 오게 돌립니다.",
                    "그 다음 아래 공식을 자리가 맞을 때까지 반복합니다. 한 번, 세 번, 또는 다섯 번 만에 들어갑니다.",
                    "공식이 아래 십자를 망가뜨리는 것처럼 보여도 괜찮습니다. 반복하면 제자리로 돌아옵니다.",
                },
                new[]
                {
                    new Algorithm("모서리 넣기", "R U R' U'",
                        "넣을 모서리를 오른쪽 위 앞에 두고, 들어갈 때까지 반복"),
                },
                "R U R' U'"),

            new Lesson(3, "가운데 층",
                new[]
                {
                    "아래 두 줄이 끝났습니다. 이제 가운데 층의 네 조각을 채웁니다.",
                    "위층에서 노란색이 없는 조각을 찾습니다. 그 조각의 앞면 색이 앞면 가운데 색과 맞도록 위층을 돌립니다.",
                    "조각이 오른쪽으로 내려가야 하면 첫 번째 공식, 왼쪽으로 내려가야 하면 두 번째 공식을 씁니다.",
                    "넣을 자리에 이미 엉뚱한 조각이 들어 있으면, 아무 공식이나 한 번 써서 그 조각을 위로 빼낸 뒤 다시 하세요.",
                },
                new[]
                {
                    new Algorithm("오른쪽으로 넣기", "U R U' R' U' F' U F",
                        "조각을 오른쪽 자리로 내릴 때"),
                    new Algorithm("왼쪽으로 넣기", "U' L' U L U F U' F'",
                        "조각을 왼쪽 자리로 내릴 때"),
                },
                "U R U' R' U' F' U F"),

            new Lesson(4, "노란 십자",
                new[]
                {
                    "아래 두 층이 끝났습니다. 남은 건 맨 위층뿐입니다.",
                    "위 면의 노란색 모양을 봅니다. 점 하나, 한 줄, 또는 ㄱ자 모양일 겁니다.",
                    "한 줄이면 그 줄이 좌우로 눕도록 돌리고, ㄱ자면 꺾인 부분이 왼쪽 위로 가게 돌린 뒤 공식을 씁니다.",
                    "점 하나면 공식을 세 번까지 반복하면 십자가 생깁니다. 지금은 모서리 색이 맞지 않아도 괜찮습니다.",
                },
                new[]
                {
                    new Algorithm("십자 만들기", "F R U R' U' F'",
                        "점·한 줄·ㄱ자 어느 경우든 이걸 반복"),
                },
                "F R U R' U' F'"),

            new Lesson(5, "노란 면",
                new[]
                {
                    "십자가 생겼으니 위 면 전체를 노란색으로 채웁니다.",
                    "노란색이 위를 향한 모서리가 몇 개인지 셉니다. 0개, 1개, 2개 중 하나입니다.",
                    "1개라면 그 모서리를 왼쪽 아래에 두고 공식을 씁니다. 0개나 2개라면 아무 데서나 한 번 쓰고 다시 세어 보세요.",
                    "이 공식은 아래 두 층을 건드리지 않습니다. 위 면이 노랗게 될 때까지 반복하면 됩니다.",
                },
                new[]
                {
                    new Algorithm("수네", "R U R' U R U2 R'",
                        "노란 면을 채울 때까지 반복"),
                },
                "R U R' U R U2 R'"),

            new Lesson(6, "모서리 자리 맞추기",
                new[]
                {
                    "위 면이 노랗게 됐지만 옆면 색은 아직 어긋나 있습니다.",
                    "네 모서리 중 이미 제자리에 있는 것이 있는지 찾습니다. 옆면 두 색이 각각 그 면 가운데 색과 맞으면 제자리입니다.",
                    "제자리인 모서리를 왼쪽 앞에 두고 공식을 씁니다. 제자리가 하나도 없으면 아무 데서나 한 번 쓰면 하나가 맞습니다.",
                    "이 공식은 왼쪽 앞 모서리를 그대로 두고 나머지 셋을 돌립니다. 다 맞을 때까지 반복하세요.",
                },
                new[]
                {
                    new Algorithm("모서리 돌리기", "R' F R' B2 R F' R' B2 R2",
                        "제자리인 모서리를 왼쪽 앞에 두고 반복"),
                },
                "R' F R' B2 R F' R' B2 R2"),

            new Lesson(7, "마지막 조각",
                new[]
                {
                    "마지막입니다. 모서리는 다 맞았고 그 사이 조각들만 남았습니다.",
                    "이미 맞은 면이 하나 있는지 찾습니다. 있으면 그 면을 뒤로 보냅니다.",
                    "공식을 쓰고, 다 맞지 않았으면 한 번 더 씁니다.",
                    "맞은 면이 하나도 없으면 아무 데서나 한 번 쓰면 하나가 생깁니다. 그 다음 다시 하세요.",
                },
                new[]
                {
                    new Algorithm("조각 돌리기", "R U' R U R U R U' R' U' R2",
                        "맞은 면을 뒤로 보내고 사용"),
                },
                "R U' R U R U R U' R' U' R2"),
        };

        /// 코스에 나오는 공식 + 알아 두면 좋은 것들.
        /// CFOP 전체(OLL 57개 / PLL 21개)는 싣지 않는다.
        static readonly Algorithm[] LibraryAlgorithms =
        {
            new Algorithm("모서리 넣기", "R U R' U'", "첫 층 모서리를 아래로 넣을 때"),
            new Algorithm("가운데 오른쪽", "U R U' R' U' F' U F", "가운데 층 조각을 오른쪽으로"),
            new Algorithm("가운데 왼쪽", "U' L' U L U F U' F'", "가운데 층 조각을 왼쪽으로"),
            new Algorithm("십자 만들기", "F R U R' U' F'", "위 면에 십자를 만들 때"),
            new Algorithm("수네", "R U R' U R U2 R'", "위 면을 노랗게 채울 때"),
            new Algorithm("안티수네", "R U2 R' U' R U' R'", "수네의 반대 방향"),
            new Algorithm("모서리 돌리기", "R' F R' B2 R F' R' B2 R2", "위층 모서리 자리를 맞출 때"),
            new Algorithm("조각 돌리기", "R U' R U R U R U' R' U' R2", "마지막 조각들을 맞출 때"),
            new Algorithm("티 공식", "R U R' U' R' F R2 U' R' U' R U R' F'", "자주 쓰는 마지막 층 공식"),
        };
    }
}
