using System.Collections.Generic;
using System.Globalization;
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
        static readonly char[] RosterSep = { ',' };
        static readonly char[] RosterWeightSep = { ':' };

        readonly GameContext _ctx;
        readonly SpawnBand _band;
        readonly List<float> _weights = new List<float>(8);
        readonly List<EnemyDef> _roster = new List<EnemyDef>(6);
        readonly List<float> _rosterWeights = new List<float>(6);
        readonly List<float> _nextAt = new List<float>(4);
        readonly List<int> _budget = new List<int>(4);
        readonly List<int> _released = new List<int>(4);
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
            _budget.Clear();
            _released.Clear();
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
                _budget.Add(Mathf.CeilToInt(total * Mathf.Max(0f, sp.BudgetPct) * 0.01f));
                _released.Add(0);
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
            Vector2 pos = arena.Clamp(run.Player.Pos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach, 1f);

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
                if (run.DayElapsed < _nextAt[s] || run.DayElapsed > sp.To)
                {
                    continue;
                }

                _nextAt[s] = _nextAt[s] + sp.Interval;

                int owed = Owed(sp, s, run.DayElapsed);

                if (_released[s] == 0)
                {
                    // The day must never open on an empty screen, so the opening release always pays a
                    // whole group even before the schedule has accrued one. It is borrowed rather than
                    // added: nothing is owed again until the schedule catches up, and the window still
                    // spends exactly its budget.
                    owed = Mathf.Max(owed, sp.GroupSize);
                }
                else if (owed < sp.GroupSize && _released[s] + owed < _budget[s])
                {
                    // Enemies arrive in groups. An even drip reads as sparse no matter how many arrive
                    // in total, and being swamped is the only pressure this game has, so the schedule
                    // is allowed to run behind until it can pay a whole group at once. That is also
                    // what makes the ramp show up as shorter gaps rather than a thickening trickle.
                    continue;
                }

                owed = Mathf.Min(owed, _budget[s] - _released[s]);
                if (owed <= 0)
                {
                    continue;
                }

                _band.BeginBurst();

                for (int n = 0; n < owed; n++)
                {
                    if (run.SpawnedToday >= day.TotalSpawn)
                    {
                        break;
                    }

                    EnemyDef def = PickEnemy(sp);
                    if (def == null)
                    {
                        break;
                    }

                    _released[s]++;
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

        /// <summary>
        /// How many enemies this spawner is behind its schedule right now.
        ///
        /// The budget used to be drained at a fixed group size every fixed interval, which spends it
        /// far faster than the day is long: every day ran out of enemies around two thirds through and
        /// the rest of the shift was spent walking around an empty office waiting for the clock. Paying
        /// against a cumulative schedule instead spends the budget exactly at the closing bell, for any
        /// combination of interval, group size and window a designer writes.
        ///
        /// The schedule ramps. Arrival rate climbs linearly from 2/(1+ramp) to 2*ramp/(1+ramp) times
        /// the average, so it integrates to exactly one budget over the window while the last hour is
        /// the busy one. The rate is what ramps and the count is what stays pinned, never the reverse:
        /// a ramp applied to the count would quietly change how many enemies a day produces, and the
        /// exp curve, the KPI target and the drop economy are all solved against that number.
        /// </summary>
        int Owed(SpawnerDef sp, int index, float elapsed)
        {
            // Read one interval ahead, because a tick pays for the period that follows it rather than
            // the one behind it. Without the lead the opening tick of every window is owed nothing and
            // the first group lands one interval late, which on Monday is most of the margin on the
            // ten second first upgrade.
            float span = Mathf.Max(0.01f, sp.To - sp.From);
            float u = Mathf.Clamp01((elapsed - sp.From + sp.Interval) / span);

            float ramp = Mathf.Max(0.05f, sp.Ramp);
            float startRate = 2f / (1f + ramp);
            float progress = startRate * u + startRate * (ramp - 1f) * u * u * 0.5f;

            int due = Mathf.RoundToInt(_budget[index] * Mathf.Clamp01(progress));
            return Mathf.Max(0, due - _released[index]);
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

        /// <summary>
        /// A behaviorParam roster, written "mail:5,deadline:3,bug:2". A bare id still works and weighs
        /// one, so every roster that used to be a single id reads the same.
        ///
        /// Rebuilt on every call rather than cached against the raw string. The strings do repeat, but
        /// a hot reload hands out new <see cref="EnemyDef"/> instances behind identical text, and a
        /// cache keyed on that text would keep spawning the previous table's numbers.
        ///
        /// The two scratch lists live only until the next roster call, so nothing reached from inside
        /// a spawn loop may draw from a roster of its own. No behaviour does today: OnSpawn either
        /// dispatches or does nothing, and the one behaviour that spawns does it on death.
        /// </summary>
        void BuildRoster(string raw)
        {
            _roster.Clear();
            _rosterWeights.Clear();

            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            string[] entries = raw.Split(RosterSep);
            for (int i = 0; i < entries.Length; i++)
            {
                string[] parts = entries[i].Split(RosterWeightSep, 2);

                EnemyDef def = _ctx.Cfg.Enemy(parts[0].Trim());
                if (def == null)
                {
                    continue;
                }

                float weight = 1f;
                if (parts.Length == 2 &&
                    !float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out weight))
                {
                    weight = 1f;
                }

                if (weight <= 0f)
                {
                    continue;
                }

                _roster.Add(def);
                _rosterWeights.Add(weight);
            }
        }

        EnemyDef PickRoster()
        {
            int idx = _roster.Count == 0 ? -1 : Rng.WeightedPick(_rosterWeights);
            return idx < 0 ? null : _roster[idx];
        }

        /// <summary>
        /// Boss phase transitions dump a wave of trash in one call. Deliberately blind to
        /// concurrentMax: the wave is the punctuation of the fight, and a break that quietly produced
        /// nothing because the field was full would read as the bar having no consequence.
        /// </summary>
        public void SpawnBurst(string roster, int count)
        {
            BuildRoster(roster);
            if (_roster.Count == 0)
            {
                return;
            }

            _band.BeginBurst();
            for (int i = 0; i < count; i++)
            {
                EnemyDef def = PickRoster();
                if (def != null)
                {
                    Spawn(def, _band.NextPoint(_ctx.Run.Player.Pos), null);
                }
            }
        }

        /// <summary>
        /// One draw from a roster, for callers that place their own telegraph rather than spawning
        /// straight away. The boss summon warns the ground first, so it needs the id, not the body.
        /// </summary>
        public EnemyDef PickFromRoster(string roster)
        {
            BuildRoster(roster);
            return PickRoster();
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
