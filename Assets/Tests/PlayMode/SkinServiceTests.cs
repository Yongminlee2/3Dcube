using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cube.Core;
using Cube.App;

namespace Cube.App.Tests
{
    public class SkinServiceTests
    {
        GameObject _go;
        CubeRenderer _renderer;
        Skin _originalSkin;
        SkinArtworkLayout _originalLayout;

        [SetUp]
        public void SetUp()
        {
            SkinService.Init();
            _originalSkin = SkinService.Current;
            _originalLayout = SkinService.ArtworkLayout;
            _go = new GameObject("Cube");
            _renderer = _go.AddComponent<CubeRenderer>();
            _renderer.Build(CubeState.Solved(3));
        }

        [TearDown]
        public void TearDown()
        {
            SkinService.Apply(_originalSkin);
            SkinService.SetArtworkLayout(_originalLayout);
            if (_go != null) Object.DestroyImmediate(_go);
        }

        static bool HasTexture(Skin s) =>
            s.StickerTextures != null && System.Array.Exists(s.StickerTextures, t => t != null);

        [Test]
        public void 열_종류의_스킨이_모두_있다()
        {
            // 색만 있는 5종 + 우드 + 캐릭터 스킨 4종.
            Assert.AreEqual(10, SkinService.All.Length);
        }

        [TestCase("Skin_Starlight")]
        [TestCase("Skin_KawaiiPals")]
        [TestCase("Skin_SummerHoliday")]
        [TestCase("Skin_MoonlightResort")]
        public void 그림_스킨은_여섯_면이_모두_서로_다른_그림이다(string skinName)
        {
            var illustrated = System.Array.Find(SkinService.All, s => s.name == skinName);
            Assert.IsNotNull(illustrated);
            Assert.AreEqual(6, illustrated.StickerTextures.Length);

            for (int i = 0; i < illustrated.StickerTextures.Length; i++)
            {
                Assert.IsNotNull(illustrated.StickerTextures[i], $"{i}번 면 텍스처가 없다");
                for (int j = 0; j < i; j++)
                    Assert.AreNotEqual(illustrated.StickerTextures[j], illustrated.StickerTextures[i],
                        $"{j}번과 {i}번 면이 같은 그림을 공유한다");
            }
        }

        [Test]
        public void 캐릭터_스킨은_목록_맨_아래에_모인다()
        {
            bool metCharacter = false;
            foreach (var skin in SkinService.All)
            {
                if (skin.CharacterArtwork) metCharacter = true;
                else Assert.IsFalse(metCharacter, $"일반 스킨 {skin.name}이 캐릭터 스킨 아래에 있다");
            }
            Assert.IsTrue(metCharacter);
        }

        [UnityTest]
        public IEnumerator 스킨을_바꾸면_이미_지어진_큐브_색이_바로_바뀐다()
        {
            yield return null;
            var other = System.Array.Find(SkinService.All, s => s != _originalSkin && !HasTexture(s));
            Assert.IsNotNull(other, "비교할 색-전용 스킨이 없다");

            SkinService.Apply(other);
            yield return null;

            Color expected = other.StickerColors[_renderer.State.Get(Face.U, 0, 0)];
            TestColors.AssertSame(expected, _renderer.StickerAt(Face.U, 0, 0).sharedMaterial.color);
        }

        [Test]
        public void 스킨을_고르면_설정에_이름이_저장된다()
        {
            var other = System.Array.Find(SkinService.All, s => s != _originalSkin);
            SkinService.Apply(other);
            Assert.AreEqual(other.name, AppSettings.SkinName);
        }

        [UnityTest]
        public IEnumerator 큐브가_숨어_있는_동안_바뀐_스킨도_다시_보일_때_반영된다()
        {
            yield return null;
            var other = System.Array.Find(SkinService.All, s => s != _originalSkin && !HasTexture(s));

            _go.SetActive(false);
            yield return null;
            SkinService.Apply(other);
            yield return null;

            _go.SetActive(true);
            yield return null;

            Color expected = other.StickerColors[_renderer.State.Get(Face.U, 0, 0)];
            TestColors.AssertSame(expected, _renderer.StickerAt(Face.U, 0, 0).sharedMaterial.color);
        }

        [UnityTest]
        public IEnumerator 텍스처가_있는_스킨은_색_대신_흰_틴트와_텍스처를_쓴다()
        {
            yield return null;
            var textured = System.Array.Find(SkinService.All, HasTexture);
            Assert.IsNotNull(textured, "텍스처 스킨이 하나도 없다 — delivery/skins를 SkinImporter로 붙였는지 확인할 것");

            SkinService.Apply(textured);
            yield return null;

            int faceIndex = System.Array.FindIndex(textured.StickerTextures, t => t != null);
            Assert.GreaterOrEqual(faceIndex, 0);

            var mat = _renderer.StickerAt((Face)faceIndex, 0, 0).sharedMaterial;
            TestColors.AssertSame(Color.white, mat.color, "텍스처가 있으면 틴트는 흰색이어야 한다 — 안 그러면 대표색으로 두 번 물든다");
            Assert.AreEqual(textured.StickerTextures[faceIndex], mat.mainTexture);
        }

        [UnityTest]
        public IEnumerator 한_면_전체_모드는_그림을_NxN으로_나눈다()
        {
            var textured = System.Array.Find(SkinService.All, HasTexture);
            Assert.IsNotNull(textured);
            SkinService.Apply(textured);
            SkinService.SetArtworkLayout(SkinArtworkLayout.WholeFace);
            yield return null;

            var left = _renderer.StickerAt(Face.U, 0, 0).sharedMaterial;
            var right = _renderer.StickerAt(Face.U, 0, 1).sharedMaterial;
            Assert.AreNotSame(left, right);
            Assert.That(left.mainTextureScale.x, Is.EqualTo(1f / 3f).Within(0.001f));
            Assert.That(left.mainTextureScale.y, Is.EqualTo(1f / 3f).Within(0.001f));
            Assert.AreNotEqual(left.mainTextureOffset.x, right.mainTextureOffset.x);
            Assert.AreEqual(left.mainTexture, right.mainTexture);
        }

        [UnityTest]
        public IEnumerator 섞인_저장상태에서도_그림_센터는_가운데_조각이다()
        {
            var textured = System.Array.Find(SkinService.All, HasTexture);
            Assert.IsNotNull(textured);
            SkinService.Apply(textured);
            SkinService.SetArtworkLayout(SkinArtworkLayout.WholeFace);

            var scrambled = CubeState.Solved(3);
            scrambled.Apply(MoveNotation.Parse("R U F2 L'", 3));
            _renderer.Build(scrambled);
            yield return null;

            for (int face = 0; face < Faces.Count; face++)
            {
                if (textured.StickerTextures[face] == null) continue;
                var center = _renderer.StickerAt((Face)face, 1, 1).sharedMaterial;
                Assert.That(center.mainTextureOffset.x, Is.EqualTo(1f / 3f).Within(0.001f),
                    $"{(Face)face}면 센터 그림의 가로 조각이 가운데가 아니다");
                Assert.That(center.mainTextureOffset.y, Is.EqualTo(1f / 3f).Within(0.001f),
                    $"{(Face)face}면 센터 그림의 세로 조각이 가운데가 아니다");
            }
        }

        [UnityTest]
        public IEnumerator 섞인_저장상태를_풀면_한면_그림도_제자리로_복원된다()
        {
            var textured = System.Array.Find(SkinService.All, HasTexture);
            Assert.IsNotNull(textured);
            SkinService.Apply(textured);
            SkinService.SetArtworkLayout(SkinArtworkLayout.WholeFace);

            var moves = MoveNotation.Parse("R U F2 L'", 3);
            var scrambled = CubeState.Solved(3);
            scrambled.Apply(moves);
            _renderer.Build(scrambled);

            var rotator = _go.AddComponent<LayerRotator>();
            rotator.Init(_renderer);
            for (int i = moves.Count - 1; i >= 0; i--)
                rotator.ApplyInstant(new[] { moves[i].Inverse });
            yield return null;

            Assert.IsTrue(_renderer.State.IsSolved());
            for (int face = 0; face < Faces.Count; face++)
            {
                if (textured.StickerTextures[face] == null) continue;
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        var mat = _renderer.StickerAt((Face)face, row, col).sharedMaterial;
                        Assert.That(mat.mainTextureOffset.x, Is.EqualTo(col / 3f).Within(0.001f));
                        Assert.That(mat.mainTextureOffset.y,
                            Is.EqualTo(1f - (row + 1f) / 3f).Within(0.001f));
                    }
            }
        }

        [UnityTest]
        public IEnumerator 색이_맞아도_센터_그림_방향이_틀리면_완성이_아니다()
        {
            var textured = System.Array.Find(SkinService.All, HasTexture);
            Assert.IsNotNull(textured);
            SkinService.Apply(textured);
            SkinService.SetArtworkLayout(SkinArtworkLayout.WholeFace);
            yield return null;

            Assert.IsTrue(_renderer.IsSolvedWithArtwork());
            var center = _renderer.CubieAt(1, 2, 1).GetComponent<CubieMarker>();
            center.Orientation = Quaternion.AngleAxis(90f, Vector3.up);

            Assert.IsTrue(_renderer.State.IsSolved(), "색 상태는 여전히 완성이다");
            Assert.IsFalse(_renderer.IsSolvedWithArtwork(),
                "센터 그림이 90도 돌아갔는데 완성으로 처리됐다");
        }

        [UnityTest]
        public IEnumerator 조각_반복_모드는_같은_면_재질을_공유한다()
        {
            var textured = System.Array.Find(SkinService.All, HasTexture);
            Assert.IsNotNull(textured);
            SkinService.Apply(textured);
            SkinService.SetArtworkLayout(SkinArtworkLayout.RepeatPerSticker);
            yield return null;

            var left = _renderer.StickerAt(Face.U, 0, 0).sharedMaterial;
            var right = _renderer.StickerAt(Face.U, 0, 1).sharedMaterial;
            Assert.AreSame(left, right);
            Assert.That(left.mainTextureScale, Is.EqualTo(Vector2.one));
        }

        [UnityTest]
        public IEnumerator 텍스처_없는_면은_기존처럼_플랫_컬러를_쓴다()
        {
            yield return null;
            var textured = System.Array.Find(SkinService.All, HasTexture);
            Assert.IsNotNull(textured);

            SkinService.Apply(textured);
            yield return null;

            int noTexFace = System.Array.FindIndex(textured.StickerTextures, t => t == null);
            if (noTexFace < 0) yield break; // 이 스킨은 여섯 면이 전부 텍스처다 — 검증할 필요 없음

            var mat = _renderer.StickerAt((Face)noTexFace, 0, 0).sharedMaterial;
            Assert.IsNull(mat.mainTexture);
            TestColors.AssertSame(textured.StickerColors[noTexFace], mat.color);
        }
    }
}
