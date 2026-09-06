using SeaVillage.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    public class ShipUpgradePanel : UIPanel, IContextualPanel
    {
        [SerializeField] private TextMeshProUGUI _headerText;
        [SerializeField] private TextMeshProUGUI _effectText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private TMP_Text _goldRequirementText;
        [SerializeField] private Image _woodRequirementIcon;
        [SerializeField] private TMP_Text _woodRequirementText;
        [SerializeField] private Image _ingotRequirementIcon;
        [SerializeField] private TMP_Text _ingotRequirementText;

        public void Initialize()
        {
            RefreshState();
        }

        protected override void AddListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Close);
            if (_upgradeButton != null)
                _upgradeButton.onClick.AddListener(ConfirmUpgrade);
        }

        protected override void RemoveListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);
            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(Close);
            if (_upgradeButton != null)
                _upgradeButton.onClick.RemoveListener(ConfirmUpgrade);
        }

        private void RefreshState()
        {
            SpecialShopPanelUtility.SetText(_headerText, "배 업그레이드");
            InventoryData inventory = InventoryManager.PlayerInventoryOrNull;
            long gold = CurrencyManager.HasInstance
                ? CurrencyManager.Instance.GetPlayerBalance(CurrencyType.Gold)
                : 0;
            int wood = inventory?.GetItemCount(MineForgeCatalog.WoodItemId) ?? 0;
            int ingot = inventory?.GetItemCount(MineForgeCatalog.IngotItemId) ?? 0;
            bool alreadyUpgraded = InventoryManager.HasInstance
                && InventoryManager.Instance.ShipLevel >= MineForgeCatalog.ShipUpgradeTargetLevel;
            bool canUpgrade = !alreadyUpgraded
                && gold >= MineForgeCatalog.ShipUpgradeGoldCost
                && wood >= MineForgeCatalog.WoodCount
                && ingot >= MineForgeCatalog.IngotCount;

            RefreshRequirement(
                _goldRequirementText,
                gold,
                MineForgeCatalog.ShipUpgradeGoldCost,
                gold >= MineForgeCatalog.ShipUpgradeGoldCost);
            RefreshRequirement(
                _woodRequirementIcon,
                _woodRequirementText,
                MineForgeCatalog.WoodItemId,
                wood,
                MineForgeCatalog.WoodCount,
                wood >= MineForgeCatalog.WoodCount);
            RefreshRequirement(
                _ingotRequirementIcon,
                _ingotRequirementText,
                MineForgeCatalog.IngotItemId,
                ingot,
                MineForgeCatalog.IngotCount,
                ingot >= MineForgeCatalog.IngotCount);
            SpecialShopPanelUtility.SetText(
                _effectText,
                alreadyUpgraded ? "배 업그레이드 완료" : MineForgeCatalog.ShipUpgradeEffect);
            SetButtonEnabled(_upgradeButton, canUpgrade);
            EnsureValidSelection();
        }

        private void ConfirmUpgrade()
        {
            if (!SpecialShopAccessPolicy.CanUseCurrentTown(SpecialShopFeature.SpecialContent))
            {
                UIManager.Instance?.ShowAlertMessage($"호감도 {SpecialShopAccessPolicy.GetRequiredAffinity(SpecialShopFeature.SpecialContent)} 이상 필요");
                RefreshState();
                return;
            }

            UIManager.Instance.ShowConfirmMessage(
                "골드와 재료를 사용해 배를 업그레이드하시겠습니까?",
                ExecuteUpgrade,
                null,
                "배 업그레이드");
        }

        private void ExecuteUpgrade()
        {
            if (!SpecialShopAccessPolicy.CanUseCurrentTown(SpecialShopFeature.SpecialContent))
            {
                UIManager.Instance?.ShowAlertMessage($"호감도 {SpecialShopAccessPolicy.GetRequiredAffinity(SpecialShopFeature.SpecialContent)} 이상 필요");
                RefreshState();
                return;
            }

            bool success = ShipUpgradeProcessor.TryUpgrade(
                MineForgeCatalog.WoodItemId,
                MineForgeCatalog.IngotItemId,
                MineForgeCatalog.WoodCount,
                MineForgeCatalog.IngotCount,
                MineForgeCatalog.ShipUpgradeGoldCost,
                MineForgeCatalog.ShipUpgradeTargetLevel,
                out string failReason);

            UIManager manager = UIManager.Instance;
            void Handler()
            {
                manager.OnPanelClosed -= Handler;
                manager.ShowAlertMessage(
                    success ? "배를 업그레이드했다" : failReason,
                    RefreshState,
                    success ? "업그레이드 완료" : "알림");
            }

            manager.OnPanelClosed += Handler;
        }

        private static void RefreshRequirement(
            TMP_Text requirementText,
            long ownedCount,
            long requiredCount,
            bool sufficient)
        {
            if (requirementText == null)
                return;

            string ownedColor = sufficient ? "#419665" : "#DC4B4B";
            requirementText.text = $"<color={ownedColor}>{ownedCount}</color> / {requiredCount}";
        }

        private static void RefreshRequirement(
            Image iconImage,
            TMP_Text requirementText,
            int itemId,
            long ownedCount,
            long requiredCount,
            bool sufficient)
        {
            Sprite icon = null;
            if (itemId > 0)
            {
                var item = SpecialShopPanelUtility.GetItem(itemId);
                icon = item?.Icon;
                if (icon == null && UIManager.HasInstance)
                    icon = UIManager.Instance.LoadItemIcon(item?.Image);
            }

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            RefreshRequirement(requirementText, ownedCount, requiredCount, sufficient);
        }
    }
}
