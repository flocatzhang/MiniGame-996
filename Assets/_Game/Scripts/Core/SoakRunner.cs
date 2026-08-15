using OfficeHell.Model;
using OfficeHell.Systems;
using UnityEngine;

namespace OfficeHell.Core
{
    /// <summary>
    /// Unattended play through of the first working days, enabled by a command line switch on either
    /// the player or the editor:
    ///
    ///   OfficeHell.exe -officehell-soak 120 -logFile soak.log
    ///
    /// The headless self test covers Config, Model and Systems but constructs no view, no ui and no
    /// audio. This drives the real composition root with all three layers live, so a null reference
    /// in pooling, in the ui build or in the audio pool surfaces as a failure instead of surviving
    /// until someone plays that far.
    /// </summary>
    public sealed class SoakRunner : MonoBehaviour
    {
        const string SecondsFlag = "-officehell-soak";
        const float DefaultSeconds = 120f;

        GameApp _app;
        InputProvider _input;
        float _remaining;
        int _daysClosed;
        int _cardsPicked;
        int _errors;

        /// <summary>Polled by the editor side driver, which owns process exit while in play mode.</summary>
        public bool Finished { get; private set; }

        public int ExitCode { get; private set; }

        public string Summary { get; private set; }

        public static bool WantsSoak(out float seconds)
        {
            seconds = DefaultSeconds;

            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != SecondsFlag)
                {
                    continue;
                }

                if (i + 1 < args.Length)
                {
                    float parsed;
                    if (float.TryParse(args[i + 1], out parsed) && parsed > 0f)
                    {
                        seconds = parsed;
                    }
                }

                return true;
            }

            return false;
        }

        public void Bind(GameApp app, InputProvider input, float seconds)
        {
            _app = app;
            _input = input;
            _remaining = seconds;

            // Engine input is meaningless in batch mode, so the soak drives the snapshot itself.
            if (_input != null)
            {
                _input.enabled = false;
            }

            Application.logMessageReceived += OnLog;
            Debug.Log("[Soak] started, budget " + seconds.ToString("0") + "s");
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors++;
            }
        }

        void Update()
        {
            if (Finished || _app == null || _app.Driver == null)
            {
                return;
            }

            _remaining -= Time.unscaledDeltaTime;

            GameFlowFsm flow = _app.Driver.Flow;
            switch (flow.State)
            {
                case GameState.MainMenu:
                    _app.StartRun();
                    break;

                case GameState.OffWork:
                    _daysClosed++;
                    flow.SkipOffWork();
                    break;

                // Always the first card. A random pick would make a failure impossible to reproduce
                // from the log, and the point of the soak is that a failure can be replayed.
                case GameState.LevelUp:
                    if (flow.Cards != null && flow.Cards.Offers.Count > 0)
                    {
                        flow.Cards.Pick(0);
                        _cardsPicked++;
                    }

                    flow.ResolveLevelUp();
                    break;

                case GameState.Result:
                    // Dying is a valid outcome, the restart path is worth exercising too.
                    _app.StartRun();
                    break;
            }

            DriveInput();

            if (_remaining <= 0f)
            {
                Finish();
            }
        }

        /// <summary>
        /// Chases the nearest drop, which exercises the magnet, the step pickup and the auto equip
        /// path. Standing still would leave every one of them untested.
        /// </summary>
        void DriveInput()
        {
            RunModel run = _app.Ctx.Run;
            Vector2 target = Vector2.zero;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < run.Loots.Count; i++)
            {
                if (run.Loots[i].IsDead)
                {
                    continue;
                }

                float sqr = (run.Loots[i].Pos - run.Player.Pos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    target = run.Loots[i].Pos;
                }
            }

            InputSnapshot snapshot = new InputSnapshot();
            snapshot.PointerValid = true;
            snapshot.PointerWorld = bestSqr < float.MaxValue ? target : Vector2.zero;
            _app.Driver.Input.Snapshot = snapshot;
        }

        void Finish()
        {
            RunModel run = _app.Ctx.Run;
            Summary = string.Format(
                "days closed {0}, day {1}, kills {2}, cards {3}, level {4}, weapons {5}, alive {6}, errors {7}",
                _daysClosed, run.DayIndex, run.Kills, _cardsPicked, run.Player.Level,
                run.Player.EquippedCount(), run.AliveEnemies, _errors);

            if (_errors > 0)
            {
                ExitCode = 1;
            }
            else if (_daysClosed < 1 || run.Kills < 1)
            {
                ExitCode = 2;
            }

            Finished = true;

            Debug.Log("[Soak] finished: " + Summary);
            Debug.Log(ExitCode == 0 ? "[Soak] RESULT PASSED" : "[Soak] RESULT FAILED, code " + ExitCode);

            // In the editor the play mode driver owns the exit, Application.Quit is a no op there.
            if (!Application.isEditor)
            {
                Application.Quit(ExitCode);
            }
        }
    }
}
