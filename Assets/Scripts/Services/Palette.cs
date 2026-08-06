using UnityEngine;

namespace Cube.App
{
    /// 화면에 쓰이는 모든 색이 여기서 나온다. 코드에 색 리터럴을 쓰지 않는다.
    [CreateAssetMenu(menuName = "Cube/Palette")]
    public sealed class Palette : ScriptableObject
    {
        public Color Background;
        public Color Surface;
        public Color TextPrimary;
        public Color TextSecondary;
        public Color Border;
        public Color Accent;
        public Color CubeBody;

        /// 면 번호 순서: U, D, F, B, L, R
        public Color[] StickerColors = new Color[6];
    }
}
