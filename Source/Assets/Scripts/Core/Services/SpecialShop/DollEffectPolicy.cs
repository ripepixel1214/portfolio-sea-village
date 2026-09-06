using SeaVillage.Data;
using UnityEngine;

namespace SeaVillage.Core
{
    public static class DollEffectPolicy
    {
        public const int PlayerSpeedDollItemId = 116015;
        public const int ShipSpeedDollItemId = 116016;

        public static float PlayerSpeedMultiplier =>
            OwnsDoll(PlayerSpeedDollItemId) ? 1.5f : 1f;

        public static float ShipSpeedMultiplier =>
            OwnsDoll(ShipSpeedDollItemId) ? 1.5f : 1f;

        public static Sprite PlayerInventorySpriteOrNull
        {
            get
            {
                if (!OwnsDoll(PlayerSpeedDollItemId) || !DataManager.HasInstance)
                    return null;

                SpecialShopContentCatalog catalog = DataManager.Instance.SpecialShopContentCatalog;
                if (catalog?.Dolls == null)
                    return null;

                for (int i = 0; i < catalog.Dolls.Count; i++)
                {
                    DollRewardDefinition definition = catalog.Dolls[i];
                    if (definition != null && definition.DollItemId == PlayerSpeedDollItemId)
                        return definition.PlayerInventorySprite;
                }

                return null;
            }
        }

        public static bool OwnsDoll(int dollItemId)
        {
            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;
            if (playerInventory != null && playerInventory.HasItem(dollItemId, 1))
                return true;

            InventoryData shipInventory = InventoryManager.ShipInventoryOrNull;
            return shipInventory != null && shipInventory.HasItem(dollItemId, 1);
        }
    }
}
