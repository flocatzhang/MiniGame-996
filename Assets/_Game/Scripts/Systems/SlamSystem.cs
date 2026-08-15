using System.Collections.Generic;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// Resolves queued keyboard strikes. One OverlapCircle equivalent per strike, no projectiles and
    /// no pooling of flying objects, which is why six keyboards at once stay readable on screen.
    /// </summary>
    public sealed class SlamSystem
    {
        readonly GameContext _ctx;
        readonly List<int> _scratch = new List<int>(128);

        public SlamSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        public void Tick(float dt)
        {
            RunModel run = _ctx.Run;
            float now = GameClock.Now;

            for (int i = 0; i < run.Slams.Count; i++)
            {
                SlamModel s = run.Slams[i];
                if (s.IsDead || now < s.LandAt)
                {
                    continue;
                }

                s.IsDead = true;

                if (s.SelectAll)
                {
                    ResolveSelectAll(s);
                }
                else
                {
                    ResolveCircle(s);
                }

                EvtArg a = new EvtArg();
                a.I0 = s.Slot;
                a.I1 = s.SelectAll ? 1 : 0;
                a.F0 = s.Radius;
                a.P0 = s.Target;
                _ctx.Bus.Dispatch(EventID.SlamLanded, a);
            }
        }

        void ResolveCircle(SlamModel s)
        {
            RunModel run = _ctx.Run;
            _ctx.Grid.QueryCircle(s.Target, s.Radius, _scratch);

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

                float reach = s.Radius + e.Radius;
                if ((e.Pos - s.Target).sqrMagnitude > reach * reach)
                {
                    continue;
                }

                Apply(s, e);
            }
        }

        /// <summary>
        /// Ctrl + A. No spatial query at all, just walk the live list once. The joke and the code
        /// happen to be the same shape, which is the cheapest kind of feature there is.
        /// </summary>
        void ResolveSelectAll(SlamModel s)
        {
            RunModel run = _ctx.Run;
            for (int i = 0; i < run.Enemies.Count; i++)
            {
                EnemyModel e = run.Enemies[i];
                if (!e.IsDead)
                {
                    Apply(s, e);
                }
            }

            EvtArg a = new EvtArg();
            a.I0 = s.Slot;
            a.P0 = s.Target;
            _ctx.Bus.Dispatch(EventID.SelectAll, a);
        }

        void Apply(SlamModel s, EnemyModel e)
        {
            if (s.SlowPct > 0f)
            {
                e.SlowPct = Mathf.Max(e.SlowPct, s.SlowPct);
                e.SlowUntil = GameClock.Now + 1.5f;
            }

            if (s.Knockback > 0f)
            {
                Vector2 dir = e.Pos - s.Target;
                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = Vector2.up;
                }

                e.Knockback += dir.normalized * s.Knockback;
            }

            CombatSystem.DealDamageToEnemy(_ctx, e, s.Damage, s.Target);
        }
    }
}
