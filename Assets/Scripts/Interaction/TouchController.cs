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

        enum Mode { Idle, Undecided, Rotating, Orbiting }
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

            if (Input.GetMouseButtonDown(0)) Begin(Input.mousePosition);
            else if (Input.GetMouseButton(0) && _mode != Mode.Idle) Drag(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0) && _mode != Mode.Idle) End();
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

                // 미리보기 피벗을 걷어내되, 큐비는 지금 자세 그대로 둔다.
                for (int i = _pivot.childCount - 1; i >= 0; i--)
                    _pivot.GetChild(i).SetParent(_renderer.transform, true);
                _pivot.SetParent(null, false);
                Destroy(_pivot.gameObject);
                _pivot = null;

                if (commit) _rotator.EnqueueFromAngle(_move, _angle);
                else _rotator.SnapAll();   // 못 미쳤으면 되돌린다
            }

            _mode = Mode.Idle;
            _angle = 0f;
        }
    }
}
