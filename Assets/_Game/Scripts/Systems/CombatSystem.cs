using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// All hit resolution. No colliders and no rigidbodies anywhere: every test is a squared
    /// distance against candidates returned by the spatial grid.
    /// </summary>
    public sealed class CombatSystem
    {
        readonly GameContext _ctx;
        readonly List<int> _scratch = new List<int>(128);

        public CombatSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        public void Tick(float dt)
        {
            ResolveProjectiles();
            ResolveContact();
        }

        void ResolveProjectiles()
        {
            RunModel run = _ctx.Run;
            PlayerModel player = run.Player;

            for (int i = 0; i < run.Projectiles.Count; i++)
            {
                ProjectileModel p = run.Projectiles[i];
                if (p.IsDead)
                {
                    continue;
                }

                if (p.FromEnemy)
                {
                    // Folders that explode on landing must not also hit on contact.
                    if (p.ExplodeRadius > 0f)
                    {
                        continue;
                    }

                    float reach = p.Radius + player.Radius;
                    if ((player.Pos - p.Pos).sqrMagnitude <= reach * reach)
                    {
                        DealDamageToPlayer(_ctx, p.Damage, p.Pos);
                        p.IsDead = true;
                    }

                    continue;
                }

                _ctx.Grid.QueryCircle(p.Pos, p.Radius + 1.5f, _scratch);
                for (int s = 0; s < _scratch.Count; s++)
                {
                    int idx = _scratch[s];
                    if (idx < 0 || idx >= run.Enemies.Count)
                    {
                        continue;
                    }

                    EnemyModel e = run.Enemies[idx];
                    if (e.IsDead || p.AlreadyHit(e.Id))
                    {
                        continue;
                    }

                    float reach = p.Radius + e.Radius;
                    if ((e.Pos - p.Pos).sqrMagnitude > reach * reach)
                    {
                        continue;
                    }

                    p.MarkHit(e.Id);

                    if (p.PinSeconds > 0f)
                    {
                        e.PinUntil = Mathf.Max(e.PinUntil, GameClock.Now + p.PinSeconds);
                    }

                    if (p.Knockback > 0f && p.Vel.sqrMagnitude > 0.0001f)
                    {
                        e.Knockback += p.Vel.normalized * p.Knockback;
                    }

                    DealDamageToEnemy(_ctx, e, p.Damage, p.Pos);

                    if (p.PierceLeft > 0)
                    {
                        p.PierceLeft--;
                    }
                    else
                    {
                        p.IsDead = true;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Contact damage takes the strongest overlapping enemy rather than the first one found.
        /// With a shared invulnerability window, using the first hit would make being swarmed by
        /// twenty mobs feel exactly like being touched by one, which removes the only real pressure.
        /// </summary>
        void ResolveContact()
        {
            RunModel run = _ctx.Run;
            PlayerModel player = run.Player;
            float now = GameClock.Now;

            if (!player.Alive)
            {
                return;
            }

            _ctx.Grid.QueryCircle(player.Pos, player.Radius + 2f, _scratch);

            float worst = 0f;
            Vector2 worstPos = player.Pos;
            bool invulnerable = player.IsInvulnerable(now);

            for (int i = 0; i < _scratch.Count; i++)
            {
                int idx = _scratch[i];
                if (idx < 0 || idx >= run.Enemies.Count)
                {
                    continue;
                }

                EnemyModel e = run.Enemies[idx];
                if (e.IsDead)
                {
                    continue;
                }

                float reach = player.Radius + e.Radius;
                if ((e.Pos - player.Pos).sqrMagnitude > reach * reach)
                {
                    continue;
                }

                // The universal grace window. Anything born on top of the player, split BUGs and
                // summoned meetings included, is inert for half a second. Losing sanity to something
                // that materialised inside you is the one hit players always blame on the game.
                if (!e.CanTouch(now))
                {
                    continue;
                }

                IEnemyBehavior b = e.Def != null ? EnemyBehaviorRegistry.Get(e.Def.Behavior) : null;
                bool suicide = b != null && b.DiesOnContact;

                if (!invulnerable && e.ContactDamage > worst)
                {
                    worst = e.ContactDamage;
                    worstPos = e.Pos;
                }

                // A charger spends itself even against an invulnerable player, otherwise it would
                // stick to the player through the whole dodge window.
                if (suicide)
                {
                    KillEnemy(_ctx, e);
                }
            }

            if (worst > 0f)
            {
                DealDamageToPlayer(_ctx, worst, worstPos);
            }
        }

        // ---------- shared entry points ----------

        public static void DealDamageToEnemy(GameContext ctx, EnemyModel e, float rawDamage, Vector2 fromPos)
        {
            float now = GameClock.Now;
            if (e.IsDead || now < e.InvulnUntil)
            {
                return;
            }

            PlayerModel p = ctx.Run.Player;

            bool crit;
            float damage = CombatFormula.ApplyCrit(
                rawDamage, p.Stats.Get(StatType.CritChance), p.Stats.Get(StatType.CritMulti), out crit);

            e.Hp -= damage;
            e.FlashUntil = now + 0.06f;

            EvtArg a = new EvtArg();
            a.I0 = e.Id;
            a.I1 = crit ? 1 : 0;
            a.F0 = damage;
            a.P0 = e.Pos;
            a.O0 = e;
            ctx.Bus.Dispatch(EventID.EnemyDamaged, a);

            if (e.Hp > 0f)
            {
                return;
            }

            if (e.IsBoss && e.BarsLeft > 1)
            {
                BreakBossBar(ctx, e);
                return;
            }

            KillEnemy(ctx, e);
        }

        /// <summary>
        /// One bar down, two to go. The pause is not padding: it is the punctuation of the fight,
        /// and the wave of trash that arrives with it is what keeps the boss from being a stat check.
        /// </summary>
        static void BreakBossBar(GameContext ctx, EnemyModel e)
        {
            float now = GameClock.Now;

            e.BarsLeft--;
            e.Hp = e.MaxHp;
            e.Phase = e.BarsTotal - e.BarsLeft + 1;
            e.InvulnUntil = now + e.Def.Param.GetFloat("phaseInvuln", 2f);

            ctx.Run.BossBarsLeft = e.BarsLeft;

            int adds = Mathf.Max(0, (int)e.Def.Param.GetFloat("phaseAdds", 20f));
            string addId = e.Def.Param.GetString("phaseAddId", "mail");
            EnemyDef add = ctx.Cfg.Enemy(addId);
            if (add != null && ctx.Spawner != null && adds > 0)
            {
                ctx.Spawner.SpawnBurst(add, adds);
            }

            EvtArg a = new EvtArg();
            a.I0 = e.Id;
            a.I1 = e.Phase;
            a.F0 = e.BarsLeft;
            a.P0 = e.Pos;
            a.O0 = e;
            ctx.Bus.Dispatch(EventID.BossPhaseChanged, a);
        }

        public static void DealDamageToPlayer(GameContext ctx, float rawDamage, Vector2 fromPos)
        {
            PlayerModel p = ctx.Run.Player;
            float now = GameClock.Now;

            if (!p.Alive || p.IsInvulnerable(now))
            {
                return;
            }

            if (CombatFormula.RollDodge(p.Stats.Get(StatType.Dodge), ctx.Cfg.Player.DodgeCap))
            {
                EvtArg d = new EvtArg();
                d.P0 = p.Pos;
                ctx.Bus.Dispatch(EventID.PlayerDodged, d);
                return;
            }

            float damage = CombatFormula.IncomingDamage(rawDamage, p.EffectiveDef());

            if (p.Shield > 0f)
            {
                float absorbed = Mathf.Min(p.Shield, damage);
                p.Shield -= absorbed;
                damage -= absorbed;

                if (p.Shield <= 0f)
                {
                    p.Shield = 0f;
                    OnShieldBroken(ctx, p);
                }
            }

            p.San -= damage;
            p.HitCount++;
            p.LastHitAt = now;
            p.InvulnUntil = now + ctx.Cfg.Player.InvulnAfterHit;

            EvtArg a = new EvtArg();
            a.F0 = damage;
            a.F1 = rawDamage;
            a.P0 = p.Pos;
            a.P1 = fromPos;
            ctx.Bus.Dispatch(EventID.PlayerDamaged, a);

            if (p.San > 0f)
            {
                return;
            }

            // Orange headphone: one save per day. Checked here rather than in the flow so no code
            // path can reach the fail state without passing through it.
            if (p.DeathSaveReady)
            {
                p.DeathSaveReady = false;
                p.San = p.MaxSan * 0.2f;
                p.InvulnUntil = now + 1.5f;

                EvtArg s = new EvtArg();
                s.F0 = p.San;
                s.P0 = p.Pos;
                ctx.Bus.Dispatch(EventID.PlayerHealed, s);
                return;
            }

            p.San = 0f;
            p.Alive = false;
            ctx.Bus.Dispatch(EventID.PlayerDied);
        }

        /// <summary>Orange headphone answers the broken shield with a shove instead of nothing.</summary>
        static void OnShieldBroken(GameContext ctx, PlayerModel p)
        {
            EvtArg e = new EvtArg();
            e.P0 = p.Pos;
            ctx.Bus.Dispatch(EventID.PlayerShieldBroken, e);

            if (p.QualityOf(EquipSlot.Head) < Quality.Orange)
            {
                return;
            }

            float radius = 3f;
            float damage = p.Stats.Get(StatType.Atk);
            RunModel run = ctx.Run;

            for (int i = 0; i < run.Enemies.Count; i++)
            {
                EnemyModel en = run.Enemies[i];
                if (en.IsDead)
                {
                    continue;
                }

                Vector2 delta = en.Pos - p.Pos;
                if (delta.sqrMagnitude > radius * radius)
                {
                    continue;
                }

                if (delta.sqrMagnitude > 0.0001f)
                {
                    en.Knockback += delta.normalized * 6f;
                }

                DealDamageToEnemy(ctx, en, damage, p.Pos);
            }
        }

        public static void KillEnemy(GameContext ctx, EnemyModel e)
        {
            if (e.IsDead)
            {
                return;
            }

            e.IsDead = true;
            e.Hp = 0f;

            ctx.Run.CountKill(e.DefId);

            if (e.IsBoss)
            {
                ctx.Run.BossDefeated = true;
                ctx.Run.BossBarsLeft = 0;
            }

            IEnemyBehavior b = e.Def != null ? EnemyBehaviorRegistry.Get(e.Def.Behavior) : null;
            if (b != null)
            {
                b.OnDeath(e, ctx);
            }

            EvtArg a = new EvtArg();
            a.I0 = e.Id;
            a.I1 = e.Def != null ? (int)e.Def.Tier : 0;
            a.P0 = e.Pos;
            a.O0 = e;
            ctx.Bus.Dispatch(EventID.EnemyKilled, a);
        }
    }
}
