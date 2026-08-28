using System;
using System.Collections.Generic;
using System.Text;

namespace AgesOfCalradiaReligions
{
    /// <summary>
    /// Bannerlord 1.4.x can write an invalid save when one synchronized string
    /// grows beyond its internal per-string limit. Keep every persisted segment
    /// comfortably below that boundary and reconstruct the payload on load.
    /// </summary>
    internal static class ChunkedSavePayload
    {
        internal const int MaximumChunkCharacters = 12000;

        internal static List<string> Split(string payload)
        {
            payload = payload ?? string.Empty;
            List<string> chunks = new List<string>(Math.Max(1, (payload.Length + MaximumChunkCharacters - 1) / MaximumChunkCharacters));
            if (payload.Length == 0)
            {
                chunks.Add(string.Empty);
                return chunks;
            }

            for (int offset = 0; offset < payload.Length; offset += MaximumChunkCharacters)
                chunks.Add(payload.Substring(offset, Math.Min(MaximumChunkCharacters, payload.Length - offset)));
            return chunks;
        }

        internal static string Join(List<string> chunks)
        {
            if (chunks == null || chunks.Count == 0) return string.Empty;
            StringBuilder builder = new StringBuilder();
            foreach (string chunk in chunks) builder.Append(chunk ?? string.Empty);
            return builder.ToString();
        }

        internal static bool HasPayload(List<string> chunks)
        {
            return chunks != null && chunks.Count > 0;
        }
    }
}
