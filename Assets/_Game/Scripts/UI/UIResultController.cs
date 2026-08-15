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
    /// The one screen that gets screenshotted, so it is laid out as a resignation letter rather than as
    /// a scoreboard. Reveal is staged with a click to skip: the pause before the KPI bar stalls at 99 is
    /// the setup for the punchline, and dumping every row at once throws it away.
    /// </summary>
    public sealed class UIResultController : UIControllerBase
    {
        /// <summary>Reveal step boundaries in seconds, unscaled.</summary>
        static readonly float[] StepAt = { 0f, 0.5f, 1.1f, 2.0f, 2.6f, 3.1f };

        readonly UIContext _ctx;
        readonly StringBuilder _sb = new StringBuilder(512);
        readonly List<KeyValuePair<string, int>> _rows = new List<KeyValuePair<string, int>>(12);

        Text _title;
        Text _stamp;
        Text _rankValue;
        Text _salaryValue;
        Text _sanValue;
        RectTransform _bigRow;
        Text _reportTitle;
        Text _reportBody;
        Text _bestCaption;
        Text _bestLoot;
        Text _loadoutCaption;
        Text _loadout;
        Text _kpiLabel;
        Image _kpiFill;
        RectTransform _kpiRoot;
        Text _sealText;
        RectTransform _sealRoot;
        Text _comment;
        RectTransform _buttons;

        int _kpiTarget;
        int _step;
        bool _skipped;

        public Action OnRestartClicked;
        public Action OnMenuClicked;

        public UIResultController(UIContext ctx)
        {
            _ctx = ctx;
        }

        protected override void OnUIInit()
        {
            Image bg = UIFactory.CreateImage(Root, "Bg", new Color(0.94f, 0.93f, 0.90f, 0.97f));
            UIFactory.Stretch(bg.rectTransform);
            bg.raycastTarget = true;

            Color ink = new Color(0.13f, 0.13f, 0.16f);
            Color faint = new Color(0.42f, 0.42f, 0.46f);

            _title = UIFactory.AnchoredText(Root, "Title", "离 职 证 明", 72, ink,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(220f, -96f), new Vector2(900f, 96f));

            _stamp = UIFactory.AnchoredText(Root, "Stamp", "", 32, faint,
                TextAnchor.MiddleRight, new Vector2(1f, 1f), new Vector2(-220f, -96f), new Vector2(700f, 56f));

            Rule(-152f);
            BuildBigNumbers(ink, faint);
            Rule(-340f);
            BuildReport(ink, faint);
            Rule(-556f);
            BuildLoot(ink, faint);
            Rule(-676f);
            BuildKpi(ink, faint);
            BuildSeal();
            BuildButtons();
        }

        void Rule(float y)
        {
            Image line = UIFactory.CreateImage(Root, "Rule", new Color(0.13f, 0.13f, 0.16f, 0.28f));
            UIFactory.Anchor(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1480f, 3f));
        }

        void BuildBigNumbers(Color ink, Color faint)
        {
            GameObject holder = new GameObject("BigNumbers");
            holder.transform.SetParent(Root, false);
            _bigRow = holder.AddComponent<RectTransform>();
            UIFactory.Anchor(_bigRow, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(1480f, 176f));

            _rankValue = BigCell(-460f, "最终职位", "实习生", ink, faint, 54);
            _salaryValue = BigCell(0f, "累计工资", "¥0", new Color(0.72f, 0.42f, 0.16f), faint, 62);
            _sanValue = BigCell(460f, "剩余 SAN", "0", new Color(0.24f, 0.45f, 0.66f), faint, 62);
        }

        Text BigCell(float x, string caption, string value, Color valueColor, Color captionColor, int size)
        {
            UIFactory.AnchoredText(_bigRow, caption, caption, 28, captionColor,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(x, -22f), new Vector2(420f, 44f));

            return UIFactory.AnchoredText(_bigRow, caption + "Value", value, size, valueColor,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(x, -104f), new Vector2(420f, 84f));
        }

        void BuildReport(Color ink, Color faint)
        {
            _reportTitle = UIFactory.AnchoredText(Root, "ReportTitle", "本周工作量", 32, faint,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(220f, -376f), new Vector2(700f, 44f));

            _reportBody = UIFactory.AnchoredText(Root, "ReportBody", "", 30, ink,
                TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(260f, -416f), new Vector2(1400f, 140f));
        }

        void BuildLoot(Color ink, Color faint)
        {
            _bestCaption = UIFactory.AnchoredText(Root, "BestCaption", "本周最佳掉落", 30, faint,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(220f, -594f), new Vector2(360f, 44f));

            _bestLoot = UIFactory.AnchoredText(Root, "BestLoot", "无", 32, ink,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(600f, -594f), new Vector2(900f, 44f));

            _loadoutCaption = UIFactory.AnchoredText(Root, "LoadoutCaption", "最终配置", 30, faint,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(220f, -644f), new Vector2(360f, 44f));

            _loadout = UIFactory.AnchoredText(Root, "Loadout", "", 34, ink,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(600f, -644f), new Vector2(900f, 44f));
        }

        void BuildKpi(Color ink, Color faint)
        {
            GameObject holder = new GameObject("Kpi");
            holder.transform.SetParent(Root, false);
            _kpiRoot = holder.AddComponent<RectTransform>();
            UIFactory.Anchor(_kpiRoot, new Vector2(0.5f, 1f), new Vector2(0f, -720f), new Vector2(1480f, 60f));

            _kpiLabel = UIFactory.AnchoredText(_kpiRoot, "Label", "KPI 完成度", 30, faint,
                TextAnchor.MiddleLeft, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(400f, 44f));

            Image bgi;
            _kpiFill = UIFactory.CreateBar(_kpiRoot, "Bar", Vector2.zero, new Vector2(880f, 30f),
                new Color(0.13f, 0.13f, 0.16f, 0.14f), new Color(0.78f, 0.26f, 0.22f, 1f), out bgi);

            RectTransform rt = _kpiFill.rectTransform.parent as RectTransform;
            UIFactory.Anchor(rt, new Vector2(0f, 0.5f), new Vector2(420f, 0f), new Vector2(880f, 30f));
            _kpiFill.fillAmount = 0f;
        }

        void BuildSeal()
        {
            GameObject holder = new GameObject("Seal");
            holder.transform.SetParent(Root, false);
            _sealRoot = holder.AddComponent<RectTransform>();
            UIFactory.Anchor(_sealRoot, new Vector2(0.5f, 0f), new Vector2(0f, 232f), new Vector2(320f, 120f));

            Image ring = UIFactory.CreateImage(_sealRoot, "Ring", new Color(0.78f, 0.16f, 0.14f, 0.16f));
            UIFactory.Stretch(ring.rectTransform);

            _sealText = UIFactory.CreateText(_sealRoot, "Text", "未达标", 58,
                new Color(0.78f, 0.16f, 0.14f), TextAnchor.MiddleCenter);
            UIFactory.Stretch(_sealText.rectTransform);

            _comment = UIFactory.AnchoredText(Root, "Comment", "", 32, new Color(0.3f, 0.3f, 0.34f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(1400f, 46f));
        }

        void BuildButtons()
        {
            GameObject holder = new GameObject("Buttons");
            holder.transform.SetParent(Root, false);
            _buttons = holder.AddComponent<RectTransform>();
            UIFactory.Anchor(_buttons, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(900f, 100f));

            Button again = UIFactory.CreateButton(_buttons, "BtnAgain", "再 来 一 天", 40, new Vector2(340f, 92f),
                new Color(0.78f, 0.28f, 0.24f, 1f), () =>
                {
                    if (OnRestartClicked != null)
                    {
                        OnRestartClicked();
                    }
                });

            UIFactory.Anchor(again.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(-190f, 0f), new Vector2(340f, 92f));

            Button quit = UIFactory.CreateButton(_buttons, "BtnQuit", "离 职", 40, new Vector2(340f, 92f),
                new Color(0.34f, 0.34f, 0.38f, 1f), () =>
                {
                    if (OnMenuClicked != null)
                    {
                        OnMenuClicked();
                    }
                });

            UIFactory.Anchor(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(190f, 0f), new Vector2(340f, 92f));
        }

        protected override void OnUIOpen()
        {
            RunModel run = _ctx.Game.Run;
            ConfigManager cfg = _ctx.Game.Cfg;
            bool cleared = run.Ending != Ending.Fail;

            _title.text = cleared ? "述 职 报 告" : "离 职 证 明";
            _stamp.text = cleared
                ? WorkClockModel.Stamp(cfg.DayCount, WeekdayOf(cfg, cfg.DayCount), 1f, cfg.Clock)
                : WorkClockModel.Stamp(run.DayIndex, WeekdayOf(cfg, run.DayIndex), run.DayProgress01, cfg.Clock);

            _rankValue.text = cfg.RankOf(run.Player.Level);

            // A clear pays the fixed joke figure. Anything short of that prorates by time served, so the
            // number still lands exactly on 9,996 when the six days are actually finished.
            int salary = cleared
                ? cfg.Progression.FinalSalary
                : CombatFormula.Salary(run.CombatSeconds, cfg.TotalCombatSeconds, cfg.Progression);

            _salaryValue.text = "¥" + salary.ToString("N0");
            _sanValue.text = Mathf.Max(0, Mathf.CeilToInt(run.Player.San)).ToString();

            _reportBody.text = BuildReportText(run, cfg);
            _bestLoot.text = run.AnyLootPicked
                ? "[" + QualityName(run.BestQuality) + "] " + run.BestLootName
                : "无";

            _bestLoot.color = run.AnyLootPicked && run.BestQuality >= Quality.Yellow
                ? cfg.QualityOf(run.BestQuality).Color
                : new Color(0.13f, 0.13f, 0.16f);

            _loadout.text = Loadout(run.Player);

            _kpiTarget = run.Kpi(cfg.Progression);
            _kpiFill.fillAmount = 0f;
            _kpiLabel.text = "KPI 完成度";

            _sealText.text = cleared ? "未达标" : "已离职";
            _comment.text = Comment(run, cfg);

            _step = 0;
            _skipped = false;
            ApplyStep(0);
        }

        /// <summary>Six boxes then three, so the loadout reads at a glance without any icons.</summary>
        static string Loadout(PlayerModel p)
        {
            StringBuilder sb = new StringBuilder(16);
            for (int i = 0; i < PlayerModel.WeaponSlots; i++)
            {
                sb.Append(p.Weapons[i].IsEmpty ? '□' : '▣');
            }

            sb.Append("    ");

            for (int i = 0; i < PlayerModel.ArmorSlots; i++)
            {
                sb.Append(p.Armors[i].IsEmpty ? '□' : '▣');
            }

            return sb.ToString();
        }

        static string WeekdayOf(ConfigManager cfg, int dayIndex)
        {
            DayDef d = cfg.Day(dayIndex);
            return d != null ? d.Weekday : string.Empty;
        }

        /// <summary>
        /// Sorted by count, two columns. Naming the enemy type is what turns a kill tally into a work
        /// log, and the work log is the joke the whole screen is built around.
        /// </summary>
        string BuildReportText(RunModel run, ConfigManager cfg)
        {
            _rows.Clear();
            foreach (KeyValuePair<string, int> kv in run.KillsByType)
            {
                _rows.Add(kv);
            }

            _rows.Sort(CompareDescending);

            _sb.Length = 0;
            if (_rows.Count == 0)
            {
                _sb.Append("本周无产出");
                return _sb.ToString();
            }

            int shown = 0;
            for (int i = 0; i < _rows.Count && shown < 8; i++)
            {
                EnemyDef def = cfg.Enemy(_rows[i].Key);
                if (def == null)
                {
                    continue;
                }

                _sb.Append(def.ReportVerb).Append(' ').Append(_rows[i].Value).Append(' ').Append(def.ReportUnit);
                _sb.Append(shown % 2 == 1 ? "\n" : "          ");
                shown++;
            }

            return _sb.ToString();
        }

        static int CompareDescending(KeyValuePair<string, int> a, KeyValuePair<string, int> b)
        {
            return b.Value.CompareTo(a.Value);
        }

        /// <summary>The boss's line about your year. Reads off the ending and how much sanity was left.</summary>
        static string Comment(RunModel run, ConfigManager cfg)
        {
            float pct = run.Player.MaxSan > 0f ? run.Player.San / run.Player.MaxSan * 100f : 0f;

            switch (run.Ending)
            {
                case Ending.Clear:
                    return pct > 50f ? "表现尚可，明年继续努力。" : "工作完成了，但你的状态需要调整一下。";

                case Ending.ClearTimeout:
                    return "述职未完成，下周继续。";

                default:
                    if (run.DayIndex <= 2)
                    {
                        return "试用期未通过。";
                    }

                    return run.DayIndex <= 4 ? "建议你重新考虑一下职业规划。" : "就差一点，可惜。";
            }
        }

        protected override void OnUITick(float unscaledDt)
        {
            float t = _skipped ? 999f : StateSeconds();

            int want = 0;
            for (int i = 0; i < StepAt.Length; i++)
            {
                if (t >= StepAt[i])
                {
                    want = i;
                }
            }

            if (want != _step)
            {
                _step = want;
                ApplyStep(want);
            }

            // The bar crawls to its final value and stops. Stopping short of 100 is the point, so it is
            // animated rather than snapped: a snapped bar reads as a number, a crawling one reads as a gag.
            if (_step >= 5)
            {
                float target = _kpiTarget / 100f;
                _kpiFill.fillAmount = Mathf.Min(target, _kpiFill.fillAmount + unscaledDt * 0.55f);

                int shown = Mathf.RoundToInt(_kpiFill.fillAmount * 100f);
                _kpiLabel.text = "KPI 完成度  " + shown + "%";
            }

            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                Skip();
            }
        }

        float StateSeconds()
        {
            return _ctx.Driver.Flow.StateSeconds;
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
            _kpiFill.fillAmount = _kpiTarget / 100f;
            _kpiLabel.text = "KPI 完成度  " + _kpiTarget + "%";
        }

        void ApplyStep(int step)
        {
            _bigRow.gameObject.SetActive(step >= 1);
            _reportTitle.enabled = step >= 2;
            _reportBody.enabled = step >= 2;
            _bestCaption.enabled = step >= 3;
            _bestLoot.enabled = step >= 3;
            _loadoutCaption.enabled = step >= 3;
            _loadout.enabled = step >= 3;
            _kpiRoot.gameObject.SetActive(step >= 4);
            _sealRoot.gameObject.SetActive(step >= 5);
            _comment.enabled = step >= 5;
            _buttons.gameObject.SetActive(step >= 5);
        }

        static string QualityName(Quality q)
        {
            switch (q)
            {
                case Quality.Blue: return "蓝";
                case Quality.Yellow: return "黄";
                case Quality.Orange: return "橙";
                default: return "白";
            }
        }
    }
}
