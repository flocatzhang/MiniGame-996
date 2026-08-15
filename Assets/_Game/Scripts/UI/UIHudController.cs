using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using OfficeHell.Systems;
using OfficeHell.View;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    public sealed class UIHudController : UIControllerBase
    {
        readonly UIContext _ctx;

        Image _sanFill;
        Text _sanText;
        Image _expFill;
        Text _rankText;
        Text _kpiText;
        Image _kpiFill;
        Text _statsText;
        Text _dayText;
        Text _clockText;
        Text _periodText;
        Text _bannerText;
        Text _skillText;
        Image _skillFill;

        Text _bossName;
        Image _bossFill;
        RectTransform _bossRoot;
        readonly Image[] _bossPip = new Image[3];

        WorkClockView _clock;

        readonly Image[] _slotFill = new Image[PlayerModel.WeaponSlots];
        readonly Image[] _slotBg = new Image[PlayerModel.WeaponSlots];
        readonly Text[] _slotLabel = new Text[PlayerModel.WeaponSlots];
        readonly Text[] _armorLabel = new Text[PlayerModel.ArmorSlots];
        readonly Image[] _armorBg = new Image[PlayerModel.ArmorSlots];

        public UIHudController(UIContext ctx)
        {
            _ctx = ctx;
        }

        protected override void OnUIInit()
        {
            BuildSanAndExp();
            BuildKpi();
            BuildClock();
            BuildWeaponSlots();
            BuildArmorSlots();
            BuildSkill();
            BuildBoss();
            BuildBanner();

            _clock = new WorkClockView(_clockText, _periodText);
        }

        void BuildSanAndExp()
        {
            Image ignored;

            _sanFill = UIFactory.CreateBar(Root, "SanBar", new Vector2(36f, -30f), new Vector2(460f, 34f),
                new Color(0f, 0f, 0f, 0.55f), new Color(0.35f, 0.78f, 0.95f, 1f), out ignored);

            _sanText = UIFactory.AnchoredText(Root, "SanText", "99 / 99", 24, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(48f, -47f), new Vector2(460f, 34f));

            _expFill = UIFactory.CreateBar(Root, "ExpBar", new Vector2(36f, -70f), new Vector2(460f, 12f),
                new Color(0f, 0f, 0f, 0.55f), new Color(0.95f, 0.82f, 0.35f, 1f), out ignored);

            _rankText = UIFactory.AnchoredText(Root, "Rank", "实习生 Lv.1", 28, new Color(0.95f, 0.9f, 0.7f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(36f, -92f), new Vector2(600f, 40f));

            _statsText = UIFactory.AnchoredText(Root, "Stats", "", 22, new Color(0.62f, 0.66f, 0.72f),
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(36f, -132f), new Vector2(700f, 140f));
        }

        /// <summary>
        /// KPI sits top right, opposite sanity, because they are the two numbers the design frames as
        /// opposites: one is how much you can still take, the other is how much they got out of you.
        /// </summary>
        void BuildKpi()
        {
            Image ignored;
            _kpiFill = UIFactory.CreateBar(Root, "KpiBar", Vector2.zero, new Vector2(420f, 26f),
                new Color(0f, 0f, 0f, 0.55f), new Color(0.95f, 0.45f, 0.35f, 1f), out ignored);

            RectTransform rt = _kpiFill.rectTransform.parent as RectTransform;
            UIFactory.Anchor(rt, new Vector2(1f, 1f), new Vector2(-36f, -30f), new Vector2(420f, 26f));

            _kpiText = UIFactory.AnchoredText(Root, "KpiText", "KPI 0%", 30, new Color(1f, 0.88f, 0.8f),
                TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-36f, -62f), new Vector2(420f, 40f));
        }

        void BuildClock()
        {
            _clockText = UIFactory.AnchoredText(Root, "Clock", "09:00", 76, new Color(0.9f, 0.94f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(420f, 100f));

            _periodText = UIFactory.AnchoredText(Root, "Period", "周一 · 上午", 26, new Color(0.6f, 0.64f, 0.72f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(420f, 40f));

            _dayText = UIFactory.AnchoredText(Root, "Day", "第 1 天", 30, new Color(0.85f, 0.87f, 0.92f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(620f, 44f));
        }

        void BuildWeaponSlots()
        {
            const float slotWidth = 128f;
            const float gap = 8f;
            float totalWidth = PlayerModel.WeaponSlots * slotWidth + (PlayerModel.WeaponSlots - 1) * gap;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                float x = startX + i * (slotWidth + gap);

                GameObject holder = new GameObject("Slot" + i);
                holder.transform.SetParent(Root, false);
                RectTransform rt = holder.AddComponent<RectTransform>();
                UIFactory.Anchor(rt, new Vector2(0.5f, 0f), new Vector2(x + slotWidth * 0.5f, 84f), new Vector2(slotWidth, 74f));

                _slotBg[i] = UIFactory.CreateImage(holder.transform, "Bg", new Color(0f, 0f, 0f, 0.55f));
                UIFactory.Stretch(_slotBg[i].rectTransform);

                _slotFill[i] = UIFactory.CreateImage(holder.transform, "Cd", new Color(0.32f, 0.62f, 0.9f, 0.55f));
                UIFactory.Stretch(_slotFill[i].rectTransform);
                _slotFill[i].type = Image.Type.Filled;
                _slotFill[i].fillMethod = Image.FillMethod.Vertical;
                _slotFill[i].fillAmount = 0f;

                _slotLabel[i] = UIFactory.CreateText(holder.transform, "Label", "空", 20, Color.white, TextAnchor.MiddleCenter);
                UIFactory.Stretch(_slotLabel[i].rectTransform);
            }
        }

        /// <summary>
        /// Armour is auto equipped and never chosen, so it only needs to be readable, not interactive.
        /// It still has to be on screen: the yellow headphone and the orange body change how damage
        /// resolves, and an invisible defensive item reads as the game cheating.
        /// </summary>
        void BuildArmorSlots()
        {
            const float slotWidth = 150f;
            const float gap = 8f;

            for (int i = 0; i < PlayerModel.ArmorSlots; i++)
            {
                GameObject holder = new GameObject("Armor" + i);
                holder.transform.SetParent(Root, false);
                RectTransform rt = holder.AddComponent<RectTransform>();
                UIFactory.Anchor(rt, new Vector2(1f, 0f),
                    new Vector2(-36f, 96f + i * (34f + gap)), new Vector2(slotWidth, 34f));

                _armorBg[i] = UIFactory.CreateImage(holder.transform, "Bg", new Color(0f, 0f, 0f, 0.45f));
                UIFactory.Stretch(_armorBg[i].rectTransform);

                _armorLabel[i] = UIFactory.CreateText(holder.transform, "Label", "-", 20,
                    new Color(0.5f, 0.52f, 0.58f), TextAnchor.MiddleCenter);
                UIFactory.Stretch(_armorLabel[i].rectTransform);
            }
        }

        void BuildSkill()
        {
            Image ignored;
            _skillFill = UIFactory.CreateBar(Root, "SkillBar", Vector2.zero, new Vector2(230f, 26f),
                new Color(0f, 0f, 0f, 0.55f), new Color(0.4f, 0.9f, 0.7f, 1f), out ignored);

            RectTransform rt = _skillFill.rectTransform.parent as RectTransform;
            UIFactory.Anchor(rt, new Vector2(0f, 0f), new Vector2(36f, 40f), new Vector2(230f, 26f));

            _skillText = UIFactory.AnchoredText(Root, "SkillText", "摸鱼", 24, Color.white,
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(36f, 78f), new Vector2(320f, 30f));
        }

        /// <summary>
        /// Three bars of 9999 with no shared indicator would look like the same bar refilling. The pips
        /// carry which bar this is, so a phase transition reads as progress instead of as a reset.
        /// </summary>
        void BuildBoss()
        {
            GameObject holder = new GameObject("BossBar");
            holder.transform.SetParent(Root, false);
            _bossRoot = holder.AddComponent<RectTransform>();
            UIFactory.Anchor(_bossRoot, new Vector2(0.5f, 0f), new Vector2(0f, 196f), new Vector2(900f, 60f));

            _bossName = UIFactory.AnchoredText(_bossRoot, "Name", "", 28, new Color(1f, 0.72f, 0.9f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(900f, 34f));

            Image ignored;
            _bossFill = UIFactory.CreateBar(_bossRoot, "Fill", Vector2.zero, new Vector2(900f, 24f),
                new Color(0f, 0f, 0f, 0.6f), new Color(0.95f, 0.35f, 0.72f, 1f), out ignored);

            RectTransform rt = _bossFill.rectTransform.parent as RectTransform;
            UIFactory.Anchor(rt, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(900f, 24f));

            for (int i = 0; i < _bossPip.Length; i++)
            {
                GameObject pip = new GameObject("Pip" + i);
                pip.transform.SetParent(_bossRoot, false);
                _bossPip[i] = pip.AddComponent<Image>();
                _bossPip[i].sprite = PrimitiveFactory.Pixel;
                _bossPip[i].raycastTarget = false;
                UIFactory.Anchor(_bossPip[i].rectTransform, new Vector2(0.5f, 0f),
                    new Vector2(-480f + i * 22f, 12f), new Vector2(16f, 16f));
            }

            _bossRoot.gameObject.SetActive(false);
        }

        void BuildBanner()
        {
            _bannerText = UIFactory.AnchoredText(Root, "Banner", "", 84, new Color(1f, 0.95f, 0.85f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1600f, 140f));
        }

        protected override void OnUITick(float unscaledDt)
        {
            RunModel run = _ctx.Game.Run;
            PlayerModel p = run.Player;
            ConfigManager cfg = _ctx.Game.Cfg;

            float maxSan = Mathf.Max(1f, p.MaxSan);
            _sanFill.fillAmount = Mathf.Clamp01(p.San / maxSan);
            _sanText.text = Mathf.CeilToInt(p.San) + " / " + Mathf.RoundToInt(maxSan) +
                            (p.Shield > 0.5f ? "  +护盾 " + Mathf.RoundToInt(p.Shield) : "");

            _expFill.fillAmount = p.ExpToNext > 0 ? Mathf.Clamp01((float)p.Exp / p.ExpToNext) : 0f;
            _rankText.text = cfg.RankOf(p.Level) + " Lv." + p.Level;

            int kpi = run.Kpi(cfg.Progression);
            _kpiFill.fillAmount = kpi / 100f;
            _kpiText.text = "KPI " + kpi + "%";

            _dayText.text = "第 " + run.DayIndex + " 天 · 敌 " + run.AliveEnemies + " · 杀 " + run.Kills;

            _statsText.text = string.Format(
                "ATK {0:0.#}   暴击 {1:0.#}% x{2:0.#}   DEF {3:0.#}   闪避 {4:0.#}%\n速度 {5:0.##}   急速 {6:0.#}%   幸运 {7:0.#}   吸取 {8:0.##}\n强度 HP x{9:0.00}  DMG x{10:0.00}   保底 {11:0.0}s   欠账 {12}",
                p.Stats.Get(StatType.Atk),
                p.Stats.Get(StatType.CritChance),
                p.Stats.Get(StatType.CritMulti) * 0.01f,
                p.EffectiveDef(),
                p.Stats.Get(StatType.Dodge),
                p.EffectiveMoveSpeed(GameClock.Now),
                p.EffectiveHaste(GameClock.Now),
                p.Stats.Get(StatType.Luck),
                p.MagnetRadius,
                run.HpScale,
                run.DmgScale,
                run.SecondsSinceLastLegendary,
                run.SpawnDebt);

            _clock.Refresh(run, cfg.Clock);
            RefreshSlots(p);
            RefreshArmor(p);
            RefreshSkill(p);
            RefreshBoss(run);
            RefreshBanner(run);
        }

        void RefreshSlots(PlayerModel p)
        {
            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                WeaponRuntime rt = p.Weapons[i];
                if (rt.IsEmpty)
                {
                    _slotLabel[i].text = "空";
                    _slotLabel[i].color = new Color(0.4f, 0.42f, 0.48f);
                    _slotFill[i].fillAmount = 0f;
                    continue;
                }

                QualityDef qd = _ctx.Game.Cfg.QualityOf(rt.Quality);
                _slotLabel[i].text = rt.Def.Name;
                _slotLabel[i].color = qd.Color;
                _slotFill[i].fillAmount = 1f - _ctx.Driver.Weapons.CooldownProgress01(rt);
            }
        }

        void RefreshArmor(PlayerModel p)
        {
            for (int i = 0; i < PlayerModel.ArmorSlots; i++)
            {
                ArmorRuntime rt = p.Armors[i];
                if (rt.IsEmpty)
                {
                    _armorLabel[i].text = SlotWord((EquipSlot)(i + 1));
                    _armorLabel[i].color = new Color(0.36f, 0.38f, 0.44f);
                    continue;
                }

                _armorLabel[i].text = rt.Def.Name;
                _armorLabel[i].color = _ctx.Game.Cfg.QualityOf(rt.Quality).Color;
            }
        }

        static string SlotWord(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Head: return "[ 头 ]";
                case EquipSlot.Body: return "[ 身 ]";
                case EquipSlot.Feet: return "[ 脚 ]";
                default: return "-";
            }
        }

        void RefreshSkill(PlayerModel p)
        {
            float progress = _ctx.Driver.Skill.CooldownProgress01();
            _skillFill.fillAmount = progress;

            float remaining = Mathf.Max(0f, p.SkillReadyAt - GameClock.Now);
            _skillText.text = remaining <= 0.01f
                ? _ctx.Game.Cfg.Skill.Name + " 就绪 (空格)"
                : _ctx.Game.Cfg.Skill.Name + " " + remaining.ToString("0.0") + "s";
        }

        void RefreshBoss(RunModel run)
        {
            EnemyModel boss = run.Boss;
            if (boss == null)
            {
                if (_bossRoot.gameObject.activeSelf)
                {
                    _bossRoot.gameObject.SetActive(false);
                }

                return;
            }

            if (!_bossRoot.gameObject.activeSelf)
            {
                _bossRoot.gameObject.SetActive(true);
            }

            _bossFill.fillAmount = boss.MaxHp > 0f ? Mathf.Clamp01(boss.Hp / boss.MaxHp) : 0f;
            _bossName.text = boss.Def.Name + "  ·  第 " + boss.Phase + " 阶段  ·  " + Mathf.CeilToInt(boss.Hp);

            bool invuln = GameClock.Now < boss.InvulnUntil;
            _bossFill.color = invuln
                ? new Color(0.6f, 0.62f, 0.7f, 1f)
                : new Color(0.95f, 0.35f, 0.72f, 1f);

            for (int i = 0; i < _bossPip.Length; i++)
            {
                bool used = i >= boss.BarsLeft;
                _bossPip[i].enabled = i < boss.BarsTotal;
                _bossPip[i].color = used
                    ? new Color(0.25f, 0.25f, 0.3f, 0.8f)
                    : new Color(1f, 0.55f, 0.85f, 1f);
            }
        }

        void RefreshBanner(RunModel run)
        {
            GameFlowFsm flow = _ctx.Driver.Flow;
            if (flow.State != GameState.DayStart)
            {
                if (_bannerText.text.Length > 0)
                {
                    _bannerText.text = string.Empty;
                }

                return;
            }

            float t = Mathf.Clamp01(flow.StateSeconds / GameFlowFsm.DayIntroSeconds);
            _bannerText.text = run.Day != null ? run.Day.Label : "第 " + run.DayIndex + " 天";

            Color c = _bannerText.color;
            c.a = t < 0.7f ? 1f : Mathf.InverseLerp(1f, 0.7f, t);
            _bannerText.color = c;
        }
    }
}
