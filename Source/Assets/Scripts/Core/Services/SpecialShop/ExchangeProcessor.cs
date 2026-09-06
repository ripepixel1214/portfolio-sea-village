using System.Collections.Generic;

namespace SeaVillage.Core
{
    /// <summary>
    /// 교환 비용 소모와 보상 지급을 하나의 원자적 작업으로 처리합니다.
    /// </summary>
    public static class ExchangeProcessor
    {
        public static bool TryExchange(ExchangeOffer offer, out string failReason)
        {
            failReason = string.Empty;

            InventoryData inventory = InventoryManager.PlayerInventoryOrNull;
            var costs = new List<ItemCost>
            {
                new ItemCost(offer.RequiredItemId, offer.RequiredCount),
            };

            if (!InventoryTransaction.TryConsumeItems(inventory, costs, out failReason))
                return false;

            if (InventoryTransaction.TryGrantItem(inventory, offer.RewardItemId, offer.RewardCount, out failReason))
                return true;

            InventoryTransaction.RollbackConsumedItems(inventory, costs);
            return false;
        }
    }
}
