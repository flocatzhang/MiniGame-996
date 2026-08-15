using System;
using System.Collections.Generic;
using System.Text;
using OfficeHell.Config;
using OfficeHell.Model;
using OfficeHell.View;
using UnityEngine;

namespace OfficeHell.UI
{
    /// <summary>Populates the prefab-authored three-second transition between working days.</summary>
    public sealed class UIOffWorkController : UIControllerBase
    {
        readonly UIContext _ctx;
        readonly UIOffWorkView _view;
        readonly StringBuilder _summary = new StringBuilder(192);

        public Action OnSkipClicked;

        public UIOffWorkController(UIContext ctx, UIOffWorkView view)
        {
            _ctx = ctx;
            _view = view;
        }

        protected override void OnUIInit()
        {
            _view.SkipButton.onClick.AddListener(Skip);
            Sprite[] bossFrames = ArtCatalog.Frames("boss");
            if (bossFrames.Length > 0)
            {
                _view.BossPortrait.sprite = bossFrames[0];
                _view.BossPortrait.preserveAspect = true;
            }
        }

        protected override void OnUIDestroy()
        {
            _view.SkipButton.onClick.RemoveListener(Skip);
        }

        protected override void OnUIOpen()
        {
            RunModel run = _ctx.Game.Run;
            ConfigManager cfg = _ctx.Game.Cfg;
            int kpi = run.Kpi(cfg.Progression);
            int remainingKpi = Mathf.Max(0, 99 - kpi);
            DayDef tomorrow = cfg.Day(run.DayIndex + 1);

            _view.DayTitle.text = run.Day != null
                ? run.Day.Weekday + " · " + run.Day.Label + " · 下班"
                : "下班了";
            _view.Speech.text = "KPI 还差 <color=#E62C2C>" + remainingKpi + "%</color>！\n下班了？？";

            _summary.Length = 0;
            _summary.Append("今日处理  ").Append(run.KilledToday).Append(" 项")
                .Append("    累计击败  ").Append(run.Kills).Append(" 项")
                .Append("\n职位  ").Append(cfg.RankOf(run.Player.Level)).Append(" Lv.").Append(run.Player.Level)
                .Append("    SAN  ").Append(Mathf.Max(0, Mathf.CeilToInt(run.Player.San))).Append(" / ")
                .Append(Mathf.RoundToInt(run.Player.MaxSan));
            AppendTopWork(run, cfg);
            _view.Summary.text = _summary.ToString();

            _view.NextDay.text = tomorrow != null
                ? "明天 " + tomorrow.Weekday + "：" + tomorrow.Label + "  ·  " +
                  Mathf.RoundToInt(tomorrow.Duration) + " 秒  ·  HP x" +
                  cfg.HpScale(run.DayIndex + 1).ToString("0.00") + "  DMG x" +
                  cfg.DmgScale(run.DayIndex + 1).ToString("0.00")
                : "本周工作已结束";
        }

        void AppendTopWork(RunModel run, ConfigManager cfg)
        {
            string bestId = null;
            int bestCount = 0;
            foreach (KeyValuePair<string, int> entry in run.KillsByType)
            {
                if (entry.Value > bestCount)
                {
                    bestId = entry.Key;
                    bestCount = entry.Value;
                }
            }

            EnemyDef best = bestId != null ? cfg.Enemy(bestId) : null;
            if (best != null)
            {
                _summary.Append("\n主要工作  ").Append(best.ReportVerb).Append(' ')
                    .Append(bestCount).Append(' ').Append(best.ReportUnit);
            }
        }

        protected override void OnUITick(float unscaledDt)
        {
            float t = Mathf.Clamp01(_ctx.Driver.Flow.StateSeconds / 0.35f);
            Color color = _view.Dimmer.color;
            color.a = 0.76f * t;
            _view.Dimmer.color = color;
            _view.Hint.enabled = _ctx.Driver.Flow.CanSkipOffWork;
        }

        void Skip()
        {
            if (OnSkipClicked != null) OnSkipClicked();
        }
    }
}
