using UnityEngine;

namespace Cube.App
{
    public enum SkinArtworkLayout
    {
        RepeatPerSticker = 0,
        WholeFace = 1,
    }

    /// 큐브 겉모습 하나. 다크/라이트 팔레트와는 독립적으로 고른다.
    [CreateAssetMenu(menuName = "Cube/Skin")]
    public sealed class Skin : ScriptableObject
    {
        public string DisplayName;
        public Color CubeBody;

        /// 캐릭터 일러스트 스킨은 재질/색상 스킨 뒤, 목록 맨 아래에 모아 보여 준다.
        public bool CharacterArtwork;

        /// 목록에서 감춘다. 파일은 그대로 두고 고를 수만 없게 한다.
        ///
        /// 수영복 차림 일러스트는 스토어 콘텐츠 등급을 「전체 이용가」로 낼 수 없다.
        /// 큐브를 배우는 앱이라 학습 화면에도 그대로 나오고, 나라에 따라 기준이
        /// 더 엄격하다. 지우지 않고 감춰 두었다가 등급을 정하거나 그림을 바꾸면
        /// 이 값만 꺼서 되살린다.
        public bool Hidden;

        /// 면 번호 순서: U, D, F, B, L, R
        public Color[] StickerColors = new Color[6];

        /// 이미지 텍스처 스킨용. 비어 있으면(null) 그 면은 StickerColors만 쓴다.
        /// 면 번호 순서는 StickerColors와 같다.
        public Texture2D[] StickerTextures = new Texture2D[6];
    }
}
