using UnityEngine;

namespace InputAnimatorBridge.Mobile
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        [Header("Safe Area Settings")]
        public bool enableSafeArea = true;
        public bool applyInEditor = true;

        [Tooltip("Extra padding inside the safe area (Left, Right, Top, Bottom)")]
        public Vector4 extraPadding;

        private RectTransform rectTransform;
        private Rect lastSafeArea;
        private Vector2Int lastScreenSize;
        private ScreenOrientation lastOrientation;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            if (!enableSafeArea)
                return;

            if (Screen.safeArea != lastSafeArea ||
                Screen.width != lastScreenSize.x ||
                Screen.height != lastScreenSize.y ||
                Screen.orientation != lastOrientation)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            if (!enableSafeArea)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                return;
            }

#if UNITY_EDITOR
            if (!applyInEditor && !Application.isMobilePlatform)
                return;
#endif

            Rect safeArea = Screen.safeArea;

            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastOrientation = Screen.orientation;

            // Apply extra padding
            safeArea.xMin += extraPadding.x;
            safeArea.xMax -= extraPadding.y;
            safeArea.yMax -= extraPadding.z;
            safeArea.yMin += extraPadding.w;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
