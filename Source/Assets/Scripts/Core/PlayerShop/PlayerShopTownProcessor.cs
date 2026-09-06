using UnityEngine;
using SeaVillage.Data;

namespace SeaVillage.Core
{
    public enum PlayerShopActionMode
    {
        Purchase,
        Upgrade,
        Disabled,
    }

    /// <summary>플레이어 가게 이용 조건과 건설 아이템 구매 규칙 처리</summary>
    public static class PlayerShopTownProcessor
    {
        public static PlayerShopActionMode GetActionMode(TownKey townKey)
        {
            if (townKey == TownKey.Unknown || !HasRequiredAffinity(townKey))
                return PlayerShopActionMode.Disabled;

            PlayerShopStateReadOnly state = PlayerShopManager.HasInstance
                ? PlayerShopManager.Instance.GetState(townKey)
                : null;

            if (state == null || !state.IsBuilt)
            {
                int buildItemId = ResolveTownItem(townKey, false);
                if (buildItemId <= 0)
                    return PlayerShopActionMode.Disabled;

                return OwnsItem(buildItemId) ? PlayerShopActionMode.Disabled : PlayerShopActionMode.Purchase;
            }

            if (state.BuildStage < PlayerShopBuildStage.Upgraded)
            {
                int upgradeItemId = ResolveTownItem(townKey, true);
                if (upgradeItemId <= 0)
                    return PlayerShopActionMode.Disabled;

                return OwnsItem(upgradeItemId) ? PlayerShopActionMode.Disabled : PlayerShopActionMode.Upgrade;
            }

            return PlayerShopActionMode.Disabled;
        }

        /// <summary>해당 마을의 플레이어 가게 직원 고용 가능 여부 반환</summary>
        public static bool CanHireStaff(TownKey townKey)
        {
            if (townKey == TownKey.Unknown || !HasRequiredAffinity(townKey))
                return false;

            PlayerShopStateReadOnly state = PlayerShopManager.HasInstance
                ? PlayerShopManager.Instance.GetState(townKey)
                : null;

            return state != null && state.IsBuilt;
        }

        /// <summary>해당 마을의 플레이어 가게 건설 여부 반환</summary>
        public static bool IsShopBuilt(TownKey townKey)
        {
            if (townKey == TownKey.Unknown || !PlayerShopManager.HasInstance)
                return false;

            PlayerShopStateReadOnly state = PlayerShopManager.Instance.GetState(townKey);
            return state != null && state.IsBuilt;
        }

        public static bool TryExecute(
            PlayerShopActionMode mode,
            TownKey townKey,
            out string failReason)
        {
            failReason = string.Empty;

            if (townKey == TownKey.Unknown)
            {
                failReason = "[Error] 마을 정보를 확인할 수 없다";
                return false;
            }

            if (!HasRequiredAffinity(townKey))
            {
                failReason = $"호감도 {TownAffinityRules.PlayerShopRequiredAffinity} 이상 필요";
                return false;
            }

            PlayerShopActionMode currentMode = GetActionMode(townKey);
            if (currentMode != mode)
            {
                failReason = "가게 상태가 변경되었다";
                return false;
            }

            if (mode == PlayerShopActionMode.Purchase)
                return TryPurchaseShopItem(ResolveTownItem(townKey, false), out failReason);

            if (mode == PlayerShopActionMode.Upgrade)
                return TryPurchaseShopItem(ResolveTownItem(townKey, true), out failReason);

            failReason = "현재는 진행할 수 없다";
            return false;
        }

        private static bool OwnsItem(int itemId)
        {
            if (itemId <= 0)
                return false;

            InventoryData inventory = InventoryManager.PlayerInventoryOrNull;
            return inventory != null && inventory.HasItem(itemId, 1);
        }

        private static bool HasRequiredAffinity(TownKey townKey)
        {
            return townKey != TownKey.Unknown
                && TownProgressionManager.HasInstance
                && TownProgressionManager.Instance.GetAffinity(townKey)
                    >= TownAffinityRules.PlayerShopRequiredAffinity;
        }

        private static int ResolveTownItem(TownKey townKey, bool isUpgrade)
        {
            if (townKey != TownKey.Unknown && DataManager.HasInstance && DataManager.Instance.PlayerShopUpgradeCatalog != null
                && DataManager.Instance.PlayerShopUpgradeCatalog.TryGetByTown(townKey, out PlayerShopUpgradeDefinition definition))
                return isUpgrade ? definition.UpgradeItemId : definition.BuildItemId;

            return 0;
        }

        private static bool TryPurchaseShopItem(int shopItemId, out string failReason)
        {
            failReason = string.Empty;

            if (shopItemId <= 0)
            {
                failReason = "[Error] 플레이어 가게 아이템 ID가 설정되지 않았습니다";
                return false;
            }

            CurrencyManager currencyManager = CurrencyManager.Instance;
            InventoryData inventory = InventoryManager.PlayerInventoryOrNull;
            if (currencyManager == null || inventory == null)
            {
                failReason = "[Error] 구매에 필요한 관리자를 찾을 수 없습니다";
                return false;
            }

            long purchaseCost = GetItemOriginPrice(shopItemId);
            if (!currencyManager.TrySpendPlayer(CurrencyType.Gold, purchaseCost))
            {
                failReason = "골드가 부족하다";
                return false;
            }

            if (inventory.AddItem(shopItemId, 1))
                return true;

            if (purchaseCost > 0)
                currencyManager.TryAddPlayer(CurrencyType.Gold, purchaseCost);

            failReason = "가방이 가득 차서 받을 수 없다";
            return false;
        }

        private static int GetItemOriginPrice(int itemId)
        {
            ItemData itemData = itemId > 0 ? DataManager.Instance?.GetItem(itemId) : null;
            return itemData != null ? Mathf.Max(0, itemData.OriginPrice) : 0;
        }
    }
}
