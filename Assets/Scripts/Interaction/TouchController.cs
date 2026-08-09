using UnityEngine;
using UnityEngine.EventSystems;
using Cube.Core;

namespace Cube.App
{
    /// 손가락 입력을 무브로 바꾼다. 상태도 애니메이션도 직접 건드리지 않는다.
    public sealed class TouchController : MonoBehaviour
    {
        public bool Enabled { get; set; } = true;

        Camera _cam;
        CubeRenderer _renderer;
        LayerRotator _rotator;
        OrbitCamera _orbit;
        TouchInputSettings _settings;

        enum Mode { Idle, Undecided, Orbiting, TwoFinger }
        Mode _mode = Mode.Idle;

        Vector2 _startPos, _lastPos;
        Vector3Int _hitNormal, _hitCoord;
        Vector3Int _tangentA, _tangentB;
        Vector2 _screenA, _screenB;

        Move _move;

        public void Init(Camera cam, CubeRenderer r, LayerRotator rot, OrbitCamera orbit)
        {
            _cam = cam; _renderer = r; _rotator = rot; _orbit = orbit;
            _settings = Resources.Load<TouchInputSettings>("TouchInputSettings");
            _mode = Mode.Idle;
        }

        void Update()
        {
            if (!Enabled || _cam == null || _renderer == null || _renderer.State == null) return;

            // 두 손가락은 언제나 시점 조정이다. 큐브가 화면을 거의 채우고 있어서
            // 배경을 끌 자리가 좁고, 그래서 밑면으로 돌리기가 어려웠다.
            if (Input.touchCount >= 2)
            {
                _mode = Mode.TwoFinger;
                var delta = (Input.GetTouch(0).deltaPosition + Input.GetTouch(1).deltaPosition) * 0.5f;
                _orbit.Orbit(delta);
                return;
            }

            if (_mode == Mode.TwoFinger)
            {
                // 손가락을 다 뗄 때까지는 새 드래그를 시작하지 않는다.
                if (Input.touchCount == 0) _mode = Mode.Idle;
                return;
            }

            if (Input.GetMouseButtonDown(0)) Begin(Input.mousePosition);
            else if (Input.GetMouseButton(0) && _mode != Mode.Idle) Drag(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0) && _mode != Mode.Idle) End();
        }

        void Begin(Vector2 screenPos)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            _startPos = _lastPos = screenPos;

            var ray = _cam.ScreenPointToRay(screenPos);
            float radius = _settings != null ? _settings.TouchPadding : 0.12f;
            if (!Physics.SphereCast(ray, radius, out RaycastHit hit, 100f))
            {
                _mode = Mode.Orbiting;
                return;
            }

            var marker = hit.collider.GetComponent<CubieMarker>();
            if (marker == null) { _mode = Mode.Orbiting; return; }

            _hitCoord = new Vector3Int(marker.X, marker.Y, marker.Z);
            _hitNormal = ToCoreDirection(hit.normal);
            if (_hitNormal == Vector3Int.zero) { _mode = Mode.Orbiting; return; }

            PrepareTangents(hit.point);
            _mode = Mode.Undecided;
        }

        void Drag(Vector2 screenPos)
        {
            Vector2 delta = screenPos - _lastPos;
            _lastPos = screenPos;

            if (_mode == Mode.Orbiting) { _orbit.Orbit(delta); return; }
            if (_mode != Mode.Undecided) return;

            Vector2 total = screenPos - _startPos;
            float diag = new Vector2(Screen.width, Screen.height).magnitude;
            float threshold = diag * (_settings != null ? _settings.DragStartFraction : 0.02f);
            if (total.magnitude < threshold) return;

            if (!SwipeResolver.Resolve(_hitNormal, _hitCoord, _renderer.State.N,
                                       _tangentA, _screenA, _tangentB, _screenB,
                                       total, out _move, out _))
                return;

            // 방향이 정해지는 순간 버튼과 똑같은 경로로 넘긴다.
            //
            // 예전에는 손가락을 따라 층을 미리 돌려 보여줬는데, 그러면 회전 속도가
            // 곧 손 속도가 된다 — 급히 밀면 휙 돌고 천천히 밀면 느리게 돌아
            // 같은 조작인데 매번 다르게 보였다. 이제 한 번의 스와이프는 한 번의
            // 회전이고, 속도는 설정의 회전 속도 하나가 정한다.
            _rotator.Enqueue(_move);
            _mode = Mode.Idle;   // 손을 뗄 때까지 이 드래그는 끝났다
        }

        void PrepareTangents(Vector3 worldHitPoint)
        {
            // 법선과 수직인 두 Core 축을 접선으로 삼는다.
            if (_hitNormal.x != 0)      { _tangentA = new Vector3Int(0, 1, 0); _tangentB = new Vector3Int(0, 0, 1); }
            else if (_hitNormal.y != 0) { _tangentA = new Vector3Int(1, 0, 0); _tangentB = new Vector3Int(0, 0, 1); }
            else                        { _tangentA = new Vector3Int(1, 0, 0); _tangentB = new Vector3Int(0, 1, 0); }

            _screenA = ProjectToScreen(worldHitPoint, _tangentA);
            _screenB = ProjectToScreen(worldHitPoint, _tangentB);
        }

        Vector2 ProjectToScreen(Vector3 worldPoint, Vector3Int coreDir)
        {
            Vector3 local = new Vector3(coreDir.x, coreDir.y, -coreDir.z);   // Core -> Unity
            Vector3 world = _renderer.transform.TransformDirection(local);
            Vector3 a = _cam.WorldToScreenPoint(worldPoint);
            Vector3 b = _cam.WorldToScreenPoint(worldPoint + world * 0.5f);
            return new Vector2(b.x - a.x, b.y - a.y);
        }

        Vector3Int ToCoreDirection(Vector3 worldNormal)
        {
            Vector3 local = _renderer.transform.InverseTransformDirection(worldNormal);
            Vector3 core = new Vector3(local.x, local.y, -local.z);          // Unity -> Core
            Vector3 abs = new Vector3(Mathf.Abs(core.x), Mathf.Abs(core.y), Mathf.Abs(core.z));
            if (abs.x >= abs.y && abs.x >= abs.z) return new Vector3Int((int)Mathf.Sign(core.x), 0, 0);
            if (abs.y >= abs.z)                   return new Vector3Int(0, (int)Mathf.Sign(core.y), 0);
            return new Vector3Int(0, 0, (int)Mathf.Sign(core.z));
        }

        void End() => _mode = Mode.Idle;
    }
}
