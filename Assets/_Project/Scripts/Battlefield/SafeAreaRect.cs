using UnityEngine;

namespace AccardND.Battlefield
{
    /// <summary>
    /// Ancora il proprio RectTransform alla safe area dello schermo (notch,
    /// fotocamere punch-hole, barre di sistema) e si riadatta a ogni rotazione.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaRect : MonoBehaviour
    {
        private Rect lastSafeArea = Rect.zero;
        private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea || Screen.orientation != lastOrientation)
                Apply();
        }

        private void Apply()
        {
            lastSafeArea = Screen.safeArea;
            lastOrientation = Screen.orientation;
            var rect = (RectTransform)transform;
            float width = Mathf.Max(1f, Screen.width);
            float height = Mathf.Max(1f, Screen.height);
            rect.anchorMin = new Vector2(lastSafeArea.xMin / width, lastSafeArea.yMin / height);
            rect.anchorMax = new Vector2(lastSafeArea.xMax / width, lastSafeArea.yMax / height);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
