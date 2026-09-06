using System.Collections.Generic;
using SeaVillage.Data;
using UnityEngine;

namespace SeaVillage.Core
{
    public static class RecipeCraftProcessor
    {
        public readonly struct CraftResult
        {
            public readonly bool Success;
            public readonly string Message;
            public readonly int OutputItemId;
            public readonly int OutputCount;

            public CraftResult(bool success, string message, int outputItemId, int outputCount)
            {
                Success = success;
                Message = message;
                OutputItemId = outputItemId;
                OutputCount = outputCount;
            }
        }

        public static List<int> GetCraftableItemIdsForShop(int shopId)
        {
            var result = new List<int>();
            if (shopId <= 0)
                return result;

            DataManager dataManager = DataManager.Instance;
            ShopDatabase shopDatabase = dataManager?.ShopDatabase;
            ItemDatabase itemDatabase = dataManager?.ItemDatabase;
            if (shopDatabase == null || itemDatabase == null)
                return result;

            List<ShopData> shopItems = shopDatabase.GetShopsById(shopId);
            if (shopItems == null || shopItems.Count == 0)
                return result;

            var dedupe = new HashSet<int>();
            for (int i = 0; i < shopItems.Count; i++)
            {
                ShopData shopData = shopItems[i];
                if (shopData == null || shopData.ItemID <= 0 || !dedupe.Add(shopData.ItemID))
                    continue;

                ItemData itemData = itemDatabase.GetItem(shopData.ItemID);
                if (itemData == null || itemData.Recipe <= 0)
                    continue;

                List<RecipeData> recipe = GetValidRecipeForItem(itemData.ID);
                if (recipe == null || recipe.Count == 0)
                    continue;

                result.Add(itemData.ID);
            }

            return result;
        }

        public static CraftResult TryCraftItem(int outputItemId, int outputCount)
        {
            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;
            if (playerInventory == null)
                return Fail("플레이어 인벤토리를 찾을 수 없습니다.");

            if (outputItemId <= 0)
                return Fail("제작 가능한 아이템이 없습니다.");

            List<RecipeData> recipe = GetValidRecipeForItem(outputItemId);
            if (recipe == null || recipe.Count == 0)
                return Fail("레시피 데이터가 비어 있습니다.");

            if (!HasAllIngredients(playerInventory, recipe))
                return Fail("재료가 부족합니다.");

            var removedIngredients = new List<RecipeData>(recipe.Count);
            for (int i = 0; i < recipe.Count; i++)
            {
                RecipeData ingredient = recipe[i];
                if (!playerInventory.RemoveItem(ingredient.Stuff, ingredient.Count))
                {
                    RollbackIngredients(playerInventory, removedIngredients);
                    return Fail("재료 소모 처리에 실패했습니다.");
                }

                removedIngredients.Add(ingredient);
            }

            int safeOutputCount = Mathf.Max(1, outputCount);
            if (!playerInventory.AddItem(outputItemId, safeOutputCount))
            {
                RollbackIngredients(playerInventory, removedIngredients);
                return Fail("가방 무게 또는 공간이 부족해 제작에 실패했습니다.");
            }

            string itemName = GetItemDisplayName(outputItemId);
            string message = safeOutputCount > 1
                ? $"제작 완료! {itemName} x{safeOutputCount}"
                : $"제작 완료! {itemName}";

            return new CraftResult(true, message, outputItemId, safeOutputCount);
        }

        public static List<RecipeData> GetValidRecipeForItem(int outputItemId)
        {
            ItemData outputItemData = DataManager.Instance?.ItemDatabase?.GetItem(outputItemId);
            if (outputItemData == null || outputItemData.Recipe <= 0)
                return null;

            return GetValidRecipeByRecipeId(outputItemData.Recipe);
        }

        public static bool HasAllIngredients(InventoryData inventory, List<RecipeData> recipe)
        {
            if (inventory == null || recipe == null || recipe.Count == 0)
                return false;

            for (int i = 0; i < recipe.Count; i++)
            {
                RecipeData ingredient = recipe[i];
                if (ingredient == null || ingredient.Stuff <= 0 || ingredient.Count <= 0)
                    return false;

                if (!inventory.HasItem(ingredient.Stuff, ingredient.Count))
                    return false;
            }

            return true;
        }

        public static string GetItemDisplayName(int itemId)
        {
            ItemData itemData = DataManager.Instance?.ItemDatabase?.GetItem(itemId);
            if (itemData == null)
                return itemId.ToString();

            string itemName = DataManager.Instance?.GetScriptTextKR(itemData.Name);
            return string.IsNullOrWhiteSpace(itemName) ? itemData.ID.ToString() : itemName;
        }

        private static List<RecipeData> GetValidRecipeByRecipeId(int recipeId)
        {
            RecipeDatabase recipeDatabase = DataManager.Instance?.RecipeDatabase;
            if (recipeDatabase == null || recipeId <= 0)
                return null;

            List<RecipeData> rawRecipe = recipeDatabase.GetRecipe(recipeId);
            if (rawRecipe == null || rawRecipe.Count == 0)
                return null;

            var validated = new List<RecipeData>(rawRecipe.Count);
            for (int i = 0; i < rawRecipe.Count; i++)
            {
                RecipeData row = rawRecipe[i];
                if (row == null || row.Stuff <= 0 || row.Count <= 0)
                    return null;

                validated.Add(row);
            }

            return validated.Count == rawRecipe.Count ? validated : null;
        }

        private static void RollbackIngredients(InventoryData inventory, List<RecipeData> removedIngredients)
        {
            if (inventory == null || removedIngredients == null)
                return;

            for (int i = 0; i < removedIngredients.Count; i++)
            {
                RecipeData ingredient = removedIngredients[i];
                if (ingredient == null || ingredient.Stuff <= 0 || ingredient.Count <= 0)
                    continue;

                inventory.AddItem(ingredient.Stuff, ingredient.Count);
            }
        }

        private static CraftResult Fail(string message)
        {
            return new CraftResult(false, message, 0, 0);
        }
    }
}
