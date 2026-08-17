using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Cube.Core;
using Cube.App;

namespace Cube.App.Tests
{
    public class HintUiTests
    {
        GameObject _boot;
        ScreenRouter _router;
        string _path;
        Skin _originalSkin;
        SkinArtworkLayout _originalLayout;

        [SetUp]
        public void SetUp()
        {
            CubeProgressStore.ClearAll();
            AppSettings.AnimationMs = 0;
            AppSettings.CubeSize = 3;
            _path = Path.Combine(Application.temporaryCachePath, "hintui-test.json");
            if (File.Exists(_path)) File.Delete(_path);
            AppBootstrap.StorePathOverride = _path;

            _boot = new GameObject("AppBootstrap");
            _boot.AddComponent<AppBootstrap>();
            _originalSkin = SkinService.Current;
            _originalLayout = SkinService.ArtworkLayout;
            _router = AppBootstrap.Instance.Router;
            _router.StartPractice(3);
        }

        [TearDown]
        public void TearDown()
        {
            AppSettings.AnimationMs = 120;
            SkinService.Apply(_originalSkin);
            SkinService.SetArtworkLayout(_originalLayout);
            AppBootstrap.StorePathOverride = null;
            if (_boot != null) Object.DestroyImmediate(_boot);
            if (File.Exists(_path)) File.Delete(_path);
            CubeProgressStore.ClearAll();
        }

        [UnityTest]
        public IEnumerator 힌트와_안내카드를_눌러도_큐브는_움직이지_않는다()
        {
            var practice = _router.Practice;
            practice.Scramble();
            yield return null;

            var before = practice.Renderer.State.Clone();
            practice.ShowHint();
            practice.FollowHint();
            yield return null;

            Assert.IsTrue(before.SameAs(practice.Renderer.State),
                "힌트는 설명만 하고 큐브를 대신 돌리면 안 된다");
        }

        [UnityTest]
        public IEnumerator 완성_상태에서는_따라둘_수가_없다()
        {
            var practice = _router.Practice;
            practice.ResetCube();
            yield return null;

            practice.ShowHint();
            practice.FollowHint();     // 아무 일도 없어야 한다
            yield return null;

            Assert.IsTrue(practice.Renderer.State.IsSolved());
        }

        [UnityTest]
        public IEnumerator 큐브를_다시_섞으면_들고_있던_힌트가_사라진다()
        {
            var practice = _router.Practice;
            practice.Scramble();
            yield return null;
            practice.ShowHint();

            practice.Scramble();       // 힌트가 가리키던 상태가 사라진다
            yield return null;

            var before = practice.Renderer.State.Clone();
            practice.FollowHint();     // 옛 힌트가 남아 있으면 여기서 큐브가 움직인다
            yield return null;

            Assert.IsTrue(before.SameAs(practice.Renderer.State), "섞은 뒤에도 옛 힌트가 살아 있다");
        }

        [UnityTest]
        public IEnumerator 네칸_큐브에서는_힌트를_주지_않는다()
        {
            _router.StartPractice(4);
            yield return null;

            var practice = _router.Practice;
            practice.Scramble();
            yield return null;

            var before = practice.Renderer.State.Clone();
            practice.ShowHint();
            practice.FollowHint();
            yield return null;

            Assert.IsTrue(before.SameAs(practice.Renderer.State), "4x4에서 힌트가 큐브를 움직였다");
        }

        [UnityTest]
        public IEnumerator 영어로_나열된_힌트만_순서대로_따라가면_끝까지_풀린다()
        {
            var practice = _router.Practice;
            var notation = practice.transform.Find("Hint/HintNotation").GetComponent<Text>();

            for (int seed = 0; seed < 10; seed++)
            {
                var state = CubeState.Solved(3);
                string scramble = Scrambler.Generate(3, new System.Random(seed));
                state.Apply(MoveNotation.Parse(scramble, 3));
                practice.LoadState(state);
                yield return null;

                int hintGroups = 0;
                int applied = 0;
                while (!practice.Renderer.State.IsSolved() && hintGroups < 300 && applied < 1200)
                {
                    practice.ShowHint();
                    string sequence = notation.text;
                    Assert.IsNotEmpty(sequence, $"seed={seed}: 빈 힌트가 나왔다");
                    Assert.AreEqual(sequence, HintEngine.SimplifyNotation(sequence),
                        $"seed={seed}: 의미 없는 반복이 남았다 — '{sequence}'");

                    foreach (var move in ManualButtonMoves(sequence))
                    {
                        practice.ApplyMove(move);
                        applied++;
                    }
                    hintGroups++;
                    yield return null;
                }

                Assert.IsTrue(practice.Renderer.State.IsSolved(),
                    $"seed={seed}: 힌트 {hintGroups}묶음, {applied}동작을 따라도 풀리지 않았다");
            }
        }

        [UnityTest]
        public IEnumerator 그림스킨도_힌트만_따라가면_방향까지_완성된다()
        {
            var illustrated = System.Array.Find(SkinService.All,
                skin => skin.StickerTextures != null
                     && System.Array.Exists(skin.StickerTextures, texture => texture != null));
            Assert.IsNotNull(illustrated);
            SkinService.Apply(illustrated);
            SkinService.SetArtworkLayout(SkinArtworkLayout.WholeFace);

            var practice = _router.Practice;
            var notation = practice.transform.Find("Hint/HintNotation").GetComponent<Text>();
            practice.Scramble();
            yield return null;

            int groups = 0;
            int applied = 0;
            while (!practice.Renderer.IsSolvedWithArtwork() && groups < 320 && applied < 1400)
            {
                practice.ShowHint();
                string sequence = notation.text;
                Assert.IsNotEmpty(sequence, "그림 방향을 풀 힌트가 비었다");
                foreach (var move in ManualButtonMoves(sequence))
                {
                    practice.ApplyMove(move);
                    applied++;
                }
                groups++;
                yield return null;
            }

            Assert.IsTrue(practice.Renderer.State.IsSolved());
            Assert.IsTrue(practice.Renderer.IsSolvedWithArtwork(),
                $"힌트 {groups}묶음, {applied}동작을 따라도 그림 방향까지 완성되지 않았다");
        }

        [UnityTest]
        public IEnumerator 일반스킨과_그림스킨은_같은상태에서_같은단계공식을_준다()
        {
            var plain = System.Array.Find(SkinService.All,
                skin => skin.StickerTextures == null
                     || !System.Array.Exists(skin.StickerTextures, texture => texture != null));
            var illustrated = System.Array.Find(SkinService.All,
                skin => skin.StickerTextures != null
                     && System.Array.Exists(skin.StickerTextures, texture => texture != null));
            Assert.IsNotNull(plain);
            Assert.IsNotNull(illustrated);

            var state = CubeState.Solved(3);
            state.Apply(MoveNotation.Parse(Scrambler.Generate(3, new System.Random(41)), 3));
            var practice = _router.Practice;
            var notation = practice.transform.Find("Hint/HintNotation").GetComponent<Text>();

            SkinService.Apply(plain);
            practice.LoadState(state.Clone());
            practice.ShowHint();
            string plainHint = notation.text;

            SkinService.Apply(illustrated);
            SkinService.SetArtworkLayout(SkinArtworkLayout.WholeFace);
            practice.LoadState(state.Clone());
            practice.ShowHint();
            string artworkHint = notation.text;

            Assert.AreEqual(plainHint, artworkHint,
                "같은 큐브 상태인데 스킨에 따라 색상 풀이 공식이 달라졌다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 그림스킨에서_백번더돌려도_이력을_되감지않고_현재상태공식을_준다()
        {
            var illustrated = System.Array.Find(SkinService.All,
                skin => skin.StickerTextures != null
                     && System.Array.Exists(skin.StickerTextures, texture => texture != null));
            Assert.IsNotNull(illustrated);
            SkinService.Apply(illustrated);
            SkinService.SetArtworkLayout(SkinArtworkLayout.WholeFace);

            var practice = _router.Practice;
            practice.Scramble();
            for (int seed = 100; seed < 105; seed++)
                foreach (var move in MoveNotation.Parse(Scrambler.Generate(3, new System.Random(seed)), 3))
                    practice.ApplyMove(move);

            Assert.IsFalse(practice.Renderer.State.IsSolved());
            string expected = HintEngine.SimplifyNotation(
                HintEngine.Next(practice.Renderer.State).Notation);
            practice.ShowHint();
            string actual = practice.transform.Find("Hint/HintNotation").GetComponent<Text>().text;

            Assert.AreEqual(expected, actual,
                "100번의 이동 이력을 역순으로 보여 주고 현재 상태용 단계 공식을 쓰지 않았다");
            Assert.Less(MoveNotation.Parse(actual, 3).Count, 100,
                "첫 힌트가 사용자 이동 이력만큼 길어졌다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 색을푼뒤_남은그림센터는_이력없이_전용공식으로_맞춘다()
        {
            var illustrated = System.Array.Find(SkinService.All,
                skin => skin.StickerTextures != null
                     && System.Array.Exists(skin.StickerTextures, texture => texture != null));
            Assert.IsNotNull(illustrated);
            SkinService.Apply(illustrated);
            SkinService.SetArtworkLayout(SkinArtworkLayout.WholeFace);

            var practice = _router.Practice;
            practice.ResetCube();
            var up = practice.Renderer.CubieAt(1, 2, 1).GetComponent<CubieMarker>();
            var right = practice.Renderer.CubieAt(2, 1, 1).GetComponent<CubieMarker>();
            up.Orientation = Quaternion.AngleAxis(90f, Vector3.up);
            right.Orientation = Quaternion.AngleAxis(-90f, Vector3.right);

            Assert.IsTrue(practice.Renderer.State.IsSolved());
            Assert.IsFalse(practice.Renderer.IsSolvedWithArtwork());

            var notation = practice.transform.Find("Hint/HintNotation").GetComponent<Text>();
            practice.ShowHint();
            string sequence = notation.text;
            Assert.IsNotEmpty(sequence, "그림 센터 방향 공식이 나오지 않았다");
            foreach (var move in ManualButtonMoves(sequence)) practice.ApplyMove(move);
            yield return null;

            Assert.IsTrue(practice.Renderer.State.IsSolved(), "센터 공식이 색상 큐브를 흐트러뜨렸다");
            Assert.IsTrue(practice.Renderer.IsSolvedWithArtwork(),
                $"센터 공식을 끝까지 수행했지만 그림이 완성되지 않았다: {sequence}");
        }

        [UnityTest]
        public IEnumerator 공식_일부만_실행하고_힌트를_다시_눌러도_지난_동작은_나오지_않는다()
        {
            var practice = _router.Practice;
            var notation = practice.transform.Find("Hint/HintNotation").GetComponent<Text>();
            var state = CubeState.Solved(3);
            state.Apply(MoveNotation.Parse(Scrambler.Generate(3, new System.Random(7)), 3));
            practice.LoadState(state);
            yield return null;

            practice.ShowHint();
            string original = notation.text;
            var manualMoves = ManualButtonMoves(original);
            Assert.Greater(manualMoves.Count, 2, $"부분 실행을 검사하기에 힌트가 너무 짧다 — '{original}'");

            int prefixCount = Mathf.Min(3, manualMoves.Count - 1);
            for (int i = 0; i < prefixCount; i++) practice.ApplyMove(manualMoves[i]);

            string expectedRemaining = HintEngine.SimplifyNotation(
                MoveNotation.Format(manualMoves.GetRange(prefixCount, manualMoves.Count - prefixCount), 3));
            Assert.AreEqual(expectedRemaining, notation.text,
                $"이미 실행한 앞부분이 남은 수식에서 빠지지 않았다 — 원본 '{original}'");

            string beforePress = notation.text;
            practice.ShowHint();
            Assert.AreEqual(beforePress, notation.text,
                "공식 도중 힌트를 다시 누르자 과거 경로로 재계산됐다");
            yield return null;
        }

        static List<Move> ManualButtonMoves(string sequence)
        {
            var result = new List<Move>();
            foreach (var move in MoveNotation.Parse(sequence, 3))
            {
                if (move.Turns == 2)
                {
                    int quarterTurn = move.Layer == 2 ? 3 : 1;
                    result.Add(new Move(move.Axis, move.Layer, quarterTurn));
                    result.Add(new Move(move.Axis, move.Layer, quarterTurn));
                }
                else
                {
                    result.Add(move);
                }
            }
            return result;
        }

        [UnityTest]
        public IEnumerator 연습_카드와_하단버튼은_같은_규격과_읽기좋은_글자를_쓴다()
        {
            yield return null;

            var root = _router.Practice.transform;
            var net = root.Find("NetCard") as RectTransform;
            var hint = root.Find("Hint") as RectTransform;
            var bar = root.Find("Bar") as RectTransform;
            Assert.NotNull(net);
            Assert.NotNull(hint);
            Assert.NotNull(bar);
            Assert.AreEqual(0.05f, net.anchorMin.x, 0.001f);
            Assert.AreEqual(0.95f, net.anchorMax.x, 0.001f);
            Assert.AreEqual(net.anchorMin.x, hint.anchorMin.x, 0.001f);
            Assert.AreEqual(net.anchorMax.x, hint.anchorMax.x, 0.001f);
            Assert.AreEqual(net.anchorMin.x, bar.anchorMin.x, 0.001f);
            Assert.AreEqual(net.anchorMax.x, bar.anchorMax.x, 0.001f);
            // 아이콘을 알아볼 만한 크기로 키우려면 바가 이만큼은 필요하다.
            Assert.LessOrEqual(bar.anchorMax.y - bar.anchorMin.y, 0.075f,
                "하단 버튼 바가 글자보다 지나치게 크다");

            var pad = root.Find("Pad") as RectTransform;
            Assert.NotNull(pad);
            Assert.LessOrEqual(pad.anchorMax.y - pad.anchorMin.y, 0.075f,
                "3x3 노테이션 패드가 한 줄보다 크게 자리를 차지한다");
            Assert.IsNull(root.Find("Pad/Pad_Double"), "2회 토글은 제거되어야 한다");
            Assert.IsNull(root.Find("Pad/Pad_Wide"), "넓은 수 토글은 제거되어야 한다");
            Assert.NotNull(root.Find("Pad/Pad_Prime"), "반시계 입력은 남아 있어야 한다");

            var label = root.Find("Bar/Bar_섞기/Label")?.GetComponent<Text>();
            var explanation = root.Find("Hint/HintExplanation")?.GetComponent<Text>();
            Assert.NotNull(label);
            Assert.NotNull(explanation);
            // 자동 축소가 켜져 있으면 fontSize는 무시되고 이 상한이 실제 크기가 된다.
            Assert.IsTrue(label.resizeTextForBestFit);
            Assert.GreaterOrEqual(label.resizeTextMaxSize, 20);
            Assert.LessOrEqual(label.resizeTextMaxSize, 28,
                "아이콘 옆에서 글자만 커 보인다");
            Assert.GreaterOrEqual(explanation.resizeTextMaxSize, 23);

            // 아이콘 칸이 글자 칸보다 좁으면, 굵은 글자 옆에서 아이콘만 작아 보인다.
            // 실기기에서 실제로 그렇게 보여서 고쳤던 부분이라 규격으로 박아 둔다.
            var icon = root.Find("Bar/Bar_섞기/Icon") as RectTransform;
            var labelRect = (RectTransform)label.transform;
            Assert.NotNull(icon);
            Assert.GreaterOrEqual(
                icon.anchorMax.y - icon.anchorMin.y,
                labelRect.anchorMax.y - labelRect.anchorMin.y,
                "아이콘 칸이 글자 칸보다 작다");
        }
    }
}
