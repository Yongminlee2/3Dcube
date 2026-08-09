using UnityEngine;

namespace Cube.App
{
    /// 손으로 맞춰야 하는 값들을 코드 밖으로 뺀다. 실기기에서 조정한다.
    [CreateAssetMenu(menuName = "Cube/TouchInputSettings")]
    public sealed class TouchInputSettings : ScriptableObject
    {
        // 이 값이 곧 "얼마나 밀어야 한 번 돌아가는가"다. 예전에는 미리보기를
        // 시작하는 문턱이라 넘긴 뒤에도 손을 되돌려 취소할 수 있었지만,
        // 지금은 넘기는 순간 회전이 확정된다.
        [Tooltip("이만큼 밀면 한 번 돌아간다. 화면 대각선 길이에 대한 비율. " +
                 "손이 자꾸 헛돌면 올리고, 잘 안 돌면 내린다")]
        public float DragStartFraction = 0.02f;

        [Tooltip("배경 드래그로 큐브를 굴릴 때 픽셀당 각도")]
        public float OrbitSensitivity = 0.3f;

        [Tooltip("큐브 표면 판정에 주는 여유 반경(월드 단위). 4x4에서 오조작을 줄인다")]
        public float TouchPadding = 0.12f;
    }
}
