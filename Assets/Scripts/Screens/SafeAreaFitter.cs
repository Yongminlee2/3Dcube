using UnityEngine;

namespace Cube.App
{
    /// 카메라 홀과 제스처 영역을 피해 모든 화면을 안전 영역 안에 둔다.
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rect;
        Rect _lastSafeArea;
        Vector2Int _lastScreen;

        void OnEnable()
        {
            _rect = transform as RectTransform;
            Apply();
        }

        void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea == _lastSafeArea && size == _lastScreen) return;
            Apply();
        }

        void Apply()
        {
            if (_rect == null || Screen.width <= 0 || Screen.height <= 0) return;

            var safe = Screen.safeArea;
            var min = safe.position;
            var max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
            _lastSafeArea = safe;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
