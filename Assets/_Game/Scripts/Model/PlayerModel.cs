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

        /// <summary>
        /// Ctrl + A is rationed across the whole loadout rather than per slot, so this lives on the
        /// player and not on WeaponRuntime. Six orange keyboards each counting to five independently
        /// is six sweeps inside two seconds, which reads as one continuous effect rather than six.
        /// </summary>
        public float SelectAllReadyAt;
        public SlackPassive Passives;

        /// <summary>
        /// Rolled magnitude per passive, written once when the card is picked. The quality is kept
        /// alongside so a later hand can tell an upgrade from a duplicate.
        ///
        /// The amount is stored rather than the tier because resolving a tier needs the card table,
        /// and a model that reaches into Config to answer "how long is my invulnerability" is a
        /// dependency pointing the wrong way.
        /// </summary>
        const int PassiveKinds = 5;

        readonly float[] _passiveValue = new float[PassiveKinds];
        readonly float[] _passiveValue2 = new float[PassiveKinds];
        readonly Quality[] _passiveQuality = new Quality[PassiveKinds];

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

        /// <summary>
        /// When the current shield lapses on its own. The shield used to sit there until something
        /// broke it, which made the purple tier's control immunity permanent rather than a window,
        /// and turned three enemy types into decoration for anyone wearing a purple headphone.
        /// </summary>
        public float ShieldUntil;

        public float ShieldPeak;

        /// <summary>Body orange counts hits, so the counter has to survive across days.</summary>
        public int HitCount;

        /// <summary>Hits taken since the body orange guard last fired.</summary>
        public int HitsSinceGuard;

        /// <summary>Headphone orange grants one death save per run.</summary>
        public bool DeathSaveReady;

        /// <summary>
        /// Purple slipper. Coffee marks dropped behind the player, oldest overwritten first.
        ///
        /// A fixed ring rather than a pooled entity: the count has a hard ceiling, no stain needs an
        /// identity, and they belong to the player rather than to the run. A pool exists to absorb
        /// unbounded churn and there is none here.
        /// </summary>
        public const int StainSlots = 10;

        readonly Vector2[] _stainPos = new Vector2[StainSlots];
        readonly float[] _stainUntil = new float[StainSlots];
        int _stainNext;

        public float NextStainAt;

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
        /// Purple headphone. The three control effects in the game (PPT slow, veteran attack slow,
        /// boss global slow) are all answered by this one line, which is the point of the item.
        /// </summary>
        public bool ImmuneToControl
        {
            get { return HasShield && QualityOf(EquipSlot.Head) >= Quality.Purple; }
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
        /// Body purple doubles defence below a third of sanity. Read here rather than folded into the
        /// stat sheet so it tracks current sanity without a modifier rebuild every frame.
        /// </summary>
        public float EffectiveDef()
        {
            float def = Stats.Get(StatType.Def);
            float max = MaxSan;
            if (QualityOf(EquipSlot.Body) >= Quality.Purple && max > 0f && San < max * 0.33f)
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

        /// <summary>
        /// Records a passive at the rolled amount. A repeat pick replaces rather than stacks: these
        /// are five different shapes of the same skill, and two magnitudes for one shape would need
        /// an answer for whether a green roll on top of an orange one is a downgrade.
        /// </summary>
        public void GrantPassive(SlackPassive flag, Quality q, float value, float value2)
        {
            int i = PassiveIndex(flag);
            if (i < 0)
            {
                return;
            }

            Passives |= flag;
            _passiveQuality[i] = q;
            _passiveValue[i] = value;
            _passiveValue2[i] = value2;
        }

        public void DropStain(Vector2 at, float expiresAt)
        {
            _stainPos[_stainNext] = at;
            _stainUntil[_stainNext] = expiresAt;
            _stainNext = (_stainNext + 1) % StainSlots;
        }

        public Vector2 StainPos(int i)
        {
            return _stainPos[i];
        }

        /// <summary>Zero or a time already past means the slot is empty.</summary>
        public float StainUntil(int i)
        {
            return _stainUntil[i];
        }

        /// <summary>Zero when the passive is not owned, which is what makes every getter below a no-op.</summary>
        public float PassiveValue(SlackPassive flag)
        {
            int i = PassiveIndex(flag);
            return i >= 0 && (Passives & flag) != 0 ? _passiveValue[i] : 0f;
        }

        public float PassiveValue2(SlackPassive flag)
        {
            int i = PassiveIndex(flag);
            return i >= 0 && (Passives & flag) != 0 ? _passiveValue2[i] : 0f;
        }

        public Quality PassiveQuality(SlackPassive flag)
        {
            int i = PassiveIndex(flag);
            return i >= 0 ? _passiveQuality[i] : Quality.Green;
        }

        static int PassiveIndex(SlackPassive flag)
        {
            switch (flag)
            {
                case SlackPassive.DeepSlack: return 0;
                case SlackPassive.PaidBreak: return 1;
                case SlackPassive.ReversePua: return 2;
                case SlackPassive.ExtraLife: return 3;
                case SlackPassive.MassSlack: return 4;
                default: return -1;
            }
        }

        public float SkillInvulnSeconds(SkillDef def)
        {
            return def.InvulnDuration + PassiveValue(SlackPassive.DeepSlack);
        }

        public float SkillCd(SkillDef def)
        {
            return Mathf.Max(1f, def.Cd - PassiveValue(SlackPassive.PaidBreak));
        }

        public float SkillHealPct(SkillDef def)
        {
            return def.HealPctMaxSan * (1f + PassiveValue(SlackPassive.ExtraLife) * 0.01f);
        }

        public float SkillPushRadius(SkillDef def)
        {
            return def.PushRadius * (1f + PassiveValue(SlackPassive.MassSlack) * 0.01f);
        }

        /// <summary>Percent of ATK dealt inside the push radius. Zero unless 反向 PUA is owned.</summary>
        public float SkillDamagePct()
        {
            return PassiveValue(SlackPassive.ReversePua);
        }

        public float SkillStunSeconds()
        {
            return PassiveValue2(SlackPassive.MassSlack);
        }

        public Quality QualityOf(EquipSlot slot)
        {
            ArmorRuntime rt = Armor(slot);
            return rt != null && !rt.IsEmpty ? rt.Quality : Quality.Green;
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
            ShieldUntil = 0f;
            NextShieldAt = 0f;
            HitCount = 0;
            HitsSinceGuard = 0;
            DeathSaveReady = false;
            NextStainAt = 0f;
            _stainNext = 0;
            for (int i = 0; i < StainSlots; i++)
            {
                _stainPos[i] = Vector2.zero;
                _stainUntil[i] = 0f;
            }

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
            SelectAllReadyAt = 0f;
            Passives = SlackPassive.None;
            for (int i = 0; i < PassiveKinds; i++)
            {
                _passiveValue[i] = 0f;
                _passiveValue2[i] = 0f;
                _passiveQuality[i] = Quality.Green;
            }

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
