using System;

namespace Cube.Core
{
    /// 회전 한 번.
    /// Layer는 축의 양의 방향 끝에서부터 0이며, 격자 좌표로는 N-1-Layer 이다.
    /// Turns는 90° 단위로 1~3이고, 축의 양의 방향에서 원점을 볼 때 시계방향이 1이다.
    public readonly struct Move : IEquatable<Move>
    {
        public readonly Axis Axis;
        public readonly int Layer;
        public readonly int Turns;

        public Move(Axis axis, int layer, int turns)
        {
            if (layer < 0) throw new ArgumentOutOfRangeException(nameof(layer));
            if (turns < 1 || turns > 3) throw new ArgumentOutOfRangeException(nameof(turns));
            Axis = axis; Layer = layer; Turns = turns;
        }

        public Move Inverse => new Move(Axis, Layer, 4 - Turns);

        public bool Equals(Move o) => Axis == o.Axis && Layer == o.Layer && Turns == o.Turns;
        public override bool Equals(object o) => o is Move m && Equals(m);
        public override int GetHashCode() => ((int)Axis * 397 ^ Layer) * 397 ^ Turns;
        public override string ToString() => $"{Axis}{Layer}x{Turns}";
    }
}
