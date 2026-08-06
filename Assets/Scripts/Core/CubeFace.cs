namespace Cube.Core
{
    /// 면 번호는 고정이다. 칸 배열의 인덱스가 여기에 묶여 있다.
    public enum Face { U = 0, D = 1, F = 2, B = 3, L = 4, R = 5 }

    public static class Faces
    {
        public const int Count = 6;
        public static readonly Face[] All = { Face.U, Face.D, Face.F, Face.B, Face.L, Face.R };
    }
}
