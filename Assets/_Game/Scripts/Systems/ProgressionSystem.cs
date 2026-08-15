using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// Experience and rank ups. A level up does not apply anything by itself: it queues a card choice,
    /// because that choice is now the only place the player expresses intent about their build.
    /// </summary>
    public sealed class ProgressionSystem
    {
        readonly GameContext _ctx;

        public ProgressionSystem(GameContext ctx)
        {
            _ctx = ctx;
            _ctx.Bus.Register(EventID.EnemyKilled, OnEnemyKilled);
            _ctx.Bus.Register(EventID.EquipDeclined, OnEquipDeclined);
        }

        public void Dispose()
        {
            _ctx.Bus.Unregister(EventID.EnemyKilled, OnEnemyKilled);
            _ctx.Bus.Unregister(EventID.EquipDeclined, OnEquipDeclined);
        }

        public string RankOf(int level)
        {
            return _ctx.Cfg.RankOf(level);
        }

        void OnEnemyKilled(EvtArg arg)
        {
            EnemyModel e = arg.O0 as EnemyModel;
            if (e == null || e.Def == null)
            {
                return;
            }

            AddExp(e.Def.Exp);
        }

        void OnEquipDeclined(EvtArg arg)
        {
            AddExp(arg.I0);
        }

        public void AddExp(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            PlayerModel p = _ctx.Run.Player;
            ProgressionDef prog = _ctx.Cfg.Progression;

            if (p.Level >= prog.MaxLevel)
            {
                return;
            }

            p.Exp += amount;

            int guard = 0;
            while (p.Exp >= p.ExpToNext && p.Level < prog.MaxLevel && guard++ < 64)
            {
                string before = RankOf(p.Level);

                p.Exp -= p.ExpToNext;
                p.Level++;
                p.ExpToNext = CombatFormula.ExpForLevel(p.Level, prog);

                // Queued, not consumed. A double level up inside one frame must still hand out two
                // card panels or the player silently loses a choice.
                p.PendingLevelUps++;

                EvtArg a = new EvtArg();
                a.I0 = p.Level;
                a.P0 = p.Pos;
                _ctx.Bus.Dispatch(EventID.PlayerLevelUp, a);

                string after = RankOf(p.Level);
                if (after != before)
                {
                    EvtArg r = new EvtArg();
                    r.I0 = p.Level;
                    r.O0 = after;
                    _ctx.Bus.Dispatch(EventID.PlayerRankUp, r);
                }
            }

            if (p.Level >= prog.MaxLevel)
            {
                p.Exp = 0;
            }
        }

        public void Tick(float dt)
        {
        }
    }
}
