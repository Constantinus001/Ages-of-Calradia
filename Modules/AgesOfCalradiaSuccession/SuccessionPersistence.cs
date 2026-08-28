using System;
using System.Collections.Generic;

namespace AgesOfCalradiaSuccession
{
    internal static class SuccessionPersistence
    {
        internal static string Serialize(IDictionary<string, string> laws, IDictionary<string, string> dynasties, IDictionary<string, string> monarchs,
            IDictionary<string, string> minorHeirs, IDictionary<string, string> regents)
        {
            List<string> records = new List<string> { "v3" };
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            if (laws != null) foreach (string key in laws.Keys) keys.Add(key);
            if (dynasties != null) foreach (string key in dynasties.Keys) keys.Add(key);
            if (monarchs != null) foreach (string key in monarchs.Keys) keys.Add(key);
            if (minorHeirs != null) foreach (string key in minorHeirs.Keys) keys.Add(key);
            if (regents != null) foreach (string key in regents.Keys) keys.Add(key);

            foreach (string key in keys)
            {
                records.Add(Escape(key) + "|" + Escape(Get(laws, key)) + "|" + Escape(Get(dynasties, key)) + "|" + Escape(Get(monarchs, key))
                    + "|" + Escape(Get(minorHeirs, key)) + "|" + Escape(Get(regents, key)));
            }
            return string.Join("\n", records.ToArray());
        }

        internal static void Deserialize(string payload, IDictionary<string, string> laws, IDictionary<string, string> dynasties, IDictionary<string, string> monarchs,
            IDictionary<string, string> minorHeirs, IDictionary<string, string> regents)
        {
            laws.Clear(); dynasties.Clear(); monarchs.Clear(); minorHeirs.Clear(); regents.Clear();
            if (string.IsNullOrWhiteSpace(payload)) return;
            string[] lines = payload.Replace("\r", string.Empty).Split('\n');
            if (lines.Length == 0 || (lines[0] != "v2" && lines[0] != "v3")) return;
            bool versionThree = lines[0] == "v3";
            for (int i = 1; i < lines.Length; i++)
            {
                string[] parts = lines[i].Split('|');
                if ((!versionThree && parts.Length != 4) || (versionThree && parts.Length != 6)) continue;
                string key = Unescape(parts[0]);
                if (string.IsNullOrEmpty(key)) continue;
                laws[key] = Unescape(parts[1]);
                dynasties[key] = Unescape(parts[2]);
                monarchs[key] = Unescape(parts[3]);
                if (versionThree)
                {
                    minorHeirs[key] = Unescape(parts[4]);
                    regents[key] = Unescape(parts[5]);
                }
            }
        }

        private static string Get(IDictionary<string, string> values, string key)
        {
            string value;
            return values != null && values.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
        }

        private static string Escape(string value)
        {
            return Uri.EscapeDataString(value ?? string.Empty);
        }

        private static string Unescape(string value)
        {
            return Uri.UnescapeDataString(value ?? string.Empty);
        }
    }
}
