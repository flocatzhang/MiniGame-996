using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;

namespace OfficeHell.Systems
{
    public enum GameState
    {
        MainMenu = 0,
        DayStart = 1,
        Battle = 2,
        LevelUp = 3,
        OffWork = 4,
        Result = 5,
    }

    /// <summary>
    /// Owns state transitions and the single GameClock.Scale gate.
    /// Ticks on unscaled delta so it keeps running while the logic clock is frozen.
    /// </summary>
    public sealed class GameFlowFsm
    {
        public const float DayIntroSeconds = 1.1f;

        /// <summary>
        /// How long the last bar breaking is left on screen before the result page takes over.
        /// The kill is the payoff of the whole run and it used to be followed by however much of
        /// Saturday was still on the clock, so the player fought an empty office for up to a minute
        /// after already having won.
        /// </summary>
        public const float VictoryBeatSeconds = 2f;

        readonly GameContext _ctx;

        /// <summary>Counts up once the boss is down. Negative while he is still standing.</summary>
        float _victorySeconds = -1f;

        public GameState State { get; private set; }
        public float StateSeconds { get; private set; }

        /// <summary>Set when the off work overlay may be dismissed early with a click.</summary>
        public bool CanSkipOffWork
        {
            get { return State == GameState.OffWork && StateSeconds > 0.4f; }
        }

        public CardSystem Cards;

        public GameFlowFsm(GameContext ctx)
        {
            _ctx = ctx;
            State = GameState.MainMenu;
        }

        public void GoMainMenu()
        {
            Enter(GameState.MainMenu);
        }

        /// <summary>Restart path. No scene reload, the run model and the view pools are recycled instead.</summary>
        public void StartRun()
        {
            _ctx.Run.ResetRun(_ctx.Cfg);
            GameClock.Reset();
            _victorySeconds = -1f;

            if (Cards != null)
            {
                Cards.Reset();
            }

            _ctx.Bus.Dispatch(EventID.RunStarted);
            BeginDay(1);
        }

        public void SkipOffWork()
        {
            if (State == GameState.OffWork)
            {
                BeginDay(_ctx.Run.DayIndex + 1);
            }
        }

        /// <summary>Called by the card panel once a choice has been made.</summary>
        public void ResolveLevelUp()
        {
            if (State != GameState.LevelUp)
            {
                return;
            }

            if (_ctx.Run.Player.PendingLevelUps > 0 && Cards != null)
            {
                Cards.Offer();
                StateSeconds = 0f;
                return;
            }

            Enter(GameState.Battle);
        }

        void BeginDay(int index)
        {
            _ctx.Run.BeginDay(index, _ctx.Cfg);
            Enter(GameState.DayStart);

            EvtArg a = new EvtArg();
            a.I0 = index;
            a.O0 = _ctx.Run.Day;
            _ctx.Bus.Dispatch(EventID.DayStarted, a);
        }

        /// <summary>Debug only. Ends the current day immediately through the normal path.</summary>
        public void DebugSkipDay()
        {
            if (State == GameState.Battle && _ctx.Run.Day != null)
            {
                _ctx.Run.DayElapsed = _ctx.Run.Day.Duration;
            }
        }

        /// <summary>Debug only. Jumps straight to the requested day.</summary>
        public void DebugJumpToDay(int index)
        {
            if (index < 1)
            {
                index = 1;
            }

            ClearField();
            BeginDay(index);
        }

        public void Tick(float unscaledDt)
        {
            StateSeconds += unscaledDt;

            switch (State)
            {
                case GameState.DayStart:
                    if (StateSeconds >= DayIntroSeconds)
                    {
                        Enter(GameState.Battle);
                    }

                    break;

                case GameState.Battle:
                    TickBattleClock();
                    break;

                case GameState.OffWork:
                    float span = _ctx.Run.Day != null ? _ctx.Run.Day.OffWorkSeconds : 3f;
                    if (StateSeconds >= span)
                    {
                        BeginDay(_ctx.Run.DayIndex + 1);
                    }

                    break;
            }
        }

        void TickBattleClock()
        {
            RunModel run = _ctx.Run;
            float dt = GameClock.Delta;

            run.DayElapsed += dt;
            run.CombatSeconds += dt;
            run.SecondsSinceLastLegendary += dt;

            // Checked ahead of the death test on purpose. Once the last bar is gone the run is a
            // Clear, and a stray Deadline landing during the celebration must not be able to convert
            // it into a Fail. Counted on the logic clock so a level up landing in the same frame
            // pauses the beat rather than eating it.
            if (run.BossDefeated && run.DayIndex >= _ctx.Cfg.DayCount)
            {
                if (_victorySeconds < 0f)
                {
                    _victorySeconds = dt;

                    // The adds the last bar break summoned are still walking. Letting them chip the
                    // player through the celebration is damage taken in a fight that is already over.
                    run.Player.InvulnUntil = GameClock.Now + VictoryBeatSeconds;
                }
                else
                {
                    _victorySeconds += dt;
                }

                if (_victorySeconds >= VictoryBeatSeconds)
                {
                    EndDay();
                }

                return;
            }

            if (!run.Player.Alive)
            {
                Finish(Ending.Fail);
                return;
            }

            // The pause happens between frames of combat, so a level up never eats an input.
            if (run.Player.PendingLevelUps > 0 && Cards != null)
            {
                Enter(GameState.LevelUp);
                Cards.Offer();
                return;
            }

            if (run.Day != null && run.DayElapsed >= run.Day.Duration)
            {
                EndDay();
            }
        }

        void EndDay()
        {
            RunModel run = _ctx.Run;
            bool lastDay = run.DayIndex >= _ctx.Cfg.DayCount;
            EnemyModel boss = run.Boss;

            // The boss looks at his watch and goes home. This exists so a player who ran out of time
            // with one bar left gets the full ending instead of being failed on a technicality.
            if (lastDay && boss != null && !run.BossDefeated)
            {
                EvtArg b = new EvtArg();
                b.I0 = boss.Id;
                b.P0 = boss.Pos;
                _ctx.Bus.Dispatch(EventID.BossClockedOut, b);
            }

            ClearField();

            EvtArg a = new EvtArg();
            a.I0 = run.DayIndex;
            a.I1 = run.KilledToday;
            _ctx.Bus.Dispatch(EventID.DayCleared, a);

            if (lastDay)
            {
                Finish(run.BossDefeated ? Ending.Clear : Ending.ClearTimeout);
                return;
            }

            Enter(GameState.OffWork);
        }

        /// <summary>
        /// Anyone still on the floor at 21:00 goes home. Silent kill, so the loot system, the exp
        /// system and the kill counter never see it.
        /// </summary>
        void ClearField()
        {
            RunModel run = _ctx.Run;

            for (int i = 0; i < run.Enemies.Count; i++)
            {
                run.Enemies[i].IsDead = true;
            }

            for (int i = 0; i < run.Projectiles.Count; i++)
            {
                run.Projectiles[i].IsDead = true;
            }

            for (int i = 0; i < run.Slams.Count; i++)
            {
                run.Slams[i].IsDead = true;
            }

            for (int i = 0; i < run.Telegraphs.Count; i++)
            {
                run.Telegraphs[i].IsDead = true;
            }
        }

        void Finish(Ending ending)
        {
            _ctx.Run.Ending = ending;
            Enter(GameState.Result);

            EvtArg a = new EvtArg();
            a.I0 = (int)ending;
            _ctx.Bus.Dispatch(EventID.RunEnded, a);
        }

        void Enter(GameState next)
        {
            State = next;
            StateSeconds = 0f;

            // Only Battle advances the logic clock. Everything else freezes gameplay while the
            // view layer keeps animating, because Time.timeScale is never touched.
            GameClock.Scale = next == GameState.Battle ? 1f : 0f;

            EvtArg a = new EvtArg();
            a.I0 = (int)next;
            _ctx.Bus.Dispatch(EventID.GameStateChanged, a);
        }
    }
}
