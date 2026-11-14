using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Menu Build/Android (Play Store AAB). Chỉ đụng tới target Android khi bấm menu này, bản WebGL không đổi.
/// Keystore lấy từ biến môi trường (đừng commit keystore / mật khẩu):
/// LIARS_KEYSTORE_PATH, LIARS_KEYSTORE_PASS, LIARS_KEY_ALIAS, LIARS_KEY_PASS.
/// Không đặt thì dùng cấu hình sẵn trong Player Settings > Publishing Settings.
/// </summary>
public static class BuildAndroid
{
    [MenuItem("Build/Android (Play Store AAB)")]
    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) { Debug.LogError("Build Settings chưa có scene nào được bật."); return; }

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.buildAppBundle = true;                       // Play Store bắt buộc AAB
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; // Play yêu cầu 64-bit
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        var ks = Environment.GetEnvironmentVariable("LIARS_KEYSTORE_PATH");
        if (!string.IsNullOrEmpty(ks))
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = ks;
            PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("LIARS_KEYSTORE_PASS");
            PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("LIARS_KEY_ALIAS");
            PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("LIARS_KEY_PASS");
        }
        else if (!PlayerSettings.Android.useCustomKeystore)
            Debug.LogWarning("Chưa có keystore: AAB ký bằng debug key, Play Store sẽ từ chối. Đặt LIARS_KEYSTORE_* hoặc chọn keystore trong Player Settings.");

        PlayerSettings.Android.bundleVersionCode++;                          // mỗi lần upload Play cần số mới

        Directory.CreateDirectory("Builds/Android");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/Android/LiarsCard.aab",
            target = BuildTarget.Android,
            options = BuildOptions.None
        });
        Debug.Log($"Android build: {report.summary.result}, versionCode {PlayerSettings.Android.bundleVersionCode}, {report.summary.outputPath}");
    }
}
