using System.Collections.Generic;
using System.Text;
using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using OfficeHell.Systems;
using OfficeHell.View;
using UnityEditor;
using UnityEngine;

namespace OfficeHell.EditorTools
{
    /// <summary>
    /// Headless simulation of the whole systems layer. Nothing in Systems or Model is a
    /// MonoBehaviour, so a full run can be driven from the editor with no scene, no view and no
    /// audio, which makes a pacing regression a 20 second check instead of a 10 minute playtest.
    ///
    /// Run from the menu, or in ci with
    ///   Unity.exe -batchmode -quit -projectPath &lt;dir&gt; -executeMethod OfficeHell.EditorTools.OfficeHellSelfTest.RunBatch
    /// </summary>
    public static class OfficeHellSelfTest
    {
        const float FixedDelta = 1f / 60f;
        const int MaxFrames = 200000;

        [MenuItem("Office Hell/Run Headless Self Test", false, 21)]
        public static void RunMenu()
        {
            Report report = Run();
            if (report.Failures.Count > 0)
            {
                Debug.LogError(report.ToString());
            }
            else
            {
                Debug.Log(report.ToString());
            }
        }

        public static void RunBatch()
        {
            Report report = Run();
            Debug.Log(report.ToString());

            if (report.Failures.Count > 0)
            {
                Debug.LogError("[SelfTest] FAILED with " + report.Failures.Count + " issue(s)");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[SelfTest] PASSED");
            EditorApplication.Exit(0);
        }

        public sealed class Report
        {
            public readonly List<string> Failures = new List<string>();
            public readonly StringBuilder Log = new StringBuilder();

            public void Fail(string message)
            {
                Failures.Add(message);
            }

            public void Require(bool condition, string message)
            {
                if (!condition)
                {
                    Failures.Add(message);
                }
            }

            public void Line(string message)
            {
                Log.Append('\n').Append(message);
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder("[SelfTest]");
                sb.Append(Log);
                if (Failures.Count > 0)
                {
                    sb.Append("\n").Append(Failures.Count).Append(" failure(s):");
                    for (int i = 0; i < Failures.Count; i++)
                    {
                        sb.Append("\n  - ").Append(Failures[i]);
                    }
                }

                return sb.ToString();
            }
        }

        public static Report Run()
        {
            Report report = new Report();

            ConfigManager cfg = new ConfigManager(new XmlConfigSource("Config"));
            cfg.Load();

            report.Line("config: enemies " + cfg.Enemies.Count + ", days " + cfg.Days.Count +
                        ", weapons " + cfg.Weapons.Count + ", views " + cfg.Views.Count +
                        ", cards " + cfg.Cards.Cards.Count + ", issues " + cfg.Report.Count);

            report.Require(cfg.Loaded, "ConfigManager.Load reported not loaded");
            report.Require(cfg.Report.Count == 0,
                "config validation produced " + cfg.Report.Count + " issue(s), see the Console block above");

            if (!cfg.Loaded)
            {
                return report;
            }

            TestFormulas(report, cfg);
            TestArtAssets(report, cfg);
            TestAudioAssets(report, cfg);
            TestPointerFollow(report, cfg);
            TestClockProjection(report, cfg);
            TestSpawnGeometry(report, cfg);
            TestSpawnBandKeepsDistance(report, cfg);
            TestFullRun(report, cfg);
            TestRestartLeavesNoResidue(report, cfg);

            return report;
        }

        static void TestArtAssets(Report report, ConfigManager cfg)
        {
            report.Require(ArtCatalog.Map != null, "office map art is missing from Resources");
            report.Require(ArtCatalog.Logo != null, "main logo art is missing from Resources");
            report.Require(ArtCatalog.Pie != null, "boss pie art is missing from Resources");

            string[] sets =
            {
                "player", "deadline", "mail", "ppt", "bug",
                "report", "veteran", "leader", "boss",
            };
            int[] expected = { 3, 5, 6, 4, 6, 4, 6, 4, 4 };

            for (int i = 0; i < sets.Length; i++)
            {
                Sprite[] frames = ArtCatalog.Frames(sets[i]);
                report.Require(frames.Length == expected[i],
                    "art set '" + sets[i] + "' has " + frames.Length + " frame(s), expected " + expected[i]);
            }

            ViewDef smallBug = cfg.View("v_bug_small");
            report.Require(smallBug != null && smallBug.SpriteSet == "bug",
                "small BUG should reuse the BUG animation set");

            EntityView playerView = EntityView.Create("PlayerFacingSelfTest", 0);
            playerView.Bind(cfg.View("v_player"), ViewShape.Circle, false);
            playerView.TickAnimation(0f, 1f, false);
            report.Require(playerView.Body.flipX,
                "player source frames face left and should flip when facing right");
            playerView.TickAnimation(0f, -1f, false);
            report.Require(!playerView.Body.flipX,
                "player source frames should remain unflipped when facing left");
            Object.DestroyImmediate(playerView.gameObject);
        }

        static void TestAudioAssets(Report report, ConfigManager cfg)
        {
            string[] sfx =
            {
                "sfx_coffee_drink", "sfx_coffee_drop", "sfx_drop_convert_xp", "sfx_drop_pickup",
                "sfx_enemy_bug_split", "sfx_enemy_email_death", "sfx_flow_dayend",
                "sfx_growth_card_appear", "sfx_growth_levelup", "sfx_player_death",
                "sfx_player_hurt", "sfx_ui_clockin",
                "sfx_weapon_stapler_fire", "sfx_weapon_stapler_hit",
            };
            string[] drops = { "sfx_drop_white", "sfx_drop_blue", "sfx_drop_yellow", "sfx_drop_orange" };
            float[] dropLengths = { 1.676f, 2.259f, 2.276f, 3.305f };
            int[] dropRates = { 48000, 48000, 11000, 48000 };
            string[] bgm = { "bgm_login", "bgm_battle", "bgm_boss", "bgm_result" };

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Game/Audio" });
            report.Require(guids.Length == 23,
                "derived audio directory contains " + guids.Length + " AudioClip asset(s), expected exactly 23");
            report.Require(cfg.Audio.MaxSourcePool == 24,
                "Audio.xml maxSourcePool is " + cfg.Audio.MaxSourcePool + ", expected 24");

            for (int i = 0; i < sfx.Length; i++)
            {
                string resource = "Audio/SFX/" + sfx[i];
                AudioClip clip = Resources.Load<AudioClip>(resource);
                report.Require(clip != null, "audio clip is missing from Resources: " + resource);
                if (clip != null)
                {
                    report.Require(clip.channels == 1, resource + " has " + clip.channels + " channels, expected mono");
                }

                RequireAudioImport(report,
                    "Assets/_Game/Audio/Resources/Audio/SFX/" + sfx[i] + ".wav",
                    true, false, true, AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.PCM, 1f);
            }

            for (int i = 0; i < drops.Length; i++)
            {
                string resource = "Audio/Drop/" + drops[i];
                AudioClip clip = Resources.Load<AudioClip>(resource);
                report.Require(clip != null, "drop clip is missing from Resources: " + resource);
                if (clip != null)
                {
                    report.Require(clip.channels == 2,
                        resource + " has " + clip.channels + " channels, expected stereo");
                    report.Require(clip.frequency == dropRates[i],
                        resource + " is " + clip.frequency + "Hz, expected " + dropRates[i] + "Hz");
                    report.Require(Mathf.Abs(clip.length - dropLengths[i]) <= 0.01f,
                        resource + " length is " + clip.length.ToString("0.000") +
                        "s, expected " + dropLengths[i].ToString("0.000") + "s");
                }

                RequireAudioImport(report,
                    "Assets/_Game/Audio/Resources/Audio/Drop/" + drops[i] + ".wav",
                    false, false, true, AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.Vorbis, 0.80f);
            }

            AudioClip lowSan = Resources.Load<AudioClip>("Audio/Loop/sfx_player_lowsan_loop");
            report.Require(lowSan != null, "low SAN loop is missing from Resources");
            if (lowSan != null)
            {
                report.Require(lowSan.channels == 1,
                    "low SAN loop has " + lowSan.channels + " channels, expected mono");
            }
            RequireAudioImport(report,
                "Assets/_Game/Audio/Resources/Audio/Loop/sfx_player_lowsan_loop.wav",
                true, false, false, AudioClipLoadType.CompressedInMemory, AudioCompressionFormat.Vorbis, 0.70f);

            for (int i = 0; i < bgm.Length; i++)
            {
                string resource = "Audio/BGM/" + bgm[i];
                AudioClip clip = Resources.Load<AudioClip>(resource);
                report.Require(clip != null, "BGM is missing from Resources: " + resource);
                if (clip != null)
                {
                    report.Require(clip.channels == 2, resource + " has " + clip.channels + " channels, expected stereo");
                }

                RequireAudioImport(report,
                    "Assets/_Game/Audio/Resources/Audio/BGM/" + bgm[i] + ".ogg",
                    false, true, false, AudioClipLoadType.Streaming, AudioCompressionFormat.Vorbis, 0.65f);
            }

            AudioClip email = Resources.Load<AudioClip>("Audio/SFX/sfx_enemy_email_death");
            AudioClip bug = Resources.Load<AudioClip>("Audio/SFX/sfx_enemy_bug_split");
            report.Require(email != null && Mathf.Abs(email.length - 0.30f) <= 0.01f,
                "email death clip should be 0.30s after the derived-only trim");
            report.Require(bug != null && Mathf.Abs(bug.length - 0.48f) <= 0.01f,
                "BUG split clip should retain its original 0.48s duration");

            HashSet<string> dropClips = new HashSet<string>();
            for (int i = 0; i < drops.Length; i++)
            {
                dropClips.Add(cfg.Audio.Sfx[drops[i]].Clip);
            }
            report.Require(dropClips.Count == 4,
                "the four quality keys should reference four distinct delivered drop clips");
            report.Require(cfg.Audio.Sfx["sfx_growth_card_appear"].Clip == "SFX/sfx_growth_card_appear",
                "card appearance should use its newly delivered dedicated clip");
            report.Require(Mathf.Abs(cfg.Audio.Sfx["sfx_drop_white"].Volume - 0.50f) < 0.001f &&
                           Mathf.Abs(cfg.Audio.Sfx["sfx_drop_blue"].Volume - 0.46f) < 0.001f &&
                           Mathf.Abs(cfg.Audio.Sfx["sfx_drop_yellow"].Volume - 1.00f) < 0.001f &&
                           Mathf.Abs(cfg.Audio.Sfx["sfx_drop_orange"].Volume - 0.90f) < 0.001f,
                "drop quality volume ladder should be 0.50 / 0.46 / 1.00 / 0.90");
            report.Require(Mathf.Abs(cfg.Audio.SfxVolume - 0.707946f) < 0.001f &&
                           Mathf.Abs(cfg.Audio.Sfx["sfx_drop_white"].GainDb - 3f) < 0.001f &&
                           Mathf.Abs(cfg.Audio.Sfx["sfx_drop_blue"].GainDb - 3f) < 0.001f &&
                           Mathf.Abs(cfg.Audio.Sfx["sfx_drop_yellow"].GainDb - 3f) < 0.001f &&
                           Mathf.Abs(cfg.Audio.Sfx["sfx_drop_orange"].GainDb - 3f) < 0.001f,
                "ordinary SFX should keep 3dB headroom while all drop keys take it back");
            report.Require(cfg.Audio.Sfx["sfx_drop_yellow"].DuckExempt &&
                           cfg.Audio.Sfx["sfx_drop_orange"].DuckExempt,
                "yellow and orange reward sounds must remain exempt from ducking");

            foreach (KeyValuePair<string, SfxDef> kv in cfg.Audio.Sfx)
            {
                if (!string.IsNullOrEmpty(kv.Value.Clip))
                {
                    AudioClip configured = Resources.Load<AudioClip>("Audio/" + kv.Value.Clip);
                    report.Require(configured != null,
                        "Sfx '" + kv.Key + "' references missing clip '" + kv.Value.Clip + "'");
                }
            }

            foreach (KeyValuePair<string, BgmDef> kv in cfg.Audio.Bgm)
            {
                AudioClip configured = Resources.Load<AudioClip>("Audio/" + kv.Value.Clip);
                report.Require(configured != null,
                    "Bgm '" + kv.Key + "' references missing clip '" + kv.Value.Clip + "'");
            }

            BgmDef battle = cfg.Audio.Bgm["bgm_battle"];
            BgmDef boss = cfg.Audio.Bgm["bgm_boss"];
            report.Require(Mathf.Abs(battle.CrossfadeSeconds - 0.5f) < 0.001f &&
                           Mathf.Abs(battle.PitchPerDay - 0.04f) < 0.001f,
                "battle BGM should crossfade for 0.5s and add 0.04 pitch per day");
            report.Require(Mathf.Abs(boss.PhaseOneCutoff - 2200f) < 0.1f &&
                           Mathf.Abs(boss.PhaseThreePitch - 1.06f) < 0.001f &&
                           Mathf.Abs(boss.PhaseThreeVolumeDb - 2f) < 0.001f,
                "boss BGM phase profile does not match the 2200Hz / 1.06 / +2dB contract");
            report.Require(Mathf.Abs(cfg.Coffee.LowSanThresholdPct - 33f) < 0.001f &&
                           Mathf.Abs(cfg.Audio.LowSanFadeSeconds - 0.2f) < 0.001f,
                "low SAN loop should use the existing 33% threshold and a 0.2s fade");
            report.Require(Mathf.Abs(cfg.QualityOf(Quality.Yellow).BgmLowPass - 0.3f) < 0.001f &&
                           Mathf.Abs(cfg.QualityOf(Quality.Orange).BgmLowPass - 1.2f) < 0.001f,
                "yellow/orange reward ducking should last 0.3s / 1.2s");
            report.Line("audio assets: 14 SFX + 4 stereo drops + 1 low SAN loop + 4 BGM, imports and mix profiles verified");
        }

        static void RequireAudioImport(
            Report report,
            string assetPath,
            bool forceMono,
            bool loadInBackground,
            bool preload,
            AudioClipLoadType loadType,
            AudioCompressionFormat compression,
            float quality)
        {
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            report.Require(importer != null, "audio importer is missing for " + assetPath);
            if (importer == null)
            {
                return;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            report.Require(settings.sampleRateSetting == AudioSampleRateSetting.PreserveSampleRate,
                assetPath + " should preserve the delivered sample rate");
            report.Require(importer.forceToMono == forceMono,
                assetPath + " forceToMono is " + importer.forceToMono + ", expected " + forceMono);
            report.Require(importer.loadInBackground == loadInBackground,
                assetPath + " loadInBackground is " + importer.loadInBackground + ", expected " + loadInBackground);
            report.Require(settings.preloadAudioData == preload,
                assetPath + " preloadAudioData is " + settings.preloadAudioData + ", expected " + preload);
            report.Require(settings.loadType == loadType,
                assetPath + " loadType is " + settings.loadType + ", expected " + loadType);
            report.Require(settings.compressionFormat == compression,
                assetPath + " compression is " + settings.compressionFormat + ", expected " + compression);
            report.Require(Mathf.Abs(settings.quality - quality) <= 0.001f,
                assetPath + " quality is " + settings.quality.ToString("0.00") +
                ", expected " + quality.ToString("0.00"));
        }

        static void TestPointerFollow(Report report, ConfigManager cfg)
        {
            Harness h = Harness.Create(cfg);
            InputSnapshot snapshot = new InputSnapshot();
            snapshot.PointerValid = true;
            snapshot.PointerWorld = new Vector2(2f, 0f);
            h.Driver.Input.Snapshot = snapshot;
            h.Driver.Input.Tick(FixedDelta);
            report.Require(h.Player.MoveIntent.x > 0f,
                "pointer follow did not start toward a distant cursor");

            snapshot.PointerWorld = new Vector2(0.58f, 0f);
            h.Driver.Input.Snapshot = snapshot;
            h.Driver.Input.Tick(0.1f);
            h.Driver.Movement.Tick(0.1f);
            report.Require(Mathf.Abs(h.Player.Pos.x - 0.03f) < 0.001f,
                "pointer follow crossed its stop radius during a long frame");

            h.Driver.Input.Tick(FixedDelta);
            report.Require(h.Player.MoveIntent.sqrMagnitude < 0.0001f,
                "pointer follow did not stop inside its inner dead zone");

            snapshot.PointerWorld = new Vector2(h.Player.Pos.x + 0.65f, 0f);
            h.Driver.Input.Snapshot = snapshot;
            h.Driver.Input.Tick(FixedDelta);
            report.Require(h.Player.MoveIntent.sqrMagnitude < 0.0001f,
                "pointer follow restarted inside the dead-zone hysteresis band");

            snapshot.PointerWorld = new Vector2(h.Player.Pos.x + 0.80f, 0f);
            h.Driver.Input.Snapshot = snapshot;
            h.Driver.Input.Tick(FixedDelta);
            report.Require(h.Player.MoveIntent.x > 0f,
                "pointer follow did not resume outside its outer dead zone");

            h.Dispose();
        }

        static void TestFormulas(Report report, ConfigManager cfg)
        {
            ProgressionDef prog = cfg.Progression;

            WeaponDef stapler = cfg.Weapon("stapler");
            report.Require(stapler != null, "weapon 'stapler' is missing, it is the starting weapon");
            if (stapler == null)
            {
                return;
            }

            float white = CombatFormula.WeaponDamage(stapler, cfg.WeaponQuality.Get(Quality.White), 10f);
            float orange = CombatFormula.WeaponDamage(stapler, cfg.WeaponQuality.Get(Quality.Orange), 10f);
            report.Require(orange > white, "quality coefficient does not increase weapon damage");
            report.Line(string.Format("stapler damage at atk 10: white {0:0.0}, orange {1:0.0}", white, orange));

            // DEF and HASTE share the 99 curve, which is the whole reason one mental model covers both.
            float slow = CombatFormula.AttackInterval(1f, 0f);
            float fast = CombatFormula.AttackInterval(1f, CombatFormula.Base99);
            report.Require(Mathf.Abs(slow - 1f) < 0.001f, "attack interval at 0 haste should equal the base interval");
            report.Require(Mathf.Abs(fast - 0.5f) < 0.001f, "attack interval at 99 haste should halve");

            report.Require(Mathf.Abs(CombatFormula.IncomingDamage(100f, 0f) - 100f) < 0.001f,
                "incoming damage at 0 def should be unchanged");
            report.Require(Mathf.Abs(CombatFormula.IncomingDamage(100f, CombatFormula.Base99) - 50f) < 0.001f,
                "incoming damage at 99 def should halve");

            // The first level up has to land inside ten seconds, and at roughly 2 exp per trash mob
            // that puts a hard ceiling on the level 1 cost. Asserted here because it is the one number
            // in the curve that is a player facing promise rather than a tuning knob.
            int firstLevel = CombatFormula.ExpForLevel(1, prog);
            report.Require(firstLevel == Mathf.CeilToInt(prog.ExpCoef),
                "exp for level 1 should be ceil(coef) = " + Mathf.CeilToInt(prog.ExpCoef) + ", got " + firstLevel);
            report.Require(firstLevel <= 16,
                "level 1 costs " + firstLevel + " exp, which is more than 8 mails, the first upgrade " +
                "would arrive later than the ten second promise");
            report.Require(CombatFormula.ExpForLevel(5, prog) > CombatFormula.ExpForLevel(4, prog),
                "exp curve is not monotonic");

            // Reaching the last rank must be possible but never automatic, so the whole curve is
            // measured against the exp the six days actually hand out.
            int toMaxRank = 0;
            for (int level = 1; level < prog.MaxLevel; level++)
            {
                toMaxRank += CombatFormula.ExpForLevel(level, prog);
            }

            report.Line("exp to reach " + cfg.RankOf(prog.MaxLevel) + ": " + toMaxRank);

            // The cap is the joke, so it is asserted rather than left to the ui to clamp.
            report.Require(CombatFormula.KpiPercent(prog.KpiTargetKills * 10, prog) == prog.KpiCap,
                "KPI is not capped at " + prog.KpiCap);
            report.Require(CombatFormula.KpiPercent(0, prog) == 0, "KPI at zero kills should be 0");

            float total = cfg.TotalCombatSeconds;
            report.Require(CombatFormula.Salary(total, total, prog) == prog.FinalSalary,
                "a full run should pay exactly " + prog.FinalSalary);
            report.Require(CombatFormula.Salary(total * 0.5f, total, prog) < prog.FinalSalary,
                "a half run should not pay the full salary");
            report.Line(string.Format("authored combat time {0:0}s pays {1}", total, prog.FinalSalary));

            report.Require(cfg.RankOf(1) != cfg.RankOf(prog.MaxLevel),
                "the first and last rank name are identical");

            // Stat pipeline: flat, then additive percent, then multiplicative percent.
            StatSheet sheet = new StatSheet();
            sheet.SetBase(StatType.Atk, 10f);
            sheet.AddModifier(new StatModifier(StatType.Atk, ModifierOp.Flat, 10f, 1));
            sheet.AddModifier(new StatModifier(StatType.Atk, ModifierOp.PercentAdd, 50f, 1));
            report.Require(Mathf.Abs(sheet.Get(StatType.Atk) - 30f) < 0.001f,
                "stat pipeline expected (10+10)*1.5 = 30, got " + sheet.Get(StatType.Atk));

            sheet.RemoveBySource(1);
            report.Require(Mathf.Abs(sheet.Get(StatType.Atk) - 10f) < 0.001f,
                "RemoveBySource did not restore the base value");

            TestAuraChannelsDoNotStack(report, cfg);
        }

        /// <summary>
        /// Five PPTs must slow by 25 percent, not by 125. This is the single rule that keeps a crowd of
        /// debuff enemies from adding up to a full stop, which would not read as difficulty.
        /// </summary>
        static void TestAuraChannelsDoNotStack(Report report, ConfigManager cfg)
        {
            PlayerModel p = new PlayerModel();
            p.ResetFrom(cfg.Player, cfg.Progression);

            for (int i = 0; i < 5; i++)
            {
                p.ApplyAura(AuraChannel.MoveSlow, 25f);
            }

            float expected = cfg.Player.MoveSpeed * 0.75f;
            report.Require(Mathf.Abs(p.EffectiveMoveSpeed(0f) - expected) < 0.01f,
                "five identical move slow sources stacked, speed is " + p.EffectiveMoveSpeed(0f) +
                " instead of " + expected);

            // Different channels are supposed to coexist, that is why they are separate slots.
            p.ApplyAura(AuraChannel.AttackSlow, 25f);
            report.Require(p.EffectiveHaste(0f) < 0f, "the attack slow channel did not reach haste");
            report.Require(Mathf.Abs(p.EffectiveMoveSpeed(0f) - expected) < 0.01f,
                "the attack slow channel leaked into move speed");

            report.Line("aura channels: same channel takes the max, different channels coexist");
        }

        static void TestClockProjection(Report report, ConfigManager cfg)
        {
            int h, m;
            WorkClockModel.Project(0f, cfg.Clock, out h, out m);
            report.Require(h == cfg.Clock.StartHour && m == 0,
                "clock at progress 0 should read " + cfg.Clock.StartHour + ":00, got " + h + ":" + m);

            WorkClockModel.Project(1f, cfg.Clock, out h, out m);
            report.Require(h == cfg.Clock.EndHour && m == 0,
                "clock at progress 1 should read " + cfg.Clock.EndHour + ":00, got " + h + ":" + m);

            // Snapping is what keeps a 40 second day readable instead of a minute hand blur.
            WorkClockModel.Project(0.51f, cfg.Clock, out h, out m);
            report.Require(m % cfg.Clock.SnapMinutes == 0,
                "clock minutes must snap to " + cfg.Clock.SnapMinutes + ", got " + m);

            int previous = -1;
            for (int i = 0; i <= 100; i++)
            {
                WorkClockModel.Project(i * 0.01f, cfg.Clock, out h, out m);
                int total = h * 60 + m;
                report.Require(total >= previous, "clock went backwards at progress " + (i * 0.01f));
                previous = total;
            }

            report.Line("clock projection monotonic and snapped to " + cfg.Clock.SnapMinutes + " minutes");
        }

        static void TestSpawnGeometry(Report report, ConfigManager cfg)
        {
            // Area uniform sampling: the outer half of an annulus holds most of the area, so a
            // correct sampler puts clearly more than half the points there.
            const int samples = 20000;
            float minR = 13.5f;
            float maxR = 16f;
            float mid = Mathf.Sqrt((minR * minR + maxR * maxR) * 0.5f);
            int outer = 0;

            for (int i = 0; i < samples; i++)
            {
                Vector2 p = Rng.RingPoint(Vector2.zero, minR, maxR);
                float r = p.magnitude;
                report.Require(r >= minR - 0.01f && r <= maxR + 0.01f, "ring sample outside the annulus: " + r);
                if (r > mid)
                {
                    outer++;
                }
            }

            float outerRatio = (float)outer / samples;
            report.Require(Mathf.Abs(outerRatio - 0.5f) < 0.04f,
                "ring sampling is not area uniform, outer half ratio " + outerRatio.ToString("0.000"));

            report.Line("ring sampling area uniform, outer half ratio " + outerRatio.ToString("0.000"));
        }

        /// <summary>
        /// The band's contract, asserted directly: nothing spawns on screen, nothing spawns in the
        /// player's lap, and the sides are favoured over the top. All three are invisible when correct
        /// and unmissable when broken, which is exactly the kind of rule that needs a test.
        /// </summary>
        static void TestSpawnBandKeepsDistance(Report report, ConfigManager cfg)
        {
            Harness h = Harness.Create(cfg);
            h.Driver.Flow.StartRun();

            SpawnBand band = new SpawnBand(h.Ctx);
            float halfHeight = cfg.Camera.OrthographicSize;
            float halfWidth = halfHeight * cfg.Camera.Aspect;

            int sides = 0;
            int total = 0;
            float closest = float.MaxValue;

            // Centre of the arena, so the edge fallback never fires and the raw shape is measured.
            for (int burst = 0; burst < 400; burst++)
            {
                band.BeginBurst();
                for (int n = 0; n < 6; n++)
                {
                    Vector2 p = band.NextPoint(Vector2.zero);
                    total++;
                    closest = Mathf.Min(closest, p.magnitude);

                    bool onScreen = Mathf.Abs(p.x) < halfWidth && Mathf.Abs(p.y) < halfHeight;
                    report.Require(!onScreen, "spawn point landed inside the camera frame at " + p);

                    if (Mathf.Abs(p.x) >= Mathf.Abs(p.y))
                    {
                        sides++;
                    }
                }
            }

            float sideRatio = (float)sides / total;
            report.Require(sideRatio > 0.55f,
                "the band does not favour the sides, side ratio " + sideRatio.ToString("0.00"));

            report.Line(string.Format(
                "spawn band: closest point {0:0.00} units, side ratio {1:0.00} over {2} points",
                closest, sideRatio, total));

            // A player pinned in a corner must still be attacked from somewhere.
            Vector2 corner = new Vector2(cfg.Arena.HalfWidth - 0.5f, cfg.Arena.HalfHeight - 0.5f);
            band.BeginBurst();
            for (int n = 0; n < 12; n++)
            {
                Vector2 p = band.NextPoint(corner);
                report.Require(Mathf.Abs(p.x) <= cfg.Arena.HalfWidth && Mathf.Abs(p.y) <= cfg.Arena.HalfHeight,
                    "corner fallback produced a point outside the arena at " + p);
                report.Require((p - corner).magnitude > cfg.Player.Radius * 2f,
                    "corner fallback dropped an enemy on top of the player at " + p);
            }

            h.Dispose();
        }

        static void TestFullRun(Report report, ConfigManager cfg)
        {
            Random.InitState(996);
            Harness h = Harness.Create(cfg);
            h.Driver.Flow.StartRun();
            h.EquipStarter();
            h.Player.GodMode = true;

            // The whole week, not a sample of it. The pacing claims in the design sheet are about where
            // the player is by Saturday, and a three day run cannot see any of them.
            int targetDays = cfg.DayCount;
            float budgetSeconds = 0f;
            for (int d = 1; d <= targetDays; d++)
            {
                DayDef def = cfg.Day(d);
                budgetSeconds += def != null ? def.Duration + def.OffWorkSeconds + 4f : 4f;
            }

            int maxFrames = Mathf.Min(MaxFrames, Mathf.CeilToInt(budgetSeconds / FixedDelta));
            int frames = 0;
            int peakEnemies = 0;
            int daysClosed = 0;
            int cardsPicked = 0;
            int maxRankDay = 0;
            int peakKpi = 0;
            float firstLevelUpSecond = -1f;
            GameState previous = h.Driver.Flow.State;

            while (frames < maxFrames)
            {
                frames++;
                h.Step();

                peakEnemies = Mathf.Max(peakEnemies, h.Run.AliveEnemies);
                peakKpi = Mathf.Max(peakKpi, h.Run.Kpi(cfg.Progression));

                GameState state = h.Driver.Flow.State;

                // The card panel is a hard pause. Nothing else advances until it is answered, so an
                // unanswered panel would hang the run rather than fail it, which is worse.
                if (state == GameState.LevelUp)
                {
                    report.Require(h.Driver.Cards.Offers.Count > 0,
                        "LevelUp was entered with no card offered, the run would hang here");

                    if (firstLevelUpSecond < 0f)
                    {
                        firstLevelUpSecond = h.Run.CombatSeconds;
                    }

                    if (h.Driver.Cards.Offers.Count > 0)
                    {
                        h.Driver.Cards.Pick(0);
                        cardsPicked++;
                    }

                    if (h.Player.Level >= cfg.Progression.MaxLevel && maxRankDay == 0)
                    {
                        maxRankDay = h.Run.DayIndex;
                    }

                    h.Driver.Flow.ResolveLevelUp();
                    previous = h.Driver.Flow.State;
                    continue;
                }

                if (state != previous)
                {
                    if (state == GameState.OffWork)
                    {
                        daysClosed++;
                        report.Line(DaySummary(h, cfg));

                        report.Require(h.Run.AliveEnemies == 0,
                            "day end left " + h.Run.AliveEnemies + " enemies alive");

                        h.Driver.Flow.SkipOffWork();
                    }

                    previous = h.Driver.Flow.State;
                }

                // Saturday hands over to Result rather than to another off work screen, so this is the
                // normal way out of the loop. God mode is on, which means Fail here would say the
                // invulnerability flag leaked somewhere.
                if (state == GameState.Result)
                {
                    report.Line(DaySummary(h, cfg));
                    report.Require(h.Run.DayIndex >= targetDays,
                        "the run ended on day " + h.Run.DayIndex + " of " + targetDays +
                        " as " + h.Run.Ending + ", with god mode on");
                    break;
                }

                report.Require(h.Run.Enemies.Count < 4000, "enemy list exploded to " + h.Run.Enemies.Count);
                report.Require(h.Run.Projectiles.Count < 4000, "projectile list exploded to " + h.Run.Projectiles.Count);
                report.Require(h.Run.Loots.Count < 4000, "loot list exploded to " + h.Run.Loots.Count);
            }

            // Five off work screens for six days: Saturday goes straight to the annual review.
            report.Require(daysClosed == targetDays - 1,
                "expected " + (targetDays - 1) + " off work transitions across " + targetDays +
                " days, got " + daysClosed);
            report.Require(h.Run.Ending == Ending.Clear || h.Run.Ending == Ending.ClearTimeout,
                "a surviving run ended as " + h.Run.Ending);
            report.Require(h.Run.Kills > 0, "no enemy was killed across " + frames + " frames");
            report.Require(h.Run.KillsByType.Count > 1,
                "only " + h.Run.KillsByType.Count + " enemy type(s) were killed, the report would be empty");
            report.Require(cardsPicked > 0, "no level up card was ever offered");
            report.Require(h.Player.EquippedCount() > 1,
                "only " + h.Player.EquippedCount() + " weapon slot(s) filled, auto equip is broken");
            report.Require(peakEnemies > 0, "no enemy ever spawned");

            // The rank curve is a promise about the shape of the week: the last promotion is the payoff
            // for surviving it, so it has to arrive at the end and it has to arrive at all. Landing it
            // mid week would leave the last days with no progression at all, and never landing it would
            // make the result screen advertise a rank nobody can reach.
            report.Require(h.Player.Level >= cfg.Progression.MaxLevel,
                "the run finished at Lv." + h.Player.Level + " of " + cfg.Progression.MaxLevel +
                ", the last rank is unreachable with the exp the six days hand out");
            report.Require(maxRankDay >= targetDays - 1,
                "reached the last rank on day " + maxRankDay + ", too early: the remaining days would " +
                "have no level ups left to give");

            // The cap is the joke. If the bar never touches it, the cap never reads as a cap.
            report.Require(peakKpi >= cfg.Progression.KpiCap,
                "kpi peaked at " + peakKpi + "% and never reached the " + cfg.Progression.KpiCap +
                "% cap, so the clamp never shows");

            report.Line(string.Format(
                "first level up at {0:0.0}s, last rank on day {1}, kpi peaked at {2}%",
                firstLevelUpSecond, maxRankDay, peakKpi));

            report.Line(string.Format(
                "run summary: {0} frames, {1} days, peak alive {2}, kills {3}, cards {4}, {5} Lv.{6}, " +
                "hpScale {7:0.00}, ended as {8}",
                frames, h.Run.DayIndex, peakEnemies, h.Run.Kills, cardsPicked,
                cfg.RankOf(h.Player.Level), h.Player.Level, h.Run.HpScale, h.Run.Ending));

            h.Dispose();

            TestContactDamageTakesTheWorst(report, cfg);
            TestSpawnGraceWindow(report, cfg);
            TestBugSplitsOnDeath(report, cfg);
            TestPityTimerResets(report, cfg);
            TestAutoEquipRules(report, cfg);
            TestWeaponKindsAllAct(report, cfg);
            TestBossPhasesAndClockOut(report, cfg);
        }

        static string DaySummary(Harness h, ConfigManager cfg)
        {
            return string.Format(
                "day {0} ({1}) closed at {2:0.0}s: kills {3}, kpi {4}%, weapons {5}, armour {6}, {7} Lv.{8}",
                h.Run.DayIndex, h.Run.Day != null ? h.Run.Day.Weekday : "?", h.Run.CombatSeconds,
                h.Run.Kills, h.Run.Kpi(cfg.Progression), h.Player.EquippedCount(), h.Player.ArmorCount(),
                cfg.RankOf(h.Player.Level), h.Player.Level);
        }

        /// <summary>
        /// The reason contact damage does not use the first hit found: with a shared invulnerability
        /// window that would make a swarm feel identical to a single touch.
        /// </summary>
        static void TestContactDamageTakesTheWorst(Report report, ConfigManager cfg)
        {
            EnemyDef weak = cfg.Enemy("mail");
            EnemyDef strong = cfg.Enemy("deadline");
            if (weak == null || strong == null)
            {
                report.Fail("enemies 'mail' and 'deadline' are needed by the contact damage test");
                return;
            }

            float single = MeasureContactHit(cfg, weak, null);
            float crowd = MeasureContactHit(cfg, weak, strong);

            report.Require(crowd > single + 0.01f,
                "contact damage did not take the strongest overlapping enemy: single " +
                single.ToString("0.0") + ", crowd " + crowd.ToString("0.0"));

            report.Line(string.Format("contact damage: one weak mob {0:0.0}, weak plus strong {1:0.0}", single, crowd));
        }

        static float MeasureContactHit(ConfigManager cfg, EnemyDef first, EnemyDef second)
        {
            Harness h = Harness.Create(cfg);
            h.Driver.Flow.StartRun();

            EnemyModel a = h.Driver.Spawn.Spawn(first, h.Player.Pos, null);
            a.ContactArmedAt = 0f;

            if (second != null)
            {
                EnemyModel b = h.Driver.Spawn.Spawn(second, h.Player.Pos, null);
                b.ContactArmedAt = 0f;
            }

            h.Driver.ForceRebuildGrid();

            float before = h.Player.San;
            h.Driver.Combat.Tick(FixedDelta);
            float damage = before - h.Player.San;
            h.Dispose();
            return damage;
        }

        /// <summary>
        /// Applied at the one place every enemy is born, so a split BUG, a boss summon and anything
        /// added later inherit it. Without it, being surrounded means taking damage from a mob that
        /// materialised on top of you, which reads as the game cheating rather than as a mistake.
        /// </summary>
        static void TestSpawnGraceWindow(Report report, ConfigManager cfg)
        {
            EnemyDef weak = cfg.Enemy("mail");
            if (weak == null)
            {
                report.Fail("enemy 'mail' is needed by the grace window test");
                return;
            }

            Harness h = Harness.Create(cfg);
            h.Driver.Flow.StartRun();

            EnemyModel close = h.Driver.Spawn.Spawn(weak, h.Player.Pos, null);
            report.Require(close.ContactArmedAt > GameClock.Now,
                "an enemy born on the player was armed immediately, the grace window is not applied");
            report.Require(!close.CanTouch(GameClock.Now), "CanTouch returned true inside the grace window");

            float far = cfg.Band.GraceRadius + 3f;
            EnemyModel distant = h.Driver.Spawn.Spawn(weak, h.Player.Pos + new Vector2(far, 0f), null);
            report.Require(distant.CanTouch(GameClock.Now),
                "an enemy born outside the grace radius was disarmed, it should be able to hit at once");

            h.Driver.ForceRebuildGrid();
            float before = h.Player.San;
            h.Driver.Combat.Tick(FixedDelta);
            report.Require(Mathf.Abs(h.Player.San - before) < 0.001f,
                "a freshly spawned enemy dealt contact damage inside its grace window");

            report.Line(string.Format("spawn grace: {0:0.0}s inside {1:0.0} units",
                cfg.Band.GraceSeconds, cfg.Band.GraceRadius));

            h.Dispose();
        }

        /// <summary>
        /// The one enemy where killing faster puts more on screen. Worth its own check because the
        /// children have to inherit both the grace window and a zero exp value: paying full exp for a
        /// self replicating enemy would break the level curve outright.
        /// </summary>
        static void TestBugSplitsOnDeath(Report report, ConfigManager cfg)
        {
            EnemyDef bug = cfg.Enemy("bug");
            if (bug == null)
            {
                report.Fail("enemy 'bug' is needed by the split test");
                return;
            }

            string childId = bug.Param.GetString("splitInto", null);
            report.Require(!string.IsNullOrEmpty(childId), "'bug' declares no splitInto id, it will never split");

            EnemyDef child = cfg.Enemy(childId);
            report.Require(child != null, "'bug' splits into unknown enemy '" + childId + "'");
            if (child == null)
            {
                return;
            }

            report.Require(child.Exp == 0,
                "the split child grants " + child.Exp + " exp, a self replicating enemy must grant none");

            Harness h = Harness.Create(cfg);
            h.Driver.Flow.StartRun();

            EnemyModel victim = h.Driver.Spawn.Spawn(bug, h.Player.Pos + new Vector2(6f, 0f), null);
            int before = h.Run.AliveEnemies;
            CombatSystem.KillEnemy(h.Ctx, victim);

            int spawned = 0;
            for (int i = 0; i < h.Run.Enemies.Count; i++)
            {
                if (!h.Run.Enemies[i].IsDead && h.Run.Enemies[i].DefId == childId)
                {
                    spawned++;
                }
            }

            int expected = Mathf.Max(1, (int)bug.Param.GetFloat("splitCount", 2f));
            report.Require(spawned == expected,
                "killing a BUG produced " + spawned + " children instead of " + expected);
            report.Require(h.Run.AliveEnemies == before - 1 + expected,
                "the alive count after a split does not add up");

            report.Line("BUG split: 1 death produced " + spawned + " children worth " + child.Exp + " exp");
            h.Dispose();
        }

        /// <summary>Missing the reset here is the classic way the pity rule turns into a legendary machine gun.</summary>
        static void TestPityTimerResets(Report report, ConfigManager cfg)
        {
            Harness h = Harness.Create(cfg);
            h.Driver.Flow.StartRun();

            EnemyDef weak = cfg.Enemy("mail");
            if (weak == null)
            {
                report.Fail("enemy 'mail' is needed by the pity timer test");
                h.Dispose();
                return;
            }

            h.Run.SecondsSinceLastLegendary = cfg.Loot.PityLegendarySeconds + 1f;

            EnemyModel victim = h.Driver.Spawn.Spawn(weak, h.Player.Pos + new Vector2(8f, 0f), null);
            CombatSystem.KillEnemy(h.Ctx, victim);

            bool droppedOrange = false;
            for (int i = 0; i < h.Run.Loots.Count; i++)
            {
                LootModel l = h.Run.Loots[i];
                if (l.Kind != LootKind.Coffee && l.Quality == Quality.Orange)
                {
                    droppedOrange = true;
                }
            }

            report.Require(droppedOrange, "pity timer did not force a legendary drop");
            report.Require(h.Run.SecondsSinceLastLegendary < 0.001f,
                "pity timer was not reset after the legendary dropped, it is at " +
                h.Run.SecondsSinceLastLegendary);
            report.Require(h.Run.AnyLegendaryDropped, "the legendary flag was not raised, the pity window stays tight");

            report.Line("pity timer forced a legendary and reset to 0");
            h.Dispose();
        }

        /// <summary>
        /// Auto equip is the only thing standing between the player and an inventory screen, so its
        /// three rules are asserted directly: fill empty first, replace the worst, never downgrade.
        /// </summary>
        static void TestAutoEquipRules(Report report, ConfigManager cfg)
        {
            Harness h = Harness.Create(cfg);
            h.Driver.Flow.StartRun();

            // Rule one: empty slots fill in order, so the first six drops are never wasted.
            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                LootModel l = h.Driver.Loot.SpawnWeapon(h.Player.Pos, Quality.White);
                h.Driver.Loot.CollectNow(l);
            }

            report.Require(h.Player.EquippedCount() == PlayerModel.WeaponSlots,
                "six white weapons filled " + h.Player.EquippedCount() + " of " + PlayerModel.WeaponSlots + " slots");

            // Rule two: with the board full, a better item takes the worst slot.
            h.Player.Equip(3, h.Player.Weapons[3].Def, Quality.Yellow);
            LootModel blue = h.Driver.Loot.SpawnWeapon(h.Player.Pos, Quality.Blue);
            h.Driver.Loot.CollectNow(blue);

            int blues = 0;
            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                if (h.Player.Weapons[i].Quality == Quality.Blue)
                {
                    blues++;
                }
            }

            report.Require(blues == 1, "a blue weapon did not replace a white one, blue slots " + blues);
            report.Require(h.Player.Weapons[3].Quality == Quality.Yellow,
                "the blue weapon overwrote the yellow slot instead of a white one");

            // Rule three: a downgrade converts to exp instead of vanishing, so the floor stays useful.
            int expBefore = h.Player.Exp;
            LootModel worse = h.Driver.Loot.SpawnWeapon(h.Player.Pos, Quality.White);
            h.Driver.Loot.CollectNow(worse);
            report.Require(h.Player.Exp > expBefore || h.Player.Level > 1,
                "a declined white weapon granted no exp, the late game floor becomes noise");

            report.Line("auto equip: fills empty, replaces the worst, converts downgrades to exp");
            h.Dispose();
        }

        /// <summary>All three weapon kinds get one real implementation in the greybox on purpose.</summary>
        static void TestWeaponKindsAllAct(Report report, ConfigManager cfg)
        {
            bool sawLauncher = false;
            bool sawGround = false;
            bool sawOrbit = false;

            foreach (KeyValuePair<string, WeaponDef> kv in cfg.Weapons)
            {
                sawLauncher |= kv.Value.Kind == WeaponKind.ProjectileLauncher;
                sawGround |= kv.Value.Kind == WeaponKind.GroundAoe;
                sawOrbit |= kv.Value.Kind == WeaponKind.Orbit;
            }

            report.Require(sawLauncher && sawGround && sawOrbit,
                "Weapons.xml does not cover all three behaviour kinds");

            EnemyDef weak = cfg.Enemy("mail");
            if (weak == null)
            {
                return;
            }

            // Launcher: must create a projectile aimed at a target in range.
            Harness launcher = Harness.Create(cfg);
            launcher.Driver.Flow.StartRun();
            launcher.Player.Equip(0, cfg.Weapon("stapler"), Quality.White);
            launcher.Driver.Spawn.Spawn(weak, launcher.Player.Pos + new Vector2(2f, 0f), null);
            launcher.Driver.ForceRebuildGrid();
            launcher.Driver.Weapons.Tick(FixedDelta);
            report.Require(launcher.Run.Projectiles.Count > 0, "ProjectileLauncher did not fire at a target in range");
            launcher.Dispose();

            // Ground aoe: queues a slam that lands after the wind up, never instantly. The delay is
            // the readable part of the weapon, so its absence is a failure and not a detail.
            Harness ground = Harness.Create(cfg);
            ground.Driver.Flow.StartRun();
            WeaponDef keyboard = cfg.Weapon("keyboard");
            ground.Player.Equip(0, keyboard, Quality.White);
            EnemyModel target = ground.Driver.Spawn.Spawn(weak, ground.Player.Pos + new Vector2(1f, 0f), null);
            ground.Driver.ForceRebuildGrid();
            ground.Driver.Weapons.Tick(FixedDelta);

            report.Require(ground.Run.Slams.Count > 0, "GroundAoe queued no slam against a target in range");
            report.Require(ground.Run.Projectiles.Count == 0, "GroundAoe should not create projectiles");

            float hpBefore = target.Hp;
            int guard = 0;
            while (target.Hp >= hpBefore && !target.IsDead && guard++ < 240)
            {
                ground.Step();
            }

            report.Require(target.Hp < hpBefore || target.IsDead, "the queued slam never dealt damage");
            report.Require(guard > 1, "the slam landed on the same frame it was queued, the wind up is gone");
            ground.Dispose();

            // Orbit: cards exist while equipped, with no cooldown and no projectile.
            Harness orbit = Harness.Create(cfg);
            orbit.Driver.Flow.StartRun();
            orbit.Player.Equip(0, cfg.Weapon("badge"), Quality.Blue);
            orbit.Driver.Orbits.Tick(FixedDelta);

            int expectedCards = cfg.Weapon("badge").Tier(Quality.Blue).OrbitCount;
            report.Require(orbit.Run.OrbitCards.Count == expectedCards,
                "a blue badge produced " + orbit.Run.OrbitCards.Count + " cards instead of " + expectedCards);
            report.Require(orbit.Run.Projectiles.Count == 0, "Orbit should not create projectiles");

            EnemyModel ringed = orbit.Driver.Spawn.Spawn(
                weak, orbit.Player.Pos + new Vector2(cfg.Weapon("badge").Tier(Quality.Blue).OrbitRadius, 0f), null);
            orbit.Driver.ForceRebuildGrid();

            float ringedHp = ringed.Hp;
            guard = 0;
            while (ringed.Hp >= ringedHp && !ringed.IsDead && guard++ < 240)
            {
                orbit.Driver.Orbits.Tick(FixedDelta);
                GameClock.Tick(FixedDelta);
            }

            report.Require(ringed.Hp < ringedHp || ringed.IsDead, "an orbiting card never hit an enemy on its ring");
            orbit.Dispose();

            report.Line("all three weapon kinds produced their effect");
        }

        /// <summary>
        /// The boss is the only scripted fight, and both of its unusual rules are load bearing: a bar
        /// break must grant invulnerability so the next bar cannot be burst through, and running out of
        /// time with the boss alive must still be a win rather than a technicality.
        /// </summary>
        static void TestBossPhasesAndClockOut(Report report, ConfigManager cfg)
        {
            EnemyDef bossDef = cfg.Enemy("boss");
            if (bossDef == null)
            {
                report.Fail("enemy 'boss' is missing, the last day has no fight");
                return;
            }

            report.Require(bossDef.IgnoreScaling,
                "the boss is affected by the per day growth curve, its 9999 figures are meant to be absolute");

            Harness h = Harness.Create(cfg);
            h.Driver.Flow.StartRun();
            h.Driver.Flow.DebugJumpToDay(cfg.DayCount);

            // The subject here is the ending, not survival. Without this the boss and its adds decide
            // the outcome and the assertion below would be measuring the wrong thing.
            h.Player.GodMode = true;

            EnemyModel boss = h.Driver.Spawn.Spawn(bossDef, h.Player.Pos + new Vector2(6f, 0f), null);
            report.Require(boss.BarsTotal > 1, "the boss has " + boss.BarsTotal + " bar(s), three were expected");
            report.Require(boss.IsBoss, "IsBoss is false on the boss, the health bar will never show");
            report.Require(h.Run.BossBarsTotal == boss.BarsTotal, "the run model did not record the bar count");

            int bars = boss.BarsTotal;
            float perBar = boss.MaxHp;

            // Break one bar. The phase has to advance and the boss has to go untargetable for a moment.
            CombatSystem.DealDamageToEnemy(h.Ctx, boss, perBar * 2f, boss.Pos);

            report.Require(!boss.IsDead, "one bar of damage killed the whole boss");
            report.Require(boss.BarsLeft == bars - 1,
                "breaking a bar left " + boss.BarsLeft + " of " + bars);
            report.Require(boss.Phase == 2, "the boss is in phase " + boss.Phase + " after one bar, 2 was expected");
            report.Require(boss.InvulnUntil > GameClock.Now,
                "a bar break granted no invulnerability window, the remaining bars can be burst through");
            report.Require(Mathf.Abs(boss.Hp - perBar) < 0.01f, "the next bar did not refill to full");

            // Overkill must not skip a bar: carrying damage across the break would collapse the fight.
            report.Require(h.Run.BossBarsLeft == boss.BarsLeft, "the run model bar count drifted from the boss");

            // Read before the day is ended, because clearing the field recycles the boss and its
            // pooled model is reset back to phase one.
            int phaseAfterBreak = boss.Phase;

            // 21:00. The boss goes home and the run still counts as cleared on time.
            h.Run.DayElapsed = h.Run.Day.Duration;
            int guard = 0;
            while (h.Driver.Flow.State != GameState.Result && guard++ < 600)
            {
                h.Step();
            }

            report.Require(h.Driver.Flow.State == GameState.Result,
                "the last day never resolved to a result");
            report.Require(h.Run.Ending == Ending.ClearTimeout,
                "surviving to 21:00 with the boss alive gave " + h.Run.Ending + " instead of ClearTimeout");

            report.Line(string.Format(
                "boss: {0} bars of {1:0} hp, bar break advanced to phase {2}, timeout ended as {3}",
                bars, perBar, phaseAfterBreak, h.Run.Ending));

            h.Dispose();
        }

        /// <summary>Restart is the densest source of leftover state bugs, so it gets its own check.</summary>
        static void TestRestartLeavesNoResidue(Report report, ConfigManager cfg)
        {
            Harness h = Harness.Create(cfg);
            h.Driver.Flow.StartRun();
            h.EquipStarter();
            h.Player.GodMode = true;

            // Day one is 40 seconds. Running past its end would clear the arena and the residue check
            // would then pass against an already empty run, which proves nothing.
            int frames = 0;
            while (frames < 600)
            {
                frames++;
                h.Step();
                if (h.Run.Enemies.Count > 0 && h.Run.Projectiles.Count > 0)
                {
                    break;
                }
            }

            report.Require(h.Run.Enemies.Count > 0, "nothing was alive before the restart, the test proves nothing");
            report.Require(h.Run.Projectiles.Count > 0, "no projectile was in flight before the restart");

            // A chasing harness magnetises drops within a frame or two, so put one out of reach to
            // make the loot clear an actual assertion.
            h.Driver.Loot.SpawnCoffee(h.Player.Pos + new Vector2(20f, 0f));
            report.Require(h.Run.Loots.Count > 0, "loot list was empty before the restart");

            h.Player.Passives = SlackPassive.DeepSlack | SlackPassive.MassSlack;
            h.Player.Stats.AddModifier(new StatModifier(StatType.Atk, ModifierOp.Flat, 999f, 7));
            h.Run.CountKill("mail");

            h.Run.ResetRun(cfg);
            h.Ctx.Grid.Clear();
            GameClock.Reset();

            report.Require(h.Run.Enemies.Count == 0, "restart left " + h.Run.Enemies.Count + " enemies");
            report.Require(h.Run.Projectiles.Count == 0, "restart left " + h.Run.Projectiles.Count + " projectiles");
            report.Require(h.Run.Loots.Count == 0, "restart left " + h.Run.Loots.Count + " loot items");
            report.Require(h.Run.Slams.Count == 0, "restart left " + h.Run.Slams.Count + " slams");
            report.Require(h.Run.Telegraphs.Count == 0, "restart left " + h.Run.Telegraphs.Count + " telegraphs");
            report.Require(h.Run.OrbitCards.Count == 0, "restart left " + h.Run.OrbitCards.Count + " orbit cards");
            report.Require(h.Run.Kills == 0, "restart left the kill counter at " + h.Run.Kills);
            report.Require(h.Run.KillsByType.Count == 0,
                "restart left " + h.Run.KillsByType.Count + " entries in the work report");
            report.Require(h.Run.DayIndex == 1, "restart left the day index at " + h.Run.DayIndex);
            report.Require(h.Run.Ending == Ending.None, "restart left the ending at " + h.Run.Ending);
            report.Require(h.Run.BossBarsTotal == 0, "restart left the boss bar count at " + h.Run.BossBarsTotal);
            report.Require(h.Player.Level == 1, "restart left the level at " + h.Player.Level);
            report.Require(h.Player.Passives == SlackPassive.None, "restart left passives at " + h.Player.Passives);
            report.Require(Mathf.Abs(h.Player.Stats.Get(StatType.Atk) - cfg.Player.Atk) < 0.001f,
                "restart did not clear stat modifiers, atk is " + h.Player.Stats.Get(StatType.Atk));
            report.Require(h.Player.EquippedCount() == 0, "restart left " + h.Player.EquippedCount() + " weapons equipped");
            report.Require(h.Player.ArmorCount() == 0, "restart left " + h.Player.ArmorCount() + " armour pieces equipped");

            report.Line("restart cleared entities, counters, stats, passives and equipment slots");
            h.Dispose();
        }

        /// <summary>
        /// Minimal composition root for headless runs: config, model, bus, grid and the driver.
        /// No view, no audio, no MonoBehaviour.
        /// </summary>
        sealed class Harness
        {
            public GameContext Ctx;
            public GameLoopDriver Driver;

            public RunModel Run
            {
                get { return Ctx.Run; }
            }

            public PlayerModel Player
            {
                get { return Ctx.Run.Player; }
            }

            public static Harness Create(ConfigManager cfg)
            {
                GameClock.Reset();
                GameClock.DebugScale = 1f;

                Harness h = new Harness();
                h.Ctx = new GameContext();
                h.Ctx.Cfg = cfg;
                h.Ctx.Run = new RunModel();
                h.Ctx.Bus = new EventBus();
                h.Ctx.Grid = new SpatialGrid();
                h.Ctx.Run.ResetRun(cfg);
                h.Driver = new GameLoopDriver(h.Ctx);
                return h;
            }

            public void EquipStarter()
            {
                WeaponDef def = Ctx.Cfg.Weapon("stapler");
                if (def != null)
                {
                    Player.Equip(0, def, Quality.White);
                }
            }

            /// <summary>
            /// Chases the nearest drop like a real session would. Standing still would leave the
            /// magnet, the step pickup and the auto equip path completely untested.
            /// </summary>
            public void Step()
            {
                Vector2 target = Vector2.zero;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < Run.Loots.Count; i++)
                {
                    if (Run.Loots[i].IsDead)
                    {
                        continue;
                    }

                    float sqr = (Run.Loots[i].Pos - Player.Pos).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        target = Run.Loots[i].Pos;
                    }
                }

                InputSnapshot snapshot = new InputSnapshot();
                snapshot.PointerValid = true;
                snapshot.PointerWorld = bestSqr < float.MaxValue ? target : Vector2.zero;
                Driver.Input.Snapshot = snapshot;

                GameClock.Tick(FixedDelta);
                Driver.Tick();
            }

            public void Dispose()
            {
                Driver.Dispose();
            }
        }
    }
}
