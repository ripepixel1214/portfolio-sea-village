using SeaVillage.Data;

namespace SeaVillage.Core
{
    public static class DollUnlockPolicy
    {
        public static bool IsClaimed(int dollItemId)
        {
            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;
            InventoryData shipInventory = InventoryManager.ShipInventoryOrNull;
            bool hasItem = playerInventory?.HasItem(dollItemId, 1) == true
                || shipInventory?.HasItem(dollItemId, 1) == true;

            if (hasItem)
                return true;

            if (TryGetDollDefinition(dollItemId, out DollRewardDefinition dollDefinition)
                && dollDefinition.StaffId > 0
                && PlayerShopManager.HasInstance
                && PlayerShopManager.Instance.IsStaffHired(dollDefinition.StaffId))
                return true;

            return false;
        }

        public static int GetProgress(DollRewardDefinition definition)
        {
            if (definition == null)
                return 0;

            switch (definition.Condition)
            {
                case DollUnlockCondition.TotalPlayerShopRevenue:
                    return GetTotalRevenue();
                case DollUnlockCondition.TotalPlayerShopSales:
                    return GetTotalSoldCount();
                case DollUnlockCondition.OwnItem:
                    return InventoryManager.PlayerInventoryOrNull?.GetItemCount(definition.ConditionItemId) ?? 0;
                case DollUnlockCondition.TotalTownLove:
                    return GetTotalTownLove();
                case DollUnlockCondition.TotalDollCount:
                    return GetTotalDollCount();
                default:
                    return 0;
            }
        }

        public static bool IsUnlocked(DollRewardDefinition definition)
        {
            return definition != null && GetProgress(definition) >= definition.ConditionValue;
        }

        private static int GetTotalRevenue()
        {
            if (!PlayerShopManager.HasInstance || !DataManager.HasInstance)
                return 0;

            PlayerShopUpgradeCatalog catalog = DataManager.Instance.PlayerShopUpgradeCatalog;
            if (catalog == null)
                return 0;

            long total = 0;
            for (int i = 0; i < catalog.All.Count; i++)
            {
                PlayerShopUpgradeDefinition definition = catalog.All[i];
                PlayerShopStateReadOnly state = definition != null
                    ? PlayerShopManager.Instance.GetState(definition.TownKey)
                    : null;
                if (state != null)
                    total += state.TotalRevenue;
            }

            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        private static int GetTotalSoldCount()
        {
            if (!PlayerShopManager.HasInstance || !DataManager.HasInstance)
                return 0;

            PlayerShopUpgradeCatalog catalog = DataManager.Instance.PlayerShopUpgradeCatalog;
            if (catalog == null)
                return 0;

            long total = 0;
            for (int i = 0; i < catalog.All.Count; i++)
            {
                PlayerShopUpgradeDefinition definition = catalog.All[i];
                PlayerShopStateReadOnly state = definition != null
                    ? PlayerShopManager.Instance.GetState(definition.TownKey)
                    : null;
                if (state != null)
                    total += state.TotalSoldCount;
            }

            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        private static int GetTotalTownLove()
        {
            if (!DataManager.HasInstance || !TownProgressionManager.HasInstance)
                return 0;

            SpecialShopContentCatalog catalog = DataManager.Instance.SpecialShopContentCatalog;
            if (catalog == null || catalog.LoveTownKeys == null)
                return 0;

            int total = 0;
            for (int i = 0; i < catalog.LoveTownKeys.Count; i++)
            {
                TownKey townKey = catalog.LoveTownKeys[i];
                if (townKey != TownKey.Unknown)
                    total += TownProgressionManager.Instance.GetAffinity(townKey);
            }

            return total;
        }

        private static int GetTotalDollCount()
        {
            if (!DataManager.HasInstance)
                return 0;

            SpecialShopContentCatalog catalog = DataManager.Instance.SpecialShopContentCatalog;
            if (catalog == null || catalog.Dolls == null)
                return 0;

            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;
            InventoryData shipInventory = InventoryManager.ShipInventoryOrNull;
            int total = 0;

            for (int i = 0; i < catalog.Dolls.Count; i++)
            {
                DollRewardDefinition definition = catalog.Dolls[i];
                if (definition == null)
                    continue;

                total += playerInventory?.GetItemCount(definition.DollItemId) ?? 0;
                total += shipInventory?.GetItemCount(definition.DollItemId) ?? 0;
            }

            if (PlayerShopManager.HasInstance)
            {
                for (int i = 0; i < catalog.Dolls.Count; i++)
                {
                    DollRewardDefinition definition = catalog.Dolls[i];
                    if (definition == null || definition.StaffId <= 0)
                        continue;

                    if (PlayerShopManager.Instance.IsStaffHired(definition.StaffId))
                        total++;
                }
            }

            return total;
        }

        private static bool TryGetDollDefinition(int dollItemId, out DollRewardDefinition definition)
        {
            definition = null;
            if (dollItemId <= 0 || !DataManager.HasInstance)
                return false;

            SpecialShopContentCatalog catalog = DataManager.Instance.SpecialShopContentCatalog;
            if (catalog?.Dolls == null)
                return false;

            for (int i = 0; i < catalog.Dolls.Count; i++)
            {
                DollRewardDefinition candidate = catalog.Dolls[i];
                if (candidate != null && candidate.DollItemId == dollItemId)
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
