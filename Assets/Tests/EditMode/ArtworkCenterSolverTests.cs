using System.Collections.Generic;
using NUnit.Framework;
using Cube.Core;

namespace Cube.Core.Tests
{
    public class ArtworkCenterSolverTests
    {
        [Test]
        public void 한센터_180도는_색을흐트러뜨리지않는_유한공식으로_맞춘다()
        {
            var current = new[] { 2, 0, 0, 0, 0, 0 };
            Assert.IsTrue(ArtworkCenterSolver.TryPlan(current, out var plan));
            Assert.IsNotEmpty(plan);
            Assert.Less(plan.Count, 100);

            var state = CubeState.Solved(3);
            state.Apply(plan);
            Assert.IsTrue(state.IsSolved(), "센터 보정 공식이 색상 큐브를 흐트러뜨렸다");
            AssertSolvedAfter(current, plan);
        }

        [Test]
        public void 두센터_90도는_사용자이력과무관한_유한공식으로_맞춘다()
        {
            var current = new[] { 1, 0, 0, 0, 0, 3 };
            Assert.IsTrue(ArtworkCenterSolver.TryPlan(current, out var plan));
            Assert.IsNotEmpty(plan);
            Assert.Less(plan.Count, 100);

            var state = CubeState.Solved(3);
            state.Apply(plan);
            Assert.IsTrue(state.IsSolved(), "90도 센터 공식이 색상 큐브를 흐트러뜨렸다");
            AssertSolvedAfter(current, plan);
        }

        [Test]
        public void 센터하나만_90도인_불가능상태는_거짓을돌려준다()
        {
            Assert.IsFalse(ArtworkCenterSolver.TryPlan(
                new[] { 1, 0, 0, 0, 0, 0 }, out var plan));
            Assert.IsEmpty(plan);
        }

        static void AssertSolvedAfter(IReadOnlyList<int> current, IEnumerable<Move> plan)
        {
            var turns = new int[Faces.Count];
            for (int i = 0; i < turns.Length; i++) turns[i] = current[i] & 3;

            foreach (var move in plan)
            {
                int face;
                int amount;
                if (move.Layer == 0)
                {
                    face = move.Axis == Axis.X ? (int)Face.R
                         : move.Axis == Axis.Y ? (int)Face.U
                         : (int)Face.F;
                    amount = move.Turns;
                }
                else
                {
                    face = move.Axis == Axis.X ? (int)Face.L
                         : move.Axis == Axis.Y ? (int)Face.D
                         : (int)Face.B;
                    amount = 4 - move.Turns;
                }
                turns[face] = (turns[face] + amount) & 3;
            }

            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0, 0 }, turns);
        }
    }
}
