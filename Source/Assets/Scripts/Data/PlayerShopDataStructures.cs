using System.Collections.Generic;
using MemoryPack;

namespace SeaVillage.Data
{
    /// <summary>플레이어 상점에 등록된 아이템의 저장 데이터, PlayerShopItem 과 필드 일치 필요</summary>
    [MemoryPackable]
    public partial class PlayerShopItemData
    {
        public int slotIndex;
        public int itemId;
        public int quantity;

        public int unitPrice;
        public int averagePurchasePrice;
    }

    [MemoryPackable]
    public partial class PlayerShopSettlementItemData
    {
        public int itemId;
        public int soldQuantity;
    }

    [MemoryPackable]
    public partial class StaffData
    {
        public int staffId;

        public int intelligence;
        public int charm;

        // 시청 고용 여부, 아이템형 직원과 구분
        public bool isTownHired;
        public List<int> usedStatItemIds = new List<int>();
    }

    [MemoryPackable]
    public partial class PlayerShopData
    {
        public string townKey = string.Empty;
        public int buildStage;

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

        public List<PlayerShopItemData> listedItems = new List<PlayerShopItemData>();
        public List<PlayerShopSettlementItemData> settlementItems = new List<PlayerShopSettlementItemData>();
    }
}
