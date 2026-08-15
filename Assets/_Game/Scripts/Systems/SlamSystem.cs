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
                ResolveCircle(s);

                EvtArg a = new EvtArg();
                a.I0 = s.Slot;
                a.I1 = s.SelectAll ? 1 : 0;
                a.F0 = s.Radius;
                a.P0 = s.Target;
                _ctx.Bus.Dispatch(EventID.SlamLanded, a);

                if (s.SelectAll)
                {
                    EvtArg sa = new EvtArg();
                    sa.I0 = s.Slot;
                    sa.F0 = s.Radius;
                    sa.P0 = s.Target;
                    _ctx.Bus.Dispatch(EventID.SelectAll, sa);
                }
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

        void Apply(SlamModel s, EnemyModel e)
        {
            if (s.SlowPct > 0f && s.SlowSeconds > 0f)
            {
                e.SlowPct = Mathf.Max(e.SlowPct, s.SlowPct);
                e.SlowUntil = Mathf.Max(e.SlowUntil, GameClock.Now + s.SlowSeconds);
            }

            Vector2 dir = e.Pos - s.Target;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.up;
            }

            e.TryKnockback(dir, s.Knockback, GameClock.Now);

            CombatSystem.DealDamageToEnemy(_ctx, e, s.Damage, s.Target);
        }
    }
}
