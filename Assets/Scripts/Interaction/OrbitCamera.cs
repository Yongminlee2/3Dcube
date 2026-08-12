using UnityEngine;

namespace Cube.App
{
    /// 배경 드래그로 큐브를 통째로 굴린다. 상태를 바꾸지 않는다.
    public sealed class OrbitCamera : MonoBehaviour
    {
        // 야로(Y) 45도는 앞면과 오른쪽 면이 똑같은 폭으로 보이는 진짜 대칭 구도다.
        // 34도처럼 45도에서 어긋나면 한쪽 면이 넓고 한쪽은 좁아 보여, 아무리
        // 손으로 맞춰도 "삐딱하다"는 느낌이 가시지 않는다는 의견이 있었다.
        const float DefaultPitch = -28f;
        const float DefaultYaw = 45f;
        const float MinPitch = -72f;
        const float MaxPitch = 72f;

        public static readonly Quaternion DefaultView = ComposeView(DefaultPitch, DefaultYaw);

        Transform _cubeRoot;
        TouchInputSettings _settings;
        float _pitch;
        float _yaw;

        public void Init(Transform cubeRoot)
        {
            _cubeRoot = cubeRoot;
            _settings = Resources.Load<TouchInputSettings>("TouchInputSettings");
            ResetView();
        }

        public void ResetView()
        {
            _pitch = DefaultPitch;
            _yaw = DefaultYaw;
            ApplyView();
        }

        /// 화면 드래그를 카메라 기준 회전으로 옮긴다.
        /// 카메라 축을 쓰기 때문에 큐브가 어떤 자세든 "미는 방향"이 그대로 유지된다.
        public void Orbit(Vector2 deltaPixels)
        {
            if (_cubeRoot == null) return;
            float s = _settings != null ? _settings.OrbitSensitivity : 0.3f;
            _yaw -= deltaPixels.x * s;
            _pitch = Mathf.Clamp(_pitch + deltaPixels.y * s, MinPitch, MaxPitch);
            ApplyView();
        }

        /// Y축 회전을 먼저 적용하고 화면의 X축으로 기울인다. 이렇게 조합하면
        /// 큐브의 세로축이 화면 좌우로 넘어가지 않아 시점 이동 뒤에도 수평이 유지된다.
        public static Quaternion ComposeView(float pitch, float yaw)
            => Quaternion.AngleAxis(pitch, Vector3.right)
             * Quaternion.AngleAxis(yaw, Vector3.up);

        void ApplyView()
        {
            if (_cubeRoot != null) _cubeRoot.localRotation = ComposeView(_pitch, _yaw);
        }
    }
}
