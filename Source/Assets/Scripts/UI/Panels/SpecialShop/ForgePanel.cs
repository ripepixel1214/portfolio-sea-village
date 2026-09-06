using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;

namespace SeaVillage.UI
{
    public class ForgePanel : SpecialShopMenuPanelBase
    {
        [SerializeField] private Button _craftButton;
        [SerializeField] private Button _shipUpgradeButton;

        public override void Initialize(string displayName, int shopId, Sprite shopImage = null)
        {
            base.Initialize(displayName, shopId, shopImage);
            RefreshShipUpgradeButton();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshShipUpgradeButton();
        }

        protected override void AddListeners()
        {
            base.AddListeners();

            if (_craftButton != null)
                _craftButton.onClick.AddListener(OpenCraft);
            if (_shipUpgradeButton != null)
                _shipUpgradeButton.onClick.AddListener(OpenShipUpgrade);
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_craftButton != null)
                _craftButton.onClick.RemoveListener(OpenCraft);
            if (_shipUpgradeButton != null)
                _shipUpgradeButton.onClick.RemoveListener(OpenShipUpgrade);
        }

        private void OpenCraft()
        {
            if (!EnsureFeatureAccess(SpecialShopFeature.SpecialContent))
                return;

            CraftPanel panel = UIManager.Instance?.OpenPanel<CraftPanel>();
            panel?.Initialize();
        }

        private void OpenShipUpgrade()
        {
            if (!EnsureFeatureAccess(SpecialShopFeature.SpecialContent))
                return;

            ShipUpgradePanel panel = UIManager.Instance?.OpenPanel<ShipUpgradePanel>();
            panel?.Initialize();
        }

        private void RefreshShipUpgradeButton()
        {
            bool canUpgrade = CanUseFeature(SpecialShopFeature.SpecialContent)
                              && InventoryManager.HasInstance &&
                              InventoryManager.Instance.ShipLevel < MineForgeCatalog.ShipUpgradeTargetLevel;

            RefreshFeatureButton(_craftButton, SpecialShopFeature.SpecialContent);
            SetButtonEnabled(_shipUpgradeButton, canUpgrade);
            EnsureValidSelection();
        }
    }
}
