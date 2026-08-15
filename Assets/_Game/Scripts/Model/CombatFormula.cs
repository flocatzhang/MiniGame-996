using OfficeHell.Config;
using UnityEngine;

namespace OfficeHell.Model
{
    /// <summary>
    /// Pure functions, one per formula in the design doc. Kept free of any state so a designer can
    /// read them straight against the spreadsheet and so they are trivially unit testable.
    /// </summary>
    public static class CombatFormula
    {
        /// <summary>The 99 theme is also the diminishing return constant for DEF and HASTE.</summary>
        public const float Base99 = 99f;

        /// <summary>
        /// weapon damage = (baseDamage * qualityCoef) + (atk * atkCoef * qualityCoef).
        /// The quality coefficient hits both terms, which is what makes atkCoef a build defining stat.
        /// </summary>
        public static float WeaponDamage(WeaponDef w, float qualityCoef, float atk)
        {
            return (w.BaseDamage + atk * w.AtkCoef) * qualityCoef;
        }

        /// <summary>Crit multiplier is a percentage in config, 200 means x2.</summary>
        public static float ApplyCrit(float damage, float critChancePct, float critMultiPct, out bool crit)
        {
            crit = Random.value * 100f < critChancePct;
            return crit ? damage * (critMultiPct * 0.01f) : damage;
        }

        /// <summary>
        /// incoming = raw * 99 / (99 + def). Asymptotic on purpose: flat subtraction gets stacked
        /// into invulnerability within a jam length session. 99 DEF is exactly half damage taken,
        /// which is the one number a player can work out in their head.
        /// </summary>
        public static float IncomingDamage(float raw, float def)
        {
            return raw * Base99 / (Base99 + Mathf.Max(0f, def));
        }

        /// <summary>Linear with a hard cap, dodge is the highest variance stat in the game.</summary>
        public static bool RollDodge(float dodgePct, float capPct)
        {
            float p = Mathf.Clamp(dodgePct, 0f, capPct);
            return Random.value * 100f < p;
        }

        /// <summary>
        /// interval = baseInterval * 99 / (99 + haste). Same shape as DEF so one mental model covers
        /// both, and cooldown never reaches zero no matter how much haste is stacked.
        /// </summary>
        public static float AttackInterval(float baseInterval, float hastePct)
        {
            return baseInterval * Base99 / (Base99 + Mathf.Max(0f, hastePct));
        }

        /// <summary>exp needed to go from level L to L+1 = ceil(coef * L^power), 10 and 1.55 by default.</summary>
        public static int ExpForLevel(int level, ProgressionDef p)
        {
            if (level < 1)
            {
                level = 1;
            }

            float coef = p != null ? p.ExpCoef : 10f;
            float power = p != null ? p.ExpPower : 1.55f;
            return Mathf.Max(1, Mathf.CeilToInt(coef * Mathf.Pow(level, power)));
        }

        /// <summary>
        /// KPI never reaches 100. progress = min(cap, floor(kills / target * 100)).
        /// The cap is the whole joke, so it lives in the formula rather than in a UI clamp.
        /// </summary>
        public static int KpiPercent(int kills, ProgressionDef p)
        {
            if (p == null || p.KpiTargetKills <= 0)
            {
                return 0;
            }

            int raw = Mathf.FloorToInt(kills * 100f / p.KpiTargetKills);
            return Mathf.Clamp(raw, 0, p.KpiCap);
        }

        /// <summary>
        /// weight = base * (1 + luck/100 * qualityTier) * lateBonus.
        /// Luck only scales the high tiers, otherwise stacking luck would dilute the good drops.
        /// </summary>
        public static float LootWeight(QualityDef q, float luck, int day, LootDef loot)
        {
            if (q.Weight <= 0f)
            {
                return 0f;
            }

            int tier = (int)q.Q;
            float w = q.Weight * (1f + luck * 0.01f * tier);

            if (tier >= (int)Quality.Yellow && !string.IsNullOrEmpty(loot.LateBonusApplyTo) &&
                loot.LateBonusApplyTo.IndexOf(q.Q.ToString(), System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                w *= 1f + loot.LateBonusPerDay * (day - 1);
            }

            return w;
        }

        /// <summary>
        /// Affix and armour main stat rolls share one formula: base * qualityCoef * Random(0.85, 1.15).
        /// One formula means a designer only ever tunes the base number.
        /// </summary>
        public static float RollStatValue(float baseValue, float qualityCoef)
        {
            return baseValue * qualityCoef * Random.Range(0.85f, 1.15f);
        }

        /// <summary>
        /// Pay is prorated by time served, so finishing all six days lands exactly on the fixed figure.
        /// Deriving it from the clock rather than from a per day table means the payout stays consistent
        /// with the joke no matter how the designer redistributes the day lengths.
        /// </summary>
        public static int Salary(float servedSeconds, float totalSeconds, ProgressionDef p)
        {
            int full = p != null ? p.FinalSalary : 9996;
            if (totalSeconds <= 0f)
            {
                return full;
            }

            return Mathf.Clamp(Mathf.RoundToInt(full * (servedSeconds / totalSeconds)), 0, full);
        }
    }
}
