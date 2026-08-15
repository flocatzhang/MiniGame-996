using OfficeHell.Config;
using UnityEngine;

namespace OfficeHell.Model
{
    /// <summary>Passive upgrades to the one active skill. Picked from the level up cards.</summary>
    [System.Flags]
    public enum SlackPassive
    {
        None = 0,
        DeepSlack = 1,
        PaidBreak = 2,
        ReversePua = 4,
        ExtraLife = 8,
        MassSlack = 16,
    }

    public sealed class PlayerModel
    {
        public const int WeaponSlots = 6;
        public const int ArmorSlots = 3;

        public readonly StatSheet Stats = new StatSheet();
        public readonly WeaponRuntime[] Weapons = new WeaponRuntime[WeaponSlots];
        public readonly ArmorRuntime[] Armors = new ArmorRuntime[ArmorSlots];

        public Vector2 Pos;
        public Vector2 MoveIntent;
        public Vector2 Facing = Vector2.right;
        public float Radius = 0.4f;

        public float San;
        public float Shield;
        public float InvulnUntil;
        public float LastHitAt = -999f;
        public bool Alive = true;

        public int Level = 1;
        public int Exp;
        public int ExpToNext = 10;

        /// <summary>Level ups queued for the card panel. A double level up must not eat a choice.</summary>
        public int PendingLevelUps;

        public float SkillReadyAt;
        public float SkillInvulnUntil;
        public SlackPassive Passives;

        /// <summary>
        /// Aura contributions for this frame, one slot per channel. Systems write with Max, never with
        /// +=, so five PPTs still only slow by 25 percent. Cleared at the head of every frame.
        /// </summary>
        readonly float[] _aura = new float[3];

        public float GlobalSlowUntil;
        public float GlobalSlowPct;

        public float HasteBuffUntil;
        public float HasteBuffPct;

        // ---- armour driven state ----

        /// <summary>Headphone blue: a shield every ten seconds. Zero means no headphone is worn.</summary>
        public float NextShieldAt;

        public float ShieldPeak;

        /// <summary>Body orange counts hits, so the counter has to survive across days.</summary>
        public int HitCount;

        /// <summary>Headphone orange grants one death save per day.</summary>
        public bool DeathSaveReady;

        /// <summary>Debug only, driven from the validation panel.</summary>
        public bool GodMode;

        public PlayerModel()
        {
            for (int i = 0; i < WeaponSlots; i++)
            {
                Weapons[i] = new WeaponRuntime { Slot = i };
            }

            for (int i = 0; i < ArmorSlots; i++)
            {
                Armors[i] = new ArmorRuntime { Slot = (EquipSlot)(i + 1) };
            }
        }

        public float MaxSan
        {
            get { return Stats.Get(StatType.MaxSan); }
        }

        public bool HasShield
        {
            get { return Shield > 0f; }
        }

        /// <summary>
        /// Yellow headphone. The three control effects in the game (PPT slow, veteran attack slow,
        /// boss global slow) are all answered by this one line, which is the point of the item.
        /// </summary>
        public bool ImmuneToControl
        {
            get { return HasShield && QualityOf(EquipSlot.Head) >= Quality.Yellow; }
        }

        public bool IsInvulnerable(float now)
        {
            return GodMode || now < InvulnUntil || now < SkillInvulnUntil;
        }

        public void ClearAuras()
        {
            for (int i = 0; i < _aura.Length; i++)
            {
                _aura[i] = 0f;
            }
        }

        /// <summary>Same channel takes the strongest source, never the sum. Protective, not balance.</summary>
        public void ApplyAura(AuraChannel channel, float pct)
        {
            int i = (int)channel;
            if (pct > _aura[i])
            {
                _aura[i] = pct;
            }
        }

        public float Aura(AuraChannel channel)
        {
            return _aura[(int)channel];
        }

        public float EffectiveMoveSpeed(float now)
        {
            float speed = Stats.Get(StatType.MoveSpeed);
            if (ImmuneToControl)
            {
                return speed;
            }

            float slow = _aura[(int)AuraChannel.MoveSlow];
            if (now < GlobalSlowUntil)
            {
                slow = Mathf.Max(slow, GlobalSlowPct);
            }

            return speed * Mathf.Clamp01(1f - slow * 0.01f);
        }

        public float EffectiveHaste(float now)
        {
            float haste = Stats.Get(StatType.Haste);
            if (now < HasteBuffUntil)
            {
                haste += HasteBuffPct;
            }

            if (!ImmuneToControl)
            {
                haste -= _aura[(int)AuraChannel.AttackSlow];
            }

            return haste;
        }

        /// <summary>
        /// Body yellow doubles defence below a third of sanity. Read here rather than folded into the
        /// stat sheet so it tracks current sanity without a modifier rebuild every frame.
        /// </summary>
        public float EffectiveDef()
        {
            float def = Stats.Get(StatType.Def);
            float max = MaxSan;
            if (QualityOf(EquipSlot.Body) >= Quality.Yellow && max > 0f && San < max * 0.33f)
            {
                def *= 2f;
            }

            return def;
        }

        /// <summary>Magnet radius only. The step radius stays 0.6 no matter what is worn.</summary>
        public float MagnetRadius
        {
            get
            {
                float r = Stats.Get(StatType.PickupRadius);
                if (QualityOf(EquipSlot.Feet) >= Quality.Blue)
                {
                    r *= 1.5f;
                }

                return r;
            }
        }

        public float SkillInvulnSeconds(SkillDef def)
        {
            float s = def.InvulnDuration;
            if ((Passives & SlackPassive.DeepSlack) != 0)
            {
                s += 0.8f;
            }

            return s;
        }

        public float SkillCd(SkillDef def)
        {
            float cd = def.Cd;
            if ((Passives & SlackPassive.PaidBreak) != 0)
            {
                cd -= 3f;
            }

            return Mathf.Max(1f, cd);
        }

        public float SkillHealPct(SkillDef def)
        {
            float pct = def.HealPctMaxSan;
            if ((Passives & SlackPassive.ExtraLife) != 0)
            {
                pct *= 2f;
            }

            return pct;
        }

        public float SkillPushRadius(SkillDef def)
        {
            float r = def.PushRadius;
            if ((Passives & SlackPassive.MassSlack) != 0)
            {
                r *= 2f;
            }

            return r;
        }

        public Quality QualityOf(EquipSlot slot)
        {
            ArmorRuntime rt = Armor(slot);
            return rt != null && !rt.IsEmpty ? rt.Quality : Quality.White;
        }

        public ArmorRuntime Armor(EquipSlot slot)
        {
            int i = (int)slot - 1;
            return i >= 0 && i < ArmorSlots ? Armors[i] : null;
        }

        public void ResetFrom(PlayerDef def, ProgressionDef prog)
        {
            Stats.ClearModifiers();
            Stats.SetBase(StatType.MaxSan, def.MaxSan);
            Stats.SetBase(StatType.Atk, def.Atk);
            Stats.SetBase(StatType.CritChance, def.CritChance);
            Stats.SetBase(StatType.CritMulti, def.CritMulti);
            Stats.SetBase(StatType.Def, def.Def);
            Stats.SetBase(StatType.Dodge, def.Dodge);
            Stats.SetBase(StatType.MoveSpeed, def.MoveSpeed);
            Stats.SetBase(StatType.Haste, def.Haste);
            Stats.SetBase(StatType.Luck, def.Luck);
            Stats.SetBase(StatType.PickupRadius, def.PickupRadius);

            Radius = def.Radius;
            San = def.MaxSan;
            Shield = 0f;
            ShieldPeak = 0f;
            NextShieldAt = 0f;
            HitCount = 0;
            DeathSaveReady = false;
            Pos = Vector2.zero;
            MoveIntent = Vector2.zero;
            Facing = Vector2.right;
            InvulnUntil = 0f;
            SkillInvulnUntil = 0f;
            LastHitAt = -999f;
            Alive = true;
            Level = 1;
            Exp = 0;
            ExpToNext = CombatFormula.ExpForLevel(1, prog);
            PendingLevelUps = 0;
            SkillReadyAt = 0f;
            Passives = SlackPassive.None;
            ClearAuras();
            GlobalSlowUntil = 0f;
            GlobalSlowPct = 0f;
            HasteBuffUntil = 0f;
            HasteBuffPct = 0f;

            for (int i = 0; i < WeaponSlots; i++)
            {
                Weapons[i].Clear();
            }

            for (int i = 0; i < ArmorSlots; i++)
            {
                Armors[i].Clear();
            }
        }

        public bool Equip(int slot, WeaponDef def, Quality quality)
        {
            if (slot < 0 || slot >= WeaponSlots || def == null)
            {
                return false;
            }

            WeaponRuntime rt = Weapons[slot];
            rt.DefId = def.Id;
            rt.Def = def;
            rt.Quality = quality;
            rt.NextFireAt = 0f;
            rt.AttackCount = 0;
            return true;
        }

        public int FirstEmptySlot()
        {
            for (int i = 0; i < WeaponSlots; i++)
            {
                if (Weapons[i].IsEmpty)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Auto equip replaces the worst slot, not the first one. Quality is a safe judge because a
        /// higher tier is both bigger numbers and an unlocked behaviour, never a sidegrade.
        /// </summary>
        public int LowestQualityWeaponSlot()
        {
            int best = -1;
            Quality low = Quality.Orange;
            for (int i = 0; i < WeaponSlots; i++)
            {
                if (Weapons[i].IsEmpty)
                {
                    return i;
                }

                if (best < 0 || Weapons[i].Quality < low)
                {
                    low = Weapons[i].Quality;
                    best = i;
                }
            }

            return best;
        }

        public int EquippedCount()
        {
            int n = 0;
            for (int i = 0; i < WeaponSlots; i++)
            {
                if (!Weapons[i].IsEmpty)
                {
                    n++;
                }
            }

            return n;
        }

        public int ArmorCount()
        {
            int n = 0;
            for (int i = 0; i < ArmorSlots; i++)
            {
                if (!Armors[i].IsEmpty)
                {
                    n++;
                }
            }

            return n;
        }
    }
}
