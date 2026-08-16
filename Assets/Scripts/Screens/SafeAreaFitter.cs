using UnityEngine;

namespace Cube.App
{
    /// 카메라 홀과 제스처 영역을 피해 모든 화면을 안전 영역 안에 둔다.
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        public const float AndroidBottomGuardRatio = 0.04f;

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

            CalculateAnchors(Screen.safeArea, Screen.width, Screen.height,
                Application.platform == RuntimePlatform.Android, out var min, out var max);

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
            _lastSafeArea = Screen.safeArea;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
        }

        /// 일부 Android 기기는 3버튼 내비게이션 영역을 safeArea에 넣지 않는다.
        /// 그 경우에도 하단 버튼이 시스템 뒤로/홈 버튼과 겹치지 않도록 최소 여백을 둔다.
        public static void CalculateAnchors(Rect safe, int screenWidth, int screenHeight,
                                            bool addAndroidBottomGuard,
                                            out Vector2 min, out Vector2 max)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                min = Vector2.zero;
                max = Vector2.one;
                return;
            }

            if (addAndroidBottomGuard)
                safe.yMin = Mathf.Max(safe.yMin, screenHeight * AndroidBottomGuardRatio);

            min = safe.position;
            max = safe.position + safe.size;
            min.x /= screenWidth;
            min.y /= screenHeight;
            max.x /= screenWidth;
            max.y /= screenHeight;
        }
    }
}
