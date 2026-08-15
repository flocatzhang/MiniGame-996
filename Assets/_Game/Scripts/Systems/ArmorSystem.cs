using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// The parts of the three armour slots that need a heartbeat rather than a stat line.
    ///
    /// Only the headphone shield lives here. It is the answer to all three control effects in the
    /// game, so cutting it would make Friday, the pressure peak, considerably worse.
    /// </summary>
    public sealed class ArmorSystem
    {
        const float ShieldPeriod = 10f;

        readonly GameContext _ctx;

        public ArmorSystem(GameContext ctx)
        {
            _ctx = ctx;
        }

        public void Tick(float dt)
        {
            PlayerModel p = _ctx.Run.Player;
            if (!p.Alive)
            {
                return;
            }

            if (p.QualityOf(EquipSlot.Head) < Quality.Blue)
            {
                return;
            }

            float now = GameClock.Now;
            if (now < p.NextShieldAt)
            {
                return;
            }

            p.NextShieldAt = now + ShieldPeriod;

            float amount = 15f + 0.3f * p.EffectiveDef();
            p.Shield = Mathf.Max(p.Shield, amount);
            p.ShieldPeak = p.Shield;

            EvtArg a = new EvtArg();
            a.F0 = amount;
            a.P0 = p.Pos;
            _ctx.Bus.Dispatch(EventID.PlayerShielded, a);
        }
    }
}
