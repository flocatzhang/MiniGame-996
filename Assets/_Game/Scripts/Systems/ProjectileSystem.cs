using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    public sealed class ProjectileSystem
    {
        readonly GameContext _ctx;

        public ProjectileSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        public void Tick(float dt)
        {
            RunModel run = _ctx.Run;
            ArenaDef arena = _ctx.Cfg.Arena;
            float now = GameClock.Now;
            float boundX = arena.HalfWidth + 6f;
            float boundY = arena.HalfHeight + 6f;

            for (int i = 0; i < run.Projectiles.Count; i++)
            {
                ProjectileModel p = run.Projectiles[i];
                if (p.IsDead)
                {
                    continue;
                }

                p.Pos += p.Vel * dt;

                bool expired = now >= p.DieAt;
                if (!expired && p.MaxDistance > 0f)
                {
                    expired = (p.Pos - p.Origin).sqrMagnitude >= p.MaxDistance * p.MaxDistance;
                }

                bool outOfBounds = Mathf.Abs(p.Pos.x) > boundX || Mathf.Abs(p.Pos.y) > boundY;

                if (!expired && !outOfBounds)
                {
                    continue;
                }

                if (p.ExplodeRadius > 0f && !outOfBounds)
                {
                    Explode(p);
                }

                p.IsDead = true;
            }
        }

        /// <summary>Boss KPI folders detonate where they land instead of on contact.</summary>
        void Explode(ProjectileModel p)
        {
            PlayerModel player = _ctx.Run.Player;

            EvtArg a = new EvtArg();
            a.F0 = p.ExplodeRadius;
            a.P0 = p.Pos;
            _ctx.Bus.Dispatch(EventID.BossTelegraph, a);

            float reach = p.ExplodeRadius + player.Radius;
            if ((player.Pos - p.Pos).sqrMagnitude <= reach * reach)
            {
                CombatSystem.DealDamageToPlayer(_ctx, p.Damage, p.Pos);
            }
        }
    }
}
