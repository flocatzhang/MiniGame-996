using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using UnityEngine;

namespace OfficeHell.Config
{
    /// <summary>
    /// Attribute readers that never throw. Every miss or malformed value is appended to a report
    /// list instead, so one typo in a designer table degrades a single row rather than the run.
    /// </summary>
    public static class XmlRead
    {
        public static string Str(XElement e, string name, string fallback, List<string> report)
        {
            XAttribute a = e.Attribute(name);
            if (a == null)
            {
                return fallback;
            }

            return a.Value;
        }

        public static string Required(XElement e, string name, List<string> report)
        {
            XAttribute a = e.Attribute(name);
            if (a == null || string.IsNullOrEmpty(a.Value))
            {
                Add(report, "<" + e.Name + "> missing required attribute '" + name + "'");
                return null;
            }

            return a.Value;
        }

        public static float Num(XElement e, string name, float fallback, List<string> report)
        {
            XAttribute a = e.Attribute(name);
            if (a == null)
            {
                return fallback;
            }

            float v;
            if (float.TryParse(a.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            {
                return v;
            }

            Add(report, "<" + e.Name + "> attribute '" + name + "' is not a number: '" + a.Value + "'");
            return fallback;
        }

        public static int Int(XElement e, string name, int fallback, List<string> report)
        {
            XAttribute a = e.Attribute(name);
            if (a == null)
            {
                return fallback;
            }

            int v;
            if (int.TryParse(a.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
            {
                return v;
            }

            Add(report, "<" + e.Name + "> attribute '" + name + "' is not an integer: '" + a.Value + "'");
            return fallback;
        }

        public static bool Bool(XElement e, string name, bool fallback, List<string> report)
        {
            XAttribute a = e.Attribute(name);
            if (a == null)
            {
                return fallback;
            }

            string v = a.Value;
            if (v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (v == "0" || string.Equals(v, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Add(report, "<" + e.Name + "> attribute '" + name + "' is not a bool: '" + v + "'");
            return fallback;
        }

        public static Color Col(XElement e, string name, Color fallback, List<string> report)
        {
            XAttribute a = e.Attribute(name);
            if (a == null)
            {
                return fallback;
            }

            Color parsed;
            if (ColorUtility.TryParseHtmlString(a.Value, out parsed))
            {
                return parsed;
            }

            Add(report, "<" + e.Name + "> attribute '" + name + "' is not a html color: '" + a.Value + "'");
            return fallback;
        }

        public static T Enm<T>(XElement e, string name, T fallback, List<string> report) where T : struct
        {
            XAttribute a = e.Attribute(name);
            if (a == null || string.IsNullOrEmpty(a.Value))
            {
                return fallback;
            }

            try
            {
                return (T)Enum.Parse(typeof(T), a.Value, true);
            }
            catch (Exception)
            {
                Add(report, "<" + e.Name + "> attribute '" + name + "' is not a valid " + typeof(T).Name + ": '" + a.Value + "'");
                return fallback;
            }
        }

        public static XDocument Doc(string text, string fileName, List<string> report)
        {
            if (string.IsNullOrEmpty(text))
            {
                Add(report, "file not found or empty: " + fileName);
                return null;
            }

            try
            {
                return XDocument.Parse(text);
            }
            catch (Exception ex)
            {
                Add(report, "xml syntax error in " + fileName + ": " + ex.Message);
                return null;
            }
        }

        public static void Add(List<string> report, string line)
        {
            if (report != null)
            {
                report.Add(line);
            }
        }
    }
}
