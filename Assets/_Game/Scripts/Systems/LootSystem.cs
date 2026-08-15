using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>
    /// Drops, the toss arc, the magnet and pickup. Money is gone, so every drop is either a coffee or
    /// a piece of gear, and a piece of gear the player cannot use converts to experience rather than
    /// to nothing: without that patch a floor covered in green items is noise instead of a reward.
    ///
    /// Two independent channels feed quality: a weighted random roll for green/blue/purple and a
    /// scripted channel for legendaries (Fixed guarantees plus the pity timer). Keeping orange out
    /// of the random pool is what makes the pacing in the design doc actually reproducible.
    /// </summary>
    public sealed class LootSystem
    {
        static readonly float[] BouncePhase = { 0.62f, 0.24f, 0.14f };
        static readonly float[] BounceHeight = { 1.0f, 0.24f, 0.08f };

        readonly GameContext _ctx;
        readonly List<float> _weights = new List<float>(4);
        readonly List<int> _affixPool = new List<int>(16);

        public LootSystem(GameContext ctx)
        {
            _ctx = ctx;
            _ctx.Bus.Register(EventID.EnemyKilled, OnEnemyKilled);
        }

        public void Dispose()
        {
            _ctx.Bus.Unregister(EventID.EnemyKilled, OnEnemyKilled);
        }

        void OnEnemyKilled(EvtArg arg)
        {
            EnemyModel e = arg.O0 as EnemyModel;
            if (e == null || e.Def == null)
            {
                return;
            }

            RollCoffee(e);
            RollEquipment(e);
        }

        /// <summary>
        /// Doubling the coffee rate at low sanity is anti death spiral. The player never notices it,
        /// but "this run was hopeless from the start" is the most common reason a demo gets closed.
        /// </summary>
        void RollCoffee(EnemyModel e)
        {
            CoffeeDef cf = _ctx.Cfg.Coffee;
            PlayerModel p = _ctx.Run.Player;

            float max = p.MaxSan;
            bool low = max > 0f && p.San < max * cf.LowSanThresholdPct * 0.01f;
            float chance = low ? cf.LowSanChancePct : cf.ChancePct;

            if (Rng.ChancePercent(chance))
            {
                SpawnCoffee(e.Pos);
            }
        }

        void RollEquipment(EnemyModel e)
        {
            RunModel run = _ctx.Run;
            LootDef loot = _ctx.Cfg.Loot;

            Quality? forced = e.GuaranteedDrop;

            if (!forced.HasValue)
            {
                // Tight before the first legendary, looser afterwards. The first one has to land inside
                // the window a judge actually watches; after that a flood would kill the chase.
                float threshold = run.AnyLegendaryDropped
                    ? loot.PityLegendarySeconds
                    : loot.PityFirstLegendarySeconds;

                if (run.SecondsSinceLastLegendary >= threshold)
                {
                    forced = Quality.Orange;
                }
            }

            bool wantEquipment = forced.HasValue
                                 || e.Def.Tier != EnemyTier.Normal
                                 || Rng.ChancePercent(loot.EquipChancePct);

            if (!wantEquipment)
            {
                return;
            }

            Quality quality = forced.HasValue ? forced.Value : RollQuality();

            if (Rng.ChancePercent(WeaponSharePct()))
            {
                SpawnWeapon(e.Pos, quality);
            }
            else
            {
                SpawnArmor(e.Pos, quality);
            }

            if (quality == Quality.Orange)
            {
                // Reset must happen here, not on pickup. Forgetting it is the classic way this
                // rule turns into a legendary machine gun.
                run.SecondsSinceLastLegendary = 0f;
                run.AnyLegendaryDropped = true;
            }
        }

        /// <summary>Six weapon slots against three armour slots, so weapons have to drop more often.</summary>
        float WeaponSharePct()
        {
            LootDef loot = _ctx.Cfg.Loot;
            float total = loot.WeaponShare + loot.ArmorShare;
            return total <= 0f ? 100f : loot.WeaponShare / total * 100f;
        }

        Quality RollQuality()
        {
            LootDef loot = _ctx.Cfg.Loot;
            float luck = _ctx.Run.Player.Stats.Get(StatType.Luck);

            _weights.Clear();
            for (int i = 0; i < loot.Qualities.Length; i++)
            {
                _weights.Add(CombatFormula.LootWeight(loot.Qualities[i], luck, _ctx.Run.DayIndex, loot));
            }

            int idx = Rng.WeightedPick(_weights);
            return idx < 0 ? Quality.Green : (Quality)idx;
        }

        // ---------- spawners ----------

        public LootModel SpawnCoffee(Vector2 from)
        {
            LootModel l = NewLoot(from);
            l.Kind = LootKind.Coffee;
            l.Quality = Quality.Green;
            l.Name = "咖啡";
            l.ViewId = _ctx.Cfg.Coffee.ViewId;

            Dispatch(EventID.LootDropped, l);
            return l;
        }

        public LootModel SpawnWeapon(Vector2 from, Quality quality)
        {
            List<string> order = _ctx.Cfg.WeaponOrder;
            if (order.Count == 0)
            {
                return SpawnCoffee(from);
            }

            WeaponDef def = _ctx.Cfg.Weapon(order[Random.Range(0, order.Count)]);
            if (def == null)
            {
                return SpawnCoffee(from);
            }

            LootModel l = NewLoot(from);
            l.Kind = LootKind.Weapon;
            l.Quality = quality;
            l.Slot = EquipSlot.Weapon;
            l.SourceDefId = def.Id;
            l.ViewId = def.ViewId;
            l.Name = QualityWord(quality) + def.Name;

            Dispatch(EventID.LootDropped, l);
            return l;
        }

        public LootModel SpawnArmor(Vector2 from, Quality quality)
        {
            List<ArmorBaseDef> bases = _ctx.Cfg.Loot.ArmorBases;
            if (bases.Count == 0)
            {
                return SpawnWeapon(from, quality);
            }

            ArmorBaseDef def = bases[Random.Range(0, bases.Count)];
            QualityDef qd = _ctx.Cfg.QualityOf(quality);
            float coef = _ctx.Cfg.WeaponQuality.Get(quality);

            LootModel l = NewLoot(from);
            l.Kind = LootKind.Armor;
            l.Quality = quality;
            l.Slot = def.Slot;
            l.SourceDefId = def.Id;
            l.ViewId = def.ViewId;

            int sourceId = _ctx.Run.NextSourceId();

            for (int i = 0; i < def.Mains.Count; i++)
            {
                ArmorStatDef m = def.Mains[i];
                AddMod(l, (StatType)(int)m.Stat, m.Base, coef, m.Percent, sourceId, null);
            }

            string firstAffix = RollAffixes(l, qd.AffixCount, coef, sourceId);
            l.Name = (firstAffix != null ? firstAffix : QualityWord(quality)) + def.Name;

            Dispatch(EventID.LootDropped, l);
            return l;
        }

        /// <summary>
        /// Affixes are drawn without replacement, which is why the pool is shuffled by index rather
        /// than sampled: two "卷王的" rolls on one item would read as a bug.
        /// </summary>
        string RollAffixes(LootModel l, int count, float coef, int sourceId)
        {
            List<AffixDef> pool = _ctx.Cfg.Loot.Affixes;
            if (pool.Count == 0 || count <= 0)
            {
                return null;
            }

            _affixPool.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                _affixPool.Add(i);
            }

            string first = null;
            int take = Mathf.Min(count, _affixPool.Count);

            for (int i = 0; i < take; i++)
            {
                int swap = Random.Range(i, _affixPool.Count);
                int tmp = _affixPool[i];
                _affixPool[i] = _affixPool[swap];
                _affixPool[swap] = tmp;

                AffixDef def = pool[_affixPool[i]];
                AddMod(l, (StatType)(int)def.Stat, def.Base, coef, def.Percent, sourceId, def.Name);

                if (first == null)
                {
                    first = def.Name;
                }
            }

            return first;
        }

        void AddMod(
            LootModel l,
            StatType stat,
            float baseValue,
            float coef,
            bool percent,
            int sourceId,
            string label)
        {
            float value = Mathf.Round(CombatFormula.RollStatValue(baseValue, coef));
            if (value <= 0f)
            {
                value = 1f;
            }

            l.Mods.Add(new StatModifier(
                stat, percent ? ModifierOp.PercentAdd : ModifierOp.Flat, value, sourceId));

            string name = label ?? StatLabel(stat);
            l.AffixNames.Add(name + " +" + value.ToString("0") + (percent ? "%" : ""));
        }

        static string StatLabel(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxSan: return "SAN 上限";
                case StatType.Atk: return "攻击力";
                case StatType.CritChance: return "暴击率";
                case StatType.CritMulti: return "暴击伤害";
                case StatType.Def: return "防御";
                case StatType.Dodge: return "闪避";
                case StatType.MoveSpeed: return "移速";
                case StatType.Haste: return "攻速";
                case StatType.Luck: return "幸运";
                case StatType.PickupRadius: return "拾取范围";
                default: return stat.ToString();
            }
        }

        static string QualityWord(Quality q)
        {
            switch (q)
            {
                case Quality.Blue: return "蓝色";
                case Quality.Purple: return "紫色";
                case Quality.Orange: return "橙色";
                default: return "绿色";
            }
        }

        LootModel NewLoot(Vector2 from)
        {
            ArenaDef arena = _ctx.Cfg.Arena;

            // Loot is thrown out, never placed. A static spawn has no weight to it.
            Vector2 to = from + Rng.RingPoint(Vector2.zero, 0.4f, 1.6f);
            to.x = Mathf.Clamp(to.x, -arena.HalfWidth, arena.HalfWidth);
            to.y = Mathf.Clamp(to.y, -arena.HalfHeight, arena.HalfHeight);

            LootModel l = _ctx.Run.RentLoot();
            l.From = from;
            l.To = to;
            l.Pos = from;
            l.TossT = 0f;
            l.BornAt = GameClock.Now;
            l.State = LootState.Tossing;
            return l;
        }

        void Dispatch(EventID id, LootModel l)
        {
            EvtArg a = new EvtArg();
            a.I0 = l.Id;
            a.I1 = (int)l.Quality;
            a.P0 = l.Pos;
            a.O0 = l;
            _ctx.Bus.Dispatch(id, a);
        }

        // ---------- movement and pickup ----------

        public void Tick(float dt)
        {
            RunModel run = _ctx.Run;
            LootDef loot = _ctx.Cfg.Loot;
            PlayerModel player = run.Player;

            float magnet = player.MagnetRadius;
            float step = _ctx.Cfg.Player.StepPickupRadius;
            float tossTotal = loot.TossDuration * 1.6f;

            for (int i = 0; i < run.Loots.Count; i++)
            {
                LootModel l = run.Loots[i];
                if (l.IsDead)
                {
                    continue;
                }

                switch (l.State)
                {
                    case LootState.Tossing:
                        l.TossT += dt / tossTotal;
                        if (l.TossT >= 1f)
                        {
                            l.TossT = 1f;
                            l.Pos = l.To;
                            l.State = LootState.Idle;
                        }
                        else
                        {
                            l.Pos = TossPosition(l);
                        }

                        break;

                    case LootState.Idle:
                        if (InReach(l, player, magnet, step))
                        {
                            l.State = LootState.Magnet;
                        }

                        break;

                    case LootState.Magnet:
                        Vector2 delta = player.Pos - l.Pos;
                        float dist = delta.magnitude;

                        // Collect on "this frame's travel reaches the player", not on a fixed radius.
                        // The travel is 14 * dt, which is 0.23 at 60fps against a 0.25 radius: it cleared
                        // the test by a hair at full speed and stopped clearing it the moment the frame
                        // time grew, so a drop would jump past the player, be pulled back past them next
                        // frame and orbit forever. The player could only break the loop by moving, which
                        // is why it looked like the magnet needed a couple of passes to catch.
                        float travel = loot.MagnetSpeed * dt;
                        if (dist <= Mathf.Max(0.25f, travel))
                        {
                            Collect(l);
                        }
                        else
                        {
                            l.Pos += delta / dist * travel;
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Green, blue and coffee snap in so walking is never interrupted. Purple and orange have to
        /// be stepped on: that short run is the anticipation beat, and smoothing it away would remove
        /// the memory of having gone to get the legendary.
        /// </summary>
        bool InReach(LootModel l, PlayerModel player, float magnet, float step)
        {
            float radius = l.AutoMagnet ? magnet : Mathf.Max(step, player.Radius + 0.2f);
            return (player.Pos - l.Pos).sqrMagnitude <= radius * radius;
        }

        Vector2 TossPosition(LootModel l)
        {
            LootDef loot = _ctx.Cfg.Loot;
            int bounces = Mathf.Clamp(loot.BounceCount + 1, 1, BouncePhase.Length);

            float t = l.TossT;
            float ground = Mathf.Min(1f, t / BouncePhase[0]);
            Vector2 flat = Vector2.Lerp(l.From, l.To, ground);

            float cursor = 0f;
            float height = 0f;
            for (int i = 0; i < bounces; i++)
            {
                float phase = BouncePhase[i];
                if (t <= cursor + phase)
                {
                    float local = (t - cursor) / phase;
                    height = Mathf.Sin(local * Mathf.PI) * BounceHeight[i];
                    break;
                }

                cursor += phase;
            }

            return flat + Vector2.up * height;
        }

        /// <summary>
        /// Used by the equipment card, which hands the item over rather than dropping it. Routing it
        /// through the same collect path means the change of gear feedback is identical either way.
        /// </summary>
        public void CollectNow(LootModel l)
        {
            if (l != null && !l.IsDead)
            {
                Collect(l);
            }
        }

        void Collect(LootModel l)
        {
            RunModel run = _ctx.Run;
            l.IsDead = true;

            switch (l.Kind)
            {
                case LootKind.Coffee:
                    DrinkCoffee(l);
                    break;

                case LootKind.Weapon:
                    EquipWeapon(l);
                    break;

                case LootKind.Armor:
                    EquipArmor(l);
                    break;
            }

            Dispatch(EventID.LootPicked, l);
        }

        void DrinkCoffee(LootModel l)
        {
            CoffeeDef cf = _ctx.Cfg.Coffee;
            PlayerModel p = _ctx.Run.Player;

            float before = p.San;
            p.San = Mathf.Min(p.MaxSan, p.San + p.MaxSan * cf.HealPctMaxSan * 0.01f);

            p.HasteBuffPct = Mathf.Max(p.HasteBuffPct, cf.HasteAddPct);
            p.HasteBuffUntil = GameClock.Now + cf.BuffSeconds;

            EvtArg a = new EvtArg();
            a.F0 = p.San - before;
            a.P0 = p.Pos;
            _ctx.Bus.Dispatch(EventID.CoffeeDrunk, a);
        }

        /// <summary>
        /// Quality decides, nothing else. It is a safe judge because a higher tier is both bigger
        /// numbers and an unlocked behaviour, so it can never be a sidegrade with worse affixes.
        /// </summary>
        void EquipWeapon(LootModel l)
        {
            PlayerModel p = _ctx.Run.Player;
            WeaponDef def = _ctx.Cfg.Weapon(l.SourceDefId);
            if (def == null)
            {
                return;
            }

            int empty = p.FirstEmptySlot();
            if (empty >= 0)
            {
                Fit(l, empty, def, null);
                return;
            }

            int worst = p.LowestQualityWeaponSlot();
            if (worst >= 0 && l.Quality > p.Weapons[worst].Quality)
            {
                Fit(l, worst, def, p.Weapons[worst].Def != null ? p.Weapons[worst].Def.Name : null);
                return;
            }

            Decline(l);
        }

        void Fit(LootModel l, int slot, WeaponDef def, string replaced)
        {
            PlayerModel p = _ctx.Run.Player;
            p.Equip(slot, def, l.Quality);
            NoteBest(l);

            EvtArg a = new EvtArg();
            a.I0 = slot;
            a.I1 = (int)l.Quality;
            a.O0 = l;
            a.F0 = 0f;
            _ctx.Bus.Dispatch(EventID.WeaponEquipped, a);

            if (replaced != null)
            {
                l.AffixNames.Add("替换 " + replaced);
            }
        }

        void EquipArmor(LootModel l)
        {
            PlayerModel p = _ctx.Run.Player;
            ArmorBaseDef def = _ctx.Cfg.Armor(l.SourceDefId);
            ArmorRuntime rt = p.Armor(l.Slot);
            if (def == null || rt == null)
            {
                return;
            }

            if (!rt.IsEmpty && l.Quality <= rt.Quality)
            {
                Decline(l);
                return;
            }

            string replaced = rt.IsEmpty ? null : rt.Name;

            if (!rt.IsEmpty)
            {
                p.Stats.RemoveBySource(rt.SourceId);
            }

            int sourceId = l.Mods.Count > 0 ? l.Mods[0].SourceId : _ctx.Run.NextSourceId();
            for (int i = 0; i < l.Mods.Count; i++)
            {
                p.Stats.AddModifier(l.Mods[i]);
            }

            rt.DefId = def.Id;
            rt.Def = def;
            rt.Quality = l.Quality;
            rt.Name = l.Name;
            rt.SourceId = sourceId;

            if (l.Slot == EquipSlot.Head)
            {
                // Blue headphone starts its shield cycle the moment it is worn. The orange save is
                // armed here and nowhere else: it used to refill at every day boundary, which is six
                // free deaths across a run and leaves the SAN bar with nothing to threaten.
                p.NextShieldAt = GameClock.Now;
                p.DeathSaveReady = l.Quality >= Quality.Orange;
            }
            else if (l.Slot == EquipSlot.Body)
            {
                // Counting from zero, so the guard cannot fire on the first hit after a late pickup.
                p.HitsSinceGuard = 0;
            }

            NoteBest(l);

            EvtArg a = new EvtArg();
            a.I0 = (int)l.Slot;
            a.I1 = (int)l.Quality;
            a.O0 = l;
            _ctx.Bus.Dispatch(EventID.ArmorEquipped, a);

            if (replaced != null)
            {
                l.AffixNames.Add("替换 " + replaced);
            }
        }

        /// <summary>
        /// A drop that cannot beat what is worn still has to be worth walking over. Without this the
        /// late game floor is covered in items that do nothing, and the drop feed becomes noise.
        /// </summary>
        void Decline(LootModel l)
        {
            int exp = _ctx.Cfg.Progression.DowngradeExp;

            EvtArg a = new EvtArg();
            a.I0 = exp;
            a.I1 = (int)l.Quality;
            a.P0 = l.Pos;
            a.O0 = l;
            _ctx.Bus.Dispatch(EventID.EquipDeclined, a);
        }

        void NoteBest(LootModel l)
        {
            RunModel run = _ctx.Run;
            if (!run.AnyLootPicked || l.Quality >= run.BestQuality)
            {
                run.BestQuality = l.Quality;
                run.BestLootName = l.Name;
            }

            run.AnyLootPicked = true;
        }
    }
}
