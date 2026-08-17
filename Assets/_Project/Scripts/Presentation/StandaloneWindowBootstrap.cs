using UnityEngine;

namespace AccardND.Presentation
{
    /// <summary>
    /// Su desktop apre il gioco in finestra portrait 9:16, scalata per stare
    /// nello schermo disponibile.
    /// </summary>
    internal static class StandaloneWindowBootstrap
    {
        private const int TargetWidth = 1080;
        private const int TargetHeight = 1920;
        private const int MinimumWidth = 540;
        private const int MinimumHeight = 960;
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
            int availableWidth = Mathf.Max(MinimumWidth, Display.main.systemWidth - DesktopWindowMargin);
            int availableHeight = Mathf.Max(MinimumHeight, Display.main.systemHeight - DesktopWindowMargin);
            float scale = Mathf.Min(1f, Mathf.Min(availableWidth / (float)TargetWidth, availableHeight / (float)TargetHeight));

            int width = Mathf.Max(MinimumWidth, Mathf.RoundToInt(TargetWidth * scale));
            int height = Mathf.Max(MinimumHeight, Mathf.RoundToInt(TargetHeight * scale));
            return new Vector2Int(width, height);
        }
#endif
    }
}
