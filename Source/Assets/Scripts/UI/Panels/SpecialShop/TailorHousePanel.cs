using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;

namespace SeaVillage.UI
{
    public class TailorHousePanel : SpecialShopMenuPanelBase
    {
        [SerializeField] private Button _purchaseButton;
        [SerializeField] private Button _sellButton;
        [SerializeField] private Button _dollRewardButton;

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
            if (_dollRewardButton != null)
                _dollRewardButton.onClick.AddListener(OpenDollReward);
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_purchaseButton != null)
                _purchaseButton.onClick.RemoveListener(OpenPurchasePanel);
            if (_sellButton != null)
                _sellButton.onClick.RemoveListener(OpenSellPanel);
            if (_dollRewardButton != null)
                _dollRewardButton.onClick.RemoveListener(OpenDollReward);
        }

        private void OpenDollReward()
        {
            if (!EnsureFeatureAccess(SpecialShopFeature.SpecialContent))
                return;

            DollPanel panel = UIManager.Instance?.OpenPanel<DollPanel>();
            panel?.Initialize();
        }

        private void RefreshActionState()
        {
            RefreshFeatureButton(_purchaseButton, SpecialShopFeature.GeneralTrading);
            RefreshFeatureButton(_sellButton, SpecialShopFeature.GeneralTrading);
            RefreshFeatureButton(_dollRewardButton, SpecialShopFeature.SpecialContent);
            EnsureValidSelection();
        }
    }
}
