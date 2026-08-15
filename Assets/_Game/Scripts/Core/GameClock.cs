namespace OfficeHell.Core
{
    /// <summary>
    /// Logic side clock. UnityEngine.Time.timeScale stays at 1 for the whole game, so
    /// particles, tweens and UI keep playing during hitstop / slow motion / pause.
    /// Rule: nothing under Scripts/Systems or Scripts/Model may read UnityEngine.Time.
    /// </summary>
    public static class GameClock
    {
        /// <summary>0 = frozen, 0.15 = slow motion, 1 = normal. Owned by the flow machine.</summary>
        public static float Scale = 1f;

        /// <summary>Validation only, driven from the debug panel. Never touched by gameplay code.</summary>
        public static float DebugScale = 1f;

        /// <summary>Owned by JuiceService. Drops to 0 for a hitstop without stopping the view layer.</summary>
        public static float FxScale = 1f;

        /// <summary>Scaled delta, the only delta logic systems are allowed to use.</summary>
        public static float Delta { get; private set; }

        /// <summary>Raw frame delta, used by the flow machine and view layer.</summary>
        public static float UnscaledDelta { get; private set; }

        /// <summary>Accumulated scaled time since the clock was reset.</summary>
        public static float Now { get; private set; }

        public static void Tick(float unscaledDelta)
        {
            if (unscaledDelta > 0.1f)
            {
                // Clamp editor hiccups so a single long frame cannot teleport entities.
                unscaledDelta = 0.1f;
            }

            UnscaledDelta = unscaledDelta;
            Delta = unscaledDelta * Scale * DebugScale * FxScale;
            Now += Delta;
        }

        public static void Reset()
        {
            Scale = 1f;
            FxScale = 1f;
            Delta = 0f;
            UnscaledDelta = 0f;
            Now = 0f;
        }
    }
}
