using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    public sealed class MovementSystem
    {
        readonly GameContext _ctx;

        public MovementSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        public void Tick(float dt)
        {
            PlayerModel p = _ctx.Run.Player;
            ArenaDef arena = _ctx.Cfg.Arena;

            float speed = p.EffectiveMoveSpeed(GameClock.Now);
            p.Pos += p.MoveIntent * speed * dt;

            float maxX = Mathf.Max(1f, arena.HalfWidth - p.Radius);
            float maxY = Mathf.Max(1f, arena.HalfHeight - p.Radius);
            p.Pos = new Vector2(Mathf.Clamp(p.Pos.x, -maxX, maxX), Mathf.Clamp(p.Pos.y, -maxY, maxY));
        }
    }
}
