using System;
using System.Collections.Generic;

namespace SeaVillage.Core
{
    /// <summary>
    /// 선박 업그레이드 비용 소모와 등급 상승을 원자적으로 처리합니다.
    /// 식량 저장 상한과 섬 접근 권한은 InventoryManager의 선박 등급에서 파생됩니다.
    /// </summary>
    public static class ShipUpgradeProcessor
    {
        public static bool TryUpgrade(
            int woodItemId,
            int ingotItemId,
            int woodCount,
            int ingotCount,
            int goldCost,
            int targetShipLevel,
            out string failReason)
        {
            failReason = string.Empty;

            if (!InventoryManager.HasInstance)
            {
                failReason = "[Error] 인벤토리 매니저를 찾을 수 없습니다";
                return false;
            }

            InventoryManager inventoryManager = InventoryManager.Instance;
            InventoryData inventory = inventoryManager.PlayerInventory;
            if (inventory == null)
            {
                failReason = "[Error] 가방을 찾을 수 없습니다";
                return false;
            }

            if (targetShipLevel <= inventoryManager.ShipLevel)
            {
                failReason = "이미 그 등급 이상의 배다";
                return false;
            }

            long safeGoldCost = Math.Max(0, goldCost);
            if (safeGoldCost > 0 && !CurrencyManager.Instance.CanPlayerSpend(CurrencyType.Gold, safeGoldCost))
            {
                failReason = "골드가 부족하다";
                return false;
            }

            var costs = new List<ItemCost>
            {
                new ItemCost(woodItemId, woodCount),
                new ItemCost(ingotItemId, ingotCount),
            };

            if (!InventoryTransaction.TryConsumeItems(inventory, costs, out failReason))
                return false;

            if (safeGoldCost > 0 && !CurrencyManager.Instance.TrySpendPlayer(CurrencyType.Gold, safeGoldCost))
            {
                InventoryTransaction.RollbackConsumedItems(inventory, costs);
                failReason = "[Error] 골드 소모 처리에 실패했습니다";
                return false;
            }

            inventoryManager.UpgradeShip(targetShipLevel);
            return true;
        }
    }
}
