using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Player builds for sharing: WebGL (GitHub Pages), Windows x64 and macOS. Output goes to Builds/<platform>.
//   Unity -batchmode -quit -projectPath . -executeMethod GameBuilds.BuildAll
public static class GameBuilds
{
    private const string Scene = "Assets/Scenes/Game.unity";

    [MenuItem("Tools/Retro/Build/WebGL")]
    public static void WebGL() => Build(BuildTarget.WebGL, "Builds/WebGL");

    [MenuItem("Tools/Retro/Build/Windows")]
    public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/GhostVsJason.exe");

    [MenuItem("Tools/Retro/Build/macOS")]
    public static void Mac() => Build(BuildTarget.StandaloneOSX, "Builds/macOS/GhostVsJason.app");

    public static void BuildAll()
    {
        bool ok = Build(BuildTarget.WebGL, "Builds/WebGL")
                  & Build(BuildTarget.StandaloneWindows64, "Builds/Windows/GhostVsJason.exe")
                  & Build(BuildTarget.StandaloneOSX, "Builds/macOS/GhostVsJason.app");
        if (Application.isBatchMode)
            EditorApplication.Exit(ok ? 0 : 1);
    }

    private static bool Build(BuildTarget target, string path)
    {
        PlayerSettings.productName = "Ghost vs Jason";
        PlayerSettings.companyName = "Becados Halloween";
        PlayerSettings.defaultScreenWidth = 1152;
        PlayerSettings.defaultScreenHeight = 648;
        PlayerSettings.runInBackground = true;
        if (target == BuildTarget.WebGL)
        {
            // GitHub Pages cannot send Content-Encoding headers, so let the loader decompress gzip itself.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
        }

        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = path,
            target = target,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[GameBuilds] {target}: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, " +
                  $"{report.summary.totalErrors} errors");
        return report.summary.result == BuildResult.Succeeded;
    }
}
