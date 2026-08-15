using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// Turns a day's density into enemies. Each spawner owns a time window and a share of the day's
    /// budget, which is what makes "pure mail for twenty seconds, then mail plus deadlines" something
    /// a designer writes rather than something a programmer schedules.
    /// </summary>
    public sealed class SpawnSystem
    {
        readonly GameContext _ctx;
        readonly SpawnBand _band;
        readonly List<float> _weights = new List<float>(8);
        readonly List<float> _nextAt = new List<float>(4);
        readonly List<int> _quota = new List<int>(4);
        readonly List<EnemyDef> _debt = new List<EnemyDef>(32);
        readonly HashSet<int> _firedFixed = new HashSet<int>();

        public SpawnSystem(GameContext ctx)
        {
            _ctx = ctx;
            _band = new SpawnBand(ctx);
        }

        public void OnDayBegin()
        {
            _nextAt.Clear();
            _quota.Clear();
            _debt.Clear();
            _firedFixed.Clear();

            DayDef day = _ctx.Run.Day;
            if (day == null)
            {
                return;
            }

            int total = day.TotalSpawn;
            for (int i = 0; i < day.Spawners.Count; i++)
            {
                SpawnerDef sp = day.Spawners[i];

                // First group arrives quickly so the day never opens on an empty screen.
                _nextAt.Add(sp.From + 0.25f);
                _quota.Add(Mathf.CeilToInt(total * Mathf.Max(0f, sp.BudgetPct) * 0.01f));
            }
        }

        public void Tick(float dt)
        {
            RunModel run = _ctx.Run;
            DayDef day = run.Day;
            if (day == null)
            {
                return;
            }

            TickFixed(day, run);
            TickSpawners(day, run);
            DrainDebt(day, run);
        }

        void TickFixed(DayDef day, RunModel run)
        {
            for (int i = 0; i < day.Fixed.Count; i++)
            {
                if (_firedFixed.Contains(i) || run.DayElapsed < day.Fixed[i].AtSecond)
                {
                    continue;
                }

                _firedFixed.Add(i);

                FixedSpawnDef f = day.Fixed[i];
                EnemyDef def = _ctx.Cfg.Enemy(f.EnemyId);
                if (def == null)
                {
                    continue;
                }

                // Fixed rows ignore concurrentMax on purpose: an elite that never shows up
                // because the screen was full would break the pacing guarantees.
                _band.BeginBurst();
                for (int n = 0; n < f.Count; n++)
                {
                    if (f.Entrance)
                    {
                        Entrance(def, f.GuaranteeDrop);
                    }
                    else
                    {
                        Spawn(def, _band.NextPoint(run.Player.Pos), f.GuaranteeDrop);
                    }
                }
            }
        }

        /// <summary>
        /// Elites land in plain sight instead of walking in from off screen, because an elite that
        /// strolls in from the edge reads as a slightly larger mail. The landing is warned for a
        /// second and a half and the shockwave deals nothing, so the ceremony costs no sanity.
        /// </summary>
        void Entrance(EnemyDef def, Quality? drop)
        {
            RunModel run = _ctx.Run;
            ArenaDef arena = _ctx.Cfg.Arena;

            // Derived from the shorter camera axis rather than hard coded, because "in plain sight" is
            // the entire point. A fixed radius that clears the width still lands off the top of a
            // widescreen frame, and an entrance the player never saw is just a mob that teleported in.
            float reach = _ctx.Cfg.Camera.OrthographicSize * 0.8f;

            float angle = Random.value * Mathf.PI * 2f;
            Vector2 pos = run.Player.Pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
            pos.x = Mathf.Clamp(pos.x, -arena.HalfWidth + 1f, arena.HalfWidth - 1f);
            pos.y = Mathf.Clamp(pos.y, -arena.HalfHeight + 1f, arena.HalfHeight - 1f);

            TelegraphModel t = run.RentTelegraph();
            t.Pos = pos;
            t.Radius = 1.4f;
            t.BornAt = GameClock.Now;
            t.FireAt = GameClock.Now + 1.5f;
            t.SummonEnemyId = def.Id;
            t.SummonCount = 1;
            t.SummonDrop = drop;
            t.ViewId = "v_warn_elite";
        }

        void TickSpawners(DayDef day, RunModel run)
        {
            if (_nextAt.Count != day.Spawners.Count)
            {
                OnDayBegin();
            }

            int alive = run.AliveEnemies;

            for (int s = 0; s < day.Spawners.Count; s++)
            {
                SpawnerDef sp = day.Spawners[s];
                if (run.DayElapsed < _nextAt[s] || run.DayElapsed > sp.To || _quota[s] <= 0)
                {
                    continue;
                }

                _nextAt[s] = _nextAt[s] + sp.Interval;

                // Enemies arrive in groups every couple of seconds. An even drip reads as sparse no
                // matter how many spawn in total, and being swamped is the only pressure this has.
                _band.BeginBurst();

                for (int n = 0; n < sp.GroupSize; n++)
                {
                    if (_quota[s] <= 0 || run.SpawnedToday >= day.TotalSpawn)
                    {
                        break;
                    }

                    EnemyDef def = PickEnemy(sp);
                    if (def == null)
                    {
                        break;
                    }

                    _quota[s]--;
                    run.SpawnedToday++;

                    if (alive >= day.ConcurrentMax)
                    {
                        // Owed, not discarded. Dropping it would mean the stronger the player gets the
                        // fewer enemies arrive, which inverts the pressure curve.
                        _debt.Add(def);
                        run.SpawnDebt = _debt.Count;
                        continue;
                    }

                    Spawn(def, _band.NextPoint(run.Player.Pos), null);
                    alive++;
                }
            }
        }

        void DrainDebt(DayDef day, RunModel run)
        {
            if (_debt.Count == 0)
            {
                return;
            }

            int alive = run.AliveEnemies;
            if (alive >= day.ConcurrentMax)
            {
                return;
            }

            _band.BeginBurst();
            int room = day.ConcurrentMax - alive;
            int take = Mathf.Min(room, Mathf.Min(_debt.Count, 6));

            for (int i = 0; i < take; i++)
            {
                Spawn(_debt[i], _band.NextPoint(run.Player.Pos), null);
            }

            _debt.RemoveRange(0, take);
            run.SpawnDebt = _debt.Count;
        }

        EnemyDef PickEnemy(SpawnerDef sp)
        {
            if (sp.Picks.Count == 0)
            {
                return null;
            }

            _weights.Clear();
            for (int i = 0; i < sp.Picks.Count; i++)
            {
                _weights.Add(sp.Picks[i].Weight);
            }

            int idx = Rng.WeightedPick(_weights);
            return idx < 0 ? null : _ctx.Cfg.Enemy(sp.Picks[idx].EnemyId);
        }

        /// <summary>Boss phase transitions dump a wave of trash in one call.</summary>
        public void SpawnBurst(EnemyDef def, int count)
        {
            _band.BeginBurst();
            for (int i = 0; i < count; i++)
            {
                Spawn(def, _band.NextPoint(_ctx.Run.Player.Pos), null);
            }
        }

        public EnemyModel Spawn(EnemyDef def, Vector2 pos, Quality? guaranteedDrop)
        {
            RunModel run = _ctx.Run;
            ArenaDef arena = _ctx.Cfg.Arena;
            SpawnBandDef band = _ctx.Cfg.Band;
            float now = GameClock.Now;

            pos.x = Mathf.Clamp(pos.x, -arena.HalfWidth, arena.HalfWidth);
            pos.y = Mathf.Clamp(pos.y, -arena.HalfHeight, arena.HalfHeight);

            EnemyModel e = run.RentEnemy();
            e.DefId = def.Id;
            e.Def = def;
            e.Pos = pos;

            float hpScale = def.IgnoreScaling ? 1f : run.HpScale;
            float dmgScale = def.IgnoreScaling ? 1f : run.DmgScale;

            e.MaxHp = def.Hp * hpScale;
            e.Hp = e.MaxHp;
            e.ContactDamage = def.ContactDamage * dmgScale;
            e.Radius = def.Radius;
            e.SpawnedAt = now;
            e.GuaranteedDrop = guaranteedDrop;

            if (def.Tier == EnemyTier.Boss)
            {
                e.BarsTotal = Mathf.Max(1, (int)def.Param.GetFloat("bars", 3f));
                e.BarsLeft = e.BarsTotal;
                e.Phase = 1;
                run.BossBarsTotal = e.BarsTotal;
                run.BossBarsLeft = e.BarsLeft;
            }

            // The universal safety net, applied at the one place every enemy is born. Split BUGs,
            // boss summons and anything added later inherit it without touching their own code.
            if (band.GraceSeconds > 0f &&
                (pos - run.Player.Pos).sqrMagnitude <= band.GraceRadius * band.GraceRadius)
            {
                e.ContactArmedAt = now + band.GraceSeconds;
            }

            IEnemyBehavior behavior = EnemyBehaviorRegistry.Get(def.Behavior);
            if (behavior != null)
            {
                behavior.OnSpawn(e, _ctx);
            }

            EvtArg a = new EvtArg();
            a.I0 = e.Id;
            a.I1 = (int)def.Tier;
            a.P0 = pos;
            a.O0 = e;
            _ctx.Bus.Dispatch(EventID.EnemySpawned, a);
            return e;
        }
    }
}
