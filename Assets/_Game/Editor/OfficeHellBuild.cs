using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OfficeHell.EditorTools
{
    /// <summary>
    /// Windows player build entry points.
    ///
    /// Release is the default because it is the distributable one and still supports the command
    /// line soak runner. The development entry remains for a standard editor installation, but the
    /// customised local player cannot load its output.
    ///
    ///   Unity.exe -batchmode -projectPath &lt;dir&gt; -executeMethod OfficeHell.EditorTools.OfficeHellBuild.BuildWindows
    ///   Unity.exe -batchmode -projectPath &lt;dir&gt; -executeMethod OfficeHell.EditorTools.OfficeHellBuild.BuildWindowsDevelopment
    /// </summary>
    public static class OfficeHellBuild
    {
        const string ReleaseDir = "Build/Windows";
        const string DevelopmentDir = "Build/WindowsDev";
        const string ExeName = "OfficeHell.exe";

        [MenuItem("Office Hell/Build Windows Player (Release)", false, 40)]
        public static void BuildReleaseMenu()
        {
            Build(false);
        }

        [MenuItem("Office Hell/Build Windows Player (Development)", false, 41)]
        public static void BuildDevelopmentMenu()
        {
            Build(true);
        }

        public static void BuildWindows()
        {
            EditorApplication.Exit(Build(false) ? 0 : 1);
        }

        public static void BuildWindowsDevelopment()
        {
            EditorApplication.Exit(Build(true) ? 0 : 1);
        }

        static bool Build(bool development)
        {
            OfficeHellSetup.SetupMenu();

            string dir = development ? DevelopmentDir : ReleaseDir;

            // A stale output folder is how an incremental build ends up with a data file that does
            // not match its player, which surfaces at runtime as a corrupted globalgamemanagers.
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }

            Directory.CreateDirectory(dir);

            BuildPlayerOptions options = new BuildPlayerOptions();
            options.scenes = new[] { "Assets/_Game/Scenes/Main.unity" };
            options.locationPathName = Path.Combine(dir, ExeName);
            options.target = BuildTarget.StandaloneWindows64;
            options.targetGroup = BuildTargetGroup.Standalone;
            options.options = development
                ? BuildOptions.Development | BuildOptions.AllowDebugging
                : BuildOptions.None;

            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log(string.Format("[OfficeHell] build {0} ({1}): {2} error(s), {3:0.0} MB, {4:0.0}s -> {5}",
                summary.result, development ? "development" : "release", summary.totalErrors,
                summary.totalSize / 1048576f, (float)summary.totalTime.TotalSeconds, options.locationPathName));

            return summary.result == BuildResult.Succeeded && summary.totalErrors == 0;
        }
    }
}
