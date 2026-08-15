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
        public bool Percent;

        public string Passive;

        public Quality Quality;
        public bool IsWeapon;
        public string DefId;
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
        readonly HashSet<string> _takenStats = new HashSet<string>();

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
        }

        /// <summary>Builds a fresh hand. Duplicate ids inside one hand are never offered.</summary>
        public void Offer()
        {
            CardPoolDef pool = _ctx.Cfg.Cards;
            _offers.Clear();

            int want = Mathf.Max(1, pool.Choices);
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

            EvtArg a = new EvtArg();
            a.I0 = _offers.Count;
            a.O0 = _offers;
            _ctx.Bus.Dispatch(EventID.CardsOffered, a);
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
                    _takenStats.Add(offer.Id);
                    break;

                case CardKind.Skill:
                    p.Passives |= PassiveOf(offer.Passive);
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
        /// </summary>
        void Grant(CardOffer offer)
        {
            Vector2 at = _ctx.Run.Player.Pos;
            LootModel l = offer.IsWeapon
                ? _loot.SpawnWeapon(at, offer.Quality)
                : _loot.SpawnArmor(at, offer.Quality);

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
            List<CardDef> cards = _ctx.Cfg.Cards.Cards;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].Kind == CardKind.Stat && !_takenStats.Contains(cards[i].Id))
                {
                    return true;
                }
            }

            return false;
        }

        bool HasPassive()
        {
            SlackPassive owned = _ctx.Run.Player.Passives;
            List<CardDef> cards = _ctx.Cfg.Cards.Cards;

            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].Kind != CardKind.Skill)
                {
                    continue;
                }

                SlackPassive flag = PassiveOf(cards[i].Passive);
                if (flag != SlackPassive.None && (owned & flag) == 0)
                {
                    return true;
                }
            }

            return false;
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
            SlackPassive owned = _ctx.Run.Player.Passives;

            _pool.Clear();
            for (int i = 0; i < cards.Count; i++)
            {
                CardDef c = cards[i];
                if (c.Kind != kind)
                {
                    continue;
                }

                if (kind == CardKind.Stat && _takenStats.Contains(c.Id))
                {
                    continue;
                }

                if (kind == CardKind.Skill)
                {
                    SlackPassive flag = PassiveOf(c.Passive);
                    if (flag == SlackPassive.None || (owned & flag) != 0)
                    {
                        continue;
                    }
                }

                _pool.Add(i);
            }

            if (_pool.Count == 0)
            {
                return null;
            }

            CardDef def = cards[_pool[Random.Range(0, _pool.Count)]];

            CardOffer offer = new CardOffer();
            offer.Kind = def.Kind;
            offer.Id = def.Id;
            offer.Title = def.Name;
            offer.Desc = def.Desc;
            offer.Stat = def.Stat;
            offer.Value = def.Value;
            offer.Percent = def.Percent;
            offer.Passive = def.Passive;
            return offer;
        }

        /// <summary>
        /// Quality follows the day: white on Monday, blue by Wednesday, yellow Friday, orange Saturday.
        /// It is the one growth line a player can predict, and a single predictable line is what keeps
        /// an otherwise fully random loot game from feeling arbitrary.
        /// </summary>
        CardOffer BuildEquipment()
        {
            CardPoolDef pool = _ctx.Cfg.Cards;
            int day = Mathf.Clamp(_ctx.Run.DayIndex, 1, pool.EquipQualityByDay.Length - 1);
            Quality q = pool.EquipQualityByDay[day];

            bool weapon = Rng.ChancePercent(60f);

            CardOffer offer = new CardOffer();
            offer.Kind = CardKind.Equipment;
            offer.Quality = q;
            offer.IsWeapon = weapon;

            if (weapon)
            {
                List<string> order = _ctx.Cfg.WeaponOrder;
                if (order.Count == 0)
                {
                    return null;
                }

                WeaponDef def = _ctx.Cfg.Weapon(order[Random.Range(0, order.Count)]);
                if (def == null)
                {
                    return null;
                }

                offer.Id = "equip_" + def.Id + "_" + q;
                offer.DefId = def.Id;
                offer.Title = QualityWord(q) + def.Name;

                // The card states the unlocked behaviour, not the damage number. Players pick these
                // for the effect, so a card that only shows numbers is selling the wrong thing.
                offer.Desc = TierBlurb(def, q);
            }
            else
            {
                List<ArmorBaseDef> bases = _ctx.Cfg.Loot.ArmorBases;
                if (bases.Count == 0)
                {
                    return null;
                }

                ArmorBaseDef def = bases[Random.Range(0, bases.Count)];
                offer.Id = "equip_" + def.Id + "_" + q;
                offer.DefId = def.Id;
                offer.Title = QualityWord(q) + def.Name;
                offer.Desc = ArmorBlurb(def, q);
            }

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
                        sb.Append(" · 每 ").Append(t.SelectAllEvery).Append(" 次 Ctrl+A 全屏");
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

            if (def.Slot == EquipSlot.Head && q >= Quality.Yellow)
            {
                sb.Append(" · 护盾期间免疫控制");
            }
            else if (def.Slot == EquipSlot.Head && q >= Quality.Blue)
            {
                sb.Append(" · 每 10s 获得护盾");
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

        static string QualityWord(Quality q)
        {
            switch (q)
            {
                case Quality.Blue: return "蓝色";
                case Quality.Yellow: return "黄色";
                case Quality.Orange: return "橙色";
                default: return "普通";
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
