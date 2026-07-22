using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace AccardND.Editor
{
    public static class AppIdentityConfigurator
    {
        private const string AppName = "AcCardNDie";
        private const string BundleIdentifier = "com.apesolution.accardndie";
        private const string MasterIconPath = "Assets/_Project/Art/AppIcon/accard-n-die-icon-master-1024.png";

        [MenuItem("AccardND/Configure App Identity")]
        public static void Configure()
        {
            PlayerSettings.companyName = AppName;
            PlayerSettings.productName = AppName;
            PlayerSettings.applicationIdentifier = BundleIdentifier;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(MasterIconPath);
            if (icon == null)
            {
                throw new System.InvalidOperationException($"Unable to load app icon at {MasterIconPath}");
            }

            var icons = new[] { icon };
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, icons, IconKind.Application);
            PlayerSettings.SetIcons(NamedBuildTarget.Android, icons, IconKind.Application);
            ConfigureAndroidAdaptiveIcons(icon);

            EditorUtility.SetDirty(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            AssetDatabase.SaveAssets();
            Debug.Log($"{AppName} identity and app icons configured.");
        }

        private static void ConfigureAndroidAdaptiveIcons(Texture2D icon)
        {
            var platform = NamedBuildTarget.Android;
            PlatformIconKind kind = AndroidPlatformIconKind.Adaptive;
            var adaptiveIcons = PlayerSettings.GetPlatformIcons(platform, kind);

            foreach (var adaptiveIcon in adaptiveIcons)
            {
                adaptiveIcon.SetTextures(new[] { icon, icon });
            }

            PlayerSettings.SetPlatformIcons(platform, kind, adaptiveIcons);
        }
    }
}
