using UnityEngine;

namespace AccardND.Presentation
{
    internal static class StandalonePortraitWindow
    {
        private const int TargetWidth = 1080;
        private const int TargetHeight = 1920;
        private const int MinimumWidth = 360;
        private const int MinimumHeight = 640;
        private const int DesktopWindowMargin = 80;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            Vector2Int size = CalculateWindowSize();
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
#endif
        }

#if UNITY_STANDALONE && !UNITY_EDITOR
        private static Vector2Int CalculateWindowSize()
        {
            int availableWidth = Mathf.Max(MinimumWidth, Display.main.systemWidth);
            int availableHeight = Mathf.Max(MinimumHeight, Display.main.systemHeight - DesktopWindowMargin);
            float scale = Mathf.Min(1f, Mathf.Min(availableWidth / (float)TargetWidth, availableHeight / (float)TargetHeight));

            int width = Mathf.Max(MinimumWidth, Mathf.RoundToInt(TargetWidth * scale));
            int height = Mathf.Max(MinimumHeight, Mathf.RoundToInt(TargetHeight * scale));
            return new Vector2Int(width, height);
        }
#endif
    }
}
