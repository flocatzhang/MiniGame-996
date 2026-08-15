using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// Badges orbiting the player. The cheapest weapon in the game: no pool, no lifetime, no
    /// recycling. Cards live exactly as long as the weapon that owns them is equipped.
    /// </summary>
    public sealed class OrbitSystem
    {
        readonly GameContext _ctx;
        readonly List<int> _scratch = new List<int>(64);

        float _angleDeg;
        int _signature;

        public OrbitSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        public void Reset()
        {
            _angleDeg = 0f;
            _signature = 0;
            _ctx.Run.OrbitCards.Clear();
        }

        public void Tick(float dt)
        {
            RunModel run = _ctx.Run;
            PlayerModel p = run.Player;

            RebuildIfNeeded();

            List<OrbitCardModel> cards = run.OrbitCards;
            if (cards.Count == 0)
            {
                return;
            }

            float now = GameClock.Now;
            float atk = p.Stats.Get(StatType.Atk);

            // One shared rotation angle. Per card angular speed would desynchronise the ring the
            // moment two different qualities are equipped, and the ring is the whole read.
            float fastest = 0f;
            for (int i = 0; i < cards.Count; i++)
            {
                fastest = Mathf.Max(fastest, cards[i].DegPerSec);
            }

            _angleDeg = Mathf.Repeat(_angleDeg + fastest * dt, 360f);

            for (int i = 0; i < cards.Count; i++)
            {
                OrbitCardModel c = cards[i];
                WeaponRuntime rt = p.Weapons[c.Slot];
                if (rt.IsEmpty || rt.Def.Kind != WeaponKind.Orbit)
                {
                    continue;
                }

                float rad = (_angleDeg + c.PhaseDeg) * Mathf.Deg2Rad;
                c.Pos = p.Pos + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * c.Radius;

                float qualityCoef = _ctx.Cfg.WeaponQuality.Get(rt.Quality);
                c.Damage = CombatFormula.WeaponDamage(rt.Def, qualityCoef, atk);
                c.TetherDamage = c.Damage * c.TetherPct * 0.01f;

                Resolve(c, rt, now);
            }
        }

        /// <summary>
        /// Phases are divided by the live card total, never by slot index, so six white badges from
        /// six slots still form an even ring instead of clumping wherever the slots happen to be.
        /// </summary>
        void RebuildIfNeeded()
        {
            PlayerModel p = _ctx.Run.Player;

            int sig = 17;
            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                WeaponRuntime rt = p.Weapons[i];
                bool orbit = !rt.IsEmpty && rt.Def.Kind == WeaponKind.Orbit;
                sig = sig * 31 + (orbit ? (int)rt.Quality + 1 : 0);
            }

            if (sig == _signature)
            {
                return;
            }

            _signature = sig;

            List<OrbitCardModel> cards = _ctx.Run.OrbitCards;
            cards.Clear();

            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                WeaponRuntime rt = p.Weapons[i];
                if (rt.IsEmpty || rt.Def.Kind != WeaponKind.Orbit)
                {
                    continue;
                }

                WeaponTierDef tier = rt.Tier;
                for (int n = 0; n < Mathf.Max(1, tier.OrbitCount); n++)
                {
                    OrbitCardModel c = new OrbitCardModel();
                    c.Id = _ctx.Run.NextId();
                    c.Slot = i;
                    c.ViewId = rt.Def.ViewId;
                    c.Radius = tier.OrbitRadius;
                    c.DegPerSec = tier.OrbitDegPerSec;
                    c.Knockback = tier.Knockback;
                    c.TetherPct = tier.TetherDamagePct;
                    c.Tethered = tier.TetherDamagePct > 0f;
                    cards.Add(c);
                }
            }

            int total = cards.Count;
            for (int i = 0; i < total; i++)
            {
                cards[i].PhaseDeg = 360f / total * i;
                cards[i].ClearHits();
            }

            if (total > 0)
            {
                EvtArg a = new EvtArg();
                a.I0 = total;
                _ctx.Bus.Dispatch(EventID.OrbitRebuilt, a);
            }
        }

        /// <summary>
        /// The per card, per target cooldown is not a balance knob. Without it an enemy pressed
        /// against the player is judged every frame and evaporates, which makes the badge absurd.
        /// </summary>
        void Resolve(OrbitCardModel c, WeaponRuntime rt, float now)
        {
            RunModel run = _ctx.Run;
            float cd = rt.Def.SameTargetCd;

            _ctx.Grid.QueryCircle(c.Pos, c.HitRadius + 0.6f, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                int idx = _scratch[i];
                if (idx < 0 || idx >= run.Enemies.Count)
                {
                    continue;
                }

                EnemyModel e = run.Enemies[idx];
                if (e.IsDead || !c.CanHit(e.Id, now))
                {
                    continue;
                }

                float reach = c.HitRadius + e.Radius;
                if ((e.Pos - c.Pos).sqrMagnitude > reach * reach)
                {
                    continue;
                }

                c.MarkHit(e.Id, now + cd, now);

                if (c.Knockback > 0f)
                {
                    Vector2 dir = e.Pos - run.Player.Pos;
                    if (dir.sqrMagnitude < 0.0001f)
                    {
                        dir = Vector2.up;
                    }

                    e.Knockback += dir.normalized * c.Knockback;
                }

                CombatSystem.DealDamageToEnemy(_ctx, e, c.Damage, c.Pos);
            }

            if (c.Tethered)
            {
                ResolveTether(c, now);
            }
        }

        /// <summary>
        /// Orange links the badges into one ring. The rope is sampled as a thin annulus test rather
        /// than as line segments: the visual is a circle, so the hit shape should be one too.
        /// </summary>
        void ResolveTether(OrbitCardModel c, float now)
        {
            RunModel run = _ctx.Run;
            PlayerModel p = run.Player;

            _ctx.Grid.QueryCircle(p.Pos, c.Radius + 0.5f, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                int idx = _scratch[i];
                if (idx < 0 || idx >= run.Enemies.Count)
                {
                    continue;
                }

                EnemyModel e = run.Enemies[idx];
                if (e.IsDead || !c.CanHit(e.Id, now))
                {
                    continue;
                }

                float dist = (e.Pos - p.Pos).magnitude;
                if (Mathf.Abs(dist - c.Radius) > e.Radius + 0.25f)
                {
                    continue;
                }

                c.MarkHit(e.Id, now + 0.5f, now);
                CombatSystem.DealDamageToEnemy(_ctx, e, c.TetherDamage, e.Pos);
            }
        }
    }
}
