using System.Collections.Generic;
using OfficeHell.Core;
using UnityEngine;

namespace OfficeHell.Config
{
    public enum EnemyTier
    {
        Normal = 0,
        Elite = 1,
        Boss = 2,
    }

    public enum Quality
    {
        White = 0,
        Blue = 1,
        Yellow = 2,
        Orange = 3,
    }

    /// <summary>
    /// Three attack code paths, nothing more. Every quality difference is a parameter change
    /// inside one of these three, never a fourth path.
    /// </summary>
    public enum WeaponKind
    {
        ProjectileLauncher = 0,
        GroundAoe = 1,
        Orbit = 2,
    }

    /// <summary>Six weapon slots plus three armour slots, nine in total.</summary>
    public enum EquipSlot
    {
        Weapon = 0,
        Head = 1,
        Body = 2,
        Feet = 3,
    }

    public enum ViewShape
    {
        Quad = 0,
        Triangle = 1,
        Hex = 2,
        Circle = 3,
        Diamond = 4,
    }

    public enum SynthKind
    {
        Blip = 0,
        Thud = 1,
        Chime = 2,
        Noise = 3,
        Sweep = 4,
        Bell = 5,
    }

    public enum AudioBus
    {
        Sfx = 0,
        Ui = 1,
    }

    /// <summary>Aura channels. Same channel never stacks, different channels coexist.</summary>
    public enum AuraChannel
    {
        MoveSlow = 0,
        AttackSlow = 1,
        EnemyHaste = 2,
    }

    public sealed class EnemyDef
    {
        public string Id;
        public string Name;

        /// <summary>Shown on the end of day report, "处理邮件 328 封" style.</summary>
        public string ReportVerb;

        public string ReportUnit = "个";
        public string ViewId;
        public string Behavior;
        public KvBag Param = KvBag.Empty;
        public float Hp;
        public float Speed;
        public float ContactDamage;
        public float Radius = 0.35f;
        public int Exp;
        public EnemyTier Tier;

        /// <summary>The boss numbers are absolute, the per day growth formula must not touch them.</summary>
        public bool IgnoreScaling;
    }

    public sealed class PickDef
    {
        public string EnemyId;
        public float Weight;
    }

    /// <summary>
    /// One random spawn channel. From / To carve the day into teaching windows, BudgetPct is that
    /// window's share of the day's total, which is what keeps "25 mails then 8 deadlines" authorable.
    /// </summary>
    public sealed class SpawnerDef
    {
        public float Interval = 2.5f;
        public int GroupSize = 4;
        public float From;
        public float To = 9999f;
        public float BudgetPct = 100f;
        public List<PickDef> Picks = new List<PickDef>(4);
    }

    public sealed class FixedSpawnDef
    {
        public string EnemyId;
        public int Count = 1;
        public float AtSecond;
        public Quality? GuaranteeDrop;

        /// <summary>Elites land in plain sight instead of walking in from off screen.</summary>
        public bool Entrance;
    }

    /// <summary>
    /// One working day. Duration is authored, the spawn total is derived from Density so changing a
    /// day's length never forces the designer to recompute the count by hand.
    /// </summary>
    public sealed class DayDef
    {
        public int Index;
        public string Label = "周一";

        /// <summary>Short form for the clock line. Label carries the day's theme text.</summary>
        public string Weekday = "周一";

        public float Duration = 40f;
        public float OffWorkSeconds = 3f;
        public float Density = 0.82f;

        /// <summary>Negative means "derive from Density". Day 6 is the only hand written total.</summary>
        public int TotalSpawnOverride = -1;

        public int ConcurrentMax = 30;
        public List<SpawnerDef> Spawners = new List<SpawnerDef>(2);
        public List<FixedSpawnDef> Fixed = new List<FixedSpawnDef>(2);

        public int TotalSpawn
        {
            get
            {
                if (TotalSpawnOverride >= 0)
                {
                    return TotalSpawnOverride;
                }

                return Mathf.CeilToInt(Density * Duration);
            }
        }
    }

    public sealed class ScalingDef
    {
        public float HpPerDay = 0.80f;
        public float DmgPerDay = 0.32f;
    }

    public sealed class ClockDef
    {
        public int StartHour = 9;
        public int EndHour = 21;
        public int SnapMinutes = 30;
    }

    /// <summary>
    /// Enemies are never allowed to appear inside the frustum, so the band follows the screen shape.
    /// A circular band would put the left and right points 5 units further out than the top and
    /// bottom ones, and the player would read that as "the side mobs are late".
    /// </summary>
    public sealed class SpawnBandDef
    {
        public float SemiX = 12f;
        public float SemiY = 7.5f;
        public int Sectors = 24;
        public float WeightLeft = 35f;
        public float WeightRight = 35f;
        public float WeightUp = 15f;
        public float WeightDown = 15f;
        public float OutwardPush = 1.5f;
        public float MinSeparation = 0.8f;
        public float EdgeMargin = 1f;
        public int Retries = 4;
        public int MinSectorsPerBurst = 2;
        public int MaxSectorsPerBurst = 4;

        /// <summary>Anything born closer than this to the player gets the contact damage grace window.</summary>
        public float GraceRadius = 3f;

        public float GraceSeconds = 0.5f;
    }

    /// <summary>
    /// Everything a quality tier changes about one weapon. Unspecified fields inherit from the tier
    /// below, so xml only states the delta and "white plus three deltas" stays readable.
    /// </summary>
    public sealed class WeaponTierDef
    {
        public Quality Q;

        // ProjectileLauncher
        public int ProjCount = 1;
        public float ProjSpacing = 0.3f;
        public int Pierce;
        public float Range = 6f;
        public float ProjSpeed = 12f;
        public float PinSeconds;

        // GroundAoe
        public float LockRange = 2.8f;
        public float BlastRadius = 1.5f;
        public int Slams = 1;
        public float SecondSlamPct = 60f;
        public float SlowPct;

        /// <summary>0 disables the "Ctrl + A" pass, otherwise every Nth attack hits the whole screen.</summary>
        public int SelectAllEvery;

        // Orbit
        public int OrbitCount = 1;
        public float OrbitRadius = 1.8f;
        public float OrbitDegPerSec = 90f;
        public float TetherDamagePct;

        // shared
        public float Knockback;

        public WeaponTierDef Clone(Quality q)
        {
            WeaponTierDef t = new WeaponTierDef();
            t.Q = q;
            t.ProjCount = ProjCount;
            t.ProjSpacing = ProjSpacing;
            t.Pierce = Pierce;
            t.Range = Range;
            t.ProjSpeed = ProjSpeed;
            t.PinSeconds = PinSeconds;
            t.LockRange = LockRange;
            t.BlastRadius = BlastRadius;
            t.Slams = Slams;
            t.SecondSlamPct = SecondSlamPct;
            t.SlowPct = SlowPct;
            t.SelectAllEvery = SelectAllEvery;
            t.OrbitCount = OrbitCount;
            t.OrbitRadius = OrbitRadius;
            t.OrbitDegPerSec = OrbitDegPerSec;
            t.TetherDamagePct = TetherDamagePct;
            t.Knockback = Knockback;
            return t;
        }
    }

    public sealed class WeaponDef
    {
        public string Id;
        public string Name;
        public string ViewId = "v_proj";
        public WeaponKind Kind;
        public float BaseDamage;

        /// <summary>Attacks per second before the haste formula is applied.</summary>
        public float Rate = 1f;

        public float AtkCoef = 1f;

        /// <summary>GroundAoe: the wind up before the keyboard lands. Without it the hit reads as a flash.</summary>
        public float WindupSeconds = 0.15f;

        /// <summary>Orbit: one card cannot re-hit the same enemy inside this window.</summary>
        public float SameTargetCd = 2f;

        public readonly WeaponTierDef[] Tiers = new WeaponTierDef[4];

        public WeaponTierDef Tier(Quality q)
        {
            WeaponTierDef t = Tiers[(int)q];
            return t ?? Tiers[0];
        }
    }

    public sealed class QualityCoefDef
    {
        public float White = 1.0f;
        public float Blue = 1.25f;
        public float Yellow = 1.6f;
        public float Orange = 2.1f;

        public float Get(Quality q)
        {
            switch (q)
            {
                case Quality.Blue: return Blue;
                case Quality.Yellow: return Yellow;
                case Quality.Orange: return Orange;
                default: return White;
            }
        }
    }

    public sealed class PlayerDef
    {
        public float MaxSan = 99f;
        public float Atk = 10f;
        public float CritChance = 5f;
        public float CritMulti = 200f;
        public float Def;
        public float Dodge;
        public float DodgeCap = 60f;
        public float MoveSpeed = 4.5f;
        public float Haste;
        public float Luck;
        public float InvulnAfterHit = 0.6f;

        /// <summary>Magnet radius: white, blue and coffee fly in. Wide enough to catch on a walk by.</summary>
        public float PickupRadius = 1.6f;

        /// <summary>Step radius: yellow and orange have to be walked over. That run is the payoff beat.</summary>
        public float StepPickupRadius = 0.6f;

        public float Radius = 0.4f;
    }

    public sealed class SkillDef
    {
        public string Id = "slack";
        public string Name = "摸鱼";
        public float Cd = 12f;
        public float InvulnDuration = 1.5f;
        public float HealPctMaxSan = 5f;
        public float PushRadius = 3f;
        public float PushForce = 8f;
    }

    public sealed class CameraDef
    {
        public float OrthographicSize = 6f;
        public float FollowLerp = 6f;
        public float Aspect = 16f / 9f;
    }

    /// <summary>
    /// Bounded arena, 30 x 17 units. A bounded field is what stops "run left forever" from being the
    /// optimal answer to pressure on a widescreen layout, and it is the precondition for the spawn
    /// band's edge fallback rule.
    /// </summary>
    public sealed class ArenaDef
    {
        public float HalfWidth = 15f;
        public float HalfHeight = 8.5f;
    }

    public sealed class ProgressionDef
    {
        public int MaxLevel = 9;
        public float ExpCoef = 10f;
        public float ExpPower = 1.55f;

        /// <summary>A drop that cannot beat what is worn still has to be worth walking over.</summary>
        public int DowngradeExp = 3;

        /// <summary>The joke of the whole game: the bar can never reach 100.</summary>
        public int KpiCap = 99;

        public int KpiTargetKills = 500;
        public int FinalSalary = 9996;

        public readonly string[] RankNames =
        {
            "实习生", "专员", "高级专员", "主管", "经理", "高级经理", "总监", "高级总监", "CEO",
        };
    }

    public sealed class CoffeeDef
    {
        public float ChancePct = 4f;

        /// <summary>Doubled while sanity is low. The player never notices, the quit rate does.</summary>
        public float LowSanChancePct = 8f;

        public float LowSanThresholdPct = 33f;
        public float HealPctMaxSan = 12f;
        public float HasteAddPct = 15f;
        public float BuffSeconds = 4f;
        public string ViewId = "v_coffee";
    }

    public sealed class QualityDef
    {
        public Quality Q;
        public float Weight;
        public int AffixCount;
        public Color Color = Color.white;
        public string Beam = "none";
        public float HitStop;
        public float Shake;
        public string Sfx;
        public bool Label;
        public bool AutoMagnet = true;
        public float BgmLowPass;
        public string DropLine;
    }

    /// <summary>value = base * qualityCoef * Random(0.85, 1.15), one formula for mains and affixes.</summary>
    public sealed class AffixDef
    {
        public string Id;
        public string Name;
        public StatKey Stat;
        public float Base;
        public bool Percent;
    }

    public sealed class ArmorStatDef
    {
        public StatKey Stat;
        public float Base;
        public bool Percent;
    }

    public sealed class ArmorBaseDef
    {
        public string Id;
        public string Name;
        public EquipSlot Slot = EquipSlot.Head;
        public string ViewId = "v_equip";
        public readonly List<ArmorStatDef> Mains = new List<ArmorStatDef>(2);
    }

    /// <summary>Mirror of Model.StatType kept in the config layer so xml can name a stat.</summary>
    public enum StatKey
    {
        MaxSan = 0,
        Atk = 1,
        CritChance = 2,
        CritMulti = 3,
        Def = 4,
        Dodge = 5,
        MoveSpeed = 6,
        Haste = 7,
        Luck = 8,
        PickupRadius = 9,
    }

    public sealed class LootDef
    {
        public readonly QualityDef[] Qualities = new QualityDef[4];
        public string LateBonusApplyTo = "yellow,orange";
        public float LateBonusPerDay = 0.25f;

        /// <summary>The first legendary has to land inside the three minute window.</summary>
        public float PityFirstLegendarySeconds = 120f;

        public float PityLegendarySeconds = 150f;

        public float MagnetSpeed = 14f;
        public float TossDuration = 0.4f;
        public int BounceCount = 2;
        public float EquipChancePct = 5f;

        /// <summary>Six weapon slots against three armour slots, so weapons drop more often.</summary>
        public float WeaponShare = 60f;

        public float ArmorShare = 40f;

        public readonly List<AffixDef> Affixes = new List<AffixDef>(16);
        public readonly List<ArmorBaseDef> ArmorBases = new List<ArmorBaseDef>(4);
    }

    public enum CardKind
    {
        Stat = 0,
        Equipment = 1,
        Skill = 2,
    }

    public sealed class CardDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public CardKind Kind;
        public float Weight = 1f;

        // Stat cards
        public StatKey Stat;
        public float Value;
        public bool Percent;

        // Skill cards
        public string Passive;
    }

    /// <summary>
    /// The only decision the player makes. Equipment cards exist because the shop is gone: without
    /// them looting would be something to watch rather than something to want.
    /// </summary>
    public sealed class CardPoolDef
    {
        public int Choices = 3;
        public float StatWeight = 45f;
        public float EquipWeight = 30f;
        public float SkillWeight = 25f;

        /// <summary>Index 1..6 by day. The one growth line the player can actually predict.</summary>
        public readonly Quality[] EquipQualityByDay = new Quality[7];

        public readonly List<CardDef> Cards = new List<CardDef>(24);
    }

    public sealed class ViewDef
    {
        public string Id;
        public string Prefab = "Quad_Basic";
        public Color Color = Color.white;
        public float Scale = 1f;
        public ViewShape Shape = ViewShape.Quad;
        public string SpriteSet;
        public float SpriteHeight;
        public float AnimationFps = 8f;
    }

    public sealed class SfxDef
    {
        public string Id;
        public string Clip;
        public float Volume = 1f;
        public int MaxConcurrent = 4;
        public AudioBus Bus = AudioBus.Sfx;
        public float GainDb;
        public float ThrottleSeconds = 0.06f;
        public float PitchJitter = 0.08f;
        public bool DuckExempt;
        public SynthKind Synth = SynthKind.Blip;
        public float Freq = 660f;
        public float Dur = 0.08f;
    }

    public sealed class BgmDef
    {
        public string Id;
        public string Clip;
        public float Volume = 0.5f;
        public float CutoffNormal = 22000f;
        public float CutoffDucked = 800f;
        public float CrossfadeSeconds = 0.5f;
        public float PitchPerDay;
        public float PhaseOneCutoff = 22000f;
        public float PhaseThreePitch = 1f;
        public float PhaseThreeVolumeDb;
    }

    public sealed class AudioDef
    {
        public int MaxSourcePool = 24;
        public float PitchJitter = 0.08f;
        public float ThrottleSeconds = 0.06f;
        public float SfxVolume = 1f;
        public float UiVolume = 1f;
        public float DuckVolumeDb = -6f;
        public float LowSanFadeSeconds = 0.2f;
        public readonly Dictionary<string, SfxDef> Sfx = new Dictionary<string, SfxDef>(24);
        public readonly Dictionary<string, BgmDef> Bgm = new Dictionary<string, BgmDef>(4);
    }
}
