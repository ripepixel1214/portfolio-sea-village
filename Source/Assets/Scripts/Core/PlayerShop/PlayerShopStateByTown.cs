using System;
using System.Collections.Generic;

namespace SeaVillage.Core
{
    /// <summary>마을별 플레이어 가게 상태 정보</summary>
    [Serializable]
    public class PlayerShopStateByTown
    {
        public TownKey townKey = TownKey.Unknown;
        public PlayerShopBuildStage buildStage;
        public int slotCapacity;
        public int queueCapacity;

        public int pendingRevenue;
        public int pendingTip;
        public int pendingPurchaseCost;
        public int pendingCustomerCount;
        public int totalRevenue;
        public int lastSettlementDay;
        public int lastVisitDay;
        public int dailySoldCount;
        public int totalSoldCount;

        public int cashierStaffId;
        public int salesStaffId;

        public List<PlayerShopItem> listedItems = new List<PlayerShopItem>();
        public List<PlayerShopSettlementItem> settlementItems = new List<PlayerShopSettlementItem>();

        public bool IsBuilt => buildStage != PlayerShopBuildStage.None;
        public int ShopLevel => buildStage == PlayerShopBuildStage.Upgraded ? 2 : (buildStage == PlayerShopBuildStage.Built ? 1 : 0);

        public bool HasListedItem(int itemId)
        {
            for (int i = 0; i < listedItems.Count; i++)
            {
                if (listedItems[i].itemId == itemId)
                    return true;
            }

            return false;
        }

        public PlayerShopItem GetListedItem(int itemId)
        {
            for (int i = 0; i < listedItems.Count; i++)
            {
                if (listedItems[i].itemId == itemId)
                    return listedItems[i];
            }

            return null;
        }

        /// <summary>슬롯 위치의 등록 아이템 반환, 비어 있으면 null</summary>
        public PlayerShopItem GetSlotItem(int slotIndex)
        {
            for (int i = 0; i < listedItems.Count; i++)
            {
                if (listedItems[i].slotIndex == slotIndex)
                    return listedItems[i];
            }

            return null;
        }
    }
}
