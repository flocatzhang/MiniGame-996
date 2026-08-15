using System.Collections.Generic;
using OfficeHell.Config;
using UnityEngine;

namespace OfficeHell.Model
{
    public sealed class EnemyModel
    {
        /// <summary>
        /// How fast an active knockback bleeds off, in units per second squared. Lives here rather than
        /// in the movement pass because it is what turns an authored distance into an impulse, and the
        /// two have to agree or the sheet stops describing what happens on screen.
        /// </summary>
        public const float KnockbackDecay = 8f;

        public int Id;
        public string DefId;
        public EnemyDef Def;

        public Vector2 Pos;
        public Vector2 Knockback;

        /// <summary>Internal cooldown on the knockback debuff, so it cannot be chained. See TryKnockback.</summary>
        public float KnockbackReadyAt;
        public float Hp;
        public float MaxHp;
        public float ContactDamage;
        public float Radius = 0.35f;

        /// <summary>Multiplier written by aura behaviours every frame, reset before the ai pass.</summary>
        public float SpeedMul = 1f;

        public bool IsDead;
        public float SpawnedAt;
        public float StunUntil;
        public float NextActionAt;
        public float TelegraphUntil;
        public float FlashUntil;

        /// <summary>
        /// Universal safety net. Anything born inside the band's grace radius deals no contact damage
        /// until this timestamp, so a split BUG or a summoned PPT can never chip the player on frame one.
        /// </summary>
        public float ContactArmedAt;

        /// <summary>Keyboard blue slows what it hits, so the slow lives on the target not the weapon.</summary>
        public float SlowUntil;

        public float SlowPct;

        /// <summary>Stapler orange pins its target in place for half a second.</summary>
        public float PinUntil;

        /// <summary>
        /// Published by aura behaviours at spawn so the view can draw the ground ring without
        /// re-parsing behaviour params every frame. An invisible debuff reads as the game cheating.
        /// </summary>
        public float AuraRadius;

        public AuraChannel AuraKind;

        /// <summary>Set by <Fixed> rows so elites and the boss always drop what the pacing needs.</summary>
        public Quality? GuaranteedDrop;

        // ---- boss only ----

        /// <summary>Three bars of 9999. Hp is the current bar, BarsLeft counts down to zero.</summary>
        public int BarsLeft;

        public int BarsTotal;
        public int Phase = 1;
        public float InvulnUntil;
        public float PieReadyAt;
        public float MeetingReadyAt;
        public float KpiReadyAt;
        public float RainReadyAt;

        public bool IsBoss
        {
            get { return BarsTotal > 0; }
        }

        /// <summary>Total health across every remaining bar, which is what the three stacked bars draw.</summary>
        public float TotalHpLeft
        {
            get { return BarsTotal > 0 ? Hp + MaxHp * (BarsLeft - 1) : Hp; }
        }

        public bool CanTouch(float now)
        {
            return now >= ContactArmedAt;
        }

        public float EffectiveSpeed(float now)
        {
            if (now < PinUntil || now < StunUntil)
            {
                return 0f;
            }

            float speed = Def != null ? Def.Speed : 0f;
            speed *= SpeedMul;
            if (now < SlowUntil)
            {
                speed *= Mathf.Clamp01(1f - SlowPct * 0.01f);
            }

            return speed;
        }

        /// <summary>
        /// Knockback is authored as the distance the enemy should travel, which is the unit the design
        /// sheet states it in ("击退 1 unit") and the only one a designer can verify by watching the
        /// screen. The conversion to an impulse is v = sqrt(2 * decay * distance).
        ///
        /// The cooldown is the point of this method. Impulses used to be added on top of whatever was
        /// already there, so six slots landing on one frame stacked six pushes, and an enemy under
        /// sustained fire was shoved backwards indefinitely without ever closing. One push per window,
        /// and a blocked push is dropped rather than queued: a queued one would arrive after the shot
        /// that earned it and read as a shove out of nowhere.
        /// </summary>
        public bool TryKnockback(Vector2 dir, float distance, float now)
        {
            if (distance <= 0f || dir.sqrMagnitude < 0.0001f || now < KnockbackReadyAt)
            {
                return false;
            }

            Knockback = dir.normalized * Mathf.Sqrt(2f * KnockbackDecay * distance);
            KnockbackReadyAt = now + (Def != null ? Def.KnockbackCd : 1.2f);
            return true;
        }

        /// <summary>
        /// The two panic buttons ignore the cooldown: 摸鱼 and the orange headphone shield shattering.
        /// Both carry their own long cooldown and exist to break a surround, so silently losing one
        /// because a stapler needle landed a moment earlier would spend the player's escape at exactly
        /// the moment they reached for it. They still open a fresh window behind them.
        /// </summary>
        public void ForceKnockback(Vector2 dir, float distance, float now)
        {
            KnockbackReadyAt = 0f;
            TryKnockback(dir, distance, now);
        }

        public void Reset()
        {
            Id = 0;
            DefId = null;
            Def = null;
            Pos = Vector2.zero;
            Knockback = Vector2.zero;
            KnockbackReadyAt = 0f;
            Hp = 0f;
            MaxHp = 0f;
            ContactDamage = 0f;
            Radius = 0.35f;
            SpeedMul = 1f;
            IsDead = false;
            SpawnedAt = 0f;
            StunUntil = 0f;
            NextActionAt = 0f;
            TelegraphUntil = 0f;
            FlashUntil = 0f;
            ContactArmedAt = 0f;
            SlowUntil = 0f;
            SlowPct = 0f;
            PinUntil = 0f;
            AuraRadius = 0f;
            AuraKind = AuraChannel.MoveSlow;
            GuaranteedDrop = null;
            BarsLeft = 0;
            BarsTotal = 0;
            Phase = 1;
            InvulnUntil = 0f;
            PieReadyAt = 0f;
            MeetingReadyAt = 0f;
            KpiReadyAt = 0f;
            RainReadyAt = 0f;
        }
    }

    public sealed class ProjectileModel
    {
        public int Id;
        public string ViewId;
        public Vector2 Pos;
        public Vector2 Vel;
        public Vector2 Origin;
        public float Radius = 0.16f;
        public float Damage;
        public int PierceLeft;
        public float DieAt;
        public bool IsDead;
        public bool FromEnemy;

        /// <summary>Stapler orange. Applied on hit, not on the weapon, so it survives the projectile.</summary>
        public float PinSeconds;

        public float Knockback;

        /// <summary>Range is one number in xml and means both lock radius and max flight distance.</summary>
        public float MaxDistance;

        readonly List<int> _hit = new List<int>(4);

        public bool AlreadyHit(int enemyId)
        {
            return _hit.Contains(enemyId);
        }

        public void MarkHit(int enemyId)
        {
            _hit.Add(enemyId);
        }

        public void Reset()
        {
            Id = 0;
            ViewId = null;
            Pos = Vector2.zero;
            Vel = Vector2.zero;
            Origin = Vector2.zero;
            Radius = 0.16f;
            Damage = 0f;
            PierceLeft = 0;
            DieAt = 0f;
            IsDead = false;
            FromEnemy = false;
            PinSeconds = 0f;
            Knockback = 0f;
            MaxDistance = 0f;
            _hit.Clear();
        }
    }

    /// <summary>
    /// One keyboard strike. It stores the position it locked, never the target it locked, because
    /// "the enemy walked out from under it" is feedback rather than a bug to be fixed.
    /// </summary>
    public sealed class SlamModel
    {
        public int Id;
        public int Slot;
        public Vector2 Target;

        /// <summary>Where the keyboard flies in from, so the player can see which slot fired.</summary>
        public Vector2 From;

        public float BornAt;
        public float LandAt;
        public float Radius;
        public float Damage;
        public float Knockback;
        public float SlowPct;
        public float SlowSeconds;

        /// <summary>
        /// Keyboard orange. Every fifth attack centres on the player instead of on a locked enemy and
        /// carries the far larger sweep radius. It resolves through the same circle as every other
        /// strike: the flag only marks it for the flourish the presentation layer puts on top.
        /// </summary>
        public bool SelectAll;

        public bool IsDead;

        public float Progress01(float now)
        {
            float span = LandAt - BornAt;
            return span <= 0f ? 1f : Mathf.Clamp01((now - BornAt) / span);
        }

        public void Reset()
        {
            Id = 0;
            Slot = 0;
            Target = Vector2.zero;
            From = Vector2.zero;
            BornAt = 0f;
            LandAt = 0f;
            Radius = 0f;
            Damage = 0f;
            Knockback = 0f;
            SlowPct = 0f;
            SlowSeconds = 0f;
            SelectAll = false;
            IsDead = false;
        }
    }

    /// <summary>
    /// One badge on the orbit. Phase is assigned from the global card total, not from the slot, so
    /// six green badges from six slots still form an even ring.
    /// </summary>
    public sealed class OrbitCardModel
    {
        public int Id;
        public int Slot;
        public string ViewId;
        public Vector2 Pos;
        public float PhaseDeg;
        public float Radius;
        public float DegPerSec;
        public float HitRadius = 0.3f;
        public float Damage;
        public float Knockback;

        /// <summary>Orange links every card into one ring, and the rope itself deals a share of the hit.</summary>
        public bool Tethered;

        public float TetherPct;
        public float TetherDamage;

        // A short parallel list beats a Dictionary here: at most a handful of enemies touch one card
        // inside its two second window, and this never allocates after the first few hits.
        readonly List<int> _ids = new List<int>(8);
        readonly List<float> _until = new List<float>(8);

        public bool CanHit(int enemyId, float now)
        {
            for (int i = 0; i < _ids.Count; i++)
            {
                if (_ids[i] == enemyId)
                {
                    return now >= _until[i];
                }
            }

            return true;
        }

        public void MarkHit(int enemyId, float readyAt, float now)
        {
            for (int i = _ids.Count - 1; i >= 0; i--)
            {
                if (_ids[i] == enemyId)
                {
                    _until[i] = readyAt;
                    return;
                }

                if (_until[i] <= now)
                {
                    _ids.RemoveAt(i);
                    _until.RemoveAt(i);
                }
            }

            _ids.Add(enemyId);
            _until.Add(readyAt);
        }

        public void ClearHits()
        {
            _ids.Clear();
            _until.Clear();
        }
    }

    /// <summary>
    /// A ground marker that resolves once. Covers the boss meeting summon, the KPI rain and the elite
    /// entrance: all three are "draw a circle, wait, then make something happen at that spot".
    /// </summary>
    public sealed class TelegraphModel
    {
        public int Id;
        public Vector2 Pos;
        public float Radius;
        public float BornAt;
        public float FireAt;

        /// <summary>Zero means the circle is pure warning, which is what the elite entrance uses.</summary>
        public float Damage;

        public float Knockback;
        public string SummonEnemyId;
        public int SummonCount;

        /// <summary>Carried through so an elite entrance still honours its guaranteed drop.</summary>
        public Quality? SummonDrop;

        public string ViewId = "v_warn";
        public bool IsDead;

        public float Progress01(float now)
        {
            float span = FireAt - BornAt;
            return span <= 0f ? 1f : Mathf.Clamp01((now - BornAt) / span);
        }

        public void Reset()
        {
            Id = 0;
            Pos = Vector2.zero;
            Radius = 0f;
            BornAt = 0f;
            FireAt = 0f;
            Damage = 0f;
            Knockback = 0f;
            SummonEnemyId = null;
            SummonCount = 0;
            SummonDrop = null;
            ViewId = "v_warn";
            IsDead = false;
        }
    }

    public enum LootKind
    {
        Coffee = 0,
        Weapon = 1,
        Armor = 2,
    }

    public enum LootState
    {
        Tossing = 0,
        Idle = 1,
        Magnet = 2,
    }

    public sealed class LootModel
    {
        public int Id;
        public LootKind Kind;
        public Quality Quality;
        public string Name;
        public string ViewId;

        /// <summary>Weapon drops carry a WeaponDef id, armour drops carry an ArmorBaseDef id.</summary>
        public string SourceDefId;

        public EquipSlot Slot;

        public Vector2 From;
        public Vector2 To;
        public Vector2 Pos;
        public float TossT;
        public float BornAt;
        public LootState State;
        public bool IsDead;

        /// <summary>Rolled at drop time, applied on pickup. Empty for coffee.</summary>
        public readonly List<StatModifier> Mods = new List<StatModifier>(5);

        public readonly List<string> AffixNames = new List<string>(4);

        /// <summary>Green, blue and coffee fly in. Purple and orange have to be walked over.</summary>
        public bool AutoMagnet
        {
            get { return Kind == LootKind.Coffee || Quality <= Quality.Blue; }
        }

        public void Reset()
        {
            Id = 0;
            Kind = LootKind.Coffee;
            Quality = Quality.Green;
            Name = null;
            ViewId = null;
            SourceDefId = null;
            Slot = EquipSlot.Weapon;
            From = Vector2.zero;
            To = Vector2.zero;
            Pos = Vector2.zero;
            TossT = 0f;
            BornAt = 0f;
            State = LootState.Tossing;
            IsDead = false;
            Mods.Clear();
            AffixNames.Clear();
        }
    }

    /// <summary>Per slot weapon state: which def, which quality, and when it may fire again.</summary>
    public sealed class WeaponRuntime
    {
        public int Slot;
        public string DefId;
        public WeaponDef Def;
        public Quality Quality;
        public float NextFireAt;
        public float LastFiredAt;

        /// <summary>
        /// Set when the slot is off cooldown but found nothing to shoot. The retry poll keeps pushing
        /// NextFireAt forward, so without this the readout would report a cooldown that never ends.
        /// </summary>
        public bool WaitingForTarget;

        /// <summary>Keyboard orange counts to five, so the counter belongs to the slot.</summary>
        public int AttackCount;

        public bool IsEmpty
        {
            get { return Def == null; }
        }

        public WeaponTierDef Tier
        {
            get { return Def != null ? Def.Tier(Quality) : null; }
        }

        /// <summary>
        /// Six identical weapons fire on the same frame forever unless their phase is offset, which
        /// collapses six shots into one visible shot and puts a periodic spike on the frame time.
        /// </summary>
        public float PhaseOffset
        {
            get { return 0.05f * Slot; }
        }

        public void Clear()
        {
            DefId = null;
            Def = null;
            Quality = Quality.Green;
            NextFireAt = 0f;
            LastFiredAt = 0f;
            WaitingForTarget = false;
            AttackCount = 0;
        }
    }

    /// <summary>One of the three armour slots. Quality is kept because it gates the on-hit effects.</summary>
    public sealed class ArmorRuntime
    {
        public EquipSlot Slot;
        public string DefId;
        public ArmorBaseDef Def;
        public Quality Quality;
        public string Name;
        public int SourceId;

        public bool IsEmpty
        {
            get { return Def == null; }
        }

        public void Clear()
        {
            DefId = null;
            Def = null;
            Quality = Quality.Green;
            Name = null;
            SourceId = 0;
        }
    }
}
