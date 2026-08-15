using System;
using System.Collections.Generic;
using System.Text;
using OfficeHell.Config;
using OfficeHell.Model;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.UI
{
    /// <summary>
    /// The three second gap between days. Short on purpose: it exists to let the player read what they
    /// just did and to hide the field being cleared, not to be a menu. A click skips it, and the flow
    /// machine ends it on its own even if nobody touches anything.
    /// </summary>
    public sealed class UIOffWorkController : UIControllerBase
    {
        readonly UIContext _ctx;
        readonly StringBuilder _sb = new StringBuilder(256);

        Text _title;
        Text _summary;
        Text _hint;
        Image _bg;

        public Action OnSkipClicked;

        public UIOffWorkController(UIContext ctx)
        {
            _ctx = ctx;
        }

        protected override void OnUIInit()
        {
            _bg = UIFactory.CreateImage(Root, "Bg", new Color(0.05f, 0.06f, 0.09f, 0.9f));
            UIFactory.Stretch(_bg.rectTransform);
            _bg.raycastTarget = true;

            Button skip = UIFactory.CreateButton(Root, "BtnSkip", string.Empty, 1, new Vector2(10f, 10f),
                new Color(0f, 0f, 0f, 0f), () =>
                {
                    if (OnSkipClicked != null)
                    {
                        OnSkipClicked();
                    }
                });

            UIFactory.Stretch(skip.GetComponent<RectTransform>());

            _title = UIFactory.AnchoredText(Root, "Title", "下班了", 96, new Color(0.95f, 0.93f, 0.88f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 210f), new Vector2(1200f, 130f));

            _summary = UIFactory.AnchoredText(Root, "Summary", "", 36, new Color(0.8f, 0.83f, 0.9f),
                TextAnchor.UpperCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1500f, 280f));

            _hint = UIFactory.AnchoredText(Root, "Hint", "点击任意位置继续", 26, new Color(0.45f, 0.48f, 0.55f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(1200f, 50f));
        }

        protected override void OnUIOpen()
        {
            RunModel run = _ctx.Game.Run;
            ConfigManager cfg = _ctx.Game.Cfg;
            int next = run.DayIndex + 1;
            DayDef tomorrow = cfg.Day(next);

            _title.text = run.Day != null ? run.Day.Label + " 下班" : "下班了";

            _sb.Length = 0;
            _sb.Append("今日处理 ").Append(run.KilledToday).Append(" 项  ·  KPI ")
                .Append(run.Kpi(cfg.Progression)).Append("%  ·  ")
                .Append(cfg.RankOf(run.Player.Level)).Append(" Lv.").Append(run.Player.Level)
                .Append('\n');

            _sb.Append("SAN ").Append(Mathf.CeilToInt(run.Player.San)).Append(" / ")
                .Append(Mathf.RoundToInt(run.Player.MaxSan))
                .Append("  ·  武器 ").Append(run.Player.EquippedCount()).Append(" / ").Append(PlayerModel.WeaponSlots)
                .Append("  ·  防具 ").Append(run.Player.ArmorCount()).Append(" / ").Append(PlayerModel.ArmorSlots)
                .Append('\n');

            if (tomorrow != null)
            {
                _sb.Append("明天 ").Append(tomorrow.Label).Append("  ·  ")
                    .Append(Mathf.RoundToInt(tomorrow.Duration)).Append(" 秒  ·  强度 HP x")
                    .Append(cfg.HpScale(next).ToString("0.00")).Append("  DMG x")
                    .Append(cfg.DmgScale(next).ToString("0.00"));
            }

            AppendTopKills(run, cfg);
            _summary.text = _sb.ToString();
        }

        /// <summary>
        /// The per type tally is the only place the enemy roster is named back to the player. It is what
        /// makes "邮件 328 封" land as a joke instead of reading as an anonymous kill count.
        /// </summary>
        void AppendTopKills(RunModel run, ConfigManager cfg)
        {
            if (run.KillsByType.Count == 0)
            {
                return;
            }

            string bestId = null;
            int bestCount = 0;

            foreach (KeyValuePair<string, int> kv in run.KillsByType)
            {
                if (kv.Value > bestCount)
                {
                    bestCount = kv.Value;
                    bestId = kv.Key;
                }
            }

            EnemyDef def = bestId != null ? cfg.Enemy(bestId) : null;
            if (def == null)
            {
                return;
            }

            _sb.Append('\n').Append("主要工作量  ").Append(def.ReportVerb).Append(' ')
                .Append(bestCount).Append(' ').Append(def.ReportUnit);
        }

        protected override void OnUITick(float unscaledDt)
        {
            // Fades in rather than cutting, because this overlay lands on the exact frame the field is
            // wiped and a hard cut would look like the enemies popped out of existence.
            float t = Mathf.Clamp01(_ctx.Driver.Flow.StateSeconds / 0.35f);
            Color c = _bg.color;
            c.a = 0.9f * t;
            _bg.color = c;

            _hint.enabled = _ctx.Driver.Flow.CanSkipOffWork;
        }
    }
}
