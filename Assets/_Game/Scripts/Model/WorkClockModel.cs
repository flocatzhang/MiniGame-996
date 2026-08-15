using OfficeHell.Config;
using UnityEngine;

namespace OfficeHell.Model
{
    /// <summary>
    /// Read only projection of day progress onto a working day. The clock never drives anything:
    /// the only thing that ends a day is DayElapsed reaching DayDef.Duration, which comes from xml.
    /// Keeping this one directional avoids having two sources of truth for day length.
    /// A side effect is the theme: Monday's twelve hours take 40 seconds, Saturday's take 120.
    /// </summary>
    public static class WorkClockModel
    {
        public static void Project(float progress01, ClockDef def, out int hour, out int minute)
        {
            int startMinutes = def.StartHour * 60;
            int spanMinutes = Mathf.Max(1, (def.EndHour - def.StartHour) * 60);

            int raw = startMinutes + Mathf.FloorToInt(spanMinutes * Mathf.Clamp01(progress01));

            // Snapping to a coarse step is what makes a 40 second day readable. A per minute
            // display would roll 18 minutes every second and turn into noise.
            int snap = Mathf.Max(1, def.SnapMinutes);
            raw = raw / snap * snap;
            raw = Mathf.Min(raw, def.EndHour * 60);

            hour = raw / 60;
            minute = raw % 60;
        }

        public static string Format(float progress01, ClockDef def)
        {
            int h, m;
            Project(progress01, def, out h, out m);
            return h.ToString("00") + ":" + m.ToString("00");
        }

        /// <summary>
        /// The failure screen says "崩溃于 第3天 周三 15:40" rather than a survival time. Same data,
        /// but one reads as a story and the other reads as telemetry.
        /// </summary>
        public static string Stamp(int dayIndex, string weekday, float progress01, ClockDef def)
        {
            return "第 " + dayIndex + " 天 " + weekday + " " + Format(progress01, def);
        }
    }
}
