using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// Straight line chase, no pathfinding and no obstacle avoidance by design.
    /// Runs in three passes because aura behaviours have to resolve before anything moves.
    /// </summary>
    public sealed class EnemyAiSystem
    {
        const float KnockbackDecay = 8f;

        readonly GameContext _ctx;

        public EnemyAiSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        public void Tick(float dt)
        {
            RunModel run = _ctx.Run;
            PlayerModel player = run.Player;
            float now = GameClock.Now;

            // Pass one: clear last frame's aura state. Auras are recomputed from scratch every frame
            // so a dead PPT stops slowing the player on the same frame it dies.
            player.ClearAuras();
            for (int i = 0; i < run.Enemies.Count; i++)
            {
                run.Enemies[i].SpeedMul = 1f;
            }

            // Pass two: behaviours. Auras write here, so nothing may move yet.
            for (int i = 0; i < run.Enemies.Count; i++)
            {
                EnemyModel e = run.Enemies[i];
                if (e.IsDead || e.Def == null)
                {
                    continue;
                }

                IEnemyBehavior b = EnemyBehaviorRegistry.Get(e.Def.Behavior);
                if (b != null)
                {
                    b.Tick(e, _ctx, dt);
                }
            }

            // Pass three: movement.
            for (int i = 0; i < run.Enemies.Count; i++)
            {
                EnemyModel e = run.Enemies[i];
                if (e.IsDead || e.Def == null)
                {
                    continue;
                }

                if (e.Knockback.sqrMagnitude > 0.0001f)
                {
                    e.Pos += e.Knockback * dt;
                    e.Knockback = Vector2.MoveTowards(e.Knockback, Vector2.zero, KnockbackDecay * dt);
                }

                float speed = e.EffectiveSpeed(now);
                if (speed <= 0f)
                {
                    continue;
                }

                Vector2 dir;
                IEnemyBehavior b = EnemyBehaviorRegistry.Get(e.Def.Behavior);
                if (b == null || !b.TryMove(e, _ctx, out dir))
                {
                    Vector2 delta = player.Pos - e.Pos;
                    float dist = delta.magnitude;
                    if (dist < 0.0001f)
                    {
                        continue;
                    }

                    dir = delta / dist;
                }

                if (dir.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                e.Pos += dir * speed * dt;
            }
        }
    }
}
