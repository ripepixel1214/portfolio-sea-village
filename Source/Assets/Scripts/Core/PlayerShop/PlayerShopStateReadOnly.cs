using System;
using System.Collections.Generic;

namespace SeaVillage.Core
{
    /// <summary>플레이어 가게 상태의 읽기 전용 상태</summary>
    public sealed class PlayerShopStateReadOnly
    {
        public TownKey TownKey { get; }
        public PlayerShopBuildStage BuildStage { get; }
        public bool IsBuilt => BuildStage != PlayerShopBuildStage.None;
        public int ShopLevel => BuildStage == PlayerShopBuildStage.Upgraded ? 2 : (BuildStage == PlayerShopBuildStage.Built ? 1 : 0);
        public int SlotCapacity { get; }
        public int QueueCapacity { get; }
        public int PendingRevenue { get; }
        public int PendingTip { get; }
        public int PendingPurchaseCost { get; }
        public int PendingCustomerCount { get; }
        public int TotalRevenue { get; }
        public int LastSettlementDay { get; }
        public int LastVisitDay { get; }
        public int DailySoldCount { get; }
        public int TotalSoldCount { get; }
        public StaffInfo? Cashier { get; }
        public StaffInfo? Sales { get; }
        public IReadOnlyList<PlayerShopItemReadOnly> ListedItems { get; }

        internal PlayerShopStateReadOnly(
            PlayerShopStateByTown source,
            StaffProfile cashierProfile,
            StaffProfile salesProfile)
        {
            TownKey = source.townKey;
            BuildStage = source.buildStage;
            SlotCapacity = source.slotCapacity;
            QueueCapacity = source.queueCapacity;
            PendingRevenue = source.pendingRevenue;
            PendingTip = source.pendingTip;
            PendingPurchaseCost = source.pendingPurchaseCost;
            PendingCustomerCount = source.pendingCustomerCount;
            TotalRevenue = source.totalRevenue;
            LastSettlementDay = source.lastSettlementDay;
            LastVisitDay = source.lastVisitDay;
            DailySoldCount = source.dailySoldCount;
            TotalSoldCount = source.totalSoldCount;
            Cashier = cashierProfile != null ? new StaffInfo(cashierProfile) : null;
            Sales = salesProfile != null ? new StaffInfo(salesProfile) : null;

            int listedItemCount = source.listedItems?.Count ?? 0;
            var listedItems = new List<PlayerShopItemReadOnly>(listedItemCount);
            for (int i = 0; i < listedItemCount; i++)
            {
                PlayerShopItem item = source.listedItems[i];
                if (item != null)
                    listedItems.Add(new PlayerShopItemReadOnly(item));
            }

            ListedItems = listedItems;
        }

        /// <summary>슬롯 위치의 등록 아이템 읽기 전용 상태 반환</summary>
        public PlayerShopItemReadOnly GetSlotItem(int slotIndex)
        {
            for (int i = 0; i < ListedItems.Count; i++)
            {
                PlayerShopItemReadOnly item = ListedItems[i];
                if (item.SlotIndex == slotIndex)
                    return item;
            }

            return null;
        }
    }
}
