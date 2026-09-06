using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    /// <summary>
    /// 상점 패널
    /// </summary>
    public class ShopPanel : UIPanel
    {
        private int currentShopID;
        private ShopMenuMode currentMenuMode;

        [SerializeField] private TextMeshProUGUI _headerText;
        [SerializeField] private Image _shopImage;

        [Header("Buttons")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _purchaseButton;
        [SerializeField] private Button _sellButton;

        public void Initialize(int shopID, string displayName, ShopMenuMode menuMode = ShopMenuMode.Standard, Sprite shopImage = null)
        {
            currentShopID = shopID;
            currentMenuMode = menuMode;

            if (_headerText != null)
                _headerText.text = displayName;

            ApplyShopImage(shopImage);
            ApplyMenuMode();
        }

        // 이미지가 없으면 빈 사각형이 남지 않도록 숨긴다
        private void ApplyShopImage(Sprite shopImage)
        {
            if (_shopImage == null)
                return;

            _shopImage.sprite = shopImage;
            _shopImage.enabled = shopImage != null;
        }

        public int GetShopID()
        {
            return currentShopID;
        }

        public override void OnOpen()
        {
            base.OnOpen();
            TutorialUIBinding.Bind(_purchaseButton, TutorialAnchorKeys.ShopPurchaseButton);
            TutorialUIBinding.Bind(_closeButton, TutorialAnchorKeys.ShopCloseButton);
        }

        private void ApplyMenuMode()
        {
            bool showExtendedActions = currentMenuMode == ShopMenuMode.Standard;

            if (_sellButton != null)
                _sellButton.gameObject.SetActive(showExtendedActions);
        }

        #region Button Handlers
        protected override void AddListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(CloseShop);

            if (_purchaseButton != null)
                _purchaseButton.onClick.AddListener(OpenPurchasePanel);

            if (_sellButton != null)
                _sellButton.onClick.AddListener(OpenSellPanel);
        }

        protected override void RemoveListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(CloseShop);

            if (_purchaseButton != null)
                _purchaseButton.onClick.RemoveListener(OpenPurchasePanel);

            if (_sellButton != null)
                _sellButton.onClick.RemoveListener(OpenSellPanel);
        }

        private void OpenPurchasePanel()
        {
            PurchasePanel purchasePanel = UIManager.Instance?.OpenPanel<PurchasePanel>();
            if (purchasePanel != null)
            {
                purchasePanel.SetShopContext(currentShopID, ShopUtility.GetCurrentTownStorageKey());
                TutorialEventReporter.Report(TutorialEventType.PanelOpened, TutorialTargetIds.PurchasePanel, source: TutorialEventSource.UserInterface);
            }
        }

        private void CloseShop()
        {
            Close();
            TutorialEventReporter.Report(TutorialEventType.PanelClosed, TutorialTargetIds.ShopPanel, source: TutorialEventSource.UserInterface);
        }

        private void OpenSellPanel()
        {
            SellPanel sellPanel = UIManager.Instance.OpenPanel<SellPanel>();
            if (sellPanel != null)
                sellPanel.SetMarketTown(ShopUtility.GetCurrentTownStorageKey());
        }

        #endregion

    }
}
