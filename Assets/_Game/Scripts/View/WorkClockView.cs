using OfficeHell.Config;
using OfficeHell.Model;
using UnityEngine;
using UnityEngine.UI;

namespace OfficeHell.View
{
    /// <summary>
    /// Read only projection of day progress. This class only ever reads DayProgress01, it must
    /// never be able to end a day, otherwise changing the clock speed would silently change the
    /// gameplay length and there would be two sources of truth.
    /// </summary>
    public sealed class WorkClockView
    {
        readonly Text _label;
        readonly Text _period;

        string _last;

        public WorkClockView(Text label, Text period)
        {
            _label = label;
            _period = period;
        }

        public void Refresh(RunModel run, ClockDef def)
        {
            if (_label == null)
            {
                return;
            }

            int hour, minute;
            WorkClockModel.Project(run.DayProgress01, def, out hour, out minute);

            string text = hour.ToString("00") + ":" + minute.ToString("00");
            if (text != _last)
            {
                _last = text;
                _label.text = text;
            }

            // The tint alone communicates "the day is nearly over" without reading the digits.
            float t = run.DayProgress01;
            _label.color = Color.Lerp(new Color(0.88f, 0.92f, 1f), new Color(1f, 0.55f, 0.35f), t * t);

            if (_period != null)
            {
                string weekday = run.Day != null ? run.Day.Weekday : string.Empty;
                _period.text = weekday.Length > 0 ? weekday + " · " + PeriodName(hour) : PeriodName(hour);
            }
        }

        static string PeriodName(int hour)
        {
            if (hour < 12)
            {
                return "上午";
            }

            if (hour < 14)
            {
                return "午休";
            }

            if (hour < 18)
            {
                return "下午";
            }

            return "加班";
        }
    }
}
