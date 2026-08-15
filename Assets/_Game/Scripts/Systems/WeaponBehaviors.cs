using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    public interface IWeaponBehavior
    {
        WeaponKind Kind { get; }

        /// <summary>Returns false when the weapon could not act, so the cooldown can be retried soon.</summary>
        bool Fire(WeaponRuntime rt, GameContext ctx);
    }

    /// <summary>
    /// Stapler. Every quality difference is a parameter here: needle count, pierce, range, speed and
    /// the orange pin. Multiple needles fly parallel rather than in a fan, because a spread would
    /// make the higher tiers less accurate than white, which is the opposite of what an upgrade means.
    /// </summary>
    public sealed class ProjectileLauncherBehavior : IWeaponBehavior
    {
        readonly List<int> _scratch = new List<int>(64);

        public WeaponKind Kind
        {
            get { return WeaponKind.ProjectileLauncher; }
        }

        public bool Fire(WeaponRuntime rt, GameContext ctx)
        {
            WeaponDef def = rt.Def;
            WeaponTierDef tier = rt.Tier;
            PlayerModel p = ctx.Run.Player;
            Vector2 muzzle = WeaponSlotOffsets.Muzzle(p.Pos, rt.Slot);

            int target = ctx.ClosestEnemy(muzzle, tier.Range, _scratch);
            if (target < 0)
            {
                return false;
            }

            Vector2 aim = (ctx.Run.Enemies[target].Pos - muzzle).normalized;
            if (aim.sqrMagnitude < 0.0001f)
            {
                aim = Vector2.right;
            }

            Vector2 side = new Vector2(-aim.y, aim.x);

            float qualityCoef = ctx.Cfg.WeaponQuality.Get(rt.Quality);
            float damage = CombatFormula.WeaponDamage(def, qualityCoef, p.Stats.Get(StatType.Atk));

            int count = Mathf.Max(1, tier.ProjCount);
            float spread = tier.ProjSpacing * (count - 1);

            for (int i = 0; i < count; i++)
            {
                float lateral = count > 1 ? -spread * 0.5f + tier.ProjSpacing * i : 0f;
                Vector2 origin = muzzle + side * lateral;

                ProjectileModel proj = ProjectileFactory.Spawn(
                    ctx, origin, aim * tier.ProjSpeed, damage, tier.Range, def.ViewId, false);

                proj.PierceLeft = tier.Pierce;
                proj.PinSeconds = tier.PinSeconds;
                proj.Knockback = tier.Knockback;
            }

            EvtArg a = new EvtArg();
            a.I0 = rt.Slot;
            a.I1 = (int)rt.Quality;
            a.P0 = muzzle;
            a.O0 = def;
            ctx.Bus.Dispatch(EventID.WeaponFired, a);
            return true;
        }
    }

    /// <summary>
    /// Keyboard. Locks the nearest enemy, remembers the coordinate and drops on it after a wind up.
    /// It commits to the position rather than the target on purpose: a fast enemy walking out from
    /// under the keyboard is feedback the player can read, not a miss to be corrected.
    /// </summary>
    public sealed class GroundAoeBehavior : IWeaponBehavior
    {
        readonly List<int> _scratch = new List<int>(64);

        public WeaponKind Kind
        {
            get { return WeaponKind.GroundAoe; }
        }

        public bool Fire(WeaponRuntime rt, GameContext ctx)
        {
            WeaponDef def = rt.Def;
            WeaponTierDef tier = rt.Tier;
            PlayerModel p = ctx.Run.Player;
            float now = GameClock.Now;

            bool selectAll = tier.SelectAllEvery > 0 &&
                             (rt.AttackCount + 1) % tier.SelectAllEvery == 0;

            Vector2 spot;
            if (selectAll)
            {
                spot = p.Pos;
            }
            else
            {
                int target = ctx.ClosestEnemy(p.Pos, tier.LockRange, _scratch);
                if (target < 0)
                {
                    return false;
                }

                spot = ctx.Run.Enemies[target].Pos;
            }

            rt.AttackCount++;

            float qualityCoef = ctx.Cfg.WeaponQuality.Get(rt.Quality);
            float damage = CombatFormula.WeaponDamage(def, qualityCoef, p.Stats.Get(StatType.Atk));
            Vector2 from = WeaponSlotOffsets.Muzzle(p.Pos, rt.Slot);

            Queue(ctx, rt, spot, from, damage, tier, now + def.WindupSeconds, selectAll);

            // Yellow and above hit twice. The follow up is a second strike on the same coordinate at
            // reduced damage, which keeps the code path identical instead of adding a combo state.
            for (int i = 1; i < tier.Slams; i++)
            {
                float follow = now + def.WindupSeconds + 0.18f * i;
                float pct = Mathf.Clamp(tier.SecondSlamPct, 0f, 100f) * 0.01f;
                Queue(ctx, rt, spot, from, damage * pct, tier, follow, false);
            }

            EvtArg a = new EvtArg();
            a.I0 = rt.Slot;
            a.I1 = (int)rt.Quality;
            a.P0 = spot;
            a.P1 = from;
            a.O0 = def;
            ctx.Bus.Dispatch(EventID.WeaponFired, a);
            return true;
        }

        static void Queue(
            GameContext ctx,
            WeaponRuntime rt,
            Vector2 spot,
            Vector2 from,
            float damage,
            WeaponTierDef tier,
            float landAt,
            bool selectAll)
        {
            SlamModel s = ctx.Run.RentSlam();
            s.Slot = rt.Slot;
            s.Target = spot;
            s.From = from;
            s.BornAt = GameClock.Now;
            s.LandAt = landAt;
            s.Radius = tier.BlastRadius;
            s.Damage = damage;
            s.Knockback = tier.Knockback;
            s.SlowPct = tier.SlowPct;
            s.SelectAll = selectAll;
        }
    }

    public static class WeaponBehaviors
    {
        static readonly IWeaponBehavior[] All =
        {
            new ProjectileLauncherBehavior(),
            new GroundAoeBehavior(),
        };

        /// <summary>
        /// Orbit has no fire step: the badges exist for as long as the weapon is equipped, so
        /// OrbitSystem owns them. Returning null keeps WeaponSystem from burning a cooldown on it.
        /// </summary>
        public static IWeaponBehavior Get(WeaponKind kind)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Kind == kind)
                {
                    return All[i];
                }
            }

            return null;
        }
    }
}
