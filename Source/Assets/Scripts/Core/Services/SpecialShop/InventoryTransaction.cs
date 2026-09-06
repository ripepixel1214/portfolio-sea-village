using System.Collections.Generic;

namespace SeaVillage.Core
{
    internal readonly struct ItemCost
    {
        public readonly int ItemId;
        public readonly int Count;

        public ItemCost(int itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    internal static class InventoryTransaction
    {
        public static bool HasItems(InventoryData inventory, IReadOnlyList<ItemCost> costs)
        {
            if (inventory == null || costs == null)
                return false;

            for (int i = 0; i < costs.Count; i++)
            {
                ItemCost cost = costs[i];
                if (cost.ItemId <= 0 || cost.Count <= 0)
                    return false;

                if (!inventory.HasItem(cost.ItemId, cost.Count))
                    return false;
            }

            return true;
        }

        public static bool TryConsumeItems(InventoryData inventory, IReadOnlyList<ItemCost> costs, out string failReason)
        {
            failReason = string.Empty;

            if (inventory == null)
            {
                failReason = "[Error] 인벤토리를 찾을 수 없습니다";
                return false;
            }

            if (!HasItems(inventory, costs))
            {
                failReason = "필요한 아이템이 부족하다";
                return false;
            }

            var consumed = new List<ItemCost>();
            for (int i = 0; i < costs.Count; i++)
            {
                ItemCost cost = costs[i];
                if (!inventory.RemoveItem(cost.ItemId, cost.Count))
                {
                    RollbackConsumedItems(inventory, consumed);
                    failReason = "[Error] 아이템 소모 처리에 실패했습니다";
                    return false;
                }

                consumed.Add(cost);
            }

            return true;
        }

        public static bool TryGrantItem(InventoryData inventory, int itemId, int count, out string failReason)
        {
            failReason = string.Empty;

            if (inventory == null)
            {
                failReason = "[Error] 인벤토리를 찾을 수 없습니다";
                return false;
            }

            if (itemId <= 0 || count <= 0)
            {
                failReason = "[Error] 지급할 아이템 정보가 유효하지 않습니다";
                return false;
            }

            if (!inventory.AddItem(itemId, count))
            {
                failReason = "가방이 가득 찼다";
                return false;
            }

            return true;
        }

        public static void RollbackConsumedItems(InventoryData inventory, IReadOnlyList<ItemCost> consumed)
        {
            if (inventory == null || consumed == null)
                return;

            for (int i = 0; i < consumed.Count; i++)
            {
                ItemCost cost = consumed[i];
                if (cost.ItemId > 0 && cost.Count > 0)
                    inventory.AddItem(cost.ItemId, cost.Count);
            }
        }
    }
}
