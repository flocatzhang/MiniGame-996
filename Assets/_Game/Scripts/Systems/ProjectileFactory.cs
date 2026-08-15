using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>Single place that stamps a projectile, so lifetime and view id are never forgotten.</summary>
    public static class ProjectileFactory
    {
        public static ProjectileModel Spawn(
            GameContext ctx,
            Vector2 pos,
            Vector2 velocity,
            float damage,
            float range,
            string viewId,
            bool fromEnemy)
        {
            ProjectileModel p = ctx.Run.RentProjectile();
            p.Pos = pos;
            p.Origin = pos;
            p.Vel = velocity;
            p.Damage = damage;
            p.ViewId = viewId;
            p.FromEnemy = fromEnemy;
            p.MaxDistance = range;

            // Range means both lock radius and max flight distance. A projectile that outlives its
            // range keeps a pool slot busy while flying off screen where nobody can see it hit.
            float speed = velocity.magnitude;
            float life = speed > 0.01f ? range / speed : 0.5f;
            p.DieAt = GameClock.Now + Mathf.Clamp(life, 0.05f, 8f);
            return p;
        }
    }
}
