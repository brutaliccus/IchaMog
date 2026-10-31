using System.Collections.Generic;
using System.Text;

namespace Transmog
{
    internal struct TransmogEntry
    {
        public string Prefab;
        public int Variant;

        public bool Hidden => Prefab == TransmogStore.HiddenToken;

        public TransmogEntry(string prefab, int variant)
        {
            Prefab = prefab;
            Variant = variant;
        }
    }

    /// <summary>
    /// Per-character storage in Player.m_customData (saved with the character file).
    /// Format: "helmet=PrefabName:variant;chest=-:0" where "-" means hidden.
    /// </summary>
    internal static class TransmogStore
    {
        public const string DataKey = "vpf.Transmog";
        public const string HiddenToken = "-";

        private static string _cachedRaw;
        private static readonly TransmogEntry?[] _cached = new TransmogEntry?[TransmogSlots.Count];

        private static TransmogEntry?[] Parse(string raw)
        {
            if (ReferenceEquals(raw, _cachedRaw)) return _cached;
            for (int i = 0; i < _cached.Length; i++) _cached[i] = null;
            _cachedRaw = raw;
            if (string.IsNullOrEmpty(raw)) return _cached;

            foreach (var part in raw.Split(';'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                string key = part.Substring(0, eq).Trim();
                string val = part.Substring(eq + 1);
                int idx = System.Array.IndexOf(TransmogSlots.Keys, key);
                if (idx < 0) continue;

                int variant = 0;
                string prefab = val;
                int colon = val.LastIndexOf(':');
                if (colon > 0)
                {
                    prefab = val.Substring(0, colon);
                    int.TryParse(val.Substring(colon + 1), out variant);
                }
                if (string.IsNullOrEmpty(prefab)) continue;
                _cached[idx] = new TransmogEntry(prefab, variant < 0 ? 0 : variant);
            }
            return _cached;
        }

        public static bool TryGet(Player player, TransmogSlot slot, out TransmogEntry entry)
        {
            entry = default;
            if (player == null || player.m_customData == null) return false;
            if (!player.m_customData.TryGetValue(DataKey, out var raw)) return false;
            var parsed = Parse(raw)[(int)slot];
            if (!parsed.HasValue) return false;
            entry = parsed.Value;
            return true;
        }

        /// <summary>Sets (or clears when null) the look for a slot.</summary>
        public static bool Set(Player player, TransmogSlot slot, TransmogEntry? entry)
        {
            if (player == null || player.m_customData == null) return false;
            if (entry.HasValue)
            {
                var p = entry.Value.Prefab;
                if (string.IsNullOrEmpty(p) || p.IndexOf(';') >= 0 || p.IndexOf('=') >= 0) return false;
            }

            player.m_customData.TryGetValue(DataKey, out var raw);
            var current = (TransmogEntry?[])Parse(raw).Clone();
            current[(int)slot] = entry;

            var sb = new StringBuilder();
            for (int i = 0; i < current.Length; i++)
            {
                if (!current[i].HasValue) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(TransmogSlots.Keys[i]).Append('=').Append(current[i].Value.Prefab)
                  .Append(':').Append(current[i].Value.Variant);
            }

            if (sb.Length == 0) player.m_customData.Remove(DataKey);
            else player.m_customData[DataKey] = sb.ToString();
            _cachedRaw = null; // force re-parse
            return true;
        }

        public static void ClearAll(Player player)
        {
            if (player?.m_customData == null) return;
            player.m_customData.Remove(DataKey);
            _cachedRaw = null;
        }
    }
}
