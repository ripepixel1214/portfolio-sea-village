using System;

namespace SeaVillage.Core
{
    /// <summary>마을 식별자 변환 유틸리티</summary>
    public static class TownKeyUtility
    {
        /// <summary>마을 호감도 스탯 키 접두사. Player_Gold·Boat_Food 와 같은 {접두}_{대상} 형식</summary>
        public const string LoveLevelKeyPrefix = "LoveLv_";

        /// <summary>문자열을 마을 식별자로 변환하고 성공 여부 반환</summary>
        public static bool TryParse(string value, out TownKey townKey)
        {
            townKey = ParseOrUnknown(value);
            return townKey != TownKey.Unknown;
        }

        private static TownKey ParseOrUnknown(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return TownKey.Unknown;

            string normalized = value.Trim();
            if (string.Equals(normalized, nameof(TownKey.Start), StringComparison.OrdinalIgnoreCase))
                return TownKey.Start;
            if (string.Equals(normalized, nameof(TownKey.Forest), StringComparison.OrdinalIgnoreCase))
                return TownKey.Forest;
            if (string.Equals(normalized, nameof(TownKey.Mine), StringComparison.OrdinalIgnoreCase))
                return TownKey.Mine;
            if (string.Equals(normalized, nameof(TownKey.Sea), StringComparison.OrdinalIgnoreCase))
                return TownKey.Sea;
            if (string.Equals(normalized, nameof(TownKey.Dessert), StringComparison.OrdinalIgnoreCase))
                return TownKey.Dessert;
            if (string.Equals(normalized, nameof(TownKey.Cave), StringComparison.OrdinalIgnoreCase))
                return TownKey.Cave;

            return TownKey.Unknown;
        }

        /// <summary>저장 키 문자열의 공백 정규화</summary>
        public static string NormalizeStorageKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        /// <summary>마을 식별자를 호감도 스탯 키로 변환 (e.g. TownKey.Sea → "LoveLv_Sea")</summary>
        public static string ToLoveLevelKey(TownKey townKey)
            => LoveLevelKeyPrefix + ToStorageKey(townKey);

        /// <summary>마을 이름을 호감도 스탯 키로 변환 (e.g. "Sea" → "LoveLv_Sea")</summary>
        public static string ToLoveLevelKey(string townName)
            => LoveLevelKeyPrefix + NormalizeStorageKey(townName);

        /// <summary>
        /// 구형 접미사 키({Town}LoveLv)를 정식 키(LoveLv_{Town})로 흡수한다.
        /// EventCondition 테이블과 기존 세이브가 접미사 형식을 쓰고 있어 저장소 입구에서 통일한다.
        /// </summary>
        public static string NormalizeLoveLevelKey(string key)
        {
            const string legacySuffix = "LoveLv";

            if (string.IsNullOrEmpty(key)
                || key.StartsWith(LoveLevelKeyPrefix, StringComparison.Ordinal)
                || key.Length <= legacySuffix.Length
                || !key.EndsWith(legacySuffix, StringComparison.Ordinal))
            {
                return key;
            }

            return LoveLevelKeyPrefix + key[..^legacySuffix.Length];
        }

        /// <summary>마을 식별자를 저장 키 문자열로 변환</summary>
        public static string ToStorageKey(TownKey townKey)
        {
            return townKey switch
            {
                TownKey.Start => nameof(TownKey.Start),
                TownKey.Forest => nameof(TownKey.Forest),
                TownKey.Mine => nameof(TownKey.Mine),
                TownKey.Sea => nameof(TownKey.Sea),
                TownKey.Dessert => nameof(TownKey.Dessert),
                TownKey.Cave => nameof(TownKey.Cave),
                _ => string.Empty,
            };
        }
    }
}
