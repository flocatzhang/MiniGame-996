using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// The active skill fires itself when the cooldown is up, because movement is the only input.
    /// Every passive from the card pool is a modifier read off PlayerModel rather than a branch here.
    /// </summary>
    public sealed class SkillSystem
    {
        readonly GameContext _ctx;
        readonly List<int> _scratch = new List<int>(128);

        public SkillSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>
        /// A run opens with the skill already on cooldown. It fires the instant it is ready, so an
        /// armed skill at second zero spends its invulnerability, its heal and its knockback on an
        /// empty field before the first enemy has walked in, and the player's first sight of their
        /// only active ability is it going off for no reason.
        /// </summary>
        public void Reset()
        {
            PlayerModel p = _ctx.Run.Player;
            p.SkillReadyAt = GameClock.Now + p.SkillCd(_ctx.Cfg.Skill);
        }

        public void Tick(float dt)
        {
            PlayerModel p = _ctx.Run.Player;
            if (!p.Alive || GameClock.Now < p.SkillReadyAt)
            {
                return;
            }

            Cast();
        }

        public void Cast()
        {
            SkillDef def = _ctx.Cfg.Skill;
            PlayerModel p = _ctx.Run.Player;
            float now = GameClock.Now;

            p.SkillReadyAt = now + p.SkillCd(def);
            p.SkillInvulnUntil = Mathf.Max(p.SkillInvulnUntil, now + p.SkillInvulnSeconds(def));

            float healPct = p.SkillHealPct(def);
            if (healPct > 0f)
            {
                float heal = p.MaxSan * healPct * 0.01f;
                float before = p.San;
                p.San = Mathf.Min(p.MaxSan, p.San + heal);

                if (p.San > before)
                {
                    EvtArg h = new EvtArg();
                    h.F0 = p.San - before;
                    h.P0 = p.Pos;
                    _ctx.Bus.Dispatch(EventID.PlayerHealed, h);
                }
            }

            float radius = p.SkillPushRadius(def);
            float stunSeconds = p.SkillStunSeconds();
            float damage = p.Stats.Get(StatType.Atk) * p.SkillDamagePct() * 0.01f;

            _ctx.Grid.QueryCircle(p.Pos, radius, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                int idx = _scratch[i];
                if (idx < 0 || idx >= _ctx.Run.Enemies.Count)
                {
                    continue;
                }

                EnemyModel e = _ctx.Run.Enemies[idx];
                if (e.IsDead)
                {
                    continue;
                }

                Vector2 away = e.Pos - p.Pos;
                float dist = away.magnitude;
                if (dist > radius)
                {
                    continue;
                }

                e.ForceKnockback(away, def.PushForce, now);

                if (stunSeconds > 0f)
                {
                    e.StunUntil = Mathf.Max(e.StunUntil, now + stunSeconds);
                }

                if (damage > 0f)
                {
                    CombatSystem.DealDamageToEnemy(_ctx, e, damage, p.Pos);
                }
            }

            EvtArg a = new EvtArg();
            a.F0 = radius;
            a.P0 = p.Pos;
            _ctx.Bus.Dispatch(EventID.SkillCast, a);
        }

        public float CooldownProgress01()
        {
            PlayerModel p = _ctx.Run.Player;
            float cd = p.SkillCd(_ctx.Cfg.Skill);
            float remaining = p.SkillReadyAt - GameClock.Now;
            return Mathf.Clamp01(1f - remaining / Mathf.Max(0.01f, cd));
        }
    }
}
