using System.Collections.Generic;
using System.Xml.Linq;
using OfficeHell.Core;
using UnityEngine;

namespace OfficeHell.Config
{
    /// <summary>
    /// Owns every designer facing table. Load is non throwing by design: a broken row degrades to
    /// its default and lands in Report, which is then printed as one block instead of a stack trace
    /// somewhere unrelated three systems later.
    /// </summary>
    public sealed class ConfigManager
    {
        public const string FileEnemies = "Enemies.xml";
        public const string FileDays = "Days.xml";
        public const string FileWeapons = "Weapons.xml";
        public const string FilePlayer = "Player.xml";
        public const string FileLoot = "Loot.xml";
        public const string FileCards = "Cards.xml";
        public const string FileViews = "Views.xml";
        public const string FileAudio = "Audio.xml";

        readonly IConfigSource _source;

        public ScalingDef Scaling = new ScalingDef();
        public ClockDef Clock = new ClockDef();
        public SpawnBandDef Band = new SpawnBandDef();
        public Dictionary<string, EnemyDef> Enemies = new Dictionary<string, EnemyDef>(12);
        public List<DayDef> Days = new List<DayDef>(6);
        public Dictionary<string, WeaponDef> Weapons = new Dictionary<string, WeaponDef>(4);
        public List<string> WeaponOrder = new List<string>(4);
        public QualityCoefDef WeaponQuality = new QualityCoefDef();
        public PlayerDef Player = new PlayerDef();
        public SkillDef Skill = new SkillDef();
        public CameraDef Camera = new CameraDef();
        public ArenaDef Arena = new ArenaDef();
        public ProgressionDef Progression = new ProgressionDef();
        public CoffeeDef Coffee = new CoffeeDef();
        public LootDef Loot = new LootDef();
        public CardPoolDef Cards = new CardPoolDef();
        public Dictionary<string, ViewDef> Views = new Dictionary<string, ViewDef>(20);
        public AudioDef Audio = new AudioDef();

        public readonly List<string> Report = new List<string>(32);
        public bool Loaded;

        public ConfigManager(IConfigSource source)
        {
            _source = source;
        }

        public string SourcePath
        {
            get { return _source != null ? _source.Describe() : "<none>"; }
        }

        public bool Load()
        {
            Report.Clear();

            ParseViews(Read(FileViews));
            ParseEnemies(Read(FileEnemies));
            ParseDays(Read(FileDays));
            ParseWeapons(Read(FileWeapons));
            ParsePlayer(Read(FilePlayer));
            ParseLoot(Read(FileLoot));
            ParseCards(Read(FileCards));
            ParseAudio(Read(FileAudio));

            ConfigValidator.Validate(this, Report);

            Loaded = Enemies.Count > 0 && Days.Count > 0 && Weapons.Count > 0;
            PrintReport();
            return Loaded;
        }

        public void PrintReport()
        {
            string head = string.Format(
                "[Config] {0} | enemies {1} | days {2} | weapons {3} | views {4} | affixes {5} | cards {6} | sfx {7}",
                SourcePath, Enemies.Count, Days.Count, Weapons.Count, Views.Count,
                Loot.Affixes.Count, Cards.Cards.Count, Audio.Sfx.Count);

            if (Report.Count == 0)
            {
                Debug.Log(head + "\nno issues");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder(head);
            sb.Append("\n").Append(Report.Count).Append(" issue(s):");
            for (int i = 0; i < Report.Count; i++)
            {
                sb.Append("\n  ").Append(i + 1).Append(". ").Append(Report[i]);
            }

            Debug.LogWarning(sb.ToString());
        }

        // ---------- lookups ----------

        public EnemyDef Enemy(string id)
        {
            EnemyDef d;
            return id != null && Enemies.TryGetValue(id, out d) ? d : null;
        }

        public WeaponDef Weapon(string id)
        {
            WeaponDef d;
            return id != null && Weapons.TryGetValue(id, out d) ? d : null;
        }

        public ArmorBaseDef Armor(string id)
        {
            for (int i = 0; i < Loot.ArmorBases.Count; i++)
            {
                if (Loot.ArmorBases[i].Id == id)
                {
                    return Loot.ArmorBases[i];
                }
            }

            return null;
        }

        public ArmorBaseDef ArmorForSlot(EquipSlot slot)
        {
            for (int i = 0; i < Loot.ArmorBases.Count; i++)
            {
                if (Loot.ArmorBases[i].Slot == slot)
                {
                    return Loot.ArmorBases[i];
                }
            }

            return null;
        }

        public ViewDef View(string id)
        {
            ViewDef d;
            if (id != null && Views.TryGetValue(id, out d))
            {
                return d;
            }

            return FallbackView;
        }

        public static readonly ViewDef FallbackView = new ViewDef
        {
            Id = "__fallback",
            Color = new Color(1f, 0f, 1f, 1f),
            Scale = 0.8f,
            Shape = ViewShape.Quad,
        };

        /// <summary>1 based day index. Past the last authored day the final day repeats.</summary>
        public DayDef Day(int index)
        {
            if (Days.Count == 0)
            {
                return null;
            }

            if (index < 1)
            {
                index = 1;
            }

            for (int i = 0; i < Days.Count; i++)
            {
                if (Days[i].Index == index)
                {
                    return Days[i];
                }
            }

            return Days[Days.Count - 1];
        }

        public int DayCount
        {
            get { return Days.Count; }
        }

        /// <summary>Total authored combat time. The salary proration reads this, not a hard coded 420.</summary>
        public float TotalCombatSeconds
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Days.Count; i++)
                {
                    total += Days[i].Duration;
                }

                return total;
            }
        }

        public float HpScale(int day)
        {
            return 1f + Scaling.HpPerDay * (day - 1);
        }

        public float DmgScale(int day)
        {
            return 1f + Scaling.DmgPerDay * (day - 1);
        }

        public QualityDef QualityOf(Quality q)
        {
            QualityDef d = Loot.Qualities[(int)q];
            return d ?? new QualityDef { Q = q };
        }

        public string RankOf(int level)
        {
            string[] names = Progression.RankNames;
            int idx = Mathf.Clamp(level - 1, 0, names.Length - 1);
            return names[idx];
        }

        // ---------- parsing ----------

        string Read(string file)
        {
            return _source != null ? _source.Read(file) : null;
        }

        void ParseViews(string text)
        {
            Dictionary<string, ViewDef> map = new Dictionary<string, ViewDef>(20);
            XDocument doc = XmlRead.Doc(text, FileViews, Report);
            if (doc != null && doc.Root != null)
            {
                foreach (XElement e in doc.Root.Elements("View"))
                {
                    string id = XmlRead.Required(e, "id", Report);
                    if (id == null)
                    {
                        continue;
                    }

                    ViewDef d = new ViewDef();
                    d.Id = id;
                    d.Prefab = XmlRead.Str(e, "prefab", "Quad_Basic", Report);
                    d.Color = XmlRead.Col(e, "color", Color.white, Report);
                    d.Scale = XmlRead.Num(e, "scale", 1f, Report);
                    d.Shape = XmlRead.Enm(e, "shape", ViewShape.Quad, Report);
                    d.SpriteSet = XmlRead.Str(e, "spriteSet", null, Report);
                    d.SpriteHeight = Mathf.Max(0f, XmlRead.Num(e, "spriteHeight", 0f, Report));
                    d.AnimationFps = Mathf.Max(0.1f, XmlRead.Num(e, "animationFps", 8f, Report));
                    map[id] = d;
                }
            }

            Views = map;
        }

        void ParseEnemies(string text)
        {
            Dictionary<string, EnemyDef> map = new Dictionary<string, EnemyDef>(12);
            XDocument doc = XmlRead.Doc(text, FileEnemies, Report);
            if (doc != null && doc.Root != null)
            {
                foreach (XElement e in doc.Root.Elements("Enemy"))
                {
                    string id = XmlRead.Required(e, "id", Report);
                    if (id == null)
                    {
                        continue;
                    }

                    EnemyDef d = new EnemyDef();
                    d.Id = id;
                    d.Name = XmlRead.Str(e, "name", id, Report);
                    d.ReportVerb = XmlRead.Str(e, "reportVerb", null, Report);
                    d.ReportUnit = XmlRead.Str(e, "reportUnit", "个", Report);
                    d.Hp = XmlRead.Num(e, "hp", 10f, Report);
                    d.Speed = XmlRead.Num(e, "speed", 2f, Report);
                    d.ContactDamage = XmlRead.Num(e, "contactDamage", 5f, Report);
                    d.Exp = XmlRead.Int(e, "exp", 1, Report);
                    d.Radius = XmlRead.Num(e, "radius", 0.35f, Report);
                    d.ViewId = XmlRead.Str(e, "viewId", null, Report);
                    d.Tier = XmlRead.Enm(e, "tier", EnemyTier.Normal, Report);
                    d.KnockbackCd = XmlRead.Num(e, "knockbackCd", 1.2f, Report);
                    d.IgnoreScaling = XmlRead.Bool(e, "ignoreScaling", false, Report);
                    d.Behavior = XmlRead.Str(e, "behavior", null, Report);
                    d.Param = KvBag.Parse(XmlRead.Str(e, "behaviorParam", null, Report));
                    map[id] = d;
                }
            }

            Enemies = map;
        }

        void ParseDays(string text)
        {
            List<DayDef> list = new List<DayDef>(6);
            ScalingDef scaling = new ScalingDef();
            ClockDef clock = new ClockDef();
            SpawnBandDef band = new SpawnBandDef();

            XDocument doc = XmlRead.Doc(text, FileDays, Report);
            if (doc != null && doc.Root != null)
            {
                XElement sc = doc.Root.Element("Scaling");
                if (sc != null)
                {
                    scaling.HpPerDay = XmlRead.Num(sc, "hpPerDay", scaling.HpPerDay, Report);
                    scaling.DmgPerDay = XmlRead.Num(sc, "dmgPerDay", scaling.DmgPerDay, Report);
                }

                XElement ck = doc.Root.Element("Clock");
                if (ck != null)
                {
                    clock.StartHour = XmlRead.Int(ck, "startHour", clock.StartHour, Report);
                    clock.EndHour = XmlRead.Int(ck, "endHour", clock.EndHour, Report);
                    clock.SnapMinutes = Mathf.Max(1, XmlRead.Int(ck, "snapMinutes", clock.SnapMinutes, Report));
                }

                XElement bd = doc.Root.Element("SpawnBand");
                if (bd != null)
                {
                    band.SemiX = XmlRead.Num(bd, "semiX", band.SemiX, Report);
                    band.SemiY = XmlRead.Num(bd, "semiY", band.SemiY, Report);
                    band.Sectors = Mathf.Max(4, XmlRead.Int(bd, "sectors", band.Sectors, Report));
                    band.WeightLeft = XmlRead.Num(bd, "weightLeft", band.WeightLeft, Report);
                    band.WeightRight = XmlRead.Num(bd, "weightRight", band.WeightRight, Report);
                    band.WeightUp = XmlRead.Num(bd, "weightUp", band.WeightUp, Report);
                    band.WeightDown = XmlRead.Num(bd, "weightDown", band.WeightDown, Report);
                    band.OutwardPush = XmlRead.Num(bd, "outwardPush", band.OutwardPush, Report);
                    band.MinSeparation = XmlRead.Num(bd, "minSeparation", band.MinSeparation, Report);
                    band.EdgeMargin = XmlRead.Num(bd, "edgeMargin", band.EdgeMargin, Report);
                    band.Retries = Mathf.Max(1, XmlRead.Int(bd, "retries", band.Retries, Report));
                    band.MinSectorsPerBurst = Mathf.Max(1, XmlRead.Int(bd, "minSectors", band.MinSectorsPerBurst, Report));
                    band.MaxSectorsPerBurst = Mathf.Max(1, XmlRead.Int(bd, "maxSectors", band.MaxSectorsPerBurst, Report));
                    band.GraceRadius = XmlRead.Num(bd, "graceRadius", band.GraceRadius, Report);
                    band.GraceSeconds = XmlRead.Num(bd, "graceSeconds", band.GraceSeconds, Report);
                }

                foreach (XElement e in doc.Root.Elements("Day"))
                {
                    DayDef w = new DayDef();
                    w.Index = XmlRead.Int(e, "index", list.Count + 1, Report);
                    w.Label = XmlRead.Str(e, "label", "第 " + w.Index + " 天", Report);
                    w.Weekday = XmlRead.Str(e, "weekday", "第 " + w.Index + " 天", Report);
                    w.Duration = Mathf.Max(1f, XmlRead.Num(e, "duration", 40f, Report));
                    w.OffWorkSeconds = XmlRead.Num(e, "offWork", 3f, Report);
                    w.Density = XmlRead.Num(e, "density", 0.82f, Report);
                    w.TotalSpawnOverride = XmlRead.Int(e, "totalSpawn", -1, Report);
                    w.ConcurrentMax = XmlRead.Int(e, "concurrentMax", 30, Report);

                    foreach (XElement se in e.Elements("Spawner"))
                    {
                        SpawnerDef s = new SpawnerDef();
                        s.Interval = Mathf.Max(0.05f, XmlRead.Num(se, "interval", 2.5f, Report));
                        s.GroupSize = Mathf.Max(1, XmlRead.Int(se, "groupSize", 4, Report));
                        s.From = XmlRead.Num(se, "from", 0f, Report);
                        s.To = XmlRead.Num(se, "to", w.Duration, Report);
                        s.BudgetPct = XmlRead.Num(se, "budgetPct", 100f, Report);
                        s.Ramp = Mathf.Max(0.05f, XmlRead.Num(se, "ramp", 2f, Report));

                        foreach (XElement pe in se.Elements("Pick"))
                        {
                            PickDef p = new PickDef();
                            p.EnemyId = XmlRead.Required(pe, "enemyId", Report);
                            p.Weight = XmlRead.Num(pe, "weight", 1f, Report);
                            if (p.EnemyId != null)
                            {
                                s.Picks.Add(p);
                            }
                        }

                        w.Spawners.Add(s);
                    }

                    foreach (XElement fe in e.Elements("Fixed"))
                    {
                        FixedSpawnDef f = new FixedSpawnDef();
                        f.EnemyId = XmlRead.Required(fe, "enemyId", Report);
                        f.Count = Mathf.Max(1, XmlRead.Int(fe, "count", 1, Report));
                        f.AtSecond = XmlRead.Num(fe, "atSecond", 0f, Report);
                        f.Entrance = XmlRead.Bool(fe, "entrance", false, Report);

                        string drop = XmlRead.Str(fe, "guaranteeDrop", null, Report);
                        if (!string.IsNullOrEmpty(drop))
                        {
                            f.GuaranteeDrop = ParseQuality(fe, drop);
                        }

                        if (f.EnemyId != null)
                        {
                            w.Fixed.Add(f);
                        }
                    }

                    list.Add(w);
                }
            }

            list.Sort((a, b) => a.Index.CompareTo(b.Index));
            Days = list;
            Scaling = scaling;
            Clock = clock;
            Band = band;
        }

        /// <summary>
        /// The tier names were white/blue/yellow/orange before the ladder moved to green/blue/purple/
        /// orange. Both spellings are accepted: an unrecognised tier does not fail loudly, it lands on
        /// some other tier, and a whole config quietly one step off is worse than the rename itself.
        /// </summary>
        Quality ParseQuality(XElement e, string raw)
        {
            switch (raw.ToLowerInvariant())
            {
                case "green":
                case "white":
                case "common":
                    return Quality.Green;
                case "blue":
                case "magic":
                    return Quality.Blue;
                case "purple":
                case "yellow":
                case "rare":
                    return Quality.Purple;
                case "orange":
                case "legendary":
                    return Quality.Orange;
                default:
                    XmlRead.Add(Report, "<" + e.Name + "> unknown quality '" + raw + "', treated as purple");
                    return Quality.Purple;
            }
        }

        void ParseWeapons(string text)
        {
            Dictionary<string, WeaponDef> map = new Dictionary<string, WeaponDef>(4);
            List<string> order = new List<string>(4);
            QualityCoefDef coef = new QualityCoefDef();

            XDocument doc = XmlRead.Doc(text, FileWeapons, Report);
            if (doc != null && doc.Root != null)
            {
                foreach (XElement e in doc.Root.Elements("Weapon"))
                {
                    string id = XmlRead.Required(e, "id", Report);
                    if (id == null)
                    {
                        continue;
                    }

                    WeaponDef d = new WeaponDef();
                    d.Id = id;
                    d.Name = XmlRead.Str(e, "name", id, Report);
                    d.Kind = XmlRead.Enm(e, "type", WeaponKind.ProjectileLauncher, Report);
                    d.BaseDamage = XmlRead.Num(e, "baseDamage", 0f, Report);
                    d.Rate = Mathf.Max(0.01f, XmlRead.Num(e, "rate", 1f, Report));
                    d.AtkCoef = XmlRead.Num(e, "atkCoef", 0f, Report);
                    d.WindupSeconds = XmlRead.Num(e, "windup", 0.15f, Report);
                    d.SameTargetCd = XmlRead.Num(e, "sameTargetCd", 2f, Report);
                    d.ViewId = XmlRead.Str(e, "viewId", "v_proj", Report);

                    ParseTiers(e, d);
                    map[id] = d;
                    order.Add(id);
                }

                XElement q = doc.Root.Element("Quality");
                if (q != null)
                {
                    // Pre-rename spellings feed the new ones as their fallback. This table is the only
                    // scaling ladder in the game and cards read it too, so a config that misses it
                    // does not degrade, it silently flattens every tier to the compiled defaults.
                    coef.Green = XmlRead.Num(q, "green", XmlRead.Num(q, "white", coef.Green, Report), Report);
                    coef.Blue = XmlRead.Num(q, "blue", coef.Blue, Report);
                    coef.Purple = XmlRead.Num(q, "purple", XmlRead.Num(q, "yellow", coef.Purple, Report), Report);
                    coef.Orange = XmlRead.Num(q, "orange", coef.Orange, Report);
                }
            }

            Weapons = map;
            WeaponOrder = order;
            WeaponQuality = coef;
        }

        /// <summary>
        /// Tiers inherit upward so xml only carries the delta. Reading "blue adds a second needle"
        /// is the point: a full copy of every number per tier hides which one actually changed.
        /// </summary>
        void ParseTiers(XElement weapon, WeaponDef def)
        {
            Dictionary<Quality, XElement> rows = new Dictionary<Quality, XElement>(4);
            foreach (XElement te in weapon.Elements("Tier"))
            {
                string raw = XmlRead.Required(te, "q", Report);
                if (raw == null)
                {
                    continue;
                }

                rows[ParseQuality(te, raw)] = te;
            }

            WeaponTierDef previous = new WeaponTierDef();
            for (int i = 0; i < 4; i++)
            {
                Quality q = (Quality)i;
                WeaponTierDef tier = previous.Clone(q);

                XElement row;
                if (rows.TryGetValue(q, out row))
                {
                    tier.ProjCount = Mathf.Max(1, XmlRead.Int(row, "projCount", tier.ProjCount, Report));
                    tier.ProjSpacing = XmlRead.Num(row, "spacing", tier.ProjSpacing, Report);
                    tier.Pierce = XmlRead.Int(row, "pierce", tier.Pierce, Report);
                    tier.Range = XmlRead.Num(row, "range", tier.Range, Report);
                    tier.ProjSpeed = XmlRead.Num(row, "projSpeed", tier.ProjSpeed, Report);
                    tier.PinSeconds = XmlRead.Num(row, "pinSeconds", tier.PinSeconds, Report);
                    tier.LockRange = XmlRead.Num(row, "lockRange", tier.LockRange, Report);
                    tier.BlastRadius = XmlRead.Num(row, "blastRadius", tier.BlastRadius, Report);
                    tier.Slams = Mathf.Max(1, XmlRead.Int(row, "slams", tier.Slams, Report));
                    tier.SecondSlamPct = XmlRead.Num(row, "secondSlamPct", tier.SecondSlamPct, Report);
                    tier.SlowPct = XmlRead.Num(row, "slowPct", tier.SlowPct, Report);
                    tier.SlowSeconds = XmlRead.Num(row, "slowSeconds", tier.SlowSeconds, Report);
                    tier.SelectAllEvery = XmlRead.Int(row, "selectAllEvery", tier.SelectAllEvery, Report);
                    tier.SelectAllPct = XmlRead.Num(row, "selectAllPct", tier.SelectAllPct, Report);
                    tier.SelectAllRadius = XmlRead.Num(row, "selectAllRadius", tier.SelectAllRadius, Report);
                    tier.SelectAllSharedCd = XmlRead.Num(row, "selectAllSharedCd", tier.SelectAllSharedCd, Report);
                    tier.OrbitCount = Mathf.Max(1, XmlRead.Int(row, "orbitCount", tier.OrbitCount, Report));
                    tier.OrbitRadius = XmlRead.Num(row, "orbitRadius", tier.OrbitRadius, Report);
                    tier.OrbitDegPerSec = XmlRead.Num(row, "orbitDegPerSec", tier.OrbitDegPerSec, Report);
                    tier.TetherDamagePct = XmlRead.Num(row, "tetherDamagePct", tier.TetherDamagePct, Report);
                    tier.Knockback = XmlRead.Num(row, "knockback", tier.Knockback, Report);
                }

                def.Tiers[i] = tier;
                previous = tier;
            }
        }

        void ParsePlayer(string text)
        {
            PlayerDef p = new PlayerDef();
            SkillDef s = new SkillDef();
            CameraDef c = new CameraDef();
            ArenaDef a = new ArenaDef();
            ProgressionDef pg = new ProgressionDef();
            CoffeeDef cf = new CoffeeDef();

            XDocument doc = XmlRead.Doc(text, FilePlayer, Report);
            if (doc != null && doc.Root != null)
            {
                XElement e = doc.Root.Element("Player");
                if (e != null)
                {
                    p.MaxSan = XmlRead.Num(e, "maxSan", p.MaxSan, Report);
                    p.Atk = XmlRead.Num(e, "atk", p.Atk, Report);
                    p.CritChance = XmlRead.Num(e, "critChance", p.CritChance, Report);
                    p.CritMulti = XmlRead.Num(e, "critMulti", p.CritMulti, Report);
                    p.Def = XmlRead.Num(e, "def", p.Def, Report);
                    p.Dodge = XmlRead.Num(e, "dodge", p.Dodge, Report);
                    p.DodgeCap = XmlRead.Num(e, "dodgeCap", p.DodgeCap, Report);
                    p.MoveSpeed = XmlRead.Num(e, "moveSpeed", p.MoveSpeed, Report);
                    p.Haste = XmlRead.Num(e, "haste", p.Haste, Report);
                    p.Luck = XmlRead.Num(e, "luck", p.Luck, Report);
                    p.InvulnAfterHit = XmlRead.Num(e, "invulnAfterHit", p.InvulnAfterHit, Report);
                    p.PickupRadius = XmlRead.Num(e, "pickupRadius", p.PickupRadius, Report);
                    p.StepPickupRadius = XmlRead.Num(e, "stepPickupRadius", p.StepPickupRadius, Report);
                    p.Radius = XmlRead.Num(e, "radius", p.Radius, Report);
                }

                XElement se = doc.Root.Element("Skill");
                if (se != null)
                {
                    s.Id = XmlRead.Str(se, "id", s.Id, Report);
                    s.Name = XmlRead.Str(se, "name", s.Name, Report);
                    s.Cd = Mathf.Max(0.1f, XmlRead.Num(se, "cd", s.Cd, Report));
                    s.InvulnDuration = XmlRead.Num(se, "invulnDuration", s.InvulnDuration, Report);
                    s.HealPctMaxSan = XmlRead.Num(se, "healPctMaxSan", s.HealPctMaxSan, Report);
                    s.PushRadius = XmlRead.Num(se, "pushRadius", s.PushRadius, Report);
                    s.PushForce = XmlRead.Num(se, "pushForce", s.PushForce, Report);
                }

                XElement ce = doc.Root.Element("Camera");
                if (ce != null)
                {
                    c.OrthographicSize = Mathf.Max(1f, XmlRead.Num(ce, "orthographicSize", c.OrthographicSize, Report));
                    c.FollowLerp = XmlRead.Num(ce, "followLerp", c.FollowLerp, Report);
                    c.Aspect = Mathf.Max(0.1f, XmlRead.Num(ce, "aspect", c.Aspect, Report));
                }

                XElement ae = doc.Root.Element("Arena");
                if (ae != null)
                {
                    a.HalfWidth = XmlRead.Num(ae, "halfWidth", a.HalfWidth, Report);
                    a.HalfHeight = XmlRead.Num(ae, "halfHeight", a.HalfHeight, Report);
                }

                XElement ge = doc.Root.Element("Progression");
                if (ge != null)
                {
                    pg.MaxLevel = Mathf.Max(1, XmlRead.Int(ge, "maxLevel", pg.MaxLevel, Report));
                    pg.ExpCoef = XmlRead.Num(ge, "expCoef", pg.ExpCoef, Report);
                    pg.ExpPower = XmlRead.Num(ge, "expPower", pg.ExpPower, Report);
                    pg.DowngradeExp = XmlRead.Int(ge, "downgradeExp", pg.DowngradeExp, Report);
                    pg.KpiCap = XmlRead.Int(ge, "kpiCap", pg.KpiCap, Report);
                    pg.KpiTargetKills = Mathf.Max(1, XmlRead.Int(ge, "kpiTargetKills", pg.KpiTargetKills, Report));
                    pg.FinalSalary = XmlRead.Int(ge, "finalSalary", pg.FinalSalary, Report);
                }

                XElement cfe = doc.Root.Element("Coffee");
                if (cfe != null)
                {
                    cf.ChancePct = XmlRead.Num(cfe, "chancePct", cf.ChancePct, Report);
                    cf.LowSanChancePct = XmlRead.Num(cfe, "lowSanChancePct", cf.LowSanChancePct, Report);
                    cf.LowSanThresholdPct = XmlRead.Num(cfe, "lowSanThresholdPct", cf.LowSanThresholdPct, Report);
                    // An external pre-split config remains playable: its old heal stays immediate.
                    // New configs author the two parts explicitly so the 8 + 8 contract is visible.
                    if (cfe.Attribute("healPctMaxSan") != null)
                    {
                        cf.InstantHealPctMaxSan = XmlRead.Num(
                            cfe, "healPctMaxSan", cf.InstantHealPctMaxSan, Report);
                        cf.HealOverTimePctMaxSan = 0f;
                    }

                    cf.InstantHealPctMaxSan = XmlRead.Num(
                        cfe, "instantHealPctMaxSan", cf.InstantHealPctMaxSan, Report);
                    cf.HealOverTimePctMaxSan = XmlRead.Num(
                        cfe, "healOverTimePctMaxSan", cf.HealOverTimePctMaxSan, Report);
                    cf.HealOverTimeSeconds = Mathf.Max(0.1f, XmlRead.Num(
                        cfe, "healOverTimeSeconds", cf.HealOverTimeSeconds, Report));
                    cf.WorldLifetimeSeconds = Mathf.Max(0.1f, XmlRead.Num(
                        cfe, "worldLifetimeSeconds", cf.WorldLifetimeSeconds, Report));
                    cf.HasteAddPct = XmlRead.Num(cfe, "hasteAddPct", cf.HasteAddPct, Report);
                    cf.BuffSeconds = XmlRead.Num(cfe, "buffSeconds", cf.BuffSeconds, Report);
                    cf.ViewId = XmlRead.Str(cfe, "viewId", cf.ViewId, Report);
                }
            }

            Player = p;
            Skill = s;
            Camera = c;
            Arena = a;
            Progression = pg;
            Coffee = cf;
        }

        void ParseLoot(string text)
        {
            LootDef loot = new LootDef();
            XDocument doc = XmlRead.Doc(text, FileLoot, Report);
            if (doc != null && doc.Root != null)
            {
                foreach (XElement e in doc.Root.Elements("Quality"))
                {
                    string id = XmlRead.Required(e, "id", Report);
                    if (id == null)
                    {
                        continue;
                    }

                    Quality q = ParseQuality(e, id);
                    QualityDef d = new QualityDef();
                    d.Q = q;
                    d.Weight = XmlRead.Num(e, "weight", 0f, Report);
                    d.AffixCount = XmlRead.Int(e, "affixCount", 0, Report);
                    d.Color = XmlRead.Col(e, "color", Color.white, Report);
                    d.Beam = XmlRead.Str(e, "beam", "none", Report);
                    d.HitStop = XmlRead.Num(e, "hitStop", 0f, Report);
                    d.Shake = XmlRead.Num(e, "shake", 0f, Report);
                    d.Sfx = XmlRead.Str(e, "sfx", null, Report);
                    d.Label = XmlRead.Bool(e, "label", false, Report);
                    d.AutoMagnet = XmlRead.Bool(e, "autoMagnet", true, Report);
                    d.BgmLowPass = XmlRead.Num(e, "bgmLowPass", 0f, Report);
                    d.DropLine = XmlRead.Str(e, "dropLine", null, Report);
                    d.RankName = XmlRead.Str(e, "rankName", null, Report);
                    loot.Qualities[(int)q] = d;
                }

                XElement lb = doc.Root.Element("LateBonus");
                if (lb != null)
                {
                    loot.LateBonusApplyTo = XmlRead.Str(lb, "applyTo", loot.LateBonusApplyTo, Report);
                    loot.LateBonusPerDay = XmlRead.Num(lb, "perDay", loot.LateBonusPerDay, Report);
                }

                XElement pity = doc.Root.Element("Pity");
                if (pity != null)
                {
                    loot.PityFirstLegendarySeconds =
                        XmlRead.Num(pity, "firstLegendarySeconds", loot.PityFirstLegendarySeconds, Report);
                    loot.PityLegendarySeconds =
                        XmlRead.Num(pity, "legendarySeconds", loot.PityLegendarySeconds, Report);
                }

                XElement drop = doc.Root.Element("Drop");
                if (drop != null)
                {
                    loot.EquipChancePct = XmlRead.Num(drop, "equipChancePct", loot.EquipChancePct, Report);
                    loot.WeaponShare = XmlRead.Num(drop, "weaponShare", loot.WeaponShare, Report);
                    loot.ArmorShare = XmlRead.Num(drop, "armorShare", loot.ArmorShare, Report);
                    loot.MagnetSpeed = XmlRead.Num(drop, "magnetSpeed", loot.MagnetSpeed, Report);
                    loot.TossDuration = Mathf.Max(0.05f, XmlRead.Num(drop, "tossDuration", loot.TossDuration, Report));
                    loot.BounceCount = XmlRead.Int(drop, "bounceCount", loot.BounceCount, Report);
                }

                foreach (XElement e in doc.Root.Elements("Affix"))
                {
                    string id = XmlRead.Required(e, "id", Report);
                    if (id == null)
                    {
                        continue;
                    }

                    AffixDef d = new AffixDef();
                    d.Id = id;
                    d.Name = XmlRead.Str(e, "name", id, Report);
                    d.Stat = XmlRead.Enm(e, "stat", StatKey.Atk, Report);
                    d.Base = XmlRead.Num(e, "base", 1f, Report);
                    d.Percent = XmlRead.Bool(e, "percent", false, Report);
                    loot.Affixes.Add(d);
                }

                foreach (XElement e in doc.Root.Elements("Armor"))
                {
                    string id = XmlRead.Required(e, "id", Report);
                    if (id == null)
                    {
                        continue;
                    }

                    ArmorBaseDef d = new ArmorBaseDef();
                    d.Id = id;
                    d.Name = XmlRead.Str(e, "name", id, Report);
                    d.Slot = XmlRead.Enm(e, "slot", EquipSlot.Head, Report);
                    d.ViewId = XmlRead.Str(e, "viewId", "v_equip", Report);

                    foreach (XElement me in e.Elements("Main"))
                    {
                        ArmorStatDef m = new ArmorStatDef();
                        m.Stat = XmlRead.Enm(me, "stat", StatKey.Def, Report);
                        m.Base = XmlRead.Num(me, "base", 1f, Report);
                        m.Percent = XmlRead.Bool(me, "percent", false, Report);
                        d.Mains.Add(m);
                    }

                    loot.ArmorBases.Add(d);
                }
            }

            for (int i = 0; i < loot.Qualities.Length; i++)
            {
                if (loot.Qualities[i] == null)
                {
                    loot.Qualities[i] = new QualityDef { Q = (Quality)i };
                }
            }

            Loot = loot;
        }

        void ParseCards(string text)
        {
            CardPoolDef pool = new CardPoolDef();
            for (int i = 0; i < pool.QualityByDay.Length; i++)
            {
                pool.QualityByDay[i] = Quality.Green;
            }

            XDocument doc = XmlRead.Doc(text, FileCards, Report);
            if (doc != null && doc.Root != null)
            {
                pool.Choices = Mathf.Clamp(XmlRead.Int(doc.Root, "choices", pool.Choices, Report), 1, 4);

                XElement w = doc.Root.Element("Weights");
                if (w != null)
                {
                    pool.StatWeight = XmlRead.Num(w, "stat", pool.StatWeight, Report);
                    pool.EquipWeight = XmlRead.Num(w, "equipment", pool.EquipWeight, Report);
                    pool.SkillWeight = XmlRead.Num(w, "skill", pool.SkillWeight, Report);
                }

                // EquipQuality is the pre-tier name for the same table. Still read so a config left
                // next to an older exe keeps its growth line instead of collapsing to all green.
                IEnumerable<XElement> tiers = doc.Root.Elements("CardQuality");
                if (doc.Root.Element("CardQuality") == null)
                {
                    tiers = doc.Root.Elements("EquipQuality");
                }

                foreach (XElement e in tiers)
                {
                    int day = XmlRead.Int(e, "day", 0, Report);
                    string q = XmlRead.Required(e, "quality", Report);
                    if (day >= 1 && day < pool.QualityByDay.Length && q != null)
                    {
                        pool.QualityByDay[day] = ParseQuality(e, q);
                        pool.UpgradeChanceByDay[day] =
                            Mathf.Clamp(XmlRead.Num(e, "upgradeChance", 0f, Report), 0f, 100f);
                    }
                }

                foreach (XElement e in doc.Root.Elements("Card"))
                {
                    string id = XmlRead.Required(e, "id", Report);
                    if (id == null)
                    {
                        continue;
                    }

                    CardDef d = new CardDef();
                    d.Id = id;
                    d.Name = XmlRead.Str(e, "name", id, Report);
                    d.Desc = XmlRead.Str(e, "desc", "", Report);
                    d.Kind = XmlRead.Enm(e, "kind", CardKind.Stat, Report);
                    d.Weight = Mathf.Max(0f, XmlRead.Num(e, "weight", 1f, Report));
                    d.Stat = XmlRead.Enm(e, "stat", StatKey.Atk, Report);
                    d.Value = XmlRead.Num(e, "value", 0f, Report);
                    d.Percent = XmlRead.Bool(e, "percent", false, Report);
                    d.Passive = XmlRead.Str(e, "passive", null, Report);
                    d.Value2 = XmlRead.Num(e, "value2", 0f, Report);
                    pool.Cards.Add(d);
                }
            }

            // Any day the designer forgot inherits the previous day's tier rather than dropping to green.
            for (int i = 2; i < pool.QualityByDay.Length; i++)
            {
                if (pool.QualityByDay[i] < pool.QualityByDay[i - 1])
                {
                    pool.QualityByDay[i] = pool.QualityByDay[i - 1];
                    pool.UpgradeChanceByDay[i] = pool.UpgradeChanceByDay[i - 1];
                }
            }

            Cards = pool;
        }

        void ParseAudio(string text)
        {
            AudioDef audio = new AudioDef();
            XDocument doc = XmlRead.Doc(text, FileAudio, Report);
            if (doc != null && doc.Root != null)
            {
                audio.MaxSourcePool = Mathf.Clamp(XmlRead.Int(doc.Root, "maxSourcePool", audio.MaxSourcePool, Report), 2, 48);
                audio.PitchJitter = Mathf.Clamp01(XmlRead.Num(doc.Root, "pitchJitter", audio.PitchJitter, Report));
                audio.ThrottleSeconds = Mathf.Max(0f, XmlRead.Num(doc.Root, "throttleSeconds", 0.06f, Report));
                audio.SfxVolume = Mathf.Max(0f, XmlRead.Num(doc.Root, "sfxVolume", 1f, Report));
                audio.UiVolume = Mathf.Max(0f, XmlRead.Num(doc.Root, "uiVolume", 1f, Report));
                audio.DuckVolumeDb = Mathf.Min(0f, XmlRead.Num(doc.Root, "duckVolumeDb", -6f, Report));
                audio.LowSanFadeSeconds = Mathf.Max(0.01f, XmlRead.Num(doc.Root, "lowSanFadeSeconds", 0.2f, Report));

                foreach (XElement e in doc.Root.Elements("Sfx"))
                {
                    string id = XmlRead.Required(e, "id", Report);
                    if (id == null)
                    {
                        continue;
                    }

                    SfxDef d = new SfxDef();
                    d.Id = id;
                    d.Clip = XmlRead.Str(e, "clip", null, Report);
                    d.Volume = XmlRead.Num(e, "volume", 1f, Report);
                    d.MaxConcurrent = Mathf.Max(1, XmlRead.Int(e, "maxConcurrent", 4, Report));
                    d.Bus = XmlRead.Enm(e, "bus", AudioBus.Sfx, Report);
                    d.GainDb = XmlRead.Num(e, "gainDb", 0f, Report);
                    d.ThrottleSeconds = Mathf.Max(0f, XmlRead.Num(e, "throttleSeconds", audio.ThrottleSeconds, Report));
                    d.PitchJitter = Mathf.Clamp01(XmlRead.Num(e, "pitchJitter", audio.PitchJitter, Report));
                    d.DuckExempt = XmlRead.Bool(e, "duckExempt", false, Report);
                    d.Synth = XmlRead.Enm(e, "synth", SynthKind.Blip, Report);
                    d.Freq = XmlRead.Num(e, "freq", 660f, Report);
                    d.Dur = Mathf.Clamp(XmlRead.Num(e, "dur", 0.08f, Report), 0.01f, 3f);
                    audio.Sfx[id] = d;
                }

                foreach (XElement bgm in doc.Root.Elements("Bgm"))
                {
                    BgmDef b = new BgmDef();
                    b.Id = XmlRead.Str(bgm, "id", "bgm", Report);
                    b.Clip = XmlRead.Str(bgm, "clip", null, Report);
                    b.Volume = XmlRead.Num(bgm, "volume", 0.5f, Report);
                    b.CutoffNormal = XmlRead.Num(bgm, "lowPassCutoffNormal", 22000f, Report);
                    b.CutoffDucked = XmlRead.Num(bgm, "lowPassCutoffDucked", 800f, Report);
                    b.CrossfadeSeconds = Mathf.Max(0.01f, XmlRead.Num(bgm, "crossfadeSeconds", 0.5f, Report));
                    b.PitchPerDay = XmlRead.Num(bgm, "pitchPerDay", 0f, Report);
                    b.PhaseOneCutoff = XmlRead.Num(bgm, "phaseOneCutoff", b.CutoffNormal, Report);
                    b.PhaseThreePitch = Mathf.Max(0.01f, XmlRead.Num(bgm, "phaseThreePitch", 1f, Report));
                    b.PhaseThreeVolumeDb = XmlRead.Num(bgm, "phaseThreeVolumeDb", 0f, Report);
                    audio.Bgm[b.Id] = b;
                }
            }

            Audio = audio;
        }
    }
}
