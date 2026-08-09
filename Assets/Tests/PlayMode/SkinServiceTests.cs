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

        [SetUp]
        public void SetUp()
        {
            SkinService.Init();
            _originalSkin = SkinService.Current;
            _go = new GameObject("Cube");
            _renderer = _go.AddComponent<CubeRenderer>();
            _renderer.Build(CubeState.Solved(3));
        }

        [TearDown]
        public void TearDown()
        {
            SkinService.Apply(_originalSkin);
            if (_go != null) Object.DestroyImmediate(_go);
        }

        static bool HasTexture(Skin s) =>
            s.StickerTextures != null && System.Array.Exists(s.StickerTextures, t => t != null);

        [Test]
        public void 여섯_종류의_스킨이_모두_있다()
        {
            // 색만 있는 5종(클래식/파스텔/비비드/톤다운/다크스틸) + 텍스처가 있는 우드.
            Assert.AreEqual(6, SkinService.All.Length);
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
