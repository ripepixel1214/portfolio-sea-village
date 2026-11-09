using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    /// <summary>직원 관리 패널, 계산/호객 역할 슬롯의 능력치를 보여주고 [변경]으로 직원 변경 창을 연다</summary>
    public class StaffPanel : UIPanel
    {
        private const string CashierLabel = "계산";
        private const string SalesLabel = "호객";

        [Header("Buttons")]
        [SerializeField] private Button _closeButton;

        [Header("Role Slots")]
        [SerializeField] private StaffSlotUI _cashierSlot;
        [SerializeField] private StaffSlotUI _salesSlot;

        private TownKey _townKey = TownKey.Unknown;

        private PlayerShopStateReadOnly ShopState =>
            PlayerShopManager.HasInstance ? PlayerShopManager.Instance.GetState(_townKey) : null;

        public override void OnOpen()
        {
            base.OnOpen();

            if (PlayerShopManager.HasInstance)
                PlayerShopManager.Instance.OnShopStateChanged += HandleShopStateChanged;

            Refresh();
        }

        public override void OnClose()
        {
            if (PlayerShopManager.HasInstance)
                PlayerShopManager.Instance.OnShopStateChanged -= HandleShopStateChanged;

            base.OnClose();
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

            if (_cashierSlot != null)
                _cashierSlot.OnChangeRequested += OpenCashierChange;

            if (_salesSlot != null)
                _salesSlot.OnChangeRequested += OpenSalesChange;
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);

            if (_cashierSlot != null)
                _cashierSlot.OnChangeRequested -= OpenCashierChange;

            if (_salesSlot != null)
                _salesSlot.OnChangeRequested -= OpenSalesChange;
        }
        #endregion

        public void Initialize(TownKey townKey)
        {
            _townKey = townKey;
            Refresh();
        }

        private void HandleShopStateChanged(TownKey townKey)
        {
            if (townKey == _townKey)
                Refresh();
        }

        private void Refresh()
        {
            int shopLevel = ShopState?.ShopLevel ?? 0;

            RefreshSlot(_cashierSlot, StaffRole.Cashier, CashierLabel, true);
            RefreshSlot(_salesSlot, StaffRole.Sales, SalesLabel, shopLevel >= 2);

            EnsureValidSelection();
        }

        private void RefreshSlot(StaffSlotUI slot, StaffRole role, string label, bool unlocked)
        {
            if (slot == null)
                return;

            if (!unlocked)
            {
                slot.ShowEmpty(label);
                return;
            }

            StaffInfo? assignment = PlayerShopManager.Instance.GetAssignment(_townKey, role);
            bool hasStaff = assignment is { IsAssigned: true };
            Sprite staffSprite = hasStaff ? StaffDisplayUtility.GetSprite(assignment.Value) : null;
            slot.ShowActive(label, hasStaff, staffSprite, assignment?.Intelligence ?? 0, assignment?.Charm ?? 0);
        }

        private void OpenCashierChange() => OpenChange(StaffRole.Cashier);

        private void OpenSalesChange() => OpenChange(StaffRole.Sales);

        private void OpenChange(StaffRole role)
        {
            StaffChangePanel changePanel = UIManager.Instance.OpenPanel<StaffChangePanel>();
            if (changePanel != null)
                changePanel.Initialize(_townKey, role);
        }
    }

    internal static class StaffDisplayUtility
    {
        public static Sprite GetSprite(StaffInfo staff)
        {
            if (!staff.IsAssigned)
                return null;

            if (staff.StaffId == 0)
                return GetDefaultCashierSprite();

            return GetSprite(staff.StaffId);
        }

        public static Sprite GetSprite(int staffId)
        {
            if (staffId <= 0 || !DataManager.HasInstance)
                return null;

            StaffCatalog catalog = DataManager.Instance.StaffCatalog;
            if (catalog == null || !catalog.TryGetByStaffId(staffId, out StaffDefinition definition))
                return null;

            if (definition.Sprite != null)
                return definition.Sprite;

            if (!definition.IsItemStaff)
                return null;

            ItemData item = DataManager.Instance.GetItem(definition.RequiredItemId);
            Sprite icon = item?.Icon;
            if (icon == null && item != null && UIManager.HasInstance)
                icon = UIManager.Instance.LoadItemIcon(item.Image);

            return icon;
        }

        private static Sprite GetDefaultCashierSprite()
        {
            if (!DataManager.HasInstance || DataManager.Instance.StaffCatalog == null)
            {
                Debug.LogWarning("기본 계산원 프로필을 가져올 StaffCatalog가 준비되지 않았습니다");
                return null;
            }

            PlayerGender playerGender = SaveLoadManager.HasInstance
                && SaveLoadManager.Instance.CurrentGameData != null
                    ? PlayerGenderPolicy.Normalize(SaveLoadManager.Instance.CurrentGameData.playerGender)
                    : PlayerGender.Male;

            StaffCatalog catalog = DataManager.Instance.StaffCatalog;
            Sprite sprite = playerGender == PlayerGender.Male
                ? catalog.FemalePlayerIcon
                : catalog.MalePlayerIcon;

            if (sprite == null)
                Debug.LogWarning("StaffCatalog에 기본 계산원 프로필 Sprite가 연결되지 않았습니다");

            return sprite;
        }
    }
}
