using System.Collections.Generic;

namespace SeaVillage.Data
{
    /// <summary>
    /// 저장 데이터의 List<T>를 안전하게 복사하고 관리하는 유틸리티 클래스
    /// </summary>
    internal static class SaveSnapshotList
    {
        /// <summary>
        /// List<T>에서 index 위치의 요소를 가져오거나, 없으면 새로 생성하여 반환한다
        /// </summary>
        public static T GetOrCreate<T>(List<T> target, int index) where T : class, new()
        {
            if (index < target.Count)
            {
                T existing = target[index];
                if (existing != null)
                    return existing;

                existing = new T();
                target[index] = existing;
                return existing;
            }

            var created = new T();
            target.Add(created);
            return created;
        }

        public static void Trim<T>(List<T> target, int count)
        {
            if (target.Count > count)
                target.RemoveRange(count, target.Count - count);
        }

        public static void CopyItemPriceData(ItemPriceData source, ItemPriceData target)
        {
            target.ID = source.ID;
            target.Town = source.Town;
            target.Distance = source.Distance;
            target.Preference = source.Preference;
            target.HistoryHead = source.HistoryHead;
            target.HistoryTail = source.HistoryTail;
            target.HistoryCount = source.HistoryCount;

            int historyLength = source.PriceHistory?.Length ?? 0;
            if (target.PriceHistory == null || target.PriceHistory.Length != historyLength)
                target.PriceHistory = new int[historyLength];

            if (historyLength > 0)
                System.Array.Copy(source.PriceHistory, target.PriceHistory, historyLength);
        }

        public static void CopyPlayerShopDataList(List<PlayerShopData> source, List<PlayerShopData> target)
        {
            int count = source?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                PlayerShopData sourceShop = source[i];
                if (sourceShop == null)
                {
                    SetNull(target, i);
                    continue;
                }

                PlayerShopData targetShop = GetOrCreate(target, i);
                targetShop.townKey = sourceShop.townKey;
                targetShop.buildStage = sourceShop.buildStage;
                targetShop.slotCapacity = sourceShop.slotCapacity;
                targetShop.queueCapacity = sourceShop.queueCapacity;
                targetShop.pendingRevenue = sourceShop.pendingRevenue;
                targetShop.pendingTip = sourceShop.pendingTip;
                targetShop.pendingPurchaseCost = sourceShop.pendingPurchaseCost;
                targetShop.pendingCustomerCount = sourceShop.pendingCustomerCount;
                targetShop.totalRevenue = sourceShop.totalRevenue;
                targetShop.lastSettlementDay = sourceShop.lastSettlementDay;
                targetShop.lastVisitDay = sourceShop.lastVisitDay;
                targetShop.dailySoldCount = sourceShop.dailySoldCount;
                targetShop.totalSoldCount = sourceShop.totalSoldCount;
                targetShop.cashierStaffId = sourceShop.cashierStaffId;
                targetShop.salesStaffId = sourceShop.salesStaffId;
                targetShop.listedItems ??= new List<PlayerShopItemData>();
                targetShop.settlementItems ??= new List<PlayerShopSettlementItemData>();

                CopyPlayerShopItems(sourceShop.listedItems, targetShop.listedItems);
                CopySettlementItems(sourceShop.settlementItems, targetShop.settlementItems);
            }

            Trim(target, count);
        }

        public static void CopyStaffDataList(List<StaffData> source, List<StaffData> target)
        {
            int count = source?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                StaffData sourceStaff = source[i];
                if (sourceStaff == null)
                {
                    SetNull(target, i);
                    continue;
                }

                StaffData targetStaff = GetOrCreate(target, i);
                targetStaff.staffId = sourceStaff.staffId;
                targetStaff.intelligence = sourceStaff.intelligence;
                targetStaff.charm = sourceStaff.charm;
                targetStaff.isTownHired = sourceStaff.isTownHired;
                targetStaff.usedStatItemIds ??= new List<int>();
                targetStaff.usedStatItemIds.Clear();
                if (sourceStaff.usedStatItemIds != null)
                    targetStaff.usedStatItemIds.AddRange(sourceStaff.usedStatItemIds);
            }

            Trim(target, count);
        }

        public static void CopyPlayerStatData(PlayerStatSaveData source, PlayerStatSaveData target)
        {
            target.strength = source?.strength ?? Core.PlayerStatManager.DefaultStrength;
            target.agility = source?.agility ?? Core.PlayerStatManager.DefaultAgility;
            target.usedStatItemIds ??= new List<int>();
            target.usedStatItemIds.Clear();
            if (source?.usedStatItemIds != null)
                target.usedStatItemIds.AddRange(source.usedStatItemIds);
        }

        public static void CopyTownProgressionData(
            TownProgressionSaveData source,
            TownProgressionSaveData target)
        {
            target.harborTownKey = source?.harborTownKey ?? string.Empty;
            target.harborConsecutiveNightCount = source?.harborConsecutiveNightCount ?? 0;
            target.harborLastChargedDay = source?.harborLastChargedDay ?? 0;
            target.towns ??= new List<TownProgressSaveData>();

            int count = source?.towns?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                TownProgressSaveData sourceTown = source.towns[i];
                if (sourceTown == null)
                {
                    SetNull(target.towns, i);
                    continue;
                }

                TownProgressSaveData targetTown = GetOrCreate(target.towns, i);
                targetTown.townKey = sourceTown.townKey;
                targetTown.affinity = sourceTown.affinity;
                targetTown.purchasedItemCount = sourceTown.purchasedItemCount;
                targetTown.stayedDayCount = sourceTown.stayedDayCount;
            }

            Trim(target.towns, count);
        }

        private static void CopyPlayerShopItems(
            List<PlayerShopItemData> source,
            List<PlayerShopItemData> target)
        {
            int count = source?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                PlayerShopItemData sourceItem = source[i];
                if (sourceItem == null)
                {
                    SetNull(target, i);
                    continue;
                }

                PlayerShopItemData targetItem = GetOrCreate(target, i);
                targetItem.slotIndex = sourceItem.slotIndex;
                targetItem.itemId = sourceItem.itemId;
                targetItem.quantity = sourceItem.quantity;
                targetItem.unitPrice = sourceItem.unitPrice;
                targetItem.averagePurchasePrice = sourceItem.averagePurchasePrice;
            }

            Trim(target, count);
        }

        private static void CopySettlementItems(
            List<PlayerShopSettlementItemData> source,
            List<PlayerShopSettlementItemData> target)
        {
            int count = source?.Count ?? 0;
            for (int i = 0; i < count; i++)
            {
                PlayerShopSettlementItemData sourceItem = source[i];
                if (sourceItem == null)
                {
                    SetNull(target, i);
                    continue;
                }

                PlayerShopSettlementItemData targetItem = GetOrCreate(target, i);
                targetItem.itemId = sourceItem.itemId;
                targetItem.soldQuantity = sourceItem.soldQuantity;
            }

            Trim(target, count);
        }

        private static void SetNull<T>(List<T> target, int index) where T : class
        {
            if (index < target.Count)
                target[index] = null;
            else
                target.Add(null);
        }
    }
}
