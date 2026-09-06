using System;
using System.Collections.Generic;
using SeaVillage.Core;

namespace SeaVillage.Data
{
    public static class TownDisplayNames
    {
        private static readonly Dictionary<string, string> _townDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "All", "모든 마을" },
            { "Start", "시작 마을" },
            { "Forest", "숲속 마을" },
            { "Mine", "바위 마을" },
            { "Sea", "바다 마을" },
            { "Dessert", "과자 마을" },
            { "Cave", "동굴 마을" },
        };

        public static string GetTownDisplayName(string townKey)
        {
            if (string.IsNullOrWhiteSpace(townKey))
                return string.Empty;

            string normalizedKey = townKey.Trim();
            return _townDisplayNames.TryGetValue(normalizedKey, out string displayName)
                ? displayName
                : normalizedKey;
        }

        public static string GetTownDisplayName(TownKey townKey)
        {
            return GetTownDisplayName(TownKeyUtility.ToStorageKey(townKey));
        }
    }
}
