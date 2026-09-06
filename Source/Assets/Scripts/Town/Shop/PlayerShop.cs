using System.Collections.Generic;
using SeaVillage.Core;

namespace SeaVillage.Town
{
    /// <summary>현재 마을의 플레이어 가게 표현</summary>
    public class PlayerShop : ShopBase, IPlayerShopSalesTarget
    {
        /// <summary>현재 마을 식별자 반환</summary>
        public TownKey TownKey => GameManager.HasInstance ? GameManager.Instance.CurrentTownKey : TownKey.Unknown;

        public override bool IsPlayerOwned => true;

        public override ShopType ShopType => ShopType.PlayerShop;

        public override bool IsOpen => base.IsOpen
            && PlayerShopManager.HasInstance
            && PlayerShopManager.Instance.IsShopBuilt(TownKey)
            && PlayerShopOperatingRules.CanServeCustomers(StaffEffects);

        public override ShopTier Tier =>
            PlayerShopManager.HasInstance && PlayerShopManager.Instance.GetShopLevel(TownKey) >= 2
                ? ShopTier.Tier2
                : ShopTier.Tier1;

        protected override int QueueCapacity => ResolveQueueCapacity();

        public StaffEffectReadOnly StaffEffects =>
            PlayerShopManager.HasInstance
                ? PlayerShopManager.Instance.GetStaffEffects(TownKey)
                : StaffEffectReadOnly.Empty;

        public int OfflineCustomerCount =>
            PlayerShopManager.HasInstance
                ? PlayerShopManager.Instance.GetOfflineCustomerCount(TownKey)
                : 0;

        public IReadOnlyList<PlayerShopSaleOffer> GetSaleOffers()
        {
            return PlayerShopManager.HasInstance
                ? PlayerShopManager.Instance.GetSaleOffers(TownKey)
                : System.Array.Empty<PlayerShopSaleOffer>();
        }

        public bool TryRecordCustomerVisit(out string failReason)
        {
            if (!PlayerShopManager.HasInstance)
            {
                failReason = "[Error] 플레이어 가게 매니저가 준비되지 않았습니다";
                return false;
            }

            return PlayerShopManager.Instance.TryRecordCustomerVisit(TownKey, out failReason);
        }

        public bool TryRecordCustomerPurchase(
            IReadOnlyList<PlayerShopSaleRequest> requests,
            long customerPayment,
            out PlayerShopSaleResult receipt,
            out string failReason)
        {
            if (!PlayerShopManager.HasInstance)
            {
                receipt = null;
                failReason = "[Error] 플레이어 가게 매니저가 준비되지 않았습니다";
                return false;
            }

            return PlayerShopManager.Instance.TryRecordCustomerPurchase(
                TownKey,
                requests,
                customerPayment,
                out receipt,
                out failReason);
        }

        private int ResolveQueueCapacity()
        {
            if (!PlayerShopManager.HasInstance)
                return base.QueueCapacity;

            int queueCapacity = PlayerShopManager.Instance.GetQueueCapacity(TownKey);
            return queueCapacity > 0 ? queueCapacity : base.QueueCapacity;
        }
    }
}
