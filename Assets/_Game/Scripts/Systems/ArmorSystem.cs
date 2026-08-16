using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// The parts of the three armour slots that need a heartbeat rather than a stat line: the
    /// headphone shield and the slipper's coffee trail. Everything else the three slots do is either
    /// a stat modifier or a reaction to being hit, and those live in CombatSystem.
    ///
    /// The headphone shield is the answer to all three control effects in the game, so cutting it
    /// would make Friday, the pressure peak, considerably worse.
    /// </summary>
    public sealed class ArmorSystem
    {
        const float ShieldPeriod = 10f;

        /// <summary>
        /// Purple slipper. Marks are dropped on an interval rather than per unit travelled so that
        /// standing still cannot pile ten of them on one tile, and the lifetime is kept under
        /// StainSlots * StainInterval so the ring never overwrites a mark that is still live.
        /// </summary>
        const float StainInterval = 0.3f;
        const float StainSeconds = 2.5f;
        const float StainRadius = 0.9f;
        const float StainSlowPct = 50f;

        /// <summary>Refreshed every frame the enemy stands in it, so it lapses shortly after they leave.</summary>
        const float StainSlowSeconds = 0.25f;

        /// <summary>
        /// Chip damage for standing in the trail, as a share of ATK so it keeps pace with the build
        /// instead of falling off the moment hpScale climbs. Deliberately small: the trail already
        /// halves the pack's speed, and the six weapon slots are what is supposed to kill things.
        /// Billed on an interval rather than per frame so it also reads as a number the player can
        /// see rather than a stream of ones.
        /// </summary>
        const float StainDamagePctAtk = 15f;

        const float StainDamageInterval = 0.5f;

        /// <summary>
        /// How long a granted shield lasts before lapsing on its own.
        ///
        /// The shield used to persist until it was broken, so on a build that was not being hit it was
        /// simply always up. That made the blue tier a flat sanity buffer worth several times the SAN
        /// bar over a boss fight, and it made the purple tier's control immunity permanent, which is
        /// three enemy types cancelled by one item. Half of each cycle turns it into a cadence the
        /// player can read and push into, and leaves the aura enemies something to do in the other half.
        /// </summary>
        const float ShieldSeconds = 5f;

        readonly GameContext _ctx;
        readonly List<int> _scratch = new List<int>(64);

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

            float now = GameClock.Now;

            TickStains(p, now);

            // Lapsing is not breaking: no counter, no event. The orange tier pays out for taking the
            // hit, not for standing still until the timer ran out.
            if (p.Shield > 0f && now >= p.ShieldUntil)
            {
                p.Shield = 0f;
                p.ShieldPeak = 0f;
            }

            if (p.QualityOf(EquipSlot.Head) < Quality.Blue)
            {
                return;
            }

            if (now < p.NextShieldAt)
            {
                return;
            }

            p.NextShieldAt = now + ShieldPeriod;

            float amount = 15f + 0.3f * p.EffectiveDef();
            p.Shield = Mathf.Max(p.Shield, amount);
            p.ShieldPeak = p.Shield;
            p.ShieldUntil = now + ShieldSeconds;

            EvtArg a = new EvtArg();
            a.F0 = amount;
            a.P0 = p.Pos;
            _ctx.Bus.Dispatch(EventID.PlayerShielded, a);
        }

        /// <summary>
        /// Purple slipper. Slowing what follows the player is worth more than slowing what surrounds
        /// them: the trail lands on exactly the pack that is chasing, and it is the only effect in the
        /// game that rewards retreating rather than punishing it.
        /// </summary>
        void TickStains(PlayerModel p, float now)
        {
            if (p.QualityOf(EquipSlot.Feet) < Quality.Purple)
            {
                return;
            }

            // Dropped only while moving, so parking on a doorway does not build a permanent slow field.
            if (now >= p.NextStainAt && p.MoveIntent.sqrMagnitude > 0.0001f)
            {
                p.NextStainAt = now + StainInterval;
                p.DropStain(p.Pos, now + StainSeconds);
            }

            List<EnemyModel> enemies = _ctx.Run.Enemies;
            float damage = p.Stats.Get(StatType.Atk) * StainDamagePctAtk * 0.01f;

            for (int s = 0; s < PlayerModel.StainSlots; s++)
            {
                if (now >= p.StainUntil(s))
                {
                    continue;
                }

                Vector2 at = p.StainPos(s);
                _ctx.Grid.QueryCircle(at, StainRadius, _scratch);

                for (int i = 0; i < _scratch.Count; i++)
                {
                    int idx = _scratch[i];
                    if (idx < 0 || idx >= enemies.Count)
                    {
                        continue;
                    }

                    EnemyModel e = enemies[idx];
                    if (e.IsDead)
                    {
                        continue;
                    }

                    if ((e.Pos - at).sqrMagnitude > StainRadius * StainRadius)
                    {
                        continue;
                    }

                    // Strongest wins, never the sum. A trail overlaps itself by design, and adding the
                    // marks together would stop the pack dead the moment the player walked in a circle.
                    if (now >= e.SlowUntil || e.SlowPct < StainSlowPct)
                    {
                        e.SlowPct = StainSlowPct;
                    }

                    e.SlowUntil = Mathf.Max(e.SlowUntil, now + StainSlowSeconds);

                    // The gate is on the enemy, not on the mark, so walking a tight circle bills the
                    // same rate as walking a line.
                    if (now < e.StainTickAt)
                    {
                        continue;
                    }

                    e.StainTickAt = now + StainDamageInterval;
                    CombatSystem.DealDamageToEnemy(_ctx, e, damage, at);
                }
            }
        }
    }
}
