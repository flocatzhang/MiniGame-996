using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// Drives the six weapon slots. Every slot is independent, the only shared input is
    /// haste, which feeds the asymptotic interval formula.
    /// </summary>
    public sealed class WeaponSystem
    {
        const float RetrySeconds = 0.12f;

        readonly GameContext _ctx;

        public WeaponSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>
        /// Staggering the first shot is what makes "six weapons" audible. Identical rates otherwise
        /// lock every slot onto the same frame forever: one visible volley instead of six, plus a
        /// periodic frame time spike every time they all fire together.
        /// </summary>
        public void ArmPhases()
        {
            PlayerModel p = _ctx.Run.Player;
            float now = GameClock.Now;
            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                p.Weapons[i].NextFireAt = now + p.Weapons[i].PhaseOffset;
            }
        }

        public void Tick(float dt)
        {
            PlayerModel p = _ctx.Run.Player;
            float now = GameClock.Now;
            float haste = p.EffectiveHaste(now);

            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                WeaponRuntime rt = p.Weapons[i];
                if (rt.IsEmpty || now < rt.NextFireAt)
                {
                    continue;
                }

                IWeaponBehavior behavior = WeaponBehaviors.Get(rt.Def.Kind);
                if (behavior == null)
                {
                    // Orbit weapons are always on, OrbitSystem owns them.
                    continue;
                }

                float interval = Interval(rt, haste);
                if (behavior.Fire(rt, _ctx))
                {
                    rt.LastFiredAt = now;
                    rt.NextFireAt = now + interval;
                }
                else
                {
                    // No target in range. Do not burn the cooldown, just look again shortly.
                    rt.NextFireAt = now + RetrySeconds;
                }
            }
        }

        public float CooldownProgress01(WeaponRuntime rt)
        {
            if (rt.IsEmpty)
            {
                return 0f;
            }

            if (rt.Def.Kind == WeaponKind.Orbit)
            {
                return 1f;
            }

            float haste = _ctx.Run.Player.EffectiveHaste(GameClock.Now);
            float interval = Interval(rt, haste);
            if (interval <= 0f)
            {
                return 1f;
            }

            float remaining = rt.NextFireAt - GameClock.Now;
            return Mathf.Clamp01(1f - remaining / interval);
        }

        static float Interval(WeaponRuntime rt, float haste)
        {
            return CombatFormula.AttackInterval(1f / Mathf.Max(0.01f, rt.Def.Rate), haste);
        }
    }
}
