using System;
using System.Collections.Generic;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    public static class PlayerInventoryViewPolicy
    {
        public static bool ShouldDisplay(ItemData itemData, ISet<TownKey> hiddenOrigins)
        {
            if (itemData == null)
                return false;

            return hiddenOrigins == null
                || !TownKeyUtility.TryParse(itemData.Town, out TownKey originTown)
                || !hiddenOrigins.Contains(originTown);
        }

        public static int CalculateMarketRatePercent(int originPrice, int currentPrice)
        {
            if (originPrice <= 0 || currentPrice < 0)
                return 0;

            return (int)Math.Round(
                currentPrice * 100d / originPrice,
                MidpointRounding.AwayFromZero);
        }

        public static string GetOriginLabel(string town)
        {
            if (!TownKeyUtility.TryParse(town, out TownKey townKey))
                return TownDisplayNames.GetTownDisplayName(town);

            return townKey switch
            {
                TownKey.Start => "시작",
                TownKey.Forest => "숲속",
                TownKey.Mine => "바위",
                TownKey.Sea => "바다",
                TownKey.Dessert => "과자",
                TownKey.Cave => "동굴",
                _ => TownDisplayNames.GetTownDisplayName(town),
            };
        }
    }
}
