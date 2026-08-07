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

        enum Mode { Idle, Undecided, Rotating, Orbiting, TwoFinger }
        Mode _mode = Mode.Idle;

        Vector2 _startPos, _lastPos;
        Vector3Int _hitNormal, _hitCoord;
        Vector3Int _tangentA, _tangentB;
        Vector2 _screenA, _screenB;

        Move _move;
        Transform _pivot;
        float _angle;
        float _dragSign;

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
                if (_mode != Mode.TwoFinger) { CancelDrag(); _mode = Mode.TwoFinger; }
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

        /// 돌리던 것을 없던 일로 하고 큐비를 원래 자세로 돌려놓는다.
        void CancelDrag()
        {
            if (_pivot != null) ReleasePivot(toIdentity: true);
            _mode = Mode.Idle;
            _angle = 0f;
        }

        /// 미리보기 피벗을 걷어내고 큐비를 큐브 뿌리로 되돌린다.
        ///
        /// toIdentity면 피벗을 0도로 되돌린 뒤 떼어낸다. 비스듬한 자세로 떼어낸 다음
        /// 각도를 반올림해 맞추려 하면 엉뚱한 자세가 나와 큐브가 깨진다.
        void ReleasePivot(bool toIdentity)
        {
            if (_pivot == null) return;
            if (toIdentity) _pivot.localRotation = Quaternion.identity;

            for (int i = _pivot.childCount - 1; i >= 0; i--)
                _pivot.GetChild(i).SetParent(_renderer.transform, true);

            _pivot.SetParent(null, false);
            Destroy(_pivot.gameObject);
            _pivot = null;
        }

        void Begin(Vector2 screenPos)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            _startPos = _lastPos = screenPos;
            _angle = 0f;

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

            Vector2 total = screenPos - _startPos;

            if (_mode == Mode.Undecided)
            {
                float diag = new Vector2(Screen.width, Screen.height).magnitude;
                float threshold = diag * (_settings != null ? _settings.DragStartFraction : 0.02f);
                if (total.magnitude < threshold) return;

                if (!SwipeResolver.Resolve(_hitNormal, _hitCoord, _renderer.State.N,
                                           _tangentA, _screenA, _tangentB, _screenB,
                                           total, out _move, out float along))
                    return;

                _dragSign = Mathf.Sign(along);
                BeginPreview();
                _mode = Mode.Rotating;   // 축은 여기서 잠긴다
            }

            if (_mode == Mode.Rotating) UpdatePreview(total);
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

        void BeginPreview()
        {
            _pivot = new GameObject("DragPivot").transform;
            _pivot.SetParent(_renderer.transform, false);
            foreach (var t in _renderer.CubiesInLayer(_move)) t.SetParent(_pivot, true);
        }

        void UpdatePreview(Vector2 totalDrag)
        {
            Vector2 dir = (Mathf.Abs(Vector2.Dot(totalDrag, _screenA.normalized))
                        >= Mathf.Abs(Vector2.Dot(totalDrag, _screenB.normalized))) ? _screenA : _screenB;
            float pixels = Vector2.Dot(totalDrag, dir.normalized) * _dragSign;
            float dpp = _settings != null ? _settings.DegreesPerPixel : 0.55f;

            // 90도를 넘지 않게 잡아둔다. 한 번의 드래그는 한 번의 회전이다.
            _angle = Mathf.Clamp(Mathf.Max(0f, pixels) * dpp, 0f, 90f) * SignOfMove();
            _pivot.localRotation = Quaternion.AngleAxis(_angle, CubeRenderer.UnityAxis(_move.Axis));
        }

        /// 미리보기가 도는 방향. LayerRotator가 실제로 돌릴 각도와 부호를 맞춰야
        /// 손을 뗄 때 큐브가 튀지 않는다.
        float SignOfMove() => Mathf.Sign(LayerRotator.TotalAngle(_move));

        void End()
        {
            if (_mode == Mode.Rotating && _pivot != null)
            {
                float snap = _settings != null ? _settings.SnapAngle : 45f;
                bool commit = Mathf.Abs(_angle) >= snap;

                // 넘겼으면 지금 자세 그대로 두고 이어서 돌리고,
                // 못 넘겼으면 피벗을 0도로 되돌려 원래 자세를 정확히 복원한다.
                ReleasePivot(toIdentity: !commit);

                if (commit) _rotator.EnqueueFromAngle(_move, _angle);
            }

            _mode = Mode.Idle;
            _angle = 0f;
        }
    }
}
