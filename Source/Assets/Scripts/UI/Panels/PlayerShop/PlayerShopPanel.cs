using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;
using TMPro;

namespace SeaVillage.UI
{
    /// <summary>내 가게 메뉴 패널, 열릴 때 매출을 회수하고 판매대·직원 관리·가게 정보로 분기</summary>
    public class PlayerShopPanel : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _standButton;
        [SerializeField] private Button _staffButton;
        [SerializeField] private Button _infoButton;

        [Header("Texts")]
        [SerializeField] private TMP_Text _headerText;

        private TownKey _townKey = TownKey.Unknown;

        private PlayerShopStateReadOnly PlayerShopState =>
            PlayerShopManager.HasInstance ? PlayerShopManager.Instance.GetState(_townKey) : null;

        public override void OnOpen()
        {
            base.OnOpen();
            Refresh();
        }

        #region Event Listeners
        protected override void AddListeners()
        {
            base.AddListeners();

            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);

            if (_standButton != null)
                _standButton.onClick.AddListener(OpenStandPanel);

            if (_staffButton != null)
                _staffButton.onClick.AddListener(OpenStaffPanel);

            if (_infoButton != null)
                _infoButton.onClick.AddListener(OpenInfoPanel);

            Refresh();
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);

            if (_standButton != null)
                _standButton.onClick.RemoveListener(OpenStandPanel);

            if (_staffButton != null)
                _staffButton.onClick.RemoveListener(OpenStaffPanel);

            if (_infoButton != null)
                _infoButton.onClick.RemoveListener(OpenInfoPanel);
        }
        #endregion

        /// <summary>대상 오피스 지정 후 미회수 매출 정산</summary>
        public void Initialize(TownKey townKey)
        {
            _townKey = townKey;

            Refresh();
        }

        private void Refresh()
        {
            bool isBuilt = PlayerShopState is { IsBuilt: true };

            if (_headerText != null)
                _headerText.text = "내 가게";

            SetButtonEnabled(_standButton, isBuilt);
            SetButtonEnabled(_staffButton, isBuilt);
            SetButtonEnabled(_infoButton, isBuilt);
            EnsureValidSelection();
        }

        #region Menu Routing
        private void OpenStandPanel()
        {
            PlayerShopStandPanel standPanel = UIManager.Instance.OpenPanel<PlayerShopStandPanel>();
            if (standPanel != null)
                standPanel.Initialize(_townKey);
        }

        private void OpenStaffPanel()
        {
            StaffPanel staffPanel = UIManager.Instance.OpenPanel<StaffPanel>();
            if (staffPanel != null)
                staffPanel.Initialize(_townKey);
        }

        private void OpenInfoPanel()
        {
            PlayerShopInfoPanel infoPanel = UIManager.Instance.OpenPanel<PlayerShopInfoPanel>();
            if (infoPanel != null)
                infoPanel.Initialize(_townKey);
        }
        #endregion
    }
}
