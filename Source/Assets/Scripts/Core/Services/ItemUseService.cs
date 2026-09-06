using UnityEngine;
using SeaVillage.Data;

namespace SeaVillage.Core
{
    /// <summary>
    /// 아이템 사용 효과 적용 서비스. 식량 충전, 스탯 증가 등 아이템 사용 시 발생하는 효과를 처리하는 유틸리티 클래스
    /// </summary>
    public static class ItemUseService
    {
        private delegate bool TryEffectOperation(out string failReason);

        private static readonly string[] SupportedUseTypes = { "Food", "Cal", "Charm", "Str", "Dex" };

        public static bool TryUseOnPlayer(int itemID, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!TryGetItemDataAndUsage(itemID, out ItemData itemData, out string usage))
            {
                resultMessage = "이 아이템은 사용할 수 없다";
                return false;
            }

            if (!TryGetItemEffectValue(itemData, out int effectValue))
            {
                resultMessage = "이 아이템은 사용할 수 없다";
                return false;
            }

            PlayerStatType statType;
            if (usage == "Str")
                statType = PlayerStatType.Strength;
            else if (usage == "Dex")
                statType = PlayerStatType.Agility;
            else
            {
                resultMessage = "이 아이템은 플레이어에게 사용할 수 없다";
                return false;
            }

            if (!PlayerStatManager.HasInstance || !PlayerStatManager.Instance.IsInitialized)
            {
                resultMessage = "[Error] 플레이어 스탯 정보가 준비되지 않았습니다";
                return false;
            }

            PlayerStatManager manager = PlayerStatManager.Instance;
            if (!TryConsumeOneAndApply(
                    itemID,
                    (out string failReason) => manager.CanApplyStatItem(statType, effectValue, itemID, out failReason),
                    (out string failReason) => manager.TryApplyStatItem(statType, effectValue, itemID, out failReason),
                    out resultMessage))
                return false;

            resultMessage = statType == PlayerStatType.Strength
                ? "조금 강해진 것 같다."
                : "조금 빨라진 것 같다.";
            return true;
        }

        public static bool TryUseOnStaff(int itemID, int staffId, out string resultMessage)
        {
            resultMessage = string.Empty;
            if (!TryGetItemDataAndUsage(itemID, out ItemData itemData, out string usage))
            {
                resultMessage = "이 아이템은 사용할 수 없다";
                return false;
            }

            if (usage != "Cal" && usage != "Charm")
            {
                resultMessage = "이 아이템은 직원에게 사용할 수 없다";
                return false;
            }

            if (!TryGetItemEffectValue(itemData, out int effectValue))
            {
                resultMessage = "이 아이템은 사용할 수 없다";
                return false;
            }

            if (!PlayerShopManager.HasInstance)
            {
                resultMessage = "[Error] 직원 정보가 준비되지 않았습니다";
                return false;
            }

            StaffStatType statType = usage == "Cal"
                ? StaffStatType.Intelligence
                : StaffStatType.Charm;
            PlayerShopManager manager = PlayerShopManager.Instance;
            if (!TryConsumeOneAndApply(
                    itemID,
                    (out string failReason) => manager.CanApplyStaffStatItem(staffId, statType, effectValue, itemID, out failReason),
                    (out string failReason) => manager.TryApplyStaffStatItem(staffId, statType, effectValue, itemID, out failReason),
                    out resultMessage))
                return false;

            resultMessage = statType == StaffStatType.Intelligence
                ? "조금 똑똑해보인다."
                : "조금 달라진 것 같다.";
            return true;
        }

        public static bool ConsumeFoodForShip(int itemID, int quantity = 1, bool consumeFromShipInventory = false)
        {
            if (!TryGetItemDataAndUsage(itemID, out ItemData itemData, out string usage))
                return false;

            if (usage != "Food")
            {
                Debug.LogWarning($"[ItemUseService] Item {itemID} is not Food.");
                return false;
            }

            if (!TryGetItemEffectValue(itemData, out int effectValue))
                return false;

            InventoryManager inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null)
            {
                Debug.LogWarning("[ItemUseService] InventoryManager not found.");
                return false;
            }

            InventoryData sourceInventory = consumeFromShipInventory
                ? inventoryManager.ShipInventory
                : inventoryManager.PlayerInventory;

            if (sourceInventory == null)
            {
                Debug.LogWarning("[ItemUseService] Food source inventory not available.");
                return false;
            }

            int totalFoodValue = effectValue * quantity;
            if (!inventoryManager.CanAddShipFood(totalFoodValue))
            {
                Debug.LogWarning($"[ItemUseService] Ship food storage is full. current={inventoryManager.ShipFoodStorage}, max={inventoryManager.ShipFoodCapacity}");
                return false;
            }

            int rollbackPurchasePrice = sourceInventory.Items.TryGetValue(itemID, out InventoryItem sourceItem)
                ? sourceItem.averagePurchasePrice
                : 0;

            if (!sourceInventory.RemoveItem(itemID, quantity))
            {
                Debug.LogWarning($"[ItemUseService] Failed to remove food item {itemID} from source inventory.");
                return false;
            }

            if (!inventoryManager.TryAddShipFood(totalFoodValue))
            {
                sourceInventory.AddItem(itemID, quantity, rollbackPurchasePrice);
                Debug.LogWarning("[ItemUseService] Failed to add ship food. Consumed item was rolled back.");
                return false;
            }

            return true;
        }

        private static bool TryGetItemDataAndUsage(int itemID, out ItemData itemData, out string usage)
        {
            itemData = DataManager.Instance?.ItemDatabase.GetItem(itemID);
            usage = itemData != null ? itemData.Usage : null;

            if (itemData == null)
            {
                Debug.LogWarning($"[ItemUseService] Item {itemID} not found in database.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(usage) || usage == "NULL")
            {
                Debug.LogWarning($"[ItemUseService] Item {itemID} has no usable usage.");
                return false;
            }

            for (int i = 0; i < SupportedUseTypes.Length; i++)
            {
                if (SupportedUseTypes[i] == usage)
                    return true;
            }

            Debug.LogWarning($"[ItemUseService] Unsupported usage '{usage}' for item {itemID}.");
            return false;
        }

        private static bool TryGetItemEffectValue(ItemData itemData, out int effectValue)
        {
            effectValue = Mathf.Max(0, itemData != null ? itemData.Value : 0);
            if (effectValue <= 0)
            {
                Debug.LogWarning($"[ItemUseService] Item {itemData?.ID} has invalid effect value.");
                return false;
            }

            return true;
        }

        private static bool TryRemoveOneFromPlayerInventory(
            int itemID,
            out InventoryData inventory,
            out int rollbackPurchasePrice,
            out string failReason)
        {
            inventory = InventoryManager.PlayerInventoryOrNull;
            rollbackPurchasePrice = 0;
            failReason = string.Empty;

            if (inventory == null || !inventory.Items.TryGetValue(itemID, out InventoryItem item) || item.quantity <= 0)
            {
                failReason = "사용할 아이템이 없다";
                return false;
            }

            rollbackPurchasePrice = item.averagePurchasePrice;
            if (!inventory.RemoveItem(itemID, 1))
            {
                failReason = "[Error] 아이템 소모 처리에 실패했습니다";
                return false;
            }

            return true;
        }

        private static bool TryConsumeOneAndApply(
            int itemId,
            TryEffectOperation validateEffect,
            TryEffectOperation applyEffect,
            out string resultMessage)
        {
            if (validateEffect == null || applyEffect == null)
            {
                resultMessage = "[Error] 아이템 효과 처리기가 유효하지 않습니다";
                return false;
            }

            if (!validateEffect(out resultMessage))
                return false;

            if (!TryRemoveOneFromPlayerInventory(
                    itemId,
                    out InventoryData inventory,
                    out int rollbackPurchasePrice,
                    out resultMessage))
            {
                return false;
            }

            if (applyEffect(out resultMessage))
                return true;

            RollBackItem(inventory, itemId, rollbackPurchasePrice);
            return false;
        }

        private static void RollBackItem(InventoryData inventory, int itemID, int rollbackPurchasePrice)
        {
            if (inventory == null || inventory.AddItem(itemID, 1, rollbackPurchasePrice))
                return;

            Debug.LogError($"[ItemUseService] Failed to roll back stat item {itemID} after effect failure.");
        }
    }
}
