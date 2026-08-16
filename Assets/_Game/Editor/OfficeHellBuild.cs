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
        const string IconSourcePath = "testAssets/logo.png";
        const string IconAssetPath = "Assets/_Game/UI/Art/WindowsPlayerIcon.png";

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
            ConfigureWindowsIcon();

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log(string.Format("[OfficeHell] build {0} ({1}): {2} error(s), {3:0.0} MB, {4:0.0}s -> {5}",
                summary.result, development ? "development" : "release", summary.totalErrors,
                summary.totalSize / 1048576f, (float)summary.totalTime.TotalSeconds, options.locationPathName));

            return summary.result == BuildResult.Succeeded && summary.totalErrors == 0;
        }

        static void ConfigureWindowsIcon()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string sourcePath = Path.Combine(projectRoot, IconSourcePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Windows player icon source is missing.", sourcePath);
            }

            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(sourcePath), false))
            {
                Object.DestroyImmediate(source);
                throw new InvalidDataException("Windows player icon source is not a readable PNG: " + sourcePath);
            }

            int side = Mathf.NextPowerOfTwo(Mathf.Max(source.width, source.height));
            Texture2D square = new Texture2D(side, side, TextureFormat.RGBA32, false);
            square.SetPixels32(new Color32[side * side]);
            int offsetX = (side - source.width) / 2;
            int offsetY = (side - source.height) / 2;
            square.SetPixels(offsetX, offsetY, source.width, source.height, source.GetPixels());
            square.Apply(false, false);

            string iconFilePath = Path.Combine(projectRoot,
                IconAssetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(iconFilePath));
            File.WriteAllBytes(iconFilePath, square.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(square);

            AssetDatabase.ImportAsset(IconAssetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(IconAssetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidDataException("Unity could not import the generated Windows player icon.");
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = side;
            importer.SaveAndReimport();

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconAssetPath);
            if (icon == null)
            {
                throw new InvalidDataException("Unity did not load the generated Windows player icon asset.");
            }

            int[] iconSizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone);
            Texture2D[] icons = new Texture2D[iconSizes.Length];
            for (int i = 0; i < icons.Length; i++)
            {
                icons[i] = icon;
            }

            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, icons);
            Debug.Log("[OfficeHell] Windows icon: " + sourcePath + " -> " + side + "x" + side);
        }
    }
}
