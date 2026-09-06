using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;

namespace SeaVillage.UI
{
    public class PotionShopPanel : SpecialShopMenuPanelBase
    {
        [SerializeField] private Button _purchaseButton;
        [SerializeField] private Button _sellButton;
        [SerializeField] private Button _officeButton;
        [SerializeField] private Button _craftButton;

        public override void Initialize(string displayName, int shopId, Sprite shopImage = null)
        {
            base.Initialize(displayName, shopId, shopImage);
            RefreshActionState();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshActionState();
        }

        protected override void AddListeners()
        {
            base.AddListeners();

            if (_purchaseButton != null)
                _purchaseButton.onClick.AddListener(OpenPurchasePanel);
            if (_sellButton != null)
                _sellButton.onClick.AddListener(OpenSellPanel);
            if (_officeButton != null)
                _officeButton.onClick.AddListener(OpenOffice);
            if (_craftButton != null)
                _craftButton.onClick.AddListener(OpenPotionCraft);
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_purchaseButton != null)
                _purchaseButton.onClick.RemoveListener(OpenPurchasePanel);
            if (_sellButton != null)
                _sellButton.onClick.RemoveListener(OpenSellPanel);
            if (_officeButton != null)
                _officeButton.onClick.RemoveListener(OpenOffice);
            if (_craftButton != null)
                _craftButton.onClick.RemoveListener(OpenPotionCraft);
        }

        private void OpenOffice()
        {
            OpenOfficePanel();
        }

        private void OpenPotionCraft()
        {
            if (!EnsureFeatureAccess(SpecialShopFeature.SpecialContent))
                return;

            if (!ValidateShopId("포션 제조"))
                return;

            PotionCraftPanel panel = UIManager.Instance?.OpenPanel<PotionCraftPanel>();
            panel?.Initialize(ShopId);
        }

        private void RefreshActionState()
        {
            RefreshFeatureButton(_purchaseButton, SpecialShopFeature.GeneralTrading);
            RefreshFeatureButton(_sellButton, SpecialShopFeature.GeneralTrading);
            RefreshFeatureButton(_officeButton, SpecialShopFeature.SpecialContent);
            RefreshFeatureButton(_craftButton, SpecialShopFeature.SpecialContent);
            EnsureValidSelection();
        }
    }
}
