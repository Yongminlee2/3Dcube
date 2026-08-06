using System;
using System.Collections.Generic;

namespace Cube.Core
{
    /// 지금까지 적용한 회전을 순서대로 쌓는다.
    /// 되돌리기/다시하기와, 나중에 힌트가 진행 상황을 읽는 통로가 된다.
    public sealed class MoveHistory
    {
        readonly List<Move> _done = new List<Move>();
        readonly List<Move> _undone = new List<Move>();

        public int Count => _done.Count;
        public IReadOnlyList<Move> Moves => _done;
        public bool CanUndo => _done.Count > 0;
        public bool CanRedo => _undone.Count > 0;

        public void Push(Move m)
        {
            _done.Add(m);
            _undone.Clear();   // 새 수를 두면 다시하기 갈래는 버린다
        }

        /// 되돌리려면 호출자가 적용해야 할 무브(역무브)를 돌려준다.
        public Move Undo()
        {
            if (!CanUndo) throw new InvalidOperationException("되돌릴 수가 없다");
            var m = _done[_done.Count - 1];
            _done.RemoveAt(_done.Count - 1);
            _undone.Add(m);
            return m.Inverse;
        }

        /// 다시하려면 호출자가 적용해야 할 무브를 돌려준다.
        public Move Redo()
        {
            if (!CanRedo) throw new InvalidOperationException("다시할 수가 없다");
            var m = _undone[_undone.Count - 1];
            _undone.RemoveAt(_undone.Count - 1);
            _done.Add(m);
            return m;
        }

        public void Clear()
        {
            _done.Clear();
            _undone.Clear();
        }
    }
}
