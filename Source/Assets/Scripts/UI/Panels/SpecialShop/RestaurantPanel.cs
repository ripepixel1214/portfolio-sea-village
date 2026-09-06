using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;

namespace SeaVillage.UI
{
    /// <summary>
    /// 레스토랑 패널
    /// </summary>
    public class RestaurantPanel : SpecialShopMenuPanelBase
    {
        [Header("Buttons")]
        [SerializeField] private Button _purchaseButton;
        [SerializeField] private Button _cookButton;

        [Header("Cooking")]
        [SerializeField] private int _cookOutputCount = 1;

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

            if (_cookButton != null)
                _cookButton.onClick.AddListener(OnCookButtonClicked);
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_purchaseButton != null)
                _purchaseButton.onClick.RemoveListener(OpenPurchasePanel);

            if (_cookButton != null)
                _cookButton.onClick.RemoveListener(OnCookButtonClicked);
        }

        private void OnCookButtonClicked()
        {
            OpenCraftPanel(DisplayName, _cookOutputCount);
        }

        private void RefreshActionState()
        {
            RefreshFeatureButton(_purchaseButton, SpecialShopFeature.GeneralTrading);
            RefreshFeatureButton(_cookButton, SpecialShopFeature.SpecialContent);
            EnsureValidSelection();
        }
    }
}
