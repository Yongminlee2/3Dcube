using UnityEngine;

namespace Cube.App
{
    /// 손으로 맞춰야 하는 값들을 코드 밖으로 뺀다. 실기기에서 조정한다.
    [CreateAssetMenu(menuName = "Cube/TouchInputSettings")]
    public sealed class TouchInputSettings : ScriptableObject
    {
        [Tooltip("회전을 시작하는 드래그 거리. 화면 대각선 길이에 대한 비율")]
        public float DragStartFraction = 0.02f;

        [Tooltip("손을 뗐을 때 이 각도를 넘었으면 90도로 붙이고, 못 넘었으면 되돌린다")]
        public float SnapAngle = 45f;

        [Tooltip("배경 드래그로 큐브를 굴릴 때 픽셀당 각도")]
        public float OrbitSensitivity = 0.3f;

        [Tooltip("층을 미리 돌려 보여줄 때 픽셀당 각도")]
        public float DegreesPerPixel = 0.55f;

        [Tooltip("큐브 표면 판정에 주는 여유 반경(월드 단위). 4x4에서 오조작을 줄인다")]
        public float TouchPadding = 0.12f;
    }
}
