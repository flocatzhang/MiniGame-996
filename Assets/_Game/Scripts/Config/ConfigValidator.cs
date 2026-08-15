using System.Collections.Generic;
using UnityEngine;

namespace OfficeHell.Config
{
    /// <summary>
    /// Cross reference pass. Xml gives up compile time checking, so every id reference and every
    /// value that has a known sane range is checked once at load and reported as a single block.
    /// </summary>
    public static class ConfigValidator
    {
        public static void Validate(ConfigManager cfg, List<string> report)
        {
            ValidateViewRefs(cfg, report);
            ValidateEnemies(cfg, report);
            ValidateDays(cfg, report);
            ValidateWeapons(cfg, report);
            ValidateLoot(cfg, report);
            ValidateCards(cfg, report);
            ValidateProgression(cfg, report);
            ValidateSpawnGeometry(cfg, report);
            ValidateAudio(cfg, report);
        }

        static void ValidateViewRefs(ConfigManager cfg, List<string> report)
        {
            foreach (KeyValuePair<string, EnemyDef> kv in cfg.Enemies)
            {
                if (!string.IsNullOrEmpty(kv.Value.ViewId) && !cfg.Views.ContainsKey(kv.Value.ViewId))
                {
                    report.Add("Enemy '" + kv.Key + "' references missing viewId '" + kv.Value.ViewId + "'");
                }
            }

            foreach (KeyValuePair<string, WeaponDef> kv in cfg.Weapons)
            {
                if (!string.IsNullOrEmpty(kv.Value.ViewId) && !cfg.Views.ContainsKey(kv.Value.ViewId))
                {
                    report.Add("Weapon '" + kv.Key + "' references missing viewId '" + kv.Value.ViewId + "'");
                }
            }

            for (int i = 0; i < cfg.Loot.ArmorBases.Count; i++)
            {
                ArmorBaseDef a = cfg.Loot.ArmorBases[i];
                if (!string.IsNullOrEmpty(a.ViewId) && !cfg.Views.ContainsKey(a.ViewId))
                {
                    report.Add("Armor '" + a.Id + "' references missing viewId '" + a.ViewId + "'");
                }
            }

            if (!cfg.Views.ContainsKey(cfg.Coffee.ViewId))
            {
                report.Add("Coffee references missing viewId '" + cfg.Coffee.ViewId + "'");
            }
        }

        static void ValidateEnemies(ConfigManager cfg, List<string> report)
        {
            float playerSpeed = cfg.Player.MoveSpeed;
            foreach (KeyValuePair<string, EnemyDef> kv in cfg.Enemies)
            {
                EnemyDef d = kv.Value;
                if (d.Hp <= 0f)
                {
                    report.Add("Enemy '" + d.Id + "' has hp " + d.Hp);
                }

                if (d.Radius <= 0f)
                {
                    report.Add("Enemy '" + d.Id + "' has radius " + d.Radius);
                }

                // Pressure has to come from being surrounded, never from being outrun.
                if (d.Speed >= playerSpeed)
                {
                    report.Add("Enemy '" + d.Id + "' speed " + d.Speed + " >= player moveSpeed " + playerSpeed +
                               ", it will outrun the player");
                }

                // Zero puts knockback back to being applied on every hit that carries it, which with
                // six slots firing is enough to hold a body off the player for the whole day.
                if (d.KnockbackCd <= 0f)
                {
                    report.Add("Enemy '" + d.Id + "' has knockbackCd " + d.KnockbackCd +
                               ", it can be knocked back continuously");
                }

                if (!string.IsNullOrEmpty(d.Behavior) && !Systems.EnemyBehaviorRegistry.Exists(d.Behavior))
                {
                    report.Add("Enemy '" + d.Id + "' references unknown behavior '" + d.Behavior + "'");
                }

                if (string.IsNullOrEmpty(d.ReportVerb) && d.Tier != EnemyTier.Boss)
                {
                    report.Add("Enemy '" + d.Id + "' has no reportVerb, it cannot appear on the end of day report");
                }
            }
        }

        static void ValidateDays(ConfigManager cfg, List<string> report)
        {
            if (cfg.Days.Count == 0)
            {
                report.Add("Days.xml produced no <Day> rows");
                return;
            }

            HashSet<int> seen = new HashSet<int>();
            for (int i = 0; i < cfg.Days.Count; i++)
            {
                DayDef w = cfg.Days[i];
                if (!seen.Add(w.Index))
                {
                    report.Add("Day index " + w.Index + " is declared more than once");
                }

                if (w.Spawners.Count == 0 && w.Fixed.Count == 0)
                {
                    report.Add("Day " + w.Index + " has neither <Spawner> nor <Fixed>, it will be empty");
                }

                if (w.ConcurrentMax <= 0)
                {
                    report.Add("Day " + w.Index + " concurrentMax is " + w.ConcurrentMax);
                }

                if (w.TotalSpawn <= 0)
                {
                    report.Add("Day " + w.Index + " resolves to totalSpawn " + w.TotalSpawn);
                }

                float budget = 0f;
                float lastArrival = 0f;
                for (int s = 0; s < w.Spawners.Count; s++)
                {
                    SpawnerDef sp = w.Spawners[s];
                    budget += sp.BudgetPct;
                    if (sp.BudgetPct > 0f)
                    {
                        lastArrival = Mathf.Max(lastArrival, Mathf.Min(sp.To, w.Duration));
                    }

                    if (sp.To <= sp.From)
                    {
                        report.Add("Day " + w.Index + " spawner window is empty, from " + sp.From + " to " + sp.To);
                    }

                    if (sp.From > w.Duration)
                    {
                        report.Add("Day " + w.Index + " spawner opens at " + sp.From +
                                   " which is past the day duration " + w.Duration + ", it will never fire");
                    }

                    float total = 0f;
                    for (int p = 0; p < sp.Picks.Count; p++)
                    {
                        total += sp.Picks[p].Weight;
                        if (cfg.Enemy(sp.Picks[p].EnemyId) == null)
                        {
                            report.Add("Day " + w.Index + " picks unknown enemyId '" + sp.Picks[p].EnemyId + "'");
                        }
                    }

                    if (total <= 0f)
                    {
                        report.Add("Day " + w.Index + " spawner total pick weight is " + total);
                    }
                }

                // The budget is what turns "40 seconds" into a count, so a day that does not sum to
                // 100 silently spawns more or fewer enemies than the density line promised.
                if (w.Spawners.Count > 0 && Mathf.Abs(budget - 100f) > 0.5f)
                {
                    report.Add("Day " + w.Index + " spawner budgetPct sums to " + budget.ToString("0.#") +
                               " instead of 100, the authored totalSpawn " + w.TotalSpawn + " will not be met");
                }

                // A window that closes early leaves the rest of the shift with nothing arriving, and
                // an empty office is not read as having cleared the day, it is read as the game having
                // stopped. Days that author their own total have authored their own pacing with it, so
                // Saturday is allowed to hand the last two thirds of the fight to the boss.
                if (w.Spawners.Count > 0 && w.TotalSpawnOverride < 0 && lastArrival < w.Duration - 0.5f)
                {
                    report.Add("Day " + w.Index + " stops spawning at " + lastArrival.ToString("0.#") +
                               "s of " + w.Duration + "s, leaving " + (w.Duration - lastArrival).ToString("0.#") +
                               "s with nothing arriving");
                }

                for (int f = 0; f < w.Fixed.Count; f++)
                {
                    if (cfg.Enemy(w.Fixed[f].EnemyId) == null)
                    {
                        report.Add("Day " + w.Index + " <Fixed> references unknown enemyId '" + w.Fixed[f].EnemyId + "'");
                    }

                    if (w.Fixed[f].AtSecond > w.Duration)
                    {
                        report.Add("Day " + w.Index + " <Fixed> atSecond " + w.Fixed[f].AtSecond +
                                   " is past the day duration " + w.Duration + ", it will never spawn");
                    }
                }
            }
        }

        static void ValidateWeapons(ConfigManager cfg, List<string> report)
        {
            if (cfg.Weapons.Count == 0)
            {
                report.Add("Weapons.xml produced no <Weapon> rows");
                return;
            }

            foreach (KeyValuePair<string, WeaponDef> kv in cfg.Weapons)
            {
                WeaponDef w = kv.Value;
                if (w.BaseDamage <= 0f && w.AtkCoef <= 0f)
                {
                    report.Add("Weapon '" + w.Id + "' has neither baseDamage nor atkCoef, it deals nothing");
                }

                for (int i = 0; i < w.Tiers.Length; i++)
                {
                    WeaponTierDef t = w.Tiers[i];
                    if (t == null)
                    {
                        report.Add("Weapon '" + w.Id + "' has no tier row for " + (Quality)i);
                        continue;
                    }

                    switch (w.Kind)
                    {
                        case WeaponKind.ProjectileLauncher:
                            if (t.ProjSpeed <= 0f)
                            {
                                report.Add("Weapon '" + w.Id + "' tier " + t.Q + " has projSpeed " + t.ProjSpeed);
                            }

                            if (t.Range <= 0f)
                            {
                                report.Add("Weapon '" + w.Id + "' tier " + t.Q + " has range " + t.Range);
                            }

                            break;

                        case WeaponKind.GroundAoe:
                            if (t.BlastRadius <= 0f)
                            {
                                report.Add("Weapon '" + w.Id + "' tier " + t.Q + " has blastRadius " + t.BlastRadius);
                            }

                            if (t.LockRange <= 0f)
                            {
                                report.Add("Weapon '" + w.Id + "' tier " + t.Q + " has lockRange " + t.LockRange);
                            }

                            // A slow that outlasts the interval that reapplies it is renewed before it
                            // ever lapses, so it stops being a debuff and becomes the enemy's speed.
                            // The weapon reads as doing nothing while quietly halving the whole field.
                            if (t.SlowPct > 0f && w.Rate > 0f && t.SlowSeconds >= 1f / w.Rate)
                            {
                                report.Add("Weapon '" + w.Id + "' tier " + t.Q + " slows for " + t.SlowSeconds +
                                           "s on a " + (1f / w.Rate).ToString("0.##") +
                                           "s interval, so the slow never lapses");
                            }

                            if (t.SelectAllEvery > 0 && t.SelectAllPct <= 0f)
                            {
                                report.Add("Weapon '" + w.Id + "' tier " + t.Q +
                                           " fires Ctrl+A every " + t.SelectAllEvery +
                                           " attacks but selectAllPct is " + t.SelectAllPct + ", it deals nothing");
                            }

                            // The sweep is the tier's payoff, so it has to out reach the strike it
                            // replaces. At or below blastRadius it is a normal strike that gave up its
                            // target lock, its knockback and half its damage for nothing.
                            if (t.SelectAllEvery > 0 && t.SelectAllRadius <= t.BlastRadius)
                            {
                                report.Add("Weapon '" + w.Id + "' tier " + t.Q + " has selectAllRadius " +
                                           t.SelectAllRadius + " against blastRadius " + t.BlastRadius +
                                           ", the Ctrl+A pass covers less than the strike it replaces");
                            }

                            // Six slots reaching their fifth attack independently is six sweeps inside
                            // two seconds. Without a shared cooldown the tier's set piece degrades into
                            // the ambient state of the fight, which is invisible rather than powerful.
                            if (t.SelectAllEvery > 0 && t.SelectAllSharedCd <= 0f)
                            {
                                report.Add("Weapon '" + w.Id + "' tier " + t.Q +
                                           " fires Ctrl+A with no selectAllSharedCd, so six slots can " +
                                           "sweep at once and the pass stops reading as an event");
                            }

                            break;

                        case WeaponKind.Orbit:
                            if (t.OrbitRadius <= 0f)
                            {
                                report.Add("Weapon '" + w.Id + "' tier " + t.Q + " has orbitRadius " + t.OrbitRadius);
                            }

                            if (Mathf.Abs(t.OrbitDegPerSec) < 1f)
                            {
                                report.Add("Weapon '" + w.Id + "' tier " + t.Q + " barely rotates, orbitDegPerSec " +
                                           t.OrbitDegPerSec);
                            }

                            break;
                    }
                }

                // Tiers must never regress: the whole promise of a higher quality is "strictly better".
                for (int i = 1; i < w.Tiers.Length; i++)
                {
                    WeaponTierDef lo = w.Tiers[i - 1];
                    WeaponTierDef hi = w.Tiers[i];
                    if (lo == null || hi == null)
                    {
                        continue;
                    }

                    if (w.Kind == WeaponKind.ProjectileLauncher && hi.ProjCount < lo.ProjCount)
                    {
                        report.Add("Weapon '" + w.Id + "' tier " + hi.Q + " fires fewer projectiles than " + lo.Q);
                    }

                    if (w.Kind == WeaponKind.Orbit && hi.OrbitCount < lo.OrbitCount)
                    {
                        report.Add("Weapon '" + w.Id + "' tier " + hi.Q + " orbits fewer cards than " + lo.Q);
                    }
                }
            }
        }

        static void ValidateLoot(ConfigManager cfg, List<string> report)
        {
            float randomWeight = 0f;
            for (int i = 0; i < cfg.Loot.Qualities.Length; i++)
            {
                QualityDef q = cfg.Loot.Qualities[i];
                randomWeight += Mathf.Max(0f, q.Weight);

                if (!string.IsNullOrEmpty(q.Sfx) && !cfg.Audio.Sfx.ContainsKey(q.Sfx))
                {
                    report.Add("Quality '" + q.Q + "' references missing sfx id '" + q.Sfx + "'");
                }

                // rankName is the only part of an item name that carries the tier, so two tiers sharing
                // a word means two different items arrive under one name. Nothing errors, the player
                // just loses the ability to tell an upgrade from a sidegrade by reading it.
                for (int j = 0; j < i; j++)
                {
                    if (cfg.Loot.Qualities[j].RankName == q.RankName)
                    {
                        report.Add("Quality '" + q.Q + "' and '" + cfg.Loot.Qualities[j].Q +
                                   "' share rankName '" + q.RankName +
                                   "', so both tiers produce the same item name");
                    }
                }
            }

            if (randomWeight <= 0f)
            {
                report.Add("every loot quality weight is zero, the random channel can never drop anything");
            }

            if (cfg.Loot.Qualities[(int)Quality.Orange].Weight > 0f)
            {
                report.Add("Quality 'Orange' has weight " + cfg.Loot.Qualities[(int)Quality.Orange].Weight +
                           " in the random channel. Pacing is only controllable when legendaries come " +
                           "from <Fixed> guarantees and the pity timer, so weight is expected to be 0.");
            }

            // applyTo is matched against the tier's enum name at roll time, so a spelling that resolves
            // to nothing does not fail, it just stops applying and the late day drops quietly stay at
            // day one odds. The white/yellow to green/purple rename makes that a live hazard for any
            // config file that outlived it.
            ValidateLateBonusTiers(cfg.Loot.LateBonusApplyTo, report);

            if (cfg.Loot.Affixes.Count == 0)
            {
                report.Add("Loot.xml produced no <Affix> rows, dropped equipment will grant nothing");
            }

            float share = cfg.Loot.WeaponShare + cfg.Loot.ArmorShare;
            if (Mathf.Abs(share - 100f) > 0.5f)
            {
                report.Add("Loot weaponShare + armorShare is " + share.ToString("0.#") + " instead of 100");
            }

            if (cfg.Loot.ArmorShare > 0f)
            {
                for (int s = (int)EquipSlot.Head; s <= (int)EquipSlot.Feet; s++)
                {
                    if (cfg.ArmorForSlot((EquipSlot)s) == null)
                    {
                        report.Add("no <Armor> row covers slot " + (EquipSlot)s + ", that slot can never fill");
                    }
                }
            }

            if (cfg.Loot.PityFirstLegendarySeconds > cfg.Loot.PityLegendarySeconds)
            {
                report.Add("Pity firstLegendarySeconds " + cfg.Loot.PityFirstLegendarySeconds +
                           " is longer than legendarySeconds " + cfg.Loot.PityLegendarySeconds +
                           ", the first legendary is meant to be the tightest guarantee");
            }
        }

        static void ValidateLateBonusTiers(string applyTo, List<string> report)
        {
            if (string.IsNullOrEmpty(applyTo))
            {
                return;
            }

            string[] parts = applyTo.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string name = parts[i].Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                bool known = false;
                for (int q = 0; q <= (int)Quality.Orange; q++)
                {
                    if (string.Equals(name, ((Quality)q).ToString(), System.StringComparison.OrdinalIgnoreCase))
                    {
                        known = true;
                    }
                }

                if (!known)
                {
                    report.Add("LateBonus applyTo lists '" + name + "', which is not a quality tier. " +
                               "The later day weighting silently does nothing for it. " +
                               "Tiers are green / blue / purple / orange.");
                }
            }
        }

        static void ValidateCards(ConfigManager cfg, List<string> report)
        {
            CardPoolDef pool = cfg.Cards;
            int stat = 0;
            int skill = 0;
            for (int i = 0; i < pool.Cards.Count; i++)
            {
                CardDef c = pool.Cards[i];
                if (c.Kind == CardKind.Stat)
                {
                    stat++;
                    if (Mathf.Abs(c.Value) < 0.0001f)
                    {
                        report.Add("Card '" + c.Id + "' is a stat card with value 0");
                    }
                }
                else if (c.Kind == CardKind.Skill)
                {
                    skill++;
                    if (string.IsNullOrEmpty(c.Passive))
                    {
                        report.Add("Card '" + c.Id + "' is a skill card with no passive id");
                    }

                    if (Mathf.Abs(c.Value) < 0.0001f)
                    {
                        report.Add("Card '" + c.Id + "' is a skill card with value 0, the passive would do nothing");
                    }
                }

                if (c.Kind == CardKind.Equipment)
                {
                    continue;
                }

                // The amount is only in the card face because the template put it there. Without the
                // placeholder the tiers are invisible and every quality reads as the same card.
                if (c.Desc != null && c.Desc.IndexOf("{v}") < 0)
                {
                    report.Add("Card '" + c.Id + "' desc has no {v} placeholder, its quality tier cannot be read");
                }

                bool hasSecond = c.Desc != null && c.Desc.IndexOf("{v2}") >= 0;
                bool setsSecond = Mathf.Abs(c.Value2) > 0.0001f;
                if (hasSecond != setsSecond)
                {
                    report.Add("Card '" + c.Id + "' disagrees about value2: desc " +
                               (hasSecond ? "uses" : "omits") + " {v2} while the value is " + c.Value2);
                }
            }

            // Saturday is already the top tier, so a chance to go above it silently does nothing.
            for (int d = 1; d < pool.QualityByDay.Length; d++)
            {
                if (pool.QualityByDay[d] >= Quality.Orange && pool.UpgradeChanceByDay[d] > 0f)
                {
                    report.Add("day " + d + " is already orange but carries upgradeChance " +
                               pool.UpgradeChanceByDay[d] + ", which can never apply");
                }
            }

            // Three choices per level up need at least as many candidates, otherwise the panel repeats.
            if (pool.StatWeight > 0f && stat < pool.Choices)
            {
                report.Add("only " + stat + " stat card(s) for " + pool.Choices + " choices, the panel will repeat");
            }

            if (pool.SkillWeight > 0f && skill == 0)
            {
                report.Add("skill cards are weighted at " + pool.SkillWeight + " but none are declared");
            }

            float weights = pool.StatWeight + pool.EquipWeight + pool.SkillWeight;
            if (weights <= 0f)
            {
                report.Add("every card kind weight is zero, level up cannot offer anything");
            }
        }

        static void ValidateProgression(ConfigManager cfg, List<string> report)
        {
            ProgressionDef p = cfg.Progression;
            if (p.RankNames.Length < p.MaxLevel)
            {
                report.Add("maxLevel " + p.MaxLevel + " exceeds the " + p.RankNames.Length + " declared rank names");
            }

            if (p.KpiCap >= 100)
            {
                report.Add("kpiCap is " + p.KpiCap + ". The bar reaching 100 removes the joke, expected 99.");
            }

            if (cfg.Player.MaxSan > 99f)
            {
                report.Add("player maxSan is " + cfg.Player.MaxSan + ", the 99 ceiling is a design constant");
            }

            if (cfg.Player.StepPickupRadius >= cfg.Player.PickupRadius)
            {
                report.Add("stepPickupRadius " + cfg.Player.StepPickupRadius + " >= pickupRadius " +
                           cfg.Player.PickupRadius + ", rare drops would be magnetised like common ones");
            }
        }

        static void ValidateSpawnGeometry(ConfigManager cfg, List<string> report)
        {
            // Anything spawned inside the view frustum pops into existence in front of the player.
            float halfHeight = cfg.Camera.OrthographicSize;
            float halfWidth = halfHeight * cfg.Camera.Aspect;
            SpawnBandDef band = cfg.Band;

            // Per axis checks are not enough and this is the trap worth spelling out: a band whose axes
            // both clear the screen half extents still cuts through all four corners. The ellipse
            // contains the rectangle only when the sum below is at most 1, and the failure mode of
            // getting it wrong is enemies appearing on camera at 45 degrees.
            float coverage = Sqr(halfWidth / Mathf.Max(0.01f, band.SemiX)) +
                             Sqr(halfHeight / Mathf.Max(0.01f, band.SemiY));

            if (coverage >= 1f)
            {
                report.Add("spawn band (" + band.SemiX.ToString("0.0") + ", " + band.SemiY.ToString("0.0") +
                           ") does not enclose the camera frame (" + halfWidth.ToString("0.0") + ", " +
                           halfHeight.ToString("0.0") + "): coverage " + coverage.ToString("0.00") +
                           " must be below 1 or enemies pop in on screen near the corners");
            }
            else if (coverage > 0.95f)
            {
                report.Add("spawn band coverage is " + coverage.ToString("0.00") +
                           ", enemies will appear a fraction of a unit outside the corner of the frame");
            }

            // The band is anchored on the player, so the whole reach has to fit inside the arena from
            // the centre. Otherwise the placement check fails on every attempt in those sectors and the
            // directional weighting silently degrades into "wherever the fallback lands".
            float reachX = band.SemiX + band.OutwardPush + band.EdgeMargin;
            float reachY = band.SemiY + band.OutwardPush + band.EdgeMargin;

            if (reachX > cfg.Arena.HalfWidth || reachY > cfg.Arena.HalfHeight)
            {
                report.Add("spawn band reach (" + reachX.ToString("0.0") + ", " + reachY.ToString("0.0") +
                           ") including outwardPush and edgeMargin does not fit the arena (" +
                           cfg.Arena.HalfWidth.ToString("0.0") + ", " + cfg.Arena.HalfHeight.ToString("0.0") +
                           "), those sectors will always fall back");
            }

            if (band.MaxSectorsPerBurst < band.MinSectorsPerBurst)
            {
                report.Add("spawn band maxSectors " + band.MaxSectorsPerBurst + " < minSectors " +
                           band.MinSectorsPerBurst);
            }

            if (band.Sectors < band.MaxSectorsPerBurst)
            {
                report.Add("spawn band has " + band.Sectors + " sectors but a burst wants " +
                           band.MaxSectorsPerBurst);
            }

            float weight = band.WeightLeft + band.WeightRight + band.WeightUp + band.WeightDown;
            if (weight <= 0f)
            {
                report.Add("every spawn band direction weight is zero, nothing can spawn");
            }

            if (band.GraceRadius > 0f && band.GraceSeconds <= 0f)
            {
                report.Add("spawn band graceRadius is set but graceSeconds is " + band.GraceSeconds +
                           ", a close spawn will still deal contact damage on frame one");
            }
        }

        static void ValidateAudio(ConfigManager cfg, List<string> report)
        {
            string[] requiredSfx =
            {
                "sfx_weapon_stapler_fire", "sfx_weapon_stapler_hit",
                "sfx_enemy_email_death", "sfx_enemy_bug_split",
                "sfx_player_hurt", "sfx_player_death", "sfx_player_lowsan_loop",
                "sfx_drop_green", "sfx_drop_blue", "sfx_drop_purple", "sfx_drop_orange",
                "sfx_drop_pickup", "sfx_drop_convert_xp", "sfx_coffee_drop", "sfx_coffee_drink",
                "sfx_growth_levelup", "sfx_growth_card_appear", "sfx_ui_clockin", "sfx_flow_dayend",
                "sfx_dodge", "sfx_shield_break", "sfx_skill", "sfx_slam", "sfx_select_all",
                "sfx_boss_phase", "sfx_ui_click",
            };
            string[] requiredBgm = { "bgm_login", "bgm_battle", "bgm_boss", "bgm_result" };

            for (int i = 0; i < requiredSfx.Length; i++)
            {
                if (!cfg.Audio.Sfx.ContainsKey(requiredSfx[i]))
                {
                    report.Add("Audio.xml is missing required sfx id '" + requiredSfx[i] + "'");
                }
            }

            for (int i = 0; i < requiredBgm.Length; i++)
            {
                if (!cfg.Audio.Bgm.ContainsKey(requiredBgm[i]))
                {
                    report.Add("Audio.xml is missing required bgm id '" + requiredBgm[i] + "'");
                }
            }

            foreach (KeyValuePair<string, SfxDef> kv in cfg.Audio.Sfx)
            {
                SfxDef sfx = kv.Value;
                if (sfx.MaxConcurrent <= 0)
                {
                    report.Add("Sfx '" + kv.Key + "' has maxConcurrent " + sfx.MaxConcurrent);
                }

                if (sfx.ThrottleSeconds < 0f)
                {
                    report.Add("Sfx '" + kv.Key + "' has negative throttleSeconds " + sfx.ThrottleSeconds);
                }
            }

            BgmDef battle;
            if (cfg.Audio.Bgm.TryGetValue("bgm_battle", out battle))
            {
                float saturdayPitch = 1f + battle.PitchPerDay * 5f;
                if (Mathf.Abs(saturdayPitch - 1.2f) > 0.001f)
                {
                    report.Add("bgm_battle Saturday pitch resolves to " + saturdayPitch.ToString("0.00") +
                               " instead of 1.20");
                }
            }
        }

        static float Sqr(float v)
        {
            return v * v;
        }
    }
}
