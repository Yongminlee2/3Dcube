using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cube.Core;
using Cube.App;

namespace Cube.App.Tests
{
    public class CubeRendererTests
    {
        GameObject _go;
        CubeRenderer _renderer;

        [SetUp]
        public void SetUp()
        {
            ThemeService.Init();
            _go = new GameObject("Cube");
            _renderer = _go.AddComponent<CubeRenderer>();
        }

        [TearDown]
        public void TearDown() { if (_go != null) Object.DestroyImmediate(_go); }

        [UnityTest]
        public IEnumerator 큐비를_N세제곱만큼_만든다()
        {
            foreach (int n in new[] { 2, 3, 4 })
            {
                _renderer.Build(CubeState.Solved(n));
                yield return null;
                Assert.AreEqual(n * n * n, _renderer.Cubies.Count, $"n={n}");
            }
        }

        [UnityTest]
        public IEnumerator 스티커_개수는_면_여섯_개_분량이다()
        {
            _renderer.Build(CubeState.Solved(3));
            yield return null;
            var renderers = _go.GetComponentsInChildren<MeshRenderer>();
            // 큐비 몸통 27개 + 겉면 스티커 54개
            Assert.AreEqual(27 + 54, renderers.Length);
        }

        [UnityTest]
        public IEnumerator 스티커_색이_상태와_일치한다()
        {
            var state = CubeState.Solved(3);
            state.Apply(MoveNotation.Parse("R U R' U'", 3));
            _renderer.Build(state);
            yield return null;

            var skin = SkinService.Current;
            for (int f = 0; f < 6; f++)
                for (int row = 0; row < 3; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        var mr = _renderer.StickerAt((Face)f, row, col);
                        Assert.IsNotNull(mr, $"스티커 없음 face={f} {row},{col}");
                        Color expected = skin.StickerColors[state.Get((Face)f, row, col)];
                        TestColors.AssertSame(expected, mr.sharedMaterial.color, $"face={f} {row},{col}");
                    }
        }

        [UnityTest]
        public IEnumerator 상태가_바뀌면_새로고침으로_색이_따라온다()
        {
            var state = CubeState.Solved(3);
            _renderer.Build(state);
            yield return null;

            state.Apply(new Move(Axis.X, 0, 1));
            _renderer.Refresh();
            yield return null;

            var skin = SkinService.Current;
            Color expected = skin.StickerColors[state.Get(Face.U, 1, 2)];
            TestColors.AssertSame(expected, _renderer.StickerAt(Face.U, 1, 2).sharedMaterial.color);
        }

        [UnityTest]
        public IEnumerator 격자_좌표는_큐브_가운데를_기준으로_펼쳐진다()
        {
            _renderer.Build(CubeState.Solved(3));
            yield return null;
            Assert.AreEqual(new Vector3(-1f, -1f, 1f), _renderer.GridToLocal(0, 0, 0));
            Assert.AreEqual(new Vector3(1f, 1f, -1f), _renderer.GridToLocal(2, 2, 2));
            Assert.AreEqual(Vector3.zero, _renderer.GridToLocal(1, 1, 1));
        }

        [UnityTest]
        public IEnumerator 위아래_면은_그림의_위쪽_방향을_따로_고정한다()
        {
            _renderer.Build(CubeState.Solved(3));
            yield return null;

            Vector3 upOnU = _renderer.StickerAt(Face.U, 1, 1).transform.localRotation * Vector3.up;
            Vector3 upOnD = _renderer.StickerAt(Face.D, 1, 1).transform.localRotation * Vector3.up;
            Vector3 upOnF = _renderer.StickerAt(Face.F, 1, 1).transform.localRotation * Vector3.up;

            Assert.Less(Vector3.Distance(Vector3.forward, upOnU), 0.001f,
                "U면 그림 위쪽은 B 방향(Unity +Z)이어야 한다");
            Assert.Less(Vector3.Distance(Vector3.back, upOnD), 0.001f,
                "D면 그림 위쪽은 F 방향(Unity -Z)이어야 한다");
            Assert.Less(Vector3.Distance(Vector3.up, upOnF), 0.001f,
                "옆면 그림 위쪽은 Unity +Y를 유지해야 한다");
        }
    }
}
