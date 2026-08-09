using UnityEngine;

namespace Cube.App
{
    /// 배경 드래그로 큐브를 통째로 굴린다. 상태를 바꾸지 않는다.
    public sealed class OrbitCamera : MonoBehaviour
    {
        // 야로(Y) 45도는 앞면과 오른쪽 면이 똑같은 폭으로 보이는 진짜 대칭 구도다.
        // 34도처럼 45도에서 어긋나면 한쪽 면이 넓고 한쪽은 좁아 보여, 아무리
        // 손으로 맞춰도 "삐딱하다"는 느낌이 가시지 않는다는 의견이 있었다.
        public static readonly Quaternion DefaultView = Quaternion.Euler(-28f, 45f, 0f);

        Transform _cubeRoot;
        TouchInputSettings _settings;

        public void Init(Transform cubeRoot)
        {
            _cubeRoot = cubeRoot;
            _settings = Resources.Load<TouchInputSettings>("TouchInputSettings");
            ResetView();
        }

        public void ResetView()
        {
            if (_cubeRoot != null) _cubeRoot.localRotation = DefaultView;
        }

        /// 화면 드래그를 카메라 기준 회전으로 옮긴다.
        /// 카메라 축을 쓰기 때문에 큐브가 어떤 자세든 "미는 방향"이 그대로 유지된다.
        public void Orbit(Vector2 deltaPixels)
        {
            if (_cubeRoot == null) return;
            float s = _settings != null ? _settings.OrbitSensitivity : 0.3f;
            var cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 up = cam != null ? cam.up : Vector3.up;
            Vector3 right = cam != null ? cam.right : Vector3.right;

            _cubeRoot.rotation = Quaternion.AngleAxis(-deltaPixels.x * s, up)
                               * Quaternion.AngleAxis(deltaPixels.y * s, right)
                               * _cubeRoot.rotation;
        }
    }
}
