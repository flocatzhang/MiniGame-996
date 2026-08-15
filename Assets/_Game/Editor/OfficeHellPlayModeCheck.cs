using OfficeHell.Core;
using UnityEditor;
using UnityEngine;

namespace OfficeHell.EditorTools
{
    /// <summary>
    /// Drives a real play mode session from the command line and exits with the soak result.
    ///
    ///   Unity.exe -batchmode -nographics -projectPath &lt;dir&gt; -officehell-soak 60
    ///             -executeMethod OfficeHell.EditorTools.OfficeHellPlayModeCheck.RunBatch
    ///
    /// This is the counterpart to the headless self test: that one proves the logic, this one proves
    /// the composition root, the view binder, the ui build and the audio pool survive a full run.
    /// Entering play mode reloads the domain, so the continuation has to come back through
    /// InitializeOnLoadMethod with the intent parked in SessionState.
    /// </summary>
    public static class OfficeHellPlayModeCheck
    {
        const string ArmedKey = "OfficeHell.PlayModeCheck.Armed";
        const float StartupTimeout = 30f;
        const float TailTimeout = 60f;

        static double _deadline;
        static bool _sawPlaying;

        [MenuItem("Office Hell/Run Play Mode Check", false, 22)]
        public static void RunMenu()
        {
            Arm();
            EditorApplication.EnterPlaymode();
        }

        public static void RunBatch()
        {
            float seconds;
            if (!SoakRunner.WantsSoak(out seconds))
            {
                Debug.LogError("[PlayModeCheck] pass -officehell-soak <seconds> so the run has a budget");
                EditorApplication.Exit(1);
                return;
            }

            Arm();
            EditorApplication.EnterPlaymode();
        }

        static void Arm()
        {
            SessionState.SetBool(ArmedKey, true);
        }

        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (!SessionState.GetBool(ArmedKey, false))
            {
                return;
            }

            _deadline = EditorApplication.timeSinceStartup + StartupTimeout;
            _sawPlaying = false;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.isPlaying)
            {
                if (!_sawPlaying)
                {
                    _sawPlaying = true;

                    float seconds;
                    SoakRunner.WantsSoak(out seconds);
                    _deadline = EditorApplication.timeSinceStartup + seconds + TailTimeout;
                }

                GameApp app = GameApp.Instance;
                if (app != null && app.Soak != null && app.Soak.Finished)
                {
                    Stop(app.Soak.ExitCode, "soak reported " + app.Soak.Summary);
                    return;
                }
            }
            else if (_sawPlaying)
            {
                Stop(3, "play mode ended before the soak reported a result");
                return;
            }

            if (EditorApplication.timeSinceStartup > _deadline)
            {
                Stop(4, _sawPlaying ? "soak overran its budget" : "play mode never started");
            }
        }

        static void Stop(int code, string reason)
        {
            EditorApplication.update -= Poll;
            SessionState.SetBool(ArmedKey, false);

            if (code == 0)
            {
                Debug.Log("[PlayModeCheck] PASSED: " + reason);
            }
            else
            {
                Debug.LogError("[PlayModeCheck] FAILED code " + code + ": " + reason);
            }

            EditorApplication.Exit(code);
        }
    }
}
