using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;
using SeaVillage.Data;
using TMPro;

namespace SeaVillage.UI
{
    /// <summary>가게 정보 패널, 레벨·누적 매출·직원 능력치를 표시하고 업그레이드 제출 창으로 진입</summary>
    public class PlayerShopInfoPanel : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _upgradeButton;

        [Header("Texts")]
        [SerializeField] private TMP_Text _shopLevelText;
        [SerializeField] private TMP_Text _totalRevenueText;

        [Header("Cashier Slot")]
        [SerializeField] private GameObject _cashierIcon;
        [SerializeField] private Image _cashierImage;
        [SerializeField] private TMP_Text _cashierStatText;

        [Header("Sales Slot")]
        [SerializeField] private GameObject _salesWarningText;
        [SerializeField] private GameObject _salesSlot;
        [SerializeField] private GameObject _salesIcon;
        [SerializeField] private TMP_Text _salesStatText;

        private TownKey _townKey = TownKey.Unknown;

        private PlayerShopStateReadOnly PlayerShopState =>
            PlayerShopManager.HasInstance ? PlayerShopManager.Instance.GetState(_townKey) : null;

        public override void OnOpen()
        {
            base.OnOpen();
            Refresh();
        }

        public override void OnFocusRestored()
        {
            base.OnFocusRestored();
            Refresh();
        }

        #region Event Listeners
        protected override void AddListeners()
        {
            base.AddListeners();

            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);

            if (_upgradeButton != null)
                _upgradeButton.onClick.AddListener(OpenUpgradePanel);

            Refresh();
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);

            if (_upgradeButton != null)
                _upgradeButton.onClick.RemoveListener(OpenUpgradePanel);
        }
        #endregion

        public void Initialize(TownKey townKey)
        {
            _townKey = townKey;

            Refresh();
        }

        private void Refresh()
        {
            PlayerShopStateReadOnly state = PlayerShopState;
            int shopLevel = state?.ShopLevel ?? 0;

            if (_shopLevelText != null)
                _shopLevelText.text = $"{shopLevel} Lv";

            if (_totalRevenueText != null)
                _totalRevenueText.text = $"{state?.TotalRevenue ?? 0:N0} G";

            RefreshStaffSlot(state?.Cashier, _cashierIcon, _cashierImage, _cashierStatText);

            // 판매 직원은 Lv.1 경고 텍스트 / Lv.2 슬롯 토글
            bool isUpgraded = shopLevel >= 2;

            if (_salesWarningText != null)
                _salesWarningText.SetActive(!isUpgraded);

            if (_salesSlot != null)
                _salesSlot.SetActive(isUpgraded);

            if (isUpgraded)
                RefreshStaffSlot(state?.Sales, _salesIcon, null, _salesStatText);

            SetButtonEnabled(_upgradeButton, state is { IsBuilt: true } && shopLevel < 2);
            EnsureValidSelection();
        }

        // 미고용이면 아이콘 비활성, 스탯은 기본 텍스트로 표시
        private static void RefreshStaffSlot(StaffInfo? assignment, GameObject icon, Image staffImage, TMP_Text statText)
        {
            bool isAssigned = assignment is { IsAssigned: true };
            Sprite staffSprite = isAssigned ? GetStaffSprite(assignment.Value.StaffId) : null;

            if (icon != null)
                icon.SetActive(isAssigned);

            if (staffImage != null)
            {
                staffImage.sprite = staffSprite;
                staffImage.enabled = staffSprite != null;
            }

            if (statText != null)
                statText.text = FormatStaffStat(assignment);
        }

        private static Sprite GetStaffSprite(int staffId)
        {
            return StaffDisplayUtility.GetSprite(staffId);
        }

        private static string FormatStaffStat(StaffInfo? assignment)
        {
            if (assignment is not { IsAssigned: true })
                return "지능 : -\n매력 : -";

            return $"지능 : {assignment.Value.Intelligence}\n매력 : {assignment.Value.Charm}";
        }

        private void OpenUpgradePanel()
        {
            PlayerShopBuildPanel buildPanel = UIManager.Instance.OpenPanel<PlayerShopBuildPanel>();
            if (buildPanel != null)
                buildPanel.InitializeUpgrade(_townKey);
        }
    }
}
