using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OfficeHell.EditorTools
{
    /// <summary>
    /// One time project setup. The game itself boots from a RuntimeInitializeOnLoadMethod, so an
    /// empty scene is enough to press play. This only creates the scene entry for a build and
    /// applies the landscape player settings the design assumes.
    /// </summary>
    public static class OfficeHellSetup
    {
        const string ScenePath = "Assets/_Game/Scenes/Main.unity";
        const string LogoPath = "Assets/_Game/Art/Resources/OfficeHellArt/Branding/LogoMain.png";
        const string IconPath = "Assets/_Game/Art/Branding/AppIcon.png";
        const string SetupFlagKey = "OfficeHell.SetupDone.v1";

        [InitializeOnLoadMethod]
        static void AutoSetupOnce()
        {
            // Opening a scene during batch startup cancels a pending -executeMethod, which would
            // silently skip the headless self test.
            if (Application.isBatchMode)
            {
                return;
            }

            if (SessionState.GetBool(SetupFlagKey, false) || EditorPrefs.GetBool(SetupFlagKey, false))
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                SessionState.SetBool(SetupFlagKey, true);
                EditorPrefs.SetBool(SetupFlagKey, true);
                Apply(false);
            };
        }

        [MenuItem("Office Hell/Setup Project", false, 10)]
        public static void SetupMenu()
        {
            Apply(true);
        }

        [MenuItem("Office Hell/Open Main Scene", false, 11)]
        public static void OpenMainScene()
        {
            EnsureScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Office Hell/Reveal Config Folder", false, 30)]
        public static void RevealConfig()
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "Config");
            if (Directory.Exists(dir))
            {
                EditorUtility.RevealInFinder(dir);
            }
            else
            {
                Debug.LogError("[OfficeHell] config folder missing: " + dir);
            }
        }

        static void Apply(bool verbose)
        {
            ApplyPlayerSettings();
            EnsureScene();
            EnsureSceneInBuild();

            if (verbose)
            {
                Debug.Log("[OfficeHell] setup done. Press play in any scene, the game boots itself.");
            }
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "OfficeHell";
            PlayerSettings.productName = "Office Hell";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.colorSpace = ColorSpace.Linear;

            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null)
            {
                NamedBuildTarget buildTarget = NamedBuildTarget.Standalone;
                int[] iconSizes = PlayerSettings.GetIconSizes(buildTarget, IconKind.Application);
                Texture2D[] icons = new Texture2D[iconSizes.Length];
                for (int i = 0; i < icons.Length; i++)
                {
                    icons[i] = icon;
                }
                PlayerSettings.SetIcons(buildTarget, icons, IconKind.Application);
            }

            Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            if (logo != null)
            {
                PlayerSettings.SplashScreen.show = true;
                PlayerSettings.SplashScreen.backgroundColor = new Color(0.435f, 0.737f, 0.882f, 1f);
                PlayerSettings.SplashScreen.logos = new[]
                {
                    PlayerSettings.SplashScreenLogo.Create(2f, logo),
                };
            }
        }

        static void EnsureScene()
        {
            if (File.Exists(ScenePath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            // The whole world is built in code, so any scene works as the build entry point. Saving
            // a copy of whatever is already open keeps this callable from a build without discarding
            // the scene the user is looking at, which NewScene(Single) would do silently.
            UnityEngine.SceneManagement.Scene active = EditorSceneManager.GetActiveScene();
            if (active.IsValid())
            {
                EditorSceneManager.SaveScene(active, ScenePath, true);
            }
            else
            {
                UnityEngine.SceneManagement.Scene created = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(created, ScenePath);
            }

            AssetDatabase.Refresh();
            Debug.Log("[OfficeHell] created " + ScenePath);
        }

        static void EnsureSceneInBuild()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == ScenePath)
                {
                    return;
                }
            }

            EditorBuildSettingsScene[] next = new EditorBuildSettingsScene[scenes.Length + 1];
            System.Array.Copy(scenes, next, scenes.Length);
            next[scenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = next;
        }
    }
}
