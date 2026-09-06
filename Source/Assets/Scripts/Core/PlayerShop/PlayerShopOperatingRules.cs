namespace SeaVillage.Core
{
    /// <summary>플레이어 가게 운영 단계별 고정 규칙</summary>
    public static class PlayerShopOperatingRules
    {
        public const int Level1OfflineCustomerCount = 3;
        public const int Level2OfflineCustomerCount = 6;
        public const int MaxItemsPerCustomerPurchase = 3;

        public static bool CanServeCustomers(StaffEffectReadOnly staffEffects)
        {
            return staffEffects.HasCashier;
        }

        /// <summary>가게 단계에 따른 오프라인 방문 손님 수 조회</summary>
        public static int GetOfflineCustomerCount(PlayerShopBuildStage buildStage)
        {
            return buildStage == PlayerShopBuildStage.Upgraded
                ? Level2OfflineCustomerCount
                : Level1OfflineCustomerCount;
        }
    }
}
