using System;
using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    public interface IEnemyBehavior
    {
        string Name { get; }

        void OnSpawn(EnemyModel e, GameContext ctx);

        void Tick(EnemyModel e, GameContext ctx, float dt);

        void OnDeath(EnemyModel e, GameContext ctx);

        /// <summary>Returns true when touching the player should kill this enemy.</summary>
        bool DiesOnContact { get; }

        /// <summary>
        /// Returns true when the behaviour drives its own movement. Everything else falls through to
        /// the default straight line chase, which is the only movement code most enemies need.
        /// </summary>
        bool TryMove(EnemyModel e, GameContext ctx, out Vector2 dir);
    }

    public abstract class EnemyBehaviorBase : IEnemyBehavior
    {
        public abstract string Name { get; }

        public virtual bool DiesOnContact
        {
            get { return false; }
        }

        public virtual void OnSpawn(EnemyModel e, GameContext ctx)
        {
        }

        public virtual void Tick(EnemyModel e, GameContext ctx, float dt)
        {
        }

        public virtual void OnDeath(EnemyModel e, GameContext ctx)
        {
        }

        public virtual bool TryMove(EnemyModel e, GameContext ctx, out Vector2 dir)
        {
            dir = Vector2.zero;
            return false;
        }
    }

    /// <summary>Deadline: fast charger that spends itself on impact.</summary>
    public sealed class SuicideOnContactBehavior : EnemyBehaviorBase
    {
        public override string Name
        {
            get { return "SuicideOnContact"; }
        }

        public override bool DiesOnContact
        {
            get { return true; }
        }
    }

    /// <summary>
    /// BUG: fixing one produces two. The only enemy where killing faster puts more on screen, which
    /// is a completely different kind of pressure from everything else in the game.
    /// </summary>
    public sealed class SplitOnDeathBehavior : EnemyBehaviorBase
    {
        public override string Name
        {
            get { return "SplitOnDeath"; }
        }

        public override void OnDeath(EnemyModel e, GameContext ctx)
        {
            if (ctx.Spawner == null || e.Def == null)
            {
                return;
            }

            string childId = e.Def.Param.GetString("splitInto", null);
            if (string.IsNullOrEmpty(childId))
            {
                return;
            }

            EnemyDef child = ctx.Cfg.Enemy(childId);
            if (child == null)
            {
                return;
            }

            int count = Mathf.Max(1, (int)e.Def.Param.GetFloat("splitCount", 2f));
            float spread = e.Def.Param.GetFloat("splitSpread", 1.15f);
            float arc = e.Def.Param.GetFloat("splitArc", 110f) * Mathf.Deg2Rad;
            float invuln = e.Def.Param.GetFloat("splitInvuln", 0.2f);

            // Thrown away from the player rather than spaced evenly around the corpse. Even spacing
            // put half of every brood on the near side, which is inside the orbiting badges: they
            // were deleted on the frame they appeared and the split read as not having happened.
            Vector2 away = e.Pos - ctx.Run.Player.Pos;
            float baseAngle = away.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(away.y, away.x)
                : Rng.Range(0f, Mathf.PI * 2f);

            float now = GameClock.Now;

            for (int i = 0; i < count; i++)
            {
                // Fanned across the arc rather than placed at a shared angle, so two children never
                // overlap into what looks like one body.
                float offset = count > 1 ? (float)i / (count - 1) - 0.5f : 0f;
                float angle = baseAngle + offset * arc + Rng.Range(-0.12f, 0.12f);
                float dist = spread * Rng.Range(0.8f, 1.2f);

                Vector2 pos = e.Pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
                EnemyModel spawned = ctx.Spawner.Spawn(child, pos, null);

                // Distance alone is not enough at six slots: the badges sweep, the keyboard lands in
                // an area and the stapler retargets within the frame. A fifth of a second is too short
                // to defend anything and just long enough for the player to register that two things
                // came out of the one they killed, which is the entire point of this enemy.
                if (invuln > 0f)
                {
                    spawned.InvulnUntil = now + invuln;
                }
            }
        }
    }

    /// <summary>
    /// PPT and the veteran share this one component, differing only in which channel they write.
    /// A move slow and an attack slow are the same twenty lines, and the channel rule below is what
    /// keeps five of them from stacking into a full stop.
    /// </summary>
    public sealed class AuraDebuffBehavior : EnemyBehaviorBase
    {
        readonly string _name;
        readonly AuraChannel _channel;
        readonly float _defaultPct;

        public AuraDebuffBehavior(string name, AuraChannel channel, float defaultPct)
        {
            _name = name;
            _channel = channel;
            _defaultPct = defaultPct;
        }

        public override string Name
        {
            get { return _name; }
        }

        public override void OnSpawn(EnemyModel e, GameContext ctx)
        {
            e.AuraKind = _channel;
            e.AuraRadius = e.Def.Param.GetFloat("radius", 3f);
        }

        public override void Tick(EnemyModel e, GameContext ctx, float dt)
        {
            float radius = e.AuraRadius > 0f ? e.AuraRadius : e.Def.Param.GetFloat("radius", 3f);
            float pct = e.Def.Param.GetFloat("pct", _defaultPct);

            PlayerModel p = ctx.Run.Player;
            if ((p.Pos - e.Pos).sqrMagnitude <= radius * radius)
            {
                p.ApplyAura(_channel, pct);
            }
        }
    }

    /// <summary>Leader: speeds up every trash mob around it. Also capped to the strongest source.</summary>
    public sealed class AuraHasteBehavior : EnemyBehaviorBase
    {
        readonly List<int> _scratch = new List<int>(64);

        public override string Name
        {
            get { return "AuraHaste"; }
        }

        public override void OnSpawn(EnemyModel e, GameContext ctx)
        {
            e.AuraKind = AuraChannel.EnemyHaste;
            e.AuraRadius = e.Def.Param.GetFloat("radius", 4f);
        }

        public override void Tick(EnemyModel e, GameContext ctx, float dt)
        {
            float radius = e.AuraRadius > 0f ? e.AuraRadius : 4f;
            float speedPct = e.Def.Param.GetFloat("pct", 30f);

            ctx.Grid.QueryCircle(e.Pos, radius, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                int idx = _scratch[i];
                if (idx < 0 || idx >= ctx.Run.Enemies.Count)
                {
                    continue;
                }

                EnemyModel other = ctx.Run.Enemies[idx];
                if (other.IsDead || other.Id == e.Id || other.Def == null || other.Def.Tier != EnemyTier.Normal)
                {
                    continue;
                }

                other.SpeedMul = Mathf.Max(other.SpeedMul, 1f + speedPct * 0.01f);
            }
        }
    }

    /// <summary>
    /// Weekly report: the only ranged enemy, and the only reason walking in circles is not a winning
    /// strategy. It backs off when the player closes in, so it punishes standing still rather than
    /// punishing movement. Its projectile is barely faster than the player on purpose: dodgeable.
    /// </summary>
    public sealed class RangedKeepDistanceBehavior : EnemyBehaviorBase
    {
        public override string Name
        {
            get { return "RangedKeepDistance"; }
        }

        public override void OnSpawn(EnemyModel e, GameContext ctx)
        {
            e.NextActionAt = GameClock.Now + e.Def.Param.GetFloat("interval", 2.5f) * Rng.Range(0.3f, 1f);
        }

        public override bool TryMove(EnemyModel e, GameContext ctx, out Vector2 dir)
        {
            float keep = e.Def.Param.GetFloat("keepDistance", 7f);
            Vector2 toPlayer = ctx.Run.Player.Pos - e.Pos;
            float dist = toPlayer.magnitude;

            if (dist < 0.01f)
            {
                dir = Vector2.zero;
                return true;
            }

            Vector2 unit = toPlayer / dist;

            if (dist < keep * 0.85f)
            {
                dir = -unit;
            }
            else if (dist > keep * 1.15f)
            {
                dir = unit;
            }
            else
            {
                // Inside the comfortable band it strafes, so it never becomes a stationary turret.
                dir = new Vector2(-unit.y, unit.x) * 0.4f;
            }

            return true;
        }

        public override void Tick(EnemyModel e, GameContext ctx, float dt)
        {
            float now = GameClock.Now;
            if (now < e.NextActionAt)
            {
                return;
            }

            float interval = e.Def.Param.GetFloat("interval", 2.5f);
            float projSpeed = e.Def.Param.GetFloat("projSpeed", 5f);
            float damage = e.Def.Param.GetFloat("damage", 8f) * ctx.Run.DmgScale;
            float range = e.Def.Param.GetFloat("range", 14f);

            e.NextActionAt = now + interval;

            Vector2 dir = (ctx.Run.Player.Pos - e.Pos).normalized;
            ProjectileModel p = ProjectileFactory.Spawn(ctx, e.Pos, dir * projSpeed, damage, range, "v_eproj", true);
            p.Radius = 0.22f;
        }
    }

    /// <summary>
    /// Boss. Three bars, three phases. Phase two only changes numbers, phase three adds one skill,
    /// so the whole escalation is two branches rather than three separate fights.
    /// </summary>
    public sealed class BossSkillsBehavior : EnemyBehaviorBase
    {
        const float TelegraphSeconds = 0.9f;

        public override string Name
        {
            get { return "BossSkills"; }
        }

        public override void OnSpawn(EnemyModel e, GameContext ctx)
        {
            float now = GameClock.Now;
            e.PieReadyAt = now + Cd(e, "pieCd", 15f) * 0.6f;
            e.MeetingReadyAt = now + Cd(e, "meetingCd", 20f) * 0.45f;
            e.KpiReadyAt = now + Cd(e, "kpiCd", 8f) * 0.5f;
            e.RainReadyAt = now + 3f;

            EvtArg a = new EvtArg();
            a.I0 = e.Id;
            a.I1 = e.BarsTotal;
            a.P0 = e.Pos;
            a.O0 = e;
            ctx.Bus.Dispatch(EventID.BossSpawned, a);
        }

        public override void Tick(EnemyModel e, GameContext ctx, float dt)
        {
            float now = GameClock.Now;
            if (now < e.InvulnUntil)
            {
                return;
            }

            if (now >= e.KpiReadyAt)
            {
                e.KpiReadyAt = now + Cd(e, "kpiCd", 8f) * PhaseCdScale(e);
                Telegraph(e, ctx, 1);
                CastKpi(e, ctx);
            }

            if (now >= e.MeetingReadyAt)
            {
                e.MeetingReadyAt = now + Cd(e, "meetingCd", 20f) * PhaseCdScale(e);
                Telegraph(e, ctx, 2);
                CastMeeting(e, ctx);
            }

            if (now >= e.PieReadyAt)
            {
                e.PieReadyAt = now + Cd(e, "pieCd", 15f) * PhaseCdScale(e);
                Telegraph(e, ctx, 3);
                CastPie(e, ctx);
            }

            if (e.Phase >= 3 && now >= e.RainReadyAt)
            {
                e.RainReadyAt = now + e.Def.Param.GetFloat("rainInterval", 1.5f);
                CastRain(e, ctx);
            }
        }

        static float Cd(EnemyModel e, string key, float fallback)
        {
            return e.Def.Param.GetFloat(key, fallback);
        }

        /// <summary>Phase two shortens every cooldown by 30 percent. Two numbers, whole escalation.</summary>
        static float PhaseCdScale(EnemyModel e)
        {
            return e.Phase >= 2 ? 0.7f : 1f;
        }

        void Telegraph(EnemyModel e, GameContext ctx, int skillId)
        {
            e.TelegraphUntil = GameClock.Now + TelegraphSeconds;

            EvtArg a = new EvtArg();
            a.I0 = e.Id;
            a.I1 = skillId;
            a.P0 = e.Pos;
            ctx.Bus.Dispatch(EventID.BossTelegraph, a);
        }

        /// <summary>
        /// Marks the floor around the player and bills those circles a moment later. Three, five from
        /// phase two.
        ///
        /// This used to be thrown folders, and thrown folders could not hit anyone. They were aimed at
        /// where the player stood when the boss let go, flew at 8 against the player's 4.5, and only
        /// billed whoever was inside 2.2 units of the landing point when they got there. Across a
        /// typical six units that is three quarters of a second of flight, by which time the player had
        /// walked three and a half units: any direction was a dodge, including straight at the boss,
        /// and the fight had no ranged threat at all.
        ///
        /// Marked ground answers it because the mark follows the player instead of a stale coordinate,
        /// and the dodge stops being "keep walking" and becomes "read the gaps", which is the same
        /// sentence phase three's rain already teaches. It is still fully dodgeable, and deliberately
        /// so: the circles cover part of a disc the player can leave in about the warning time, so the
        /// short answer is to step into a gap rather than to outrun the whole pattern.
        /// </summary>
        void CastKpi(EnemyModel e, GameContext ctx)
        {
            int count = e.Phase >= 2
                ? (int)e.Def.Param.GetFloat("kpiCountLate", 5f)
                : (int)e.Def.Param.GetFloat("kpiCount", 3f);

            float warn = e.Def.Param.GetFloat("kpiWarn", 0.8f);
            float radius = e.Def.Param.GetFloat("kpiBlast", 1.8f);
            float scatter = e.Def.Param.GetFloat("kpiScatter", 2.2f);
            float now = GameClock.Now;

            ArenaDef arena = ctx.Cfg.Arena;
            Vector2 center = ctx.Run.Player.Pos;

            for (int i = 0; i < count; i++)
            {
                TelegraphModel t = ctx.Run.RentTelegraph();
                t.Pos = arena.Clamp(center + Rng.RingPoint(Vector2.zero, 0f, scatter), 1f);
                t.Radius = radius;
                t.BornAt = now;
                t.FireAt = now + warn;
                t.Damage = e.ContactDamage * 0.9f;
                t.ViewId = "v_warn_kpi";
            }
        }

        /// <summary>
        /// Surrounds the player. This is the one deliberate breach of the spawn band, so it goes
        /// through a one second ground warning: a summon on top of the player with no tell is the
        /// single most common source of "this game is cheating".
        /// </summary>
        void CastMeeting(EnemyModel e, GameContext ctx)
        {
            string summonId = e.Def.Param.GetString("summonId", "ppt");
            if (ctx.Cfg.Enemy(summonId) == null)
            {
                return;
            }

            Vector2 center = ctx.Run.Player.Pos;
            int count = Mathf.Max(1, (int)e.Def.Param.GetFloat("summonCount", 4f));
            float radius = e.Def.Param.GetFloat("summonRadius", 3f);

            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.PI * 2f / count * i + Rng.Range(-0.2f, 0.2f);
                TelegraphModel t = ctx.Run.RentTelegraph();
                t.Pos = ctx.Cfg.Arena.Clamp(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, 1f);
                t.Radius = 0.9f;
                t.BornAt = GameClock.Now;
                t.FireAt = GameClock.Now + e.Def.Param.GetFloat("summonWarn", 1f);
                t.SummonEnemyId = summonId;
                t.SummonCount = 1;
                t.ViewId = "v_warn_summon";
            }
        }

        /// <summary>Global slow, the "let me paint you a picture" moment. Deeper in phase two.</summary>
        void CastPie(EnemyModel e, GameContext ctx)
        {
            PlayerModel p = ctx.Run.Player;
            float slowPct = e.Phase >= 2
                ? e.Def.Param.GetFloat("pieSlowPctLate", 40f)
                : e.Def.Param.GetFloat("pieSlowPct", 30f);
            float duration = e.Def.Param.GetFloat("pieDuration", 3f);

            p.GlobalSlowPct = slowPct;
            p.GlobalSlowUntil = GameClock.Now + duration;

            EvtArg a = new EvtArg();
            a.P0 = p.Pos;
            a.F0 = duration;
            a.F1 = slowPct;
            ctx.Bus.Dispatch(EventID.BossPieCast, a);
        }

        /// <summary>
        /// Phase three only. Random landing spots on a short loop, which is what turns the last bar
        /// from a damage race into a dodging test.
        ///
        /// Scattered around the player and clamped to the arena, not spread across the arena. The field
        /// is nearly twice the width of the frame, so arena wide placement would put most of the rain
        /// off camera: the player would take the hits they can see and never learn the pattern.
        /// </summary>
        void CastRain(EnemyModel e, GameContext ctx)
        {
            ArenaDef arena = ctx.Cfg.Arena;
            CameraDef cam = ctx.Cfg.Camera;
            int count = Mathf.Max(1, (int)e.Def.Param.GetFloat("rainCount", 4f));
            float warn = e.Def.Param.GetFloat("rainWarn", 0.7f);
            float radius = e.Def.Param.GetFloat("rainBlast", 1.5f);

            float spreadX = cam.OrthographicSize * cam.Aspect * 0.85f;
            float spreadY = cam.OrthographicSize * 0.85f;
            Vector2 center = ctx.Run.Player.Pos;

            for (int i = 0; i < count; i++)
            {
                Vector2 pos = center + new Vector2(
                    Rng.Range(-spreadX, spreadX),
                    Rng.Range(-spreadY, spreadY));

                TelegraphModel t = ctx.Run.RentTelegraph();
                t.Pos = arena.Clamp(pos, 1f);
                t.Radius = radius;
                t.BornAt = GameClock.Now;
                t.FireAt = GameClock.Now + warn;
                t.Damage = e.ContactDamage * 0.7f;
                t.ViewId = "v_warn_kpi";
            }
        }
    }

    public static class EnemyBehaviorRegistry
    {
        static readonly Dictionary<string, IEnemyBehavior> Map =
            new Dictionary<string, IEnemyBehavior>(8, StringComparer.OrdinalIgnoreCase);

        static EnemyBehaviorRegistry()
        {
            Add(new SuicideOnContactBehavior());
            Add(new SplitOnDeathBehavior());
            Add(new AuraDebuffBehavior("AuraMoveSlow", AuraChannel.MoveSlow, 25f));
            Add(new AuraDebuffBehavior("AuraAttackSlow", AuraChannel.AttackSlow, 25f));
            Add(new AuraHasteBehavior());
            Add(new RangedKeepDistanceBehavior());
            Add(new BossSkillsBehavior());
        }

        static void Add(IEnemyBehavior b)
        {
            Map[b.Name] = b;
        }

        public static bool Exists(string name)
        {
            return !string.IsNullOrEmpty(name) && Map.ContainsKey(name);
        }

        public static IEnemyBehavior Get(string name)
        {
            IEnemyBehavior b;
            if (!string.IsNullOrEmpty(name) && Map.TryGetValue(name, out b))
            {
                return b;
            }

            return null;
        }
    }
}
