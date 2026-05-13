using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class MobileBuild
{
    const string AndroidOutput = "build/android/SolarmixPhysical.apk";
    const string IOSOutput = "build/ios-physical";

    public static void BuildAndroid()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AndroidOutput));
        ApplyPlayerVersion();
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, "HIFI_HARP_SPATIALIZER");
        PlayerSettings.applicationIdentifier = "com.merry.solarmix.audio8";
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        var report = BuildPipeline.BuildPlayer(GetEnabledScenes(), AndroidOutput, BuildTarget.Android,
            BuildOptions.Development | BuildOptions.AllowDebugging);
        ExitWithReport(report);
    }

    public static void BuildAndroidAndRun()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AndroidOutput));
        ApplyPlayerVersion();
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, "HIFI_HARP_SPATIALIZER");
        PlayerSettings.applicationIdentifier = "com.merry.solarmix.audio8";
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        var report = BuildPipeline.BuildPlayer(GetEnabledScenes(), AndroidOutput, BuildTarget.Android,
            BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.AutoRunPlayer);
        ExitWithReport(report);
    }

    public static void BuildIOS()
    {
        Directory.CreateDirectory(IOSOutput);
        ApplyPlayerVersion();
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, "HIFI_HARP_SPATIALIZER");
        PlayerSettings.applicationIdentifier = "com.merry.solarmix.audio8";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

        var report = BuildPipeline.BuildPlayer(GetEnabledScenes(), IOSOutput, BuildTarget.iOS,
            BuildOptions.Development | BuildOptions.AllowDebugging);
        ExitWithReport(report);
    }

    static string[] GetEnabledScenes()
    {
        return Array.ConvertAll(
            Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled),
            scene => scene.path);
    }

    static void ApplyPlayerVersion()
    {
        PlayerSettings.bundleVersion = "0.4.0";
        PlayerSettings.iOS.buildNumber = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        PlayerSettings.productName = "Solarmix Physical";
    }

    static void ExitWithReport(BuildReport report)
    {
        var summary = report.summary;
        UnityEngine.Debug.Log($"Mobile build {summary.result}: {summary.outputPath}");
        if (summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
