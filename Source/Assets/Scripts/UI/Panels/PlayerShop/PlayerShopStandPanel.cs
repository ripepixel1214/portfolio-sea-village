using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    /// <summary>판매대 패널, 고정 슬롯을 상태에 맞춰 갱신하고 같은 칸을 다시 고르면 재고 관리 창으로 넘김</summary>
    public class PlayerShopStandPanel : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _confirmButton;

        [Header("Stand Slots")]
        [Tooltip("판매대 칸. 앞에서부터 채우며, 잠긴 칸은 가게 레벨로 결정된다.")]
        [SerializeField] private PlayerShopStandSlotUI[] _slots;

        [Header("Texts")]
        [SerializeField] private TMP_Text _headerText;

        private TownKey _townKey = TownKey.Unknown;
        private int _selectedIndex = -1;
        private NavigationArea _currentArea = NavigationArea.PlayerShopStandGrid;
        private Action[] _slotHandlers;

        private PlayerShopStateReadOnly PlayerShopState =>
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

            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(Close);

            _slotHandlers = new Action[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                    continue;

                int index = i;
                Action handler = () => OnSlotClicked(index);
                _slotHandlers[i] = handler;
                _slots[i].OnSlotClicked += handler;
            }
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(Close);

            if (_slotHandlers != null)
            {
                for (int i = 0; i < _slots.Length; i++)
                    if (_slots[i] != null && _slotHandlers[i] != null)
                        _slots[i].OnSlotClicked -= _slotHandlers[i];

                _slotHandlers = null;
            }
        }
        #endregion

        public void Initialize(TownKey townKey)
        {
            _townKey = townKey;
            _selectedIndex = -1;

            Refresh();
        }

        #region Navigation
        public override void NavigateToLeftButton() => MoveSelection(-1);

        public override void NavigateToRightButton() => MoveSelection(1);

        public override void NavigateToUpperButton()
        {
            _currentArea = NavigationArea.PlayerShopStandGrid;
            SelectCurrentButton();
        }

        public override void NavigateToLowerButton()
        {
            _currentArea = NavigationArea.PlayerShopConfirmButton;
            SelectCurrentButton();
        }

        public override void ClickSelectedButton()
        {
            Button button = GetCurrentButton();
            if (button != null && button.interactable)
                button.onClick.Invoke();
        }
        #endregion

        private void HandleShopStateChanged(TownKey townKey)
        {
            if (townKey == _townKey)
                Refresh();
        }

        private void Refresh()
        {
            if (_headerText != null)
                _headerText.text = "판매대";

            if (_slots == null || !PlayerShopManager.HasInstance)
                return;

            PlayerShopStateReadOnly state = PlayerShopState;
            int unlockedCapacity = state?.SlotCapacity ?? 0;

            for (int i = 0; i < _slots.Length; i++)
                RefreshSlot(i, state, unlockedCapacity);

            SelectCurrentButton();
        }

        private void RefreshSlot(int index, PlayerShopStateReadOnly state, int unlockedCapacity)
        {
            PlayerShopStandSlotUI slot = _slots[index];
            if (slot == null)
                return;

            if (index >= unlockedCapacity)
            {
                slot.InitializeLocked();
                SetSlotInteractable(slot, false);
                return;
            }

            PlayerShopItemReadOnly listed = state?.GetSlotItem(index);
            if (listed == null)
            {
                slot.InitializeEmpty();
            }
            else
            {
                ItemData itemData = DataManager.Instance.GetItem(listed.ItemId);
                int marketPrice = PlayerShopManager.Instance.GetListedMarketPrice(_townKey, listed.ItemId);
                slot.Initialize(itemData, listed.Quantity, listed.UnitPrice, marketPrice);
            }

            SetSlotInteractable(slot, true);
        }

        private static void SetSlotInteractable(PlayerShopStandSlotUI slot, bool isInteractable)
        {
            Button button = slot.GetButton();
            if (button != null)
                button.interactable = isInteractable;

            CanvasGroup canvasGroup = slot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = slot.gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = isInteractable ? 1f : 0.5f;
        }

        /// <summary>이미 선택된 칸을 다시 고르면 재고 관리 창으로 이동</summary>
        private void OnSlotClicked(int index)
        {
            if (_selectedIndex != index)
            {
                _selectedIndex = index;
                _currentArea = NavigationArea.PlayerShopStandGrid;
                SelectCurrentButton();
                return;
            }

            OpenStockPanel(index);
        }

        private void OpenStockPanel(int index)
        {
            PlayerShopStockPanel stockPanel = UIManager.Instance.OpenPanel<PlayerShopStockPanel>();
            if (stockPanel != null)
                stockPanel.Initialize(_townKey, index);
        }

        #region Navigation Helpers
        private void MoveSelection(int step)
        {
            int count = _slots?.Length ?? 0;
            if (_currentArea != NavigationArea.PlayerShopStandGrid || count == 0)
                return;

            int next = _selectedIndex < 0 ? 0 : _selectedIndex;
            for (int i = 0; i < count; i++)
            {
                next = (next + step + count) % count;
                if (!IsSlotSelectable(next))
                    continue;

                _selectedIndex = next;
                break;
            }

            SelectCurrentButton();
        }

        private bool IsSlotSelectable(int index)
        {
            if (_slots == null || index < 0 || index >= _slots.Length || _slots[index] == null)
                return false;

            Button button = _slots[index].GetButton();
            return button != null && button.interactable;
        }

        private void SelectCurrentButton()
        {
            Button button = GetCurrentButton();
            if (button != null && button.interactable)
                button.Select();
        }

        private Button GetCurrentButton()
        {
            if (_currentArea == NavigationArea.PlayerShopStandGrid && IsSlotSelectable(_selectedIndex))
                return _slots[_selectedIndex].GetButton();

            return _confirmButton;
        }
        #endregion
    }
}
