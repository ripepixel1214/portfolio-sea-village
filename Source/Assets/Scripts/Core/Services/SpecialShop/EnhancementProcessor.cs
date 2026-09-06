using System;
using System.Collections.Generic;
using UnityEngine;
using SeaVillage.Data;

namespace SeaVillage.Core
{
    /// <summary>
    /// 강화 재료와 골드를 소비하고 성공 시에만 다음 등급 아이템을 지급합니다.
    /// 강화 실패 시 대상 아이템은 원래 등급으로 복구되며 재료와 골드는 소비됩니다.
    /// </summary>
    public static class EnhancementProcessor
    {
        public static bool TryEnhance(
            int baseItemId,
            int enhancedItemId,
            int crystalItemId,
            EnhancementRule rule,
            out bool success,
            out string failReason)
        {
            success = false;
            failReason = string.Empty;

            if (!TryValidateRequest(baseItemId, enhancedItemId, crystalItemId, rule, out failReason))
                return false;

            InventoryData inventory = InventoryManager.PlayerInventoryOrNull;
            if (inventory == null)
            {
                failReason = "[Error] 가방을 찾을 수 없습니다";
                return false;
            }

            if (!inventory.HasItem(baseItemId, 1))
            {
                failReason = "강화할 아이템이 없다";
                return false;
            }

            long safeGoldCost = Math.Max(0, rule.RequiredGold);
            CurrencyManager currencyManager = null;
            if (safeGoldCost > 0)
            {
                if (!CurrencyManager.HasInstance)
                {
                    failReason = "[Error] 재화 정보를 찾을 수 없습니다";
                    return false;
                }

                currencyManager = CurrencyManager.Instance;
                if (!currencyManager.CanPlayerSpend(CurrencyType.Gold, safeGoldCost))
                {
                    failReason = "골드가 부족하다";
                    return false;
                }
            }

            var costs = new List<ItemCost>
            {
                new ItemCost(baseItemId, 1),
                new ItemCost(crystalItemId, Mathf.Max(1, rule.RequiredCrystalCount)),
            };

            if (!InventoryTransaction.TryConsumeItems(inventory, costs, out failReason))
                return false;

            if (safeGoldCost > 0 && !currencyManager.TrySpendPlayer(CurrencyType.Gold, safeGoldCost))
            {
                InventoryTransaction.RollbackConsumedItems(inventory, costs);
                failReason = "[Error] 골드 소모 처리에 실패했습니다";
                return false;
            }

            int clampedRate = Mathf.Clamp(rule.SuccessRatePercent, 0, 100);
            success = UnityEngine.Random.Range(0, 100) < clampedRate;
            int resultItemId = success ? enhancedItemId : baseItemId;

            if (InventoryTransaction.TryGrantItem(inventory, resultItemId, 1, out failReason))
                return true;

            InventoryTransaction.RollbackConsumedItems(inventory, costs);
            if (safeGoldCost > 0)
                currencyManager.TryAddPlayer(CurrencyType.Gold, safeGoldCost);

            success = false;
            return false;
        }

        private static bool TryValidateRequest(
            int baseItemId,
            int enhancedItemId,
            int crystalItemId,
            EnhancementRule rule,
            out string failReason)
        {
            failReason = string.Empty;

            if (baseItemId <= 0
                || enhancedItemId <= 0
                || crystalItemId <= 0
                || baseItemId == enhancedItemId
                || baseItemId == crystalItemId
                || enhancedItemId == crystalItemId)
            {
                failReason = "[Error] 강화 아이템 정보가 유효하지 않습니다";
                return false;
            }

            if (rule.RequiredCrystalCount <= 0
                || rule.RequiredGold < 0
                || rule.SuccessRatePercent < 0
                || rule.SuccessRatePercent > 100)
            {
                failReason = "[Error] 강화 규칙이 유효하지 않습니다";
                return false;
            }

            if (!DataManager.HasInstance || DataManager.Instance.ItemDatabase == null)
            {
                failReason = "[Error] 아이템 정보를 찾을 수 없습니다";
                return false;
            }

            ItemDatabase itemDatabase = DataManager.Instance.ItemDatabase;
            if (itemDatabase.GetItem(baseItemId) == null
                || itemDatabase.GetItem(enhancedItemId) == null
                || itemDatabase.GetItem(crystalItemId) == null)
            {
                failReason = "[Error] 강화 아이템 정보가 데이터베이스에 없습니다";
                return false;
            }

            return true;
        }
    }
}
