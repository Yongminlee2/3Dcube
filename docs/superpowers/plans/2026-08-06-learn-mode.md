# Phase B 학습 모드 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (or subagent-driven-development) to implement this plan task-by-task.

**Goal:** 큐브를 못 맞추는 사람이 앱만 보고 3×3을 끝까지 맞추게 하는 7단계 코스를 만든다.

**Architecture:** Phase A의 엔진과 렌더링 위에 얹는다. 단계 통과 판정(`StageChecker`)과 코스 내용(`LessonData`)은 `Cube.Core`에 두어 Unity 없이 테스트하고, 화면과 시연은 `Cube.Unity`에 둔다. 풀이 탐색은 쓰지 않는다.

**Tech Stack:** Phase A와 동일 — Unity 6000.3.18f1, URP, uGUI(legacy Text), Unity Test Framework

**설계 문서:** [2026-08-06-learn-mode-design.md](../specs/2026-08-06-learn-mode-design.md)

## Global Constraints

Phase A 계획의 Global Constraints를 그대로 따른다 ([2026-08-06-cube-core.md](2026-08-06-cube-core.md)). 테스트·빌드 명령, 어셈블리 경계, 커밋 방식이 거기 있다. 추가로:

- **면 번호와 좌표계는 절대 건드리지 않는다.** 색 배치 변경은 팔레트 값만 바꾼다
- 판정은 **센터 색 기준**이다. 큐브가 통째로 돌아가 있어도 결과가 같아야 한다
- Phase B는 **3×3만** 다룬다
- 안드로이드 빌드는 ASCII 경로 우회 없이는 실패한다 ([ascii-path-trap.md](../../ascii-path-trap.md))

## File Structure

```
Assets/Scripts/
├─ Core/
│  ├─ StageChecker.cs      단계 통과 판정
│  └─ LessonData.cs        7단계 코스 내용 (상수)
├─ Screens/
│  ├─ LearnScreen.cs       단계 목록과 진도
│  ├─ LessonScreen.cs      한 단계 화면
│  └─ AlgorithmScreen.cs   공식 라이브러리
├─ Rendering/
│  └─ LessonPlayer.cs      공식 시연 재생·되돌리기
└─ Services/
   └─ LearnProgress.cs     진도 저장
Assets/Tests/EditMode/
├─ StageCheckerTests.cs
└─ LessonDataTests.cs
Assets/Tests/PlayMode/
└─ LessonPlayerTests.cs
```

---

### Task 1: 색 배치 변경

**Files:** Modify `Assets/Editor/ProjectSetup.cs`

흰색을 D로 내리고 좌우를 맞바꾼다. 초보자 강의는 흰 십자를 바닥에서 시작하고, 표준 공식은 마지막 층이 U라고 가정한다.

- [ ] **Step 1: 팔레트 색 순서를 바꾼다**

`StandardStickers()`를 아래로 바꾼다. 배열 순서는 면 번호 U, D, F, B, L, R이다.

```csharp
        // 면 번호 순서: U, D, F, B, L, R
        //
        // 흰색을 D에 둔다. 초보자 강의는 예외 없이 흰 십자를 바닥에서 시작하고,
        // 표준 공식(Sune, T-perm 등)은 마지막 층이 U라고 가정하기 때문이다.
        // 흰색을 아래로 뒤집으면 좌우가 맞바뀌므로 L이 빨강, R이 주황이 된다.
        static Color[] StandardStickers() => new[]
        {
            Hex("#F5D000"),  // U 노랑
            Hex("#F2F2F2"),  // D 흰색
            Hex("#00A24A"),  // F 초록
            Hex("#0A5FD6"),  // B 파랑
            Hex("#E02020"),  // L 빨강
            Hex("#FF7A00"),  // R 주황
        };
```

- [ ] **Step 2: 애셋을 다시 만들고 테스트를 돌린다**

`ProjectSetup.CreateAssets` 실행 후 EditMode 36개 + PlayMode 53개가 그대로 통과해야 한다. 색은 팔레트에서만 나오므로 코드와 테스트는 영향받지 않는다.

- [ ] **Step 3: 커밋**

---

### Task 2: 단계 통과 판정

**Files:** Create `Assets/Scripts/Core/StageChecker.cs`, Test `Assets/Tests/EditMode/StageCheckerTests.cs`

**Interfaces:**
- Produces: `static bool StageChecker.Passed(CubeState s, int stage)`, `static int StageChecker.CurrentStage(CubeState s)`, `const int StageChecker.LastStage = 7`

**판정 규칙** — 전부 센터 색 `c(f) = s.Get(f,1,1)` 기준이다.

| 단계 | 조건 (앞 단계 조건을 포함한다) |
|---|---|
| 1 흰 십자 | D의 네 엣지가 c(D)이고, 각 짝 칸이 옆면 센터와 같다: `F(2,1)=c(F)`, `R(2,1)=c(R)`, `B(2,1)=c(B)`, `L(2,1)=c(L)` |
| 2 첫 층 | D 전체가 c(D)이고, F·R·B·L의 **맨 아랫줄**이 각 센터와 같다 |
| 3 가운데 층 | F·R·B·L의 **가운뎃줄**이 각 센터와 같다 |
| 4 노란 십자 | U의 네 엣지가 c(U) (모서리는 보지 않는다) |
| 5 노란 면 | U 전체가 c(U) |
| 6 모서리 위치 | F·R·B·L의 **맨 윗줄 양끝**이 각 센터와 같다 |
| 7 완성 | `s.IsSolved()` |

D 엣지와 옆면의 짝은 좌표계에서 나온다 — `D(0,1)`은 큐비 (1,0,2)이고 z=2는 F쪽이므로 짝은 `F(2,1)`이다. 네 방향 모두 `(2,1)`로 떨어진다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using NUnit.Framework;
using Cube.Core;

namespace Cube.Core.Tests
{
    public class StageCheckerTests
    {
        static CubeState Solved() => CubeState.Solved(3);

        [Test]
        public void 완성_상태는_모든_단계를_통과한다()
        {
            var c = Solved();
            for (int s = 1; s <= StageChecker.LastStage; s++)
                Assert.IsTrue(StageChecker.Passed(c, s), $"{s}단계");
            Assert.AreEqual(StageChecker.LastStage, StageChecker.CurrentStage(c));
        }

        [Test]
        public void 섞은_큐브는_대개_첫_단계도_통과하지_못한다()
        {
            int failed = 0;
            for (int seed = 0; seed < 30; seed++)
            {
                var c = Solved();
                c.Apply(MoveNotation.Parse(Scrambler.Generate(3, new System.Random(seed)), 3));
                if (!StageChecker.Passed(c, 1)) failed++;
            }
            Assert.Greater(failed, 25, "섞었는데 대부분 1단계를 통과했다면 판정이 헐겁다");
        }

        [Test]
        public void 큐브를_통째로_돌려도_판정이_같다()
        {
            // 전체 회전은 세 층을 한꺼번에 돌리는 것과 같다.
            for (int seed = 0; seed < 10; seed++)
            {
                var a = Solved();
                a.Apply(MoveNotation.Parse(Scrambler.Generate(3, new System.Random(seed)), 3));
                var b = a.Clone();
                foreach (var m in MoveNotation.Parse("Rw Rw Rw", 3)) { }
                for (int layer = 0; layer < 3; layer++) b.Apply(new Move(Axis.Y, layer, 1));

                Assert.AreEqual(StageChecker.CurrentStage(a), StageChecker.CurrentStage(b), $"seed={seed}");
            }
        }

        [Test]
        public void 단계는_누적된다()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 200; i++)
            {
                var c = Solved();
                for (int k = 0; k < rng.Next(0, 12); k++)
                    c.Apply(new Move((Axis)rng.Next(3), rng.Next(3), rng.Next(1, 4)));

                int cur = StageChecker.CurrentStage(c);
                for (int s = 1; s <= cur; s++)
                    Assert.IsTrue(StageChecker.Passed(c, s), $"{cur}단계인데 {s}단계가 거짓이다");
            }
        }

        [Test]
        public void 마지막_층만_흐트러뜨리면_세_단계까지_통과한다()
        {
            // U층만 돌리면 아래 두 층과 가운데 층은 그대로다.
            var c = Solved();
            c.Apply(MoveNotation.Parse("U", 3));
            Assert.IsTrue(StageChecker.Passed(c, 3), "3단계는 통과해야 한다");
            Assert.IsFalse(StageChecker.Passed(c, 7), "완성은 아니어야 한다");
        }

        [Test]
        public void 노란_십자_판정은_모서리를_보지_않는다()
        {
            // Sune는 U면 모서리 방향만 바꾸고 십자는 유지한다.
            var c = Solved();
            c.Apply(MoveNotation.Parse("R U R' U R U2 R'", 3));
            Assert.IsTrue(StageChecker.Passed(c, 4), "십자는 남아 있어야 한다");
            Assert.IsFalse(StageChecker.Passed(c, 5), "노란 면은 깨져 있어야 한다");
        }

        [Test]
        public void 범위_밖_단계는_예외를_던진다()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => StageChecker.Passed(Solved(), 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => StageChecker.Passed(Solved(), 8));
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다** (컴파일 오류 예상)

- [ ] **Step 3: StageChecker를 구현한다**

```csharp
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

        public static int CurrentStage(CubeState s)
        {
            for (int stage = 1; stage <= LastStage; stage++)
                if (!Passed(s, stage)) return stage - 1;
            return LastStage;
        }

        public static bool Passed(CubeState s, int stage)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.N != 3) throw new ArgumentException("학습 코스는 3x3만 다룬다", nameof(s));
            if (stage < 1 || stage > LastStage) throw new ArgumentOutOfRangeException(nameof(stage));

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

        static bool MiddleLayer(CubeState s)
        {
            foreach (var f in Sides)
            {
                byte c = Center(s, f);
                if (s.Get(f, 1, 0) != c || s.Get(f, 1, 2) != c) return false;
            }
            return true;
        }

        static bool TopCross(CubeState s)
        {
            byte u = Center(s, Face.U);
            return s.Get(Face.U, 0, 1) == u && s.Get(Face.U, 1, 0) == u
                && s.Get(Face.U, 1, 2) == u && s.Get(Face.U, 2, 1) == u;
        }

        static bool TopFace(CubeState s)
        {
            byte u = Center(s, Face.U);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    if (s.Get(Face.U, row, col) != u) return false;
            return true;
        }

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
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다** (EditMode 43개)
- [ ] **Step 5: 커밋**

---

### Task 3: 코스 내용

**Files:** Create `Assets/Scripts/Core/LessonData.cs`, Test `Assets/Tests/EditMode/LessonDataTests.cs`

**Interfaces:**
- Produces: `class Cube.Core.Algorithm` (`string Name, Notation, When`), `class Cube.Core.Lesson` (`int Stage, string Title, string[] Steps, Algorithm[] Algorithms`), `static IReadOnlyList<Lesson> LessonData.Lessons`, `static IReadOnlyList<Algorithm> LessonData.Library`

코스 내용을 코드 상수로 둔다. 외부 파일로 빼면 편집은 쉬워지지만 오타를 컴파일러가 못 잡는다. 7단계뿐이라 상수가 낫다.

테스트로 지킬 것:
- 단계가 1~7까지 빠짐없이 하나씩 있다
- 모든 공식 표기가 `MoveNotation.Parse`로 읽히고, 3×3 범위를 벗어나지 않는다
- 설명 문단이 비어 있지 않다
- 라이브러리 공식 이름이 중복되지 않는다

**공식이 실제로 하는 일까지 테스트한다.** 표기만 맞고 효과가 틀리면 배우는 사람이 헤맨다. 예: 4단계 공식을 노란 십자가 없는 상태에 적용하면 십자가 생겨야 한다.

- [ ] Step 1~5: 테스트 → 실패 확인 → 구현 → 통과 → 커밋

---

### Task 4: 공식 시연

**Files:** Create `Assets/Scripts/Rendering/LessonPlayer.cs`, Test `Assets/Tests/PlayMode/LessonPlayerTests.cs`

**Interfaces:**
- Produces: `class Cube.App.LessonPlayer : MonoBehaviour` — `void Init(CubeRenderer r, LayerRotator rot, TouchController touch)`, `void Play(string notation)`, `void StepOnce()`, `void Rewind()`, `bool IsPlaying`, `event Action Finished`

Phase A의 `LayerRotator`를 그대로 쓴다. 새 회전 경로를 만들지 않는다.

- 재생 중에는 `TouchController.Enabled = false`로 손가락 입력을 막는다
- `Rewind()`는 방금 재생한 시퀀스의 역순을 적용해 원래 상태로 되돌린다
- 한 수씩 보기는 큐에 하나만 넣는다

테스트: 재생 후 상태가 `CubeState.Apply(파싱한 무브)`와 같다 / 되돌리면 원래대로다 / 재생 중 입력이 막힌다.

- [ ] Step 1~5: 테스트 → 실패 확인 → 구현 → 통과 → 커밋

---

### Task 5: 진도 저장과 학습 홈

**Files:** Create `Assets/Scripts/Services/LearnProgress.cs`, `Assets/Scripts/Screens/LearnScreen.cs`, Test `Assets/Tests/PlayMode/LearnProgressTests.cs`
**Modify:** `Assets/Scripts/Screens/ScreenRouter.cs`, `Assets/Scripts/Screens/HomeScreen.cs`

**Interfaces:**
- Produces: `static class Cube.App.LearnProgress` — `int Completed { get; set; }`(PlayerPrefs), `bool IsUnlocked(int stage)`, `void MarkDone(int stage)`, `void Reset()`
- `class Cube.App.LearnScreen : MonoBehaviour` — `void Build(RectTransform parent, Action<int> onOpenLesson, Action onLibrary, Action onBack)`
- `ScreenRouter`에 `ScreenId.Learn`, `ScreenId.Lesson`, `ScreenId.Library` 추가

**홈의 [배우기] 버튼을 살린다.** Phase A에서 비활성으로 자리만 잡아 둔 그 버튼이다. 3×3이 아닌 크기를 고른 상태면 "3×3부터 배우세요"로 안내한다.

잠금 규칙: 1단계는 항상 열려 있고, N단계는 N-1단계를 마쳐야 열린다.

- [ ] Step 1~5: 테스트 → 실패 확인 → 구현 → 통과 → 커밋

---

### Task 6: 단계 화면

**Files:** Create `Assets/Scripts/Screens/LessonScreen.cs`, Test `Assets/Tests/PlayMode/LessonScreenTests.cs`

**Interfaces:**
- Produces: `class Cube.App.LessonScreen : MonoBehaviour` — `void Build(RectTransform parent, Action onBack)`, `void Open(int stage)`, `void Practice()`, `int Stage`

화면 구성: 위쪽에 3D 큐브(연습 화면보다 작게), 아래에 설명 문단과 공식 카드.

- 설명은 문단 단위로 넘겨 본다. 폰에서 글이 길면 답답해지고, 주된 설명 수단은 3D 시연이다
- 공식 카드를 누르면 `LessonPlayer.Play`
- [연습하기]를 누르면 그 단계 직전 상태로 큐브를 만든다. 사용자가 맞춰서 `StageChecker.Passed`가 참이 되면 축하하고 다음 단계를 연다
- 통과 판정은 `LayerRotator.MoveApplied`마다 확인한다

**연습 상태 만들기:** 완성 상태에서 시작해 그 단계가 다루는 부분만 흐트러뜨린다. 예를 들어 4~7단계 연습은 U층만 섞으면 되고(`U R U' R'` 같은 마지막 층 시퀀스), 1~3단계 연습은 전체 스크램블을 쓴다. 각 단계의 연습 시퀀스는 Task 3의 `Lesson`에 담는다.

- [ ] Step 1~5: 테스트 → 실패 확인 → 구현 → 통과 → 커밋

---

### Task 7: 공식 라이브러리와 마무리

**Files:** Create `Assets/Scripts/Screens/AlgorithmScreen.cs`
**Modify:** `docs/device-checklist.md`

- 코스 공식 + 자주 쓰는 마지막 층 공식을 카드로 나열, 누르면 시연
- CFOP 전체(OLL 57 / PLL 21)는 싣지 않는다 — 자주 쓰는 것만
- 실기기 확인 목록에 학습 모드 항목을 더한다
- APK 빌드 (ASCII 경로 환경 변수 필요)
- EditMode·PlayMode 전체 통과 확인

- [ ] Step 1~5: 구현 → 빌드 → 전체 테스트 → 커밋

---

## 이 계획이 다루지 않는 것

- 풀이 알고리즘과 "지금 상황에서 다음 수" 안내 (Phase C)
- 실물 큐브 상태 입력 (Phase C)
- 2×2·4×4 코스
- CFOP 전체 공식
- 아이콘·스토어·서명·iOS (Phase D)
