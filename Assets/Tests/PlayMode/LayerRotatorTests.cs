using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cube.Core;
using Cube.App;

namespace Cube.App.Tests
{
    public class LayerRotatorTests
    {
        GameObject _go;
        CubeRenderer _renderer;
        LayerRotator _rotator;
        Skin _originalSkin;
        SkinArtworkLayout _originalLayout;

        [SetUp]
        public void SetUp()
        {
            ThemeService.Init();
            SkinService.Init();
            _originalSkin = SkinService.Current;
            _originalLayout = SkinService.ArtworkLayout;
            var plain = System.Array.Find(SkinService.All,
                skin => skin.StickerTextures == null
                     || !System.Array.Exists(skin.StickerTextures, texture => texture != null));
            SkinService.Apply(plain);
            SkinService.SetArtworkLayout(SkinArtworkLayout.RepeatPerSticker);
            AppSettings.AnimationMs = 40;
            _go = new GameObject("Cube");
            _renderer = _go.AddComponent<CubeRenderer>();
            _rotator = _go.AddComponent<LayerRotator>();
            _renderer.Build(CubeState.Solved(3));
            _rotator.Init(_renderer);
        }

        [TearDown]
        public void TearDown()
        {
            AppSettings.AnimationMs = 120;
            SkinService.Apply(_originalSkin);
            SkinService.SetArtworkLayout(_originalLayout);
            if (_go != null) Object.DestroyImmediate(_go);
        }

        IEnumerator WaitIdle()
        {
            float t = 0f;
            while (_rotator.IsAnimating && t < 5f) { t += Time.deltaTime; yield return null; }
            Assert.IsFalse(_rotator.IsAnimating, "애니메이션이 끝나지 않았다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 무브를_넣으면_논리_상태가_즉시_커밋된다()
        {
            var before = _renderer.State.Clone();
            _rotator.Enqueue(new Move(Axis.X, 0, 1));
            // 애니메이션을 기다리지 않고 바로 확인한다
            Assert.IsFalse(before.SameAs(_renderer.State), "상태가 즉시 반영되지 않았다");
            yield return WaitIdle();
        }

        [UnityTest]
        public IEnumerator 애니메이션이_끝나면_스티커_배열이_상태와_맞는다()
        {
            _rotator.EnqueueRange(MoveNotation.Parse("R U R' U'", 3));
            yield return WaitIdle();

            var skin = SkinService.Current;
            for (int f = 0; f < 6; f++)
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        Color expected = skin.StickerColors[_renderer.State.Get((Face)f, row, col)];
                        TestColors.AssertSame(expected,
                            _renderer.StickerAt((Face)f, row, col).sharedMaterial.color,
                            $"face={f} {row},{col}");
                    }
        }

        [UnityTest]
        public IEnumerator 애니메이션이_끝나면_스티커가_제_자리에_가_있다()
        {
            _rotator.EnqueueRange(MoveNotation.Parse("R U F' L2 D", 3));
            yield return WaitIdle();

            for (int f = 0; f < 6; f++)
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        var p = CubeCoords.ToPoint((Face)f, row, col, 3);
                        Vector3 dir = new Vector3(p.NX, p.NY, -p.NZ);
                        // 스티커는 큐비 표면에 얹혀 있다. 큐비가 0.96배로 줄어 있어
                        // 정확한 오프셋은 장식용 상수에 달려 있으므로, 여기서는
                        // "올바른 큐비의 올바른 면"인지만 본다. 면이 틀리면 0.7 이상,
                        // 큐비가 틀리면 1.0 이상 어긋나므로 0.08이면 충분히 걸러진다.
                        Vector3 expected = _renderer.GridToLocal(p.X, p.Y, p.Z) + dir * 0.5f;
                        Vector3 actual = _go.transform.InverseTransformPoint(
                            _renderer.StickerAt((Face)f, row, col).transform.position);
                        Assert.Less((expected - actual).magnitude, 0.08f,
                            $"face={f} {row},{col} 기대 {expected} 실제 {actual}");
                    }
        }

        [UnityTest]
        public IEnumerator 큐가_넘치면_건너뛰고_바로_맞춘다()
        {
            var moves = new List<Move>();
            for (int i = 0; i < 20; i++) moves.Add(new Move(Axis.X, 0, 1));
            _rotator.EnqueueRange(moves);
            Assert.LessOrEqual(_rotator.QueueLength, LayerRotator.MaxQueue);

            yield return WaitIdle();
            // R을 20번 = 4의 배수이므로 완성 상태로 돌아온다
            Assert.IsTrue(_renderer.State.IsSolved());
        }

        [UnityTest]
        public IEnumerator 즉시_마무리하면_큐가_비고_상태가_맞는다()
        {
            _rotator.EnqueueRange(MoveNotation.Parse("R U R' U'", 3));
            _rotator.FinishAllImmediately();

            Assert.IsFalse(_rotator.IsAnimating);
            Assert.AreEqual(0, _rotator.QueueLength);
            yield return null;

            var skin = SkinService.Current;
            Color expected = skin.StickerColors[_renderer.State.Get(Face.U, 0, 0)];
            TestColors.AssertSame(expected, _renderer.StickerAt(Face.U, 0, 0).sharedMaterial.color);
        }

        [UnityTest]
        public IEnumerator 무브가_적용될_때마다_알림이_온다()
        {
            int count = 0;
            _rotator.MoveApplied += _ => count++;
            _rotator.EnqueueRange(MoveNotation.Parse("R U R'", 3));
            yield return WaitIdle();
            Assert.AreEqual(3, count);
        }

        [UnityTest]
        public IEnumerator 즉시_섞고_되돌려도_한면_그림_조각이_제자리로_돌아온다()
        {
            var textured = System.Array.Find(SkinService.All,
                s => s.StickerTextures != null && System.Array.Exists(s.StickerTextures, t => t != null));
            Assert.IsNotNull(textured);
            SkinService.Apply(textured);
            SkinService.SetArtworkLayout(SkinArtworkLayout.WholeFace);
            yield return null;

            var original = new Material[6 * 3 * 3];
            for (int f = 0; f < 6; f++)
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                        original[(f * 3 + row) * 3 + col]
                            = _renderer.StickerAt((Face)f, row, col).sharedMaterial;

            _rotator.ApplyInstant(MoveNotation.Parse("R U F", 3));
            _rotator.ApplyInstant(MoveNotation.Parse("F' U' R'", 3));

            Assert.IsTrue(_renderer.State.IsSolved());
            Assert.IsTrue(_renderer.IsSolvedWithArtwork(),
                "색뿐 아니라 그림 조각의 상하좌우 방향도 원래대로 돌아와야 한다");
            for (int f = 0; f < 6; f++)
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                        Assert.AreSame(original[(f * 3 + row) * 3 + col],
                            _renderer.StickerAt((Face)f, row, col).sharedMaterial,
                            $"face={f} {row},{col} 그림 조각이 바뀌었다");
        }
    }
}
