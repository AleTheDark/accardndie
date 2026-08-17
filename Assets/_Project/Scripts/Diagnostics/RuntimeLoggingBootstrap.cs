using UnityEngine;

namespace AccardND.Diagnostics
{
    /// <summary>
    /// Keeps Unity logging available while developing, but disables it in
    /// production builds on the platforms where log forwarding is costly.
    /// </summary>
    internal static class RuntimeLoggingBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ConfigureUnityLogger()
        {
#if (UNITY_ANDROID || UNITY_WEBGL) && !UNITY_EDITOR && !DEVELOPMENT_BUILD
            Debug.unityLogger.logEnabled = false;
#else
            Debug.unityLogger.logEnabled = true;
#endif
        }
    }
}
