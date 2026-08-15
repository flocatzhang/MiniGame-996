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

                if (expired || Mathf.Abs(p.Pos.x) > boundX || Mathf.Abs(p.Pos.y) > boundY)
                {
                    p.IsDead = true;
                }
            }
        }
    }
}
