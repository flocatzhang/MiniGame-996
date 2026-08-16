using System.Collections.Generic;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using UnityEngine;

namespace OfficeHell.Systems
{
    /// <summary>One offered card. Flattened on purpose so the panel needs no config lookups.</summary>
    public sealed class CardOffer
    {
        public CardKind Kind;
        public string Id;
        public string Title;
        public string Desc;

        public StatKey Stat;
        public float Value;
        public float Value2;
        public bool Percent;

        public string Passive;

        public Quality Quality;
        public bool IsWeapon;
        public string DefId;

        /// <summary>
        /// Set when this card is a higher tier of something already owned, so the panel can say
        /// 升级 rather than offering what looks like a duplicate.
        /// </summary>
        public bool IsUpgrade;
        public Quality OwnedQuality;
    }

    /// <summary>
    /// The three card choice, which after the shop was removed is the player's only decision.
    ///
    /// Equipment cards exist for exactly that reason: with drops fully automatic, a pool of pure stat
    /// cards would leave the player watching their build rather than steering it.
    /// </summary>
    public sealed class CardSystem
    {
        readonly GameContext _ctx;
        readonly LootSystem _loot;
        readonly List<CardOffer> _offers = new List<CardOffer>(4);
        readonly List<float> _kindWeights = new List<float>(3);
        readonly List<int> _pool = new List<int>(24);

        /// <summary>
        /// Highest tier of each stat card already taken. A taken card used to leave the pool for good,
        /// which was correct while there was one version of it and wrong the moment there are four:
        /// a green 攻击力 on Monday would lock the orange one out for the rest of the week.
        /// </summary>
        readonly Dictionary<string, Quality> _takenStats = new Dictionary<string, Quality>(16);

        /// <summary>Scripted weapon hands already delivered, by the level that triggered them.</summary>
        readonly HashSet<int> _handsDone = new HashSet<int>();

        public CardSystem(GameContext ctx, LootSystem loot)
        {
            _ctx = ctx;
            _loot = loot;
        }

        public List<CardOffer> Offers
        {
            get { return _offers; }
        }

        public void Reset()
        {
            _offers.Clear();
            _takenStats.Clear();
            _handsDone.Clear();
        }

        /// <summary>Builds a fresh hand. Duplicate ids inside one hand are never offered.</summary>
        public void Offer()
        {
            CardPoolDef pool = _ctx.Cfg.Cards;
            _offers.Clear();

            int want = Mathf.Max(1, pool.Choices);

            if (!OfferWeaponHand(pool, want))
            {
                int guard = 0;

                while (_offers.Count < want && guard++ < 40)
                {
                    CardKind kind = PickKind(pool);
                    CardOffer offer = Build(kind);

                    if (offer == null || Contains(offer.Id))
                    {
                        continue;
                    }

                    _offers.Add(offer);
                }
            }

            EvtArg a = new EvtArg();
            a.I0 = _offers.Count;
            a.O0 = _offers;
            _ctx.Bus.Dispatch(EventID.CardsOffered, a);
        }

        /// <summary>
        /// Replaces the draw with one card per weapon at a fixed tier, on the levels Cards.xml
        /// reserves. Bails rather than part filling: three weapons side by side is the whole point,
        /// and two of them plus whatever the draw produced is a different hand wearing its name.
        /// </summary>
        bool OfferWeaponHand(CardPoolDef pool, int want)
        {
            WeaponHandDef hand = PendingWeaponHand(pool);
            if (hand == null)
            {
                return false;
            }

            List<string> order = _ctx.Cfg.WeaponOrder;

            for (int i = 0; i < order.Count && _offers.Count < want; i++)
            {
                CardOffer offer = BuildWeapon(_ctx.Cfg.Weapon(order[i]), hand.Quality);
                if (offer != null && !Contains(offer.Id))
                {
                    _offers.Add(offer);
                }
            }

            if (_offers.Count < want)
            {
                _offers.Clear();
                return false;
            }

            _handsDone.Add(hand.Level);
            return true;
        }

        /// <summary>
        /// The lowest reserved level already reached and not yet spent. Reached rather than equalled,
        /// because a double level up from 2 to 4 would otherwise delete the beat outright; lowest
        /// first, so a jump that comes due for both still shows blue before purple.
        /// </summary>
        WeaponHandDef PendingWeaponHand(CardPoolDef pool)
        {
            int level = _ctx.Run.Player.Level;
            WeaponHandDef best = null;

            for (int i = 0; i < pool.WeaponHands.Count; i++)
            {
                WeaponHandDef h = pool.WeaponHands[i];
                if (h.Level > level || _handsDone.Contains(h.Level))
                {
                    continue;
                }

                if (best == null || h.Level < best.Level)
                {
                    best = h;
                }
            }

            return best;
        }

        public void Pick(int index)
        {
            if (index < 0 || index >= _offers.Count)
            {
                return;
            }

            CardOffer offer = _offers[index];
            PlayerModel p = _ctx.Run.Player;

            switch (offer.Kind)
            {
                case CardKind.Stat:
                    Apply(p, offer);
                    _takenStats[offer.Id] = offer.Quality;
                    break;

                case CardKind.Skill:
                    p.GrantPassive(PassiveOf(offer.Passive), offer.Quality, offer.Value, offer.Value2);
                    break;

                case CardKind.Equipment:
                    Grant(offer);
                    break;
            }

            EvtArg a = new EvtArg();
            a.I0 = index;
            a.O0 = offer;
            _ctx.Bus.Dispatch(EventID.CardPicked, a);

            if (p.PendingLevelUps > 0)
            {
                p.PendingLevelUps--;
            }

            _offers.Clear();
        }

        void Apply(PlayerModel p, CardOffer offer)
        {
            StatType stat = (StatType)(int)offer.Stat;

            if (offer.Percent)
            {
                p.Stats.AddModifier(new StatModifier(stat, ModifierOp.PercentAdd, offer.Value, 0));
            }
            else
            {
                p.Stats.AddBase(stat, offer.Value);
            }

            // Raising the sanity ceiling has to top up current sanity too, otherwise the card reads
            // as doing nothing at the moment the player picks it.
            if (stat == StatType.MaxSan)
            {
                p.San = Mathf.Min(p.MaxSan, p.San + offer.Value);
            }
        }

        /// <summary>
        /// The card hands the item straight over instead of dropping it on the floor. A reward the
        /// player has to walk to could be missed, and a missed level up reward is the worst possible
        /// outcome for the one decision in the game.
        ///
        /// DefId is passed through because the spawners otherwise roll their own base: the card named
        /// 专家订书机 and then granted whichever of the three weapons came up, so two picks in three
        /// handed over something the player never chose.
        /// </summary>
        void Grant(CardOffer offer)
        {
            Vector2 at = _ctx.Run.Player.Pos;
            LootModel l = offer.IsWeapon
                ? _loot.SpawnWeapon(at, offer.Quality, offer.DefId)
                : _loot.SpawnArmor(at, offer.Quality, offer.DefId);

            _loot.CollectNow(l);
        }

        CardKind PickKind(CardPoolDef pool)
        {
            _kindWeights.Clear();
            _kindWeights.Add(HasStatCard() ? pool.StatWeight : 0f);
            _kindWeights.Add(pool.EquipWeight);
            _kindWeights.Add(HasPassive() ? pool.SkillWeight : 0f);

            int idx = Rng.WeightedPick(_kindWeights);
            return idx < 0 ? CardKind.Equipment : (CardKind)idx;
        }

        bool Contains(string id)
        {
            for (int i = 0; i < _offers.Count; i++)
            {
                if (_offers[i].Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        bool HasStatCard()
        {
            return HasRoom(CardKind.Stat, BestQualityToday());
        }

        bool HasPassive()
        {
            return HasRoom(CardKind.Skill, BestQualityToday());
        }

        /// <summary>
        /// Whether any card of this kind can still be handed out at or below the best tier today.
        /// Checked against the ceiling rather than the floor, because a card that only the upgrade
        /// roll can reach is still a card the pool can produce.
        /// </summary>
        bool HasRoom(CardKind kind, Quality ceiling)
        {
            List<CardDef> cards = _ctx.Cfg.Cards.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].Kind != kind)
                {
                    continue;
                }

                Quality had;
                if (!Owned(cards[i], out had) || had < ceiling)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the card is already taken, with the tier it was taken at.</summary>
        bool Owned(CardDef c, out Quality had)
        {
            had = Quality.Green;

            if (c.Kind == CardKind.Stat)
            {
                return _takenStats.TryGetValue(c.Id, out had);
            }

            SlackPassive flag = PassiveOf(c.Passive);
            if (flag == SlackPassive.None)
            {
                // An unmapped passive can never be granted, so treat it as permanently taken.
                had = Quality.Orange;
                return true;
            }

            if ((_ctx.Run.Player.Passives & flag) == 0)
            {
                return false;
            }

            had = _ctx.Run.Player.PassiveQuality(flag);
            return true;
        }

        Quality BestQualityToday()
        {
            CardPoolDef pool = _ctx.Cfg.Cards;
            int day = Mathf.Clamp(_ctx.Run.DayIndex, 1, pool.QualityByDay.Length - 1);
            Quality floor = pool.QualityByDay[day];
            return pool.UpgradeChanceByDay[day] > 0f ? Raise(floor) : floor;
        }

        /// <summary>
        /// The day sets the floor and a per card roll can lift it one tier. Three cards in one colour
        /// makes quality a restatement of the day counter, and then the tiering only shows up in the
        /// numbers rather than in the choice between them.
        /// </summary>
        Quality RollQuality()
        {
            CardPoolDef pool = _ctx.Cfg.Cards;
            int day = Mathf.Clamp(_ctx.Run.DayIndex, 1, pool.QualityByDay.Length - 1);
            Quality q = pool.QualityByDay[day];
            return Rng.ChancePercent(pool.UpgradeChanceByDay[day]) ? Raise(q) : q;
        }

        static Quality Raise(Quality q)
        {
            return q >= Quality.Orange ? Quality.Orange : (Quality)((int)q + 1);
        }

        CardOffer Build(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Stat: return BuildFromPool(CardKind.Stat);
                case CardKind.Skill: return BuildFromPool(CardKind.Skill);
                default: return BuildEquipment();
            }
        }

        CardOffer BuildFromPool(CardKind kind)
        {
            List<CardDef> cards = _ctx.Cfg.Cards.Cards;
            Quality q = RollQuality();

            _pool.Clear();
            for (int i = 0; i < cards.Count; i++)
            {
                CardDef c = cards[i];
                if (c.Kind != kind)
                {
                    continue;
                }

                Quality had;
                if (Owned(c, out had) && had >= q)
                {
                    continue;
                }

                _pool.Add(i);
            }

            if (_pool.Count == 0)
            {
                return null;
            }

            CardDef def = cards[_pool[Random.Range(0, _pool.Count)]];

            Quality ownedAt;
            bool upgrade = Owned(def, out ownedAt);
            float coef = _ctx.Cfg.WeaponQuality.Get(q);

            CardOffer offer = new CardOffer();
            offer.Kind = def.Kind;

            // Deliberately not tier qualified. Offer() dedupes on this, and two tiers of 攻击力 in one
            // hand is not a choice, it is the same card next to a strictly better copy of itself.
            offer.Id = def.Id;
            offer.Title = def.Name;
            offer.Stat = def.Stat;
            offer.Value = Round(def.Value * coef);
            offer.Value2 = Round(def.Value2 * coef);
            offer.Percent = def.Percent;
            offer.Passive = def.Passive;
            offer.Quality = q;
            offer.IsUpgrade = upgrade;
            offer.OwnedQuality = ownedAt;
            offer.Desc = Fill(def.Desc, offer.Value, offer.Value2);
            return offer;
        }

        /// <summary>
        /// One decimal, and the applied value is rounded the same way the card prints it. A card that
        /// says +9.6 and grants 9.5999999 is a card that lies, just not by enough to notice.
        /// </summary>
        static float Round(float v)
        {
            return Mathf.Round(v * 10f) * 0.1f;
        }

        static string Fill(string template, float v, float v2)
        {
            if (string.IsNullOrEmpty(template))
            {
                return string.Empty;
            }

            return template
                .Replace("{v2}", v2.ToString("0.##"))
                .Replace("{v}", v.ToString("0.##"));
        }

        /// <summary>
        /// Quality follows the day: green on Monday, blue by Wednesday, purple Friday, orange Saturday.
        /// It is the one growth line a player can predict, and a single predictable line is what keeps
        /// an otherwise fully random loot game from feeling arbitrary.
        ///
        /// The upgrade roll that stat and skill cards get is deliberately not applied here. Those two
        /// only move numbers, while a tier of equipment also unlocks a behaviour, so an early roll up
        /// would hand out a mechanic days before the rest of the table is tuned against it.
        /// </summary>
        CardOffer BuildEquipment()
        {
            CardPoolDef pool = _ctx.Cfg.Cards;
            int day = Mathf.Clamp(_ctx.Run.DayIndex, 1, pool.QualityByDay.Length - 1);
            Quality q = pool.QualityByDay[day];

            List<string> order = _ctx.Cfg.WeaponOrder;
            if (order.Count > 0 && Rng.ChancePercent(60f))
            {
                return BuildWeapon(_ctx.Cfg.Weapon(order[Random.Range(0, order.Count)]), q);
            }

            List<ArmorBaseDef> bases = _ctx.Cfg.Loot.ArmorBases;
            if (bases.Count == 0)
            {
                return null;
            }

            ArmorBaseDef armor = bases[Random.Range(0, bases.Count)];

            CardOffer offer = new CardOffer();
            offer.Kind = CardKind.Equipment;
            offer.Quality = q;
            offer.IsWeapon = false;
            offer.Id = "equip_" + armor.Id + "_" + q;
            offer.DefId = armor.Id;
            offer.Title = _ctx.Cfg.QualityOf(q).RankName + armor.Name;
            offer.Desc = ArmorBlurb(armor, q);
            return offer;
        }

        CardOffer BuildWeapon(WeaponDef def, Quality q)
        {
            if (def == null)
            {
                return null;
            }

            CardOffer offer = new CardOffer();
            offer.Kind = CardKind.Equipment;
            offer.Quality = q;
            offer.IsWeapon = true;
            offer.Id = "equip_" + def.Id + "_" + q;
            offer.DefId = def.Id;
            offer.Title = _ctx.Cfg.QualityOf(q).RankName + def.Name;

            // The card states the unlocked behaviour, not the damage number. Players pick these for
            // the effect, so a card that only shows numbers is selling the wrong thing.
            offer.Desc = TierBlurb(def, q);
            return offer;
        }

        public static string TierBlurb(WeaponDef def, Quality q)
        {
            WeaponTierDef t = def.Tier(q);
            System.Text.StringBuilder sb = new System.Text.StringBuilder(48);

            switch (def.Kind)
            {
                case WeaponKind.ProjectileLauncher:
                    sb.Append("发射 ").Append(t.ProjCount).Append(" 根");
                    if (t.Pierce > 0)
                    {
                        sb.Append(" · 穿透 ").Append(t.Pierce);
                    }

                    if (t.PinSeconds > 0f)
                    {
                        sb.Append(" · 钉住 ").Append(t.PinSeconds.ToString("0.#")).Append("s");
                    }

                    break;

                case WeaponKind.GroundAoe:
                    sb.Append("爆点 ").Append(t.BlastRadius.ToString("0.#"));
                    if (t.Slams > 1)
                    {
                        sb.Append(" · 连砸 ").Append(t.Slams).Append(" 下");
                    }

                    if (t.SlowPct > 0f)
                    {
                        sb.Append(" · 减速 ").Append(t.SlowPct.ToString("0")).Append("%");
                    }

                    if (t.SelectAllEvery > 0)
                    {
                        sb.Append(" · 每 ").Append(t.SelectAllEvery).Append(" 次 Ctrl+A 全选 ")
                          .Append(t.SelectAllRadius.ToString("0.#"));
                    }

                    break;

                case WeaponKind.Orbit:
                    sb.Append(t.OrbitCount).Append(" 张环绕");
                    if (t.Knockback > 0f)
                    {
                        sb.Append(" · 撞击击退");
                    }

                    if (t.TetherDamagePct > 0f)
                    {
                        sb.Append(" · 挂绳光环 ").Append(t.TetherDamagePct.ToString("0")).Append("%");
                    }

                    break;
            }

            return sb.ToString();
        }

        static string ArmorBlurb(ArmorBaseDef def, Quality q)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(48);
            for (int i = 0; i < def.Mains.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(" · ");
                }

                sb.Append(StatWord(def.Mains[i].Stat));
            }

            // Only the top unlocked effect is named. Listing all three makes the orange card three
            // lines long, and the player is reading these under a timer with the field still moving.
            switch (def.Slot)
            {
                case EquipSlot.Head:
                    if (q >= Quality.Orange) sb.Append(" · 护盾破碎音波反击 · 整局免死一次");
                    else if (q >= Quality.Purple) sb.Append(" · 护盾期间免疫控制");
                    else if (q >= Quality.Blue) sb.Append(" · 每 10s 获得 5s 护盾");
                    break;

                case EquipSlot.Body:
                    if (q >= Quality.Orange) sb.Append(" · 每 5 次受击免伤并击退");
                    else if (q >= Quality.Purple) sb.Append(" · SAN 低于 33% 时防御翻倍");
                    else if (q >= Quality.Blue) sb.Append(" · 受击反弹 20% 伤害");
                    break;

                case EquipSlot.Feet:
                    if (q >= Quality.Orange) sb.Append(" · 闪避成功向前位移");
                    else if (q >= Quality.Purple) sb.Append(" · 留下咖啡渍减速敌人");
                    else if (q >= Quality.Blue) sb.Append(" · 拾取范围 +50%");
                    break;
            }

            return sb.ToString();
        }

        static string StatWord(StatKey key)
        {
            switch (key)
            {
                case StatKey.MaxSan: return "SAN 上限";
                case StatKey.Atk: return "攻击力";
                case StatKey.CritChance: return "暴击率";
                case StatKey.Def: return "防御";
                case StatKey.Dodge: return "闪避";
                case StatKey.MoveSpeed: return "移速";
                case StatKey.Haste: return "攻速";
                case StatKey.Luck: return "幸运";
                case StatKey.PickupRadius: return "拾取范围";
                default: return key.ToString();
            }
        }

        public static SlackPassive PassiveOf(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return SlackPassive.None;
            }

            switch (id.ToLowerInvariant())
            {
                case "deepslack": return SlackPassive.DeepSlack;
                case "paidbreak": return SlackPassive.PaidBreak;
                case "reversepua": return SlackPassive.ReversePua;
                case "extralife": return SlackPassive.ExtraLife;
                case "massslack": return SlackPassive.MassSlack;
                default: return SlackPassive.None;
            }
        }
    }
}
