using UnityEngine;

namespace AccardND.Presentation
{
    internal static class TargetFrameRateBootstrap
    {
        private const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
#if (UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
#endif
        }
    }
}
