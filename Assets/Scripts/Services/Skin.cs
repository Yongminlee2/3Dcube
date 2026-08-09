using UnityEngine;

namespace Cube.App
{
    /// 큐브 겉모습 하나. 다크/라이트 팔레트와는 독립적으로 고른다.
    [CreateAssetMenu(menuName = "Cube/Skin")]
    public sealed class Skin : ScriptableObject
    {
        public string DisplayName;
        public Color CubeBody;

        /// 면 번호 순서: U, D, F, B, L, R
        public Color[] StickerColors = new Color[6];

        /// 이미지 텍스처 스킨용. 비어 있으면(null) 그 면은 StickerColors만 쓴다.
        /// 면 번호 순서는 StickerColors와 같다.
        public Texture2D[] StickerTextures = new Texture2D[6];
    }
}
