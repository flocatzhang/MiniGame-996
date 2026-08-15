using System;
using System.Collections.Generic;
using System.Globalization;

namespace OfficeHell.Core
{
    /// <summary>
    /// Parser for the "key=value;key=value" strings used by behaviorParam in the xml tables.
    /// Avoids designing a dedicated xml node per behavior, which is the wrong trade at this stage.
    /// </summary>
    public sealed class KvBag
    {
        static readonly char[] PairSep = { ';' };
        static readonly char[] KvSep = { '=' };

        readonly Dictionary<string, string> _map = new Dictionary<string, string>(8, StringComparer.OrdinalIgnoreCase);

        public static readonly KvBag Empty = new KvBag();

        public static KvBag Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return Empty;
            }

            KvBag bag = new KvBag();
            string[] pairs = raw.Split(PairSep, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pairs.Length; i++)
            {
                string[] kv = pairs[i].Split(KvSep, 2);
                if (kv.Length != 2)
                {
                    continue;
                }

                bag._map[kv[0].Trim()] = kv[1].Trim();
            }

            return bag;
        }

        public bool Has(string key)
        {
            return _map.ContainsKey(key);
        }

        public float GetFloat(string key, float fallback)
        {
            string raw;
            float v;
            if (_map.TryGetValue(key, out raw) &&
                float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            {
                return v;
            }

            return fallback;
        }

        public int GetInt(string key, int fallback)
        {
            string raw;
            int v;
            if (_map.TryGetValue(key, out raw) &&
                int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
            {
                return v;
            }

            return fallback;
        }

        public bool GetBool(string key, bool fallback)
        {
            string raw;
            if (_map.TryGetValue(key, out raw))
            {
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }

            return fallback;
        }

        public string GetString(string key, string fallback)
        {
            string raw;
            return _map.TryGetValue(key, out raw) ? raw : fallback;
        }
    }
}
