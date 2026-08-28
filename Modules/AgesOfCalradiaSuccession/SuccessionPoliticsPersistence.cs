using System;
using System.Collections.Generic;

namespace AgesOfCalradiaSuccession
{
    internal static class SuccessionPoliticsPersistence
    {
        internal static string Serialize(params IDictionary<string, string>[] maps)
        {
            List<string> records = new List<string> { "v1" };
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (IDictionary<string, string> map in maps)
                if (map != null) foreach (string key in map.Keys) keys.Add(key);
            foreach (string key in keys)
            {
                List<string> fields = new List<string> { Escape(key) };
                foreach (IDictionary<string, string> map in maps)
                {
                    string value;
                    fields.Add(Escape(map != null && map.TryGetValue(key, out value) ? value : string.Empty));
                }
                records.Add(string.Join("|", fields.ToArray()));
            }
            return string.Join("\n", records.ToArray());
        }

        internal static void Deserialize(string payload, params IDictionary<string, string>[] maps)
        {
            foreach (IDictionary<string, string> map in maps) map.Clear();
            if (string.IsNullOrWhiteSpace(payload)) return;
            string[] lines = payload.Replace("\r", string.Empty).Split('\n');
            if (lines.Length == 0 || lines[0] != "v1") return;
            for (int line = 1; line < lines.Length; line++)
            {
                string[] fields = lines[line].Split('|');
                if (fields.Length != maps.Length + 1) continue;
                string key;
                try
                {
                    key = Unescape(fields[0]);
                }
                catch (UriFormatException)
                {
                    continue;
                }
                if (string.IsNullOrEmpty(key)) continue;
                for (int map = 0; map < maps.Length; map++)
                {
                    try
                    {
                        maps[map][key] = Unescape(fields[map + 1]);
                    }
                    catch (UriFormatException)
                    {
                        maps[map][key] = string.Empty;
                    }
                }
            }
        }

        private static string Escape(string value) { return Uri.EscapeDataString(value ?? string.Empty); }

        private static string Unescape(string value)
        {
            value = value ?? string.Empty;
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] != '%') continue;
                if (index + 2 >= value.Length || !IsHex(value[index + 1]) || !IsHex(value[index + 2]))
                    throw new UriFormatException("Malformed percent escape in succession politics payload.");
                index += 2;
            }
            return Uri.UnescapeDataString(value);
        }

        private static bool IsHex(char value)
        {
            return (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F');
        }
    }
}
