using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;

namespace SeaVillage.UI
{
    public class AcornWorkshopPanel : SpecialShopMenuPanelBase
    {
        [SerializeField] private Button _purchaseButton;
        [SerializeField] private Button _sellButton;
        [SerializeField] private Button _officeButton;
        [SerializeField] private Button _employmentButton;

        public override void Initialize(string displayName, int shopId, Sprite shopImage = null)
        {
            base.Initialize(displayName, shopId, shopImage);
            RefreshPlayerShopActionState();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshPlayerShopActionState();
        }

        protected override void AddListeners()
        {
            base.AddListeners();

            if (_purchaseButton != null)
                _purchaseButton.onClick.AddListener(OpenPurchasePanel);
            if (_sellButton != null)
                _sellButton.onClick.AddListener(OpenSellPanel);
            if (_officeButton != null)
                _officeButton.onClick.AddListener(OpenPlayerShopPurchase);
            if (_employmentButton != null)
                _employmentButton.onClick.AddListener(OpenEmploymentPanel);

            RefreshPlayerShopActionState();
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_purchaseButton != null)
                _purchaseButton.onClick.RemoveListener(OpenPurchasePanel);
            if (_sellButton != null)
                _sellButton.onClick.RemoveListener(OpenSellPanel);
            if (_officeButton != null)
                _officeButton.onClick.RemoveListener(OpenPlayerShopPurchase);
            if (_employmentButton != null)
                _employmentButton.onClick.RemoveListener(OpenEmploymentPanel);
        }

        private void OpenPlayerShopPurchase()
        {
            OpenPlayerShopAction(RefreshPlayerShopActionState);
        }

        private void RefreshPlayerShopActionState()
        {
            RefreshFeatureButton(_purchaseButton, SpecialShopFeature.GeneralTrading);
            RefreshFeatureButton(_sellButton, SpecialShopFeature.GeneralTrading);
            RefreshPlayerShopButtons(_officeButton, _employmentButton);
        }
    }
}
