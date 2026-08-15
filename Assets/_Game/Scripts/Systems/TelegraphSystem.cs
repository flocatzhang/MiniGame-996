using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// Resolves ground markers when their timer runs out. Three unrelated features share this one
    /// system because all three are the same sentence: draw a circle, wait, then act on that spot.
    /// Every breach of the spawn band goes through here, so the warning can never be forgotten.
    /// </summary>
    public sealed class TelegraphSystem
    {
        readonly GameContext _ctx;

        public TelegraphSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        public void Tick(float dt)
        {
            RunModel run = _ctx.Run;
            float now = GameClock.Now;

            for (int i = 0; i < run.Telegraphs.Count; i++)
            {
                TelegraphModel t = run.Telegraphs[i];
                if (t.IsDead || now < t.FireAt)
                {
                    continue;
                }

                t.IsDead = true;

                if (t.Damage > 0f)
                {
                    PlayerModel p = run.Player;
                    float reach = t.Radius + p.Radius;
                    if ((p.Pos - t.Pos).sqrMagnitude <= reach * reach)
                    {
                        CombatSystem.DealDamageToPlayer(_ctx, t.Damage, t.Pos);
                    }
                }

                if (t.SummonCount > 0 && !string.IsNullOrEmpty(t.SummonEnemyId) && _ctx.Spawner != null)
                {
                    EnemyDef def = _ctx.Cfg.Enemy(t.SummonEnemyId);
                    if (def != null)
                    {
                        for (int n = 0; n < t.SummonCount; n++)
                        {
                            Vector2 pos = n == 0 ? t.Pos : t.Pos + Rng.RingPoint(Vector2.zero, 0f, 0.6f);
                            _ctx.Spawner.Spawn(def, pos, t.SummonDrop);
                        }
                    }
                }

                EvtArg a = new EvtArg();
                a.I0 = t.Id;
                a.F0 = t.Radius;
                a.P0 = t.Pos;
                _ctx.Bus.Dispatch(EventID.BossTelegraph, a);
            }
        }
    }
}
