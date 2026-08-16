using System;
using System.Collections.Generic;
using System.Text;
using OfficeHell.Config;
using OfficeHell.Model;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>Populates and stages the prefab-backed end-of-run salary statement.</summary>
    public sealed class UIResultController : UIControllerBase
    {
        static readonly float[] StepAt = { 0f, 0.5f, 1.1f, 2f, 2.6f, 3.1f };
        static Sprite _kpiFillSprite;

        sealed class WorkRow
        {
            public string Label;
            public string Unit;
            public int Count;
        }

        readonly UIContext _ctx;
        readonly UIResultView _view;
        readonly Dictionary<string, WorkRow> _mergedRows = new Dictionary<string, WorkRow>(12);
        readonly List<WorkRow> _rows = new List<WorkRow>(12);

        int _kpiTarget;
        int _step;
        bool _skipped;

        public Action OnRestartClicked;
        public Action OnMenuClicked;

        public UIResultController(UIContext ctx, UIResultView view)
        {
            _ctx = ctx;
            _view = view;
        }

        protected override void OnUIInit()
        {
            ConfigureKpiProgressBar(_view.KpiFill);
            _view.RestartButton.onClick.AddListener(Restart);
            _view.MenuButton.onClick.AddListener(ReturnToMenu);
        }

        static void ConfigureKpiProgressBar(Image fill)
        {
            if (fill.sprite == null)
            {
                if (_kpiFillSprite == null)
                {
                    _kpiFillSprite = Sprite.Create(
                        Texture2D.whiteTexture,
                        new Rect(0f, 0f, 1f, 1f),
                        new Vector2(0.5f, 0.5f),
                        1f);
                    _kpiFillSprite.name = "UIResult KPI Fill";
                    _kpiFillSprite.hideFlags = HideFlags.HideAndDontSave;
                }

                fill.sprite = _kpiFillSprite;
            }

            RectTransform rect = fill.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            fill.gameObject.SetActive(true);
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
        }

        protected override void OnUIDestroy()
        {
            _view.RestartButton.onClick.RemoveListener(Restart);
            _view.MenuButton.onClick.RemoveListener(ReturnToMenu);
        }

        protected override void OnUIOpen()
        {
            RunModel run = _ctx.Game.Run;
            ConfigManager cfg = _ctx.Game.Cfg;
            bool fail = run.Ending == Ending.Fail;
            bool completed = !fail;

            _view.OutcomeBanner.sprite = run.Ending == Ending.Clear
                ? _view.ClearOutcomeSprite
                : _view.IncompleteOutcomeSprite;
            _view.OutcomeBanner.color = Color.white;
            _view.OutcomeBanner.preserveAspect = true;
            _view.OutcomeBanner.raycastTarget = false;
            _view.Outcome.text = fail ? "已离职" : "未达标";
            _view.Outcome.enabled = false;
            _view.Stamp.text = completed
                ? WorkClockModel.Stamp(cfg.DayCount, WeekdayOf(cfg, cfg.DayCount), 1f, cfg.Clock)
                : WorkClockModel.Stamp(run.DayIndex, WeekdayOf(cfg, run.DayIndex), run.DayProgress01, cfg.Clock);

            int salary = completed
                ? cfg.Progression.FinalSalary
                : CombatFormula.Salary(run.CombatSeconds, cfg.TotalCombatSeconds, cfg.Progression);
            _view.Salary.text = "¥" + salary.ToString("N0");

            FillWorkRows(run, cfg);
            _view.BestQuality.text = run.AnyLootPicked
                ? "最高品质  " + QualityName(run.BestQuality) + " · " + run.BestLootName
                : "最高品质  无掉落";
            _view.BestQuality.color = run.AnyLootPicked
                ? cfg.QualityOf(run.BestQuality).Color
                : new Color(0.18f, 0.18f, 0.2f, 1f);
            _view.Rank.text = "最终职位  " + cfg.RankOf(run.Player.Level);
            _view.San.text = "剩余 SAN  " + Mathf.Max(0, Mathf.CeilToInt(run.Player.San)) + " / " +
                             Mathf.Max(0, Mathf.CeilToInt(run.Player.MaxSan));
            _view.Loadout.text = "最终配置  " + Loadout(run.Player);
            _view.Comment.text = Comment(run);

            _kpiTarget = Mathf.Min(99, run.Kpi(cfg.Progression));
            SetKpiProgress(0f);

            _step = 0;
            _skipped = false;
            ApplyStep(0);
        }

        void FillWorkRows(RunModel run, ConfigManager cfg)
        {
            _mergedRows.Clear();
            _rows.Clear();

            foreach (KeyValuePair<string, int> entry in run.KillsByType)
            {
                EnemyDef def = cfg.Enemy(entry.Key);
                if (def == null)
                {
                    continue;
                }

                string key = def.ReportVerb + "\n" + def.ReportUnit;
                WorkRow row;
                if (!_mergedRows.TryGetValue(key, out row))
                {
                    row = new WorkRow { Label = def.ReportVerb, Unit = def.ReportUnit };
                    _mergedRows.Add(key, row);
                }

                row.Count += entry.Value;
            }

            foreach (KeyValuePair<string, WorkRow> entry in _mergedRows)
            {
                _rows.Add(entry.Value);
            }

            _rows.Sort((a, b) =>
            {
                int count = b.Count.CompareTo(a.Count);
                return count != 0 ? count : string.CompareOrdinal(a.Label, b.Label);
            });

            for (int i = 0; i < _view.WorkLabels.Length; i++)
            {
                bool hasRow = i < _rows.Count;
                _view.WorkLabels[i].text = hasRow ? _rows[i].Label : "—";
                _view.WorkValues[i].text = hasRow ? "+ " + _rows[i].Count + " " + _rows[i].Unit : string.Empty;
            }
        }

        static string Loadout(PlayerModel player)
        {
            StringBuilder value = new StringBuilder(32);
            value.Append("武器 ");
            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                value.Append(player.Weapons[i].IsEmpty ? '□' : '■');
            }

            value.Append("   防具 ");
            for (int i = 0; i < PlayerModel.ArmorSlots; i++)
            {
                value.Append(player.Armors[i].IsEmpty ? '□' : '■');
            }

            return value.ToString();
        }

        static string WeekdayOf(ConfigManager cfg, int dayIndex)
        {
            DayDef day = cfg.Day(dayIndex);
            return day != null ? day.Weekday : string.Empty;
        }

        static string Comment(RunModel run)
        {
            float pct = run.Player.MaxSan > 0f ? run.Player.San / run.Player.MaxSan * 100f : 0f;
            switch (run.Ending)
            {
                case Ending.Clear:
                    return pct > 50f ? "表现尚可，明年继续努力。" : "工作完成了，但你的状态需要调整一下。";
                case Ending.ClearTimeout:
                    return "述职未完成，下周继续。";
                default:
                    if (run.DayIndex <= 2) return "试用期未通过。";
                    return run.DayIndex <= 4 ? "建议你重新考虑一下职业规划。" : "就差一点，可惜。";
            }
        }

        protected override void OnUITick(float unscaledDt)
        {
            float elapsed = _skipped ? 999f : _ctx.Driver.Flow.StateSeconds;
            int wantedStep = 0;
            for (int i = 0; i < StepAt.Length; i++)
            {
                if (elapsed >= StepAt[i])
                {
                    wantedStep = i;
                }
            }

            if (wantedStep != _step)
            {
                _step = wantedStep;
                ApplyStep(_step);
            }

            if (_step >= 4)
            {
                float target = _kpiTarget / 100f;
                SetKpiProgress(Mathf.Min(target, _view.KpiFill.fillAmount + unscaledDt * 0.55f));
            }

            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                Skip();
            }
        }

        void Skip()
        {
            if (_skipped)
            {
                return;
            }

            _skipped = true;
            _step = StepAt.Length - 1;
            ApplyStep(_step);
            SetKpiProgress(_kpiTarget / 100f);
        }

        void SetKpiProgress(float normalized)
        {
            float value = Mathf.Clamp01(normalized);
            _view.KpiFill.fillAmount = value;
            _view.KpiLabel.text = "KPI 完成度  " + Mathf.RoundToInt(value * 100f) + "%";
        }

        void ApplyStep(int step)
        {
            _view.SalaryGroup.SetActive(step >= 1);
            _view.WorkGroup.SetActive(step >= 2);
            _view.LootGroup.SetActive(step >= 3);
            _view.KpiGroup.SetActive(step >= 4);
            _view.Comment.enabled = step >= 5;
            _view.ButtonsGroup.SetActive(step >= 5);
        }

        static string QualityName(Quality quality)
        {
            switch (quality)
            {
                case Quality.Blue: return "蓝色";
                case Quality.Purple: return "紫色";
                case Quality.Orange: return "橙色";
                default: return "绿色";
            }
        }

        void Restart()
        {
            if (OnRestartClicked != null) OnRestartClicked();
        }

        void ReturnToMenu()
        {
            if (OnMenuClicked != null) OnMenuClicked();
        }
    }
}
