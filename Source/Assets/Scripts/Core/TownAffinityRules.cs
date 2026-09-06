using System;

namespace SeaVillage.Core
{
    /// <summary>마을 호감도 범위, 해금 단계와 표시 호칭을 정의</summary>
    public static class TownAffinityRules
    {
        public const int MinAffinity = 0;
        public const int MaxAffinity = 10;
        public const int GeneralShopRequiredAffinity = 3;
        public const int SpecialShopRequiredAffinity = 5;
        public const int PlayerShopRequiredAffinity = 7;
        public const int DoubleStockRequiredAffinity = 9;
        public const int BoardQuestReward = 3;

        public static string GetTitle(int affinity)
        {
            int normalized = Clamp(affinity);
            if (normalized >= 10)
                return "마을의 일원";
            if (normalized >= 9)
                return "마을의 친구";
            if (normalized >= 7)
                return "마을의 상인";
            if (normalized >= 5)
                return "마을의 단골";
            if (normalized >= 3)
                return "마을의 손님";

            return "낯선 이방인";
        }

        public static int Clamp(int affinity)
        {
            return Math.Clamp(affinity, MinAffinity, MaxAffinity);
        }

    }
}
