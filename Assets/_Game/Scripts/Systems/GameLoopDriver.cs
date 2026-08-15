using OfficeHell.Core;

namespace OfficeHell.Systems
{
    /// <summary>
    /// The only place gameplay advances. Fixed system order, single gate, no per entity Update.
    /// GameClock.Tick itself is called by GameApp so nothing under Systems touches UnityEngine.Time.
    /// </summary>
    public sealed class GameLoopDriver
    {
        readonly GameContext _ctx;

        public readonly GameFlowFsm Flow;
        public readonly InputSystem Input;
        public readonly MovementSystem Movement;
        public readonly CameraSystem Camera;
        public readonly SpawnSystem Spawn;
        public readonly EnemyAiSystem EnemyAi;
        public readonly WeaponSystem Weapons;
        public readonly SlamSystem Slams;
        public readonly OrbitSystem Orbits;
        public readonly ProjectileSystem Projectiles;
        public readonly TelegraphSystem Telegraphs;
        public readonly CombatSystem Combat;
        public readonly LootSystem Loot;
        public readonly ProgressionSystem Progression;
        public readonly CardSystem Cards;
        public readonly SkillSystem Skill;
        public readonly ArmorSystem Armor;

        public GameLoopDriver(GameContext ctx)
        {
            _ctx = ctx;

            Flow = new GameFlowFsm(ctx);
            Input = new InputSystem(ctx);
            Movement = new MovementSystem(ctx);
            Camera = new CameraSystem(ctx);
            Spawn = new SpawnSystem(ctx);
            EnemyAi = new EnemyAiSystem(ctx);
            Weapons = new WeaponSystem(ctx);
            Slams = new SlamSystem(ctx);
            Orbits = new OrbitSystem(ctx);
            Projectiles = new ProjectileSystem(ctx);
            Telegraphs = new TelegraphSystem(ctx);
            Combat = new CombatSystem(ctx);
            Loot = new LootSystem(ctx);
            Progression = new ProgressionSystem(ctx);
            Cards = new CardSystem(ctx, Loot);
            Skill = new SkillSystem(ctx);
            Armor = new ArmorSystem(ctx);

            Flow.Cards = Cards;
            ctx.Spawner = Spawn;
            ctx.Bus.Register(EventID.DayStarted, OnDayStarted);
            ctx.Bus.Register(EventID.RunStarted, OnRunStarted);
        }

        public void Dispose()
        {
            _ctx.Bus.Unregister(EventID.DayStarted, OnDayStarted);
            _ctx.Bus.Unregister(EventID.RunStarted, OnRunStarted);
            Loot.Dispose();
            Progression.Dispose();
        }

        void OnRunStarted(EvtArg arg)
        {
            Input.Reset();
            Orbits.Reset();
        }

        void OnDayStarted(EvtArg arg)
        {
            Spawn.OnDayBegin();
            Weapons.ArmPhases();
        }

        public void Tick()
        {
            // The flow machine runs on unscaled time so a frozen state can still time out.
            Flow.Tick(GameClock.UnscaledDelta);
            Camera.Tick(GameClock.UnscaledDelta);

            float dt = GameClock.Delta;
            if (dt <= 0f)
            {
                return;
            }

            Input.Tick(dt);
            Movement.Tick(dt);
            Spawn.Tick(dt);
            EnemyAi.Tick(dt);
            Weapons.Tick(dt);
            Slams.Tick(dt);
            Orbits.Tick(dt);
            Projectiles.Tick(dt);
            Telegraphs.Tick(dt);
            Combat.Tick(dt);
            Loot.Tick(dt);
            Progression.Tick(dt);
            Skill.Tick(dt);
            Armor.Tick(dt);

            RebuildGrid();
        }

        /// <summary>
        /// Removal happens only here, at the tail. That is what lets a query hand out raw list
        /// indices for the rest of the frame without them going stale.
        /// </summary>
        void RebuildGrid()
        {
            _ctx.Run.Compact();
            _ctx.Grid.Clear();

            for (int i = 0; i < _ctx.Run.Enemies.Count; i++)
            {
                _ctx.Grid.Insert(i, _ctx.Run.Enemies[i].Pos);
            }
        }

        public void ForceRebuildGrid()
        {
            RebuildGrid();
        }
    }
}
