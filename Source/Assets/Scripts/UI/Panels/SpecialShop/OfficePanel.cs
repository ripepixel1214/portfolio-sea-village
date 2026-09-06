using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    /// <summary>
    /// 가게 및 직원 관리 패널
    /// </summary>
    public class OfficePanel : SpecialShopMenuPanelBase
    {
        [Header("Buttons")]
        [SerializeField] private Button _playerShopButton;
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

            if (_playerShopButton != null)
                _playerShopButton.onClick.AddListener(OpenPlayerShopPurchase);
            if (_employmentButton != null)
                _employmentButton.onClick.AddListener(OpenEmploymentPanel);

            RefreshPlayerShopActionState();
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_playerShopButton != null)
                _playerShopButton.onClick.RemoveListener(OpenPlayerShopPurchase);
            if (_employmentButton != null)
                _employmentButton.onClick.RemoveListener(OpenEmploymentPanel);
        }

        private void OpenPlayerShopPurchase()
        {
            OpenPlayerShopAction(RefreshPlayerShopActionState);
        }

        private void RefreshPlayerShopActionState()
        {
            RefreshPlayerShopButtons(_playerShopButton, _employmentButton);
        }
    }
}
