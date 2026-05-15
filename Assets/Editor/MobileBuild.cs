using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MobileBuild
{
    static string ProjectRoot   => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static string IOSOutput     => Path.Combine(ProjectRoot, "build", "ios-meshrir");
    static string AndroidOutput => Path.Combine(ProjectRoot, "build", "android", "SolarmixMeshRIR.apk");
    static string WebGLOutput   => Path.Combine(ProjectRoot, "build", "webgl-meshrir");

    public static void BuildAndroid()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AndroidOutput));
        ApplyPlayerVersion();
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, "MESH_RIR_SPATIALIZER");
        PlayerSettings.applicationIdentifier = "com.merry.solarmix.meshrir";
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        var report = BuildPipeline.BuildPlayer(GetEnabledScenes(), AndroidOutput, BuildTarget.Android,
            BuildOptions.Development | BuildOptions.AllowDebugging);
        ExitWithReport(report);
    }

    public static void BuildAndroidAndRun()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(AndroidOutput));
        ApplyPlayerVersion();
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, "MESH_RIR_SPATIALIZER");
        PlayerSettings.applicationIdentifier = "com.merry.solarmix.meshrir";
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        var report = BuildPipeline.BuildPlayer(GetEnabledScenes(), AndroidOutput, BuildTarget.Android,
            BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.AutoRunPlayer);
        ExitWithReport(report);
    }

    public static void BuildIOS()
    {
        string output = IOSOutput;
        Directory.CreateDirectory(output);
        UnityEngine.Debug.Log($"[MobileBuild] iOS output path: {output}");

        ApplyPlayerVersion();
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.iOS, "MESH_RIR_SPATIALIZER");
        PlayerSettings.applicationIdentifier = "com.merry.solarmix.meshrir";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

        // Release build: no on-screen debug console, no development overlay
        var report = BuildPipeline.BuildPlayer(GetEnabledScenes(), output, BuildTarget.iOS,
            BuildOptions.None);
        ExitWithReport(report);
    }

    public static void BuildWebGL()
    {
        Directory.CreateDirectory(WebGLOutput);
        UnityEngine.Debug.Log($"[MobileBuild] WebGL output: {WebGLOutput}");

        ApplyPlayerVersion();
        // WebGL shares the same scripting define as iOS — MeshRIR synthetic fallback
        // is used because StreamingAssets on WebGL requires HTTP fetch, not File.IO.
        // The synthetic source table provides full spatial audio without any .npy files.
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, "MESH_RIR_SPATIALIZER");
        PlayerSettings.applicationIdentifier = "com.merry.solarmix.meshrir";

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        var report = BuildPipeline.BuildPlayer(GetEnabledScenes(), WebGLOutput, BuildTarget.WebGL,
            BuildOptions.None);
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
        PlayerSettings.bundleVersion = "0.5.0";
        PlayerSettings.iOS.buildNumber = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        PlayerSettings.productName = "Solarmix MeshRIR";
    }

    static void ExitWithReport(BuildReport report)
    {
        var summary = report.summary;
        UnityEngine.Debug.Log($"[MobileBuild] Result: {summary.result} → {summary.outputPath}");
        if (summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
