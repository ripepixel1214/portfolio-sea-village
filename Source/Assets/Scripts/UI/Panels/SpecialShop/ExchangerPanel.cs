using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;

namespace SeaVillage.UI
{
    public class ExchangerPanel : SpecialShopMenuPanelBase
    {
        [SerializeField] private Button _purchaseButton;
        [SerializeField] private Button _sellButton;
        [SerializeField] private Button _exchangeButton;

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
            if (_exchangeButton != null)
                _exchangeButton.onClick.AddListener(OpenExchange);
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_purchaseButton != null)
                _purchaseButton.onClick.RemoveListener(OpenPurchasePanel);
            if (_sellButton != null)
                _sellButton.onClick.RemoveListener(OpenSellPanel);
            if (_exchangeButton != null)
                _exchangeButton.onClick.RemoveListener(OpenExchange);
        }

        private void OpenExchange()
        {
            if (!EnsureFeatureAccess(SpecialShopFeature.SpecialContent))
                return;

            ExchangePanel panel = UIManager.Instance?.OpenPanel<ExchangePanel>();
            panel?.Initialize();
        }

        private void RefreshActionState()
        {
            RefreshFeatureButton(_purchaseButton, SpecialShopFeature.GeneralTrading);
            RefreshFeatureButton(_sellButton, SpecialShopFeature.GeneralTrading);
            RefreshFeatureButton(_exchangeButton, SpecialShopFeature.SpecialContent);
            EnsureValidSelection();
        }
    }
}
