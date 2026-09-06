namespace SeaVillage.Core
{
    public enum SpecialShopFeature
    {
        GeneralTrading,
        SpecialContent,
        PlayerShop,
    }

    public static class SpecialShopAccessPolicy
    {
        public static int GetRequiredAffinity(SpecialShopFeature feature)
        {
            switch (feature)
            {
                case SpecialShopFeature.GeneralTrading:
                    return TownAffinityRules.GeneralShopRequiredAffinity;
                case SpecialShopFeature.PlayerShop:
                    return TownAffinityRules.PlayerShopRequiredAffinity;
                case SpecialShopFeature.SpecialContent:
                default:
                    return TownAffinityRules.SpecialShopRequiredAffinity;
            }
        }

        public static bool CanUseCurrentTown(SpecialShopFeature feature)
        {
            if (!GameManager.HasInstance || !TownProgressionManager.HasInstance)
                return false;

            TownKey townKey = GameManager.Instance.CurrentTownKey;
            if (townKey == TownKey.Unknown)
                return false;

            return TownProgressionManager.Instance.GetAffinity(townKey)
                >= GetRequiredAffinity(feature);
        }

        public static int GetPanelRequiredAffinity(SpecialShopType shopType)
        {
            switch (shopType)
            {
                case SpecialShopType.Restaurant:
                case SpecialShopType.AcornWorkshop:
                case SpecialShopType.RockExchange:
                case SpecialShopType.PotionShop:
                case SpecialShopType.TailorHouse:
                    return TownAffinityRules.GeneralShopRequiredAffinity;
                default:
                    return TownAffinityRules.SpecialShopRequiredAffinity;
            }
        }

        public static bool CanOpenCurrentTown(SpecialShopType shopType)
        {
            if (!GameManager.HasInstance || !TownProgressionManager.HasInstance)
                return false;

            TownKey townKey = GameManager.Instance.CurrentTownKey;
            if (townKey == TownKey.Unknown)
                return false;

            return TownProgressionManager.Instance.GetAffinity(townKey)
                >= GetPanelRequiredAffinity(shopType);
        }
    }
}
