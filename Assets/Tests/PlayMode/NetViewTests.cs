using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cube.Core;
using Cube.App;

namespace Cube.App.Tests
{
    public class NetViewTests
    {
        GameObject _root;
        NetView _net;

        [SetUp]
        public void SetUp()
        {
            ThemeService.Init();
            _root = new GameObject("Net", typeof(RectTransform), typeof(Canvas));
            _net = _root.AddComponent<NetView>();
        }

        [TearDown]
        public void TearDown() { if (_root != null) Object.DestroyImmediate(_root); }

        [UnityTest]
        public IEnumerator 칸을_여섯_면_분량_만든다()
        {
            foreach (int n in new[] { 2, 3, 4 })
            {
                _net.Build(n);
                yield return null;
                int count = 0;
                for (int f = 0; f < 6; f++)
                    for (int r = 0; r < n; r++)
                        for (int c = 0; c < n; c++)
                            if (_net.CellAt((Face)f, r, c) != null) count++;
                Assert.AreEqual(6 * n * n, count, $"n={n}");
            }
        }

        [UnityTest]
        public IEnumerator 칸_색이_상태와_일치한다()
        {
            var state = CubeState.Solved(3);
            state.Apply(MoveNotation.Parse("R U R' U' F2", 3));
            _net.Build(3);
            _net.Refresh(state);
            yield return null;

            var p = ThemeService.Current;
            for (int f = 0; f < 6; f++)
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                        TestColors.AssertSame(p.StickerColors[state.Get((Face)f, r, c)],
                                              _net.CellAt((Face)f, r, c).color, $"face={f} {r},{c}");
        }

        [UnityTest]
        public IEnumerator 강조를_켜면_그_칸만_색이_바뀌고_지우면_돌아온다()
        {
            var state = CubeState.Solved(3);
            _net.Build(3);
            _net.Refresh(state);
            yield return null;

            var p = ThemeService.Current;
            Color original = _net.CellAt(Face.U, 1, 1).color;
            _net.SetHighlight(Face.U, 1, 1, p.Accent);
            TestColors.AssertSame(p.Accent, _net.CellAt(Face.U, 1, 1).color);
            TestColors.AssertSame(original, _net.CellAt(Face.U, 0, 0).color);

            // 강조가 걸린 칸은 Refresh가 건드리지 않는다
            _net.Refresh(state);
            TestColors.AssertSame(p.Accent, _net.CellAt(Face.U, 1, 1).color);

            _net.ClearHighlights();
            _net.Refresh(state);
            TestColors.AssertSame(original, _net.CellAt(Face.U, 1, 1).color);
        }

        [UnityTest]
        public IEnumerator 접으면_숨고_펴면_보인다()
        {
            _net.Build(3);
            yield return null;
            _net.Expanded = false;
            Assert.IsFalse(_net.Expanded);
            _net.Expanded = true;
            Assert.IsTrue(_net.Expanded);
        }

        [UnityTest]
        public IEnumerator 다시_지으면_칸이_겹쳐_쌓이지_않는다()
        {
            _net.Build(3);
            yield return null;
            _net.Build(4);
            yield return null;
            // 면 6개만 자식으로 남아야 한다
            Assert.AreEqual(6, _root.transform.childCount);
        }
    }
}
