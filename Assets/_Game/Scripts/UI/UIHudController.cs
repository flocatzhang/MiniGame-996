using OfficeHell.Config;
using OfficeHell.Core;
using OfficeHell.Model;
using OfficeHell.Systems;
using OfficeHell.View;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>Projects the current run onto the prefab-authored battle HUD.</summary>
    public sealed class UIHudController : UIControllerBase
    {
        static Sprite _progressFillSprite;

        readonly UIContext _ctx;
        readonly UIHudView _view;
        readonly Color[] _emptyWeaponSlotColors = new Color[PlayerModel.WeaponSlots];

        public UIHudController(UIContext ctx, UIHudView view)
        {
            _ctx = ctx;
            _view = view;
        }

        protected override void OnUIInit()
        {
            for (int i = 0; i < _emptyWeaponSlotColors.Length; i++)
            {
                _emptyWeaponSlotColors[i] = _view.WeaponSlots[i].Background.color;
            }

            ConfigureProgressFill(_view.SanFill);
            ConfigureProgressFill(_view.ExpFill);
            ConfigureProgressFill(_view.SkillFill);

            Sprite[] playerFrames = ArtCatalog.Frames("player");
            if (playerFrames.Length > 0)
            {
                _view.Portrait.sprite = playerFrames[0];
                _view.Portrait.preserveAspect = true;
            }

            _view.BossRoot.SetActive(false);
        }

        static void ConfigureProgressFill(Image fill)
        {
            if (fill.sprite == null)
            {
                if (_progressFillSprite == null)
                {
                    _progressFillSprite = Sprite.Create(
                        Texture2D.whiteTexture,
                        new Rect(0f, 0f, 1f, 1f),
                        new Vector2(0.5f, 0.5f),
                        1f);
                    _progressFillSprite.name = "UIHud Progress Fill";
                    _progressFillSprite.hideFlags = HideFlags.HideAndDontSave;
                }

                fill.sprite = _progressFillSprite;
            }

            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        protected override void OnUITick(float unscaledDt)
        {
            RunModel run = _ctx.Game.Run;
            PlayerModel player = run.Player;
            ConfigManager cfg = _ctx.Game.Cfg;

            float maxSan = Mathf.Max(1f, player.MaxSan);
            _view.SanFill.fillAmount = Mathf.Clamp01(player.San / maxSan);
            _view.SanText.text = Mathf.Max(0, Mathf.CeilToInt(player.San)) + " / " + Mathf.RoundToInt(maxSan) +
                                 (player.Shield > 0.5f ? "  +护盾 " + Mathf.RoundToInt(player.Shield) : string.Empty);

            _view.ExpFill.fillAmount = player.ExpToNext > 0
                ? Mathf.Clamp01((float)player.Exp / player.ExpToNext)
                : 0f;
            _view.ExpText.text = player.Exp + " / " + player.ExpToNext;
            _view.NameText.text = cfg.RankOf(player.Level);
            _view.RankText.text = "Lv." + player.Level;

            int salary = CombatFormula.Salary(run.CombatSeconds, cfg.TotalCombatSeconds, cfg.Progression);
            _view.CoinText.text = salary.ToString("N0");
            _view.KillText.text = run.Kills.ToString();

            int kpi = run.Kpi(cfg.Progression);
            _view.KpiFill.fillAmount = kpi / 100f;
            _view.KpiText.text = "KPI 完成度  " + kpi + "%";

            RefreshWorkClock(run, cfg);
            RefreshSkill(player);
            RefreshWeapons(player);
            RefreshArmor(player);
            RefreshBoss(run);
            RefreshBanner(run);
        }

        void RefreshWorkClock(RunModel run, ConfigManager cfg)
        {
            int hour;
            int minute;
            WorkClockModel.Project(run.DayProgress01, cfg.Clock, out hour, out minute);
            _view.WorkClockText.text = hour.ToString("00") + ":" + minute.ToString("00");
            float progress = run.DayProgress01;
            _view.WorkClockText.color = Color.Lerp(
                new Color(0.08f, 0.09f, 0.14f, 1f),
                new Color(0.88f, 0.2f, 0.12f, 1f),
                progress * progress);

            string weekday = run.Day != null ? run.Day.Weekday : string.Empty;
            _view.StageText.text = weekday.Length > 0
                ? weekday + " · " + PeriodName(hour)
                : PeriodName(hour);
        }

        void RefreshSkill(PlayerModel player)
        {
            float progress = _ctx.Driver.Skill.CooldownProgress01();
            _view.SkillFill.fillAmount = progress;

            float remaining = Mathf.Max(0f, player.SkillReadyAt - GameClock.Now);
            int percent = Mathf.RoundToInt(progress * 100f);
            _view.SkillText.text = remaining <= 0.01f
                ? _ctx.Game.Cfg.Skill.Name + " · 就绪 100%"
                : _ctx.Game.Cfg.Skill.Name + " · 充能 " + percent + "% · " + remaining.ToString("0.0") + "s";
        }

        static string PeriodName(int hour)
        {
            if (hour < 12) return "上午";
            if (hour < 14) return "午休";
            if (hour < 18) return "下午";
            return "加班";
        }

        void RefreshWeapons(PlayerModel player)
        {
            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                UIHudView.WeaponSlotReferences view = _view.WeaponSlots[i];
                WeaponRuntime weapon = player.Weapons[i];
                if (weapon.IsEmpty)
                {
                    // Empty slots retain the neutral artwork/tint authored in the HUD prefab.
                    view.Background.color = _emptyWeaponSlotColors[i];
                    view.Label.text = "空";
                    view.Label.color = new Color(0.4f, 0.42f, 0.48f);
                    view.Icon.sprite = null;
                    view.Icon.enabled = false;
                    view.Icon.color = Color.white;
                    view.CooldownFill.fillAmount = 0f;
                    continue;
                }

                QualityDef quality = _ctx.Game.Cfg.QualityOf(weapon.Quality);
                view.Background.color = quality.Color;
                view.Label.text = weapon.Def.Name;
                view.Label.color = quality.Color;
                view.Icon.sprite = UIPrefabCatalog.CardIcon(weapon.Def.Id);
                view.Icon.enabled = view.Icon.sprite != null;
                view.Icon.preserveAspect = true;
                view.Icon.color = Color.white;
                view.CooldownFill.fillAmount = 1f - _ctx.Driver.Weapons.CooldownProgress01(weapon);
            }
        }

        void RefreshArmor(PlayerModel player)
        {
            for (int i = 0; i < PlayerModel.ArmorSlots; i++)
            {
                UIHudView.ArmorSlotReferences view = _view.ArmorSlots[i];
                ArmorRuntime armor = player.Armors[i];
                if (armor.IsEmpty)
                {
                    view.Label.text = SlotWord((EquipSlot)(i + 1));
                    view.Label.color = new Color(0.38f, 0.4f, 0.46f);
                    view.Icon.sprite = null;
                    view.Icon.enabled = false;
                    view.Icon.color = Color.white;
                    continue;
                }

                Color quality = _ctx.Game.Cfg.QualityOf(armor.Quality).Color;
                view.Label.text = armor.Def.Name;
                view.Label.color = quality;
                view.Icon.sprite = UIPrefabCatalog.CardIcon(armor.Def.Id);
                view.Icon.enabled = view.Icon.sprite != null;
                view.Icon.preserveAspect = true;
                view.Icon.color = Color.white;
            }
        }

        static string SlotWord(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Head: return "头";
                case EquipSlot.Body: return "身";
                case EquipSlot.Feet: return "脚";
                default: return "空";
            }
        }

        void RefreshBoss(RunModel run)
        {
            EnemyModel boss = run.Boss;
            if (boss == null)
            {
                if (_view.BossRoot.activeSelf) _view.BossRoot.SetActive(false);
                return;
            }

            if (!_view.BossRoot.activeSelf) _view.BossRoot.SetActive(true);
            _view.BossFill.fillAmount = boss.MaxHp > 0f ? Mathf.Clamp01(boss.Hp / boss.MaxHp) : 0f;
            _view.BossName.text = boss.Def.Name + "  ·  第 " + boss.Phase + " 阶段  ·  " + Mathf.CeilToInt(boss.Hp);

            bool invulnerable = GameClock.Now < boss.InvulnUntil;
            _view.BossFill.color = invulnerable
                ? new Color(0.6f, 0.62f, 0.7f, 1f)
                : new Color(0.95f, 0.35f, 0.72f, 1f);

            for (int i = 0; i < _view.BossPips.Length; i++)
            {
                bool used = i >= boss.BarsLeft;
                _view.BossPips[i].enabled = i < boss.BarsTotal;
                _view.BossPips[i].color = used
                    ? new Color(0.25f, 0.25f, 0.3f, 0.8f)
                    : new Color(1f, 0.55f, 0.85f, 1f);
            }
        }

        void RefreshBanner(RunModel run)
        {
            GameFlowFsm flow = _ctx.Driver.Flow;
            if (flow.State != GameState.DayStart)
            {
                if (_view.BannerText.text.Length > 0) _view.BannerText.text = string.Empty;
                return;
            }

            float t = Mathf.Clamp01(flow.StateSeconds / GameFlowFsm.DayIntroSeconds);
            _view.BannerText.text = run.Day != null
                ? run.Day.Weekday + " · " + run.Day.Label
                : "工作日开始";
            Color color = _view.BannerText.color;
            color.a = t < 0.7f ? 1f : Mathf.InverseLerp(1f, 0.7f, t);
            _view.BannerText.color = color;
        }
    }
}
