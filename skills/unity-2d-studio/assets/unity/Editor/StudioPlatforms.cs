using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Studio.EditorTools
{
    // Player settings per target in one click; the active build target is not switched (that reimports every texture).
    public static class StudioPlatforms
    {
        private const string Menu = "Tools/Studio/Platform Preset/";
        private static readonly List<string> Log = new List<string>();

        [MenuItem(Menu + "Mobile Portrait")]
        public static void MobilePortrait() => Mobile(true);

        [MenuItem(Menu + "Mobile Landscape")]
        public static void MobileLandscape() => Mobile(false);

        [MenuItem(Menu + "WebGL")]
        public static void WebGL()
        {
            Begin("WebGL");
            // Gzip + fallback runs on hosts that don't send Content-Encoding headers (itch.io, plain static hosting).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            PlayerSettings.runInBackground = false;
            Note("WebGL: gzip + decompression fallback, data caching, explicit exceptions only, stripping Low");
            Note("WebGL limits: no threads (no Task.Run work), audio starts after first click/tap, no System.IO outside persistentDataPath");
            Finish();
        }

        [MenuItem(Menu + "PC")]
        public static void Pc()
        {
            Begin("PC");
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            QualitySettings.vSyncCount = 1;
            Note("PC: fullscreen window 1920x1080 default, resizable, IL2CPP, vSync on");
            Finish();
        }

        private static void Mobile(bool portrait)
        {
            Begin(portrait ? "Mobile Portrait" : "Mobile Landscape");
            PlayerSettings.defaultInterfaceOrientation = portrait ? UIOrientation.Portrait : UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = portrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = !portrait;
            PlayerSettings.allowedAutorotateToLandscapeRight = !portrait;
            PlayerSettings.runInBackground = false;
            PlayerSettings.statusBarHidden = true;
            foreach (var target in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Low);
            }

            // Google Play requires 64-bit; ARMv7 only adds build time and size for devices that are mostly gone.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.optimizedFramePacing = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.iOS.targetOSVersionString = "13.0";
            Note($"{(portrait ? "portrait locked" : "landscape left/right")} · IL2CPP · Android ARM64, API 24+, target auto, ASTC, frame pacing · iOS 13+");
            Note("code side: Application.targetFrameRate = 60 in Bootstrap (mobile default is 30) · safe area · no vibration without a settings toggle");
            Finish();
        }

        private static void Begin(string preset)
        {
            Log.Clear();
            Log.Add($"# {DateTime.Now:yyyy-MM-dd HH:mm} platform preset: {preset}");
        }

        private static void Note(string line) => Log.Add(line);

        private static void Finish()
        {
            Log.Add($"active build target: {EditorUserBuildSettings.activeBuildTarget} (switch in Build Profiles when ready; it reimports assets)");
            AssetDatabase.SaveAssets();
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", ".studio");
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "platform.md"), Log);
            Debug.Log("[StudioPlatforms] " + string.Join(" | ", Log));
        }
    }
}
