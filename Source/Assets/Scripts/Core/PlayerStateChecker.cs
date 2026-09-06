using System;
using SeaVillage.Data;

namespace SeaVillage.Core
{
    public static class PlayerStateChecker
    {
        public const int FirstWreckRecoveryFoodAmount = 20;

        public static bool IsFirstWreckRecoveryRequired()
        {
            if (!CurrencyManager.HasInstance
                || !InventoryManager.HasInstance
                || !DataManager.HasInstance
                || DataManager.Instance.ItemDatabase == null)
            {
                return false;
            }

            InventoryManager inventoryManager = InventoryManager.Instance;
            InventoryData playerInventory = inventoryManager.PlayerInventory;
            if (playerInventory == null)
                return false;

            long playerGold = CurrencyManager.Instance.GetPlayerBalance(CurrencyType.Gold);
            float shipFood = inventoryManager.ShipFoodStorage;
            if (playerGold > 0L || shipFood > 0f)
                return false;

            int convertibleFood = CalculatePlayerFoodConversionAmount(
                playerInventory,
                DataManager.Instance.ItemDatabase,
                FirstWreckRecoveryFoodAmount);
            return IsFirstWreckRecoveryRequired(playerGold, shipFood, convertibleFood);
        }

        public static bool IsFirstWreckRecoveryRequired(
            long playerGold,
            float shipFood,
            int playerFoodConversionAmount)
        {
            return playerGold <= 0L
                   && shipFood <= 0f
                   && playerFoodConversionAmount < FirstWreckRecoveryFoodAmount;
        }

        private static int CalculatePlayerFoodConversionAmount(
            InventoryData inventory,
            ItemDatabase itemDatabase,
            int stopAtAmount)
        {
            int total = 0;
            var items = itemDatabase.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ItemData itemData = items[i];
                if (itemData == null
                    || !string.Equals(itemData.Usage, "Food", StringComparison.Ordinal)
                    || itemData.Value <= 0
                    || !inventory.Items.TryGetValue(itemData.ID, out InventoryItem inventoryItem)
                    || inventoryItem.quantity <= 0)
                {
                    continue;
                }

                long itemFood = (long)itemData.Value * inventoryItem.quantity;
                total = itemFood >= stopAtAmount - total
                    ? stopAtAmount
                    : total + (int)itemFood;
                if (total >= stopAtAmount)
                    return total;
            }

            return total;
        }
    }
}
