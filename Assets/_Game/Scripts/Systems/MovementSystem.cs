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
            p.Pos = arena.Clamp(p.Pos, p.Radius);
        }
    }
}
