using UnityEngine;
using Cube.Core;

namespace Cube.App
{
    /// 스와이프 하나를 무브로 바꾸는 순수 계산. 씬도 카메라도 알지 못한다.
    public static class SwipeResolver
    {
        /// normal       : 손가락이 닿은 면의 법선 (Core 좌표, 성분 하나만 ±1)
        /// cubieCoord   : 닿은 큐비의 격자 좌표
        /// tangentA/B   : 그 면 위의 두 접선 축 (Core 좌표 단위벡터)
        /// screenA/B    : 각 접선을 화면에 투영한 방향 (정규화 여부는 상관없다)
        /// drag         : 화면에서의 드래그 벡터
        /// dragAlongAxis: 고른 접선 방향으로 실린 드래그 크기 (부호 포함)
        public static bool Resolve(Vector3Int normal, Vector3Int cubieCoord, int n,
                                   Vector3Int tangentA, Vector2 screenA,
                                   Vector3Int tangentB, Vector2 screenB,
                                   Vector2 drag, out Move move, out float dragAlongAxis)
        {
            move = default;
            dragAlongAxis = 0f;
            if (drag.sqrMagnitude < 1e-6f) return false;

            float a = screenA.sqrMagnitude > 1e-6f ? Vector2.Dot(drag, screenA.normalized) : 0f;
            float b = screenB.sqrMagnitude > 1e-6f ? Vector2.Dot(drag, screenB.normalized) : 0f;

            Vector3Int tangent;
            float along;
            if (Mathf.Abs(a) >= Mathf.Abs(b)) { tangent = tangentA; along = a; }
            else                              { tangent = tangentB; along = b; }
            if (Mathf.Abs(along) < 1e-4f) return false;

            // 회전축 벡터 = 법선 × 접선 (Core 오른손 좌표)
            Vector3Int axisVec = Cross(normal, tangent);
            if (!TryAxis(axisVec, out Axis axis, out int sign)) return false;

            // Move의 turns=1은 축의 양의 방향에서 볼 때 시계방향, 곧 오른손 기준 음의 회전이다.
            int turns = sign > 0 ? 3 : 1;
            if (along < 0f) turns = 4 - turns;

            int coord = axis == Axis.X ? cubieCoord.x : axis == Axis.Y ? cubieCoord.y : cubieCoord.z;
            int layer = n - 1 - coord;
            if (layer < 0 || layer >= n) return false;

            move = new Move(axis, layer, turns);
            dragAlongAxis = along;
            return true;
        }

        static Vector3Int Cross(Vector3Int u, Vector3Int v) => new Vector3Int(
            u.y * v.z - u.z * v.y,
            u.z * v.x - u.x * v.z,
            u.x * v.y - u.y * v.x);

        static bool TryAxis(Vector3Int v, out Axis axis, out int sign)
        {
            if (v.x != 0 && v.y == 0 && v.z == 0) { axis = Axis.X; sign = v.x; return true; }
            if (v.y != 0 && v.x == 0 && v.z == 0) { axis = Axis.Y; sign = v.y; return true; }
            if (v.z != 0 && v.x == 0 && v.y == 0) { axis = Axis.Z; sign = v.z; return true; }
            axis = Axis.X; sign = 0; return false;
        }
    }
}
