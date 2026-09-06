using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;
using SeaVillage.Data;
using TMPro;

namespace SeaVillage.UI
{
    /// <summary>가게 세우기와 가게 업그레이드가 공유하는 아이템 제출 모드</summary>
    public enum PlayerShopBuildMode
    {
        Build,
        Upgrade,
    }

    /// <summary>내 가게 건설·업그레이드 패널, 인벤토리에서 가게 아이템을 제출해 확인하면 진행</summary>
    public class PlayerShopBuildPanel : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _confirmButton;

        [Header("Inventory Grid")]
        [SerializeField] private Transform _itemGridContainer;
        [SerializeField] private GameObject _slotPrefab;

        [Header("Texts")]
        [SerializeField] private TMP_Text _headerText;

        [Header("Icons")]
        [SerializeField] private Image _requiredItemIcon;
        [SerializeField] private Image _submittedItemIcon;

        private const int GridColumns = 8;
        private const int DefaultGridRows = 4;

        private PlayerShopBuildMode _mode = PlayerShopBuildMode.Build;
        private TownKey _townKey = TownKey.Unknown;
        private int _requiredItemId;
        private bool _isSubmitted;

        private int _gridRows = DefaultGridRows;
        private Button[,] _gridButtons;
        private readonly List<InventorySlotUI> _activeSlots = new List<InventorySlotUI>();
        private readonly Dictionary<InventorySlotUI, Action> _slotClickHandlers = new Dictionary<InventorySlotUI, Action>();
        private NavigationArea _currentArea = NavigationArea.PlayerShopBuildInventory;
        private Vector2Int _currentGridPos = Vector2Int.zero;
        private Vector2Int _firstSelectableGridPos = new Vector2Int(-1, -1);
        private Sprite _submittedIconDefaultSprite;
        private bool _submittedIconDefaultEnabled;
        private bool _hasSubmittedIconDefault;

        private InventoryData PlayerInventory => InventoryManager.PlayerInventoryOrNull;
        private int MaxSlotsInGrid => _gridRows * GridColumns;

        public override void OnOpen()
        {
            base.OnOpen();

            CacheSubmittedIconDefault();

            if (InventoryManager.HasInstance)
                InventoryManager.Instance.OnPlayerInventoryChanged += RefreshInventory;

            RefreshPanel();
        }

        public override void OnClose()
        {
            MenuBarPanel menuBarPanel = UIManager.Instance.GetPanel<MenuBarPanel>();
            if (menuBarPanel != null && menuBarPanel.gameObject.activeInHierarchy)
                UIManager.Instance.ClosePanel<MenuBarPanel>();

            if (InventoryManager.HasInstance)
                InventoryManager.Instance.OnPlayerInventoryChanged -= RefreshInventory;

            ClearSlots();
            _isSubmitted = false;
            base.OnClose();
        }

        #region Event Listeners
        protected override void AddListeners()
        {
            base.AddListeners();

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Close);

            if (_confirmButton != null)
                _confirmButton.onClick.AddListener(ExecuteConfirm);
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(Close);

            if (_confirmButton != null)
                _confirmButton.onClick.RemoveListener(ExecuteConfirm);
        }
        #endregion

        /// <summary>가게 세우기 모드로 초기화</summary>
        public void InitializeBuild(TownKey townKey)
            => Initialize(PlayerShopBuildMode.Build, townKey);

        /// <summary>가게 업그레이드 모드로 초기화</summary>
        public void InitializeUpgrade(TownKey townKey)
            => Initialize(PlayerShopBuildMode.Upgrade, townKey);

        #region Navigation
        public override void NavigateToUpperButton()
        {
            switch (_currentArea)
            {
                case NavigationArea.PlayerShopBuildInventory:
                    Vector2Int upperPos = new Vector2Int(_currentGridPos.x - 1, _currentGridPos.y);
                    if (IsSelectableGridPosition(upperPos))
                        _currentGridPos = upperPos;
                    else
                        _currentArea = NavigationArea.CancelButton;
                    break;
                case NavigationArea.PlayerShopConfirmButton:
                    _currentArea = NavigationArea.CancelButton;
                    break;
                case NavigationArea.CancelButton:
                    MoveToGridOrConfirm();
                    break;
            }

            SelectCurrentButton();
        }

        public override void NavigateToLowerButton()
        {
            switch (_currentArea)
            {
                case NavigationArea.CancelButton:
                    MoveToGridOrConfirm();
                    break;
                case NavigationArea.PlayerShopBuildInventory:
                    Vector2Int lowerPos = new Vector2Int(_currentGridPos.x + 1, _currentGridPos.y);
                    if (IsSelectableGridPosition(lowerPos))
                        _currentGridPos = lowerPos;
                    else if (_isSubmitted)
                        _currentArea = NavigationArea.PlayerShopConfirmButton;
                    else
                        _currentArea = NavigationArea.CancelButton;
                    break;
                case NavigationArea.PlayerShopConfirmButton:
                    _currentArea = NavigationArea.CancelButton;
                    break;
            }

            SelectCurrentButton();
        }

        public override void NavigateToLeftButton() => NavigateHorizontally(-1);

        public override void NavigateToRightButton() => NavigateHorizontally(1);

        public override void ClickSelectedButton()
        {
            Button button = GetCurrentButton();
            if (button != null && button.interactable)
                button.onClick.Invoke();
        }

        private void NavigateHorizontally(int step)
        {
            if (_currentArea == NavigationArea.PlayerShopBuildInventory)
            {
                Vector2Int nextPos = new Vector2Int(_currentGridPos.x, _currentGridPos.y + step);
                if (IsSelectableGridPosition(nextPos))
                    _currentGridPos = nextPos;
            }
            else if (_isSubmitted)
            {
                _currentArea = _currentArea == NavigationArea.CancelButton
                    ? NavigationArea.PlayerShopConfirmButton
                    : NavigationArea.CancelButton;
            }

            SelectCurrentButton();
        }

        private void MoveToGridOrConfirm()
        {
            if (IsSelectableGridPosition(_firstSelectableGridPos))
            {
                _currentArea = NavigationArea.PlayerShopBuildInventory;
                _currentGridPos = _firstSelectableGridPos;
                return;
            }

            if (_isSubmitted)
                _currentArea = NavigationArea.PlayerShopConfirmButton;
        }

        private void SelectCurrentButton()
        {
            Button button = GetCurrentButton();
            if (button != null && button.interactable)
                button.Select();
        }

        private Button GetCurrentButton()
        {
            switch (_currentArea)
            {
                case NavigationArea.PlayerShopBuildInventory:
                    if (IsSelectableGridPosition(_currentGridPos))
                        return _gridButtons[_currentGridPos.x, _currentGridPos.y];
                    break;
                case NavigationArea.PlayerShopConfirmButton:
                    return _confirmButton;
            }

            return _cancelButton;
        }

        private bool IsSelectableGridPosition(Vector2Int pos)
        {
            if (pos.x < 0 || pos.y < 0)
                return false;

            if (_gridButtons == null || pos.x >= _gridButtons.GetLength(0) || pos.y >= _gridButtons.GetLength(1))
                return false;

            Button button = _gridButtons[pos.x, pos.y];
            return button != null && button.interactable;
        }
        #endregion

        private void Initialize(PlayerShopBuildMode mode, TownKey townKey)
        {
            _mode = mode;
            _townKey = townKey;
            _requiredItemId = ResolveRequiredItemId(townKey, mode);
            _isSubmitted = false;

            if (isActiveAndEnabled)
                RefreshPanel();
        }

        /// <summary>필요 아이템은 카탈로그에서 조회(건설/업그레이드 모드별)</summary>
        private static int ResolveRequiredItemId(TownKey townKey, PlayerShopBuildMode mode)
        {
            if (townKey != TownKey.Unknown && DataManager.HasInstance && DataManager.Instance.PlayerShopUpgradeCatalog != null
                && DataManager.Instance.PlayerShopUpgradeCatalog.TryGetByTown(townKey, out PlayerShopUpgradeDefinition definition))
                return mode == PlayerShopBuildMode.Upgrade ? definition.UpgradeItemId : definition.BuildItemId;

            return 0;
        }

        private void RefreshPanel()
        {
            RefreshRequiredInfo();
            RefreshInventory();
        }

        private void RefreshRequiredInfo()
        {
            if (_headerText != null)
                _headerText.text = _mode == PlayerShopBuildMode.Upgrade ? "가게 업그레이드" : "가게 세우기";

            ItemData itemData = _requiredItemId > 0 ? DataManager.Instance.GetItem(_requiredItemId) : null;

            SetImageSprite(_requiredItemIcon, ResolveItemIcon(itemData));
            RefreshSubmittedIcon(itemData);
        }

        private void RefreshSubmittedIcon(ItemData requiredItemData)
        {
            if (_isSubmitted)
            {
                SetImageSprite(_submittedItemIcon, ResolveItemIcon(requiredItemData));
                return;
            }

            RestoreSubmittedIcon();
        }

        private void RefreshInventory()
        {
            ClearSlots();
            SetButtonEnabled(_confirmButton, _isSubmitted);

            if (_itemGridContainer == null || _slotPrefab == null || PlayerInventory == null)
            {
                _gridRows = DefaultGridRows;
                _gridButtons = new Button[_gridRows, GridColumns];
                _currentArea = NavigationArea.CancelButton;
                SelectCurrentButton();
                return;
            }

            _gridRows = Mathf.Max(Mathf.CeilToInt((float)PlayerInventory.ItemCount / GridColumns), DefaultGridRows);
            _gridButtons = new Button[_gridRows, GridColumns];
            _firstSelectableGridPos = new Vector2Int(-1, -1);

            int slotIndex = 0;
            foreach (KeyValuePair<int, InventoryItem> kvp in PlayerInventory.Items)
            {
                InventoryItem inventoryItem = kvp.Value;
                ItemData itemData = DataManager.Instance.GetItem(inventoryItem.id);
                if (itemData == null)
                    continue;

                int displayQuantity = GetDisplayQuantity(itemData.ID, inventoryItem.quantity);
                if (displayQuantity <= 0)
                {
                    CreateEmptySlot(slotIndex++);
                    continue;
                }

                bool isSelectable = !_isSubmitted && itemData.ID == _requiredItemId;
                CreateInventorySlot(slotIndex++, itemData, displayQuantity, isSelectable);
            }

            for (int i = slotIndex; i < MaxSlotsInGrid; i++)
                CreateEmptySlot(i);

            if (IsSelectableGridPosition(_firstSelectableGridPos))
            {
                _currentArea = NavigationArea.PlayerShopBuildInventory;
                _currentGridPos = _firstSelectableGridPos;
            }
            else
            {
                _currentArea = _isSubmitted ? NavigationArea.PlayerShopConfirmButton : NavigationArea.CancelButton;
                _currentGridPos = Vector2Int.zero;
            }

            SelectCurrentButton();
        }

        private int GetDisplayQuantity(int itemId, int ownedQuantity)
        {
            if (_isSubmitted && itemId == _requiredItemId)
                return ownedQuantity - 1;

            return ownedQuantity;
        }

        private void CreateInventorySlot(int index, ItemData itemData, int quantity, bool isSelectable)
        {
            InventorySlotUI slot = InstantiateSlot(index);
            if (slot == null)
                return;

            slot.Initialize(itemData, quantity);
            SetSlotSelectable(slot, isSelectable);
            if (!isSelectable)
                return;

            int row = index / GridColumns;
            int col = index % GridColumns;
            if (_firstSelectableGridPos.x < 0)
                _firstSelectableGridPos = new Vector2Int(row, col);

            int itemId = itemData.ID;
            Action handler = () => OnInventorySlotClicked(slot, itemId);
            slot.OnSlotClicked += handler;
            _slotClickHandlers[slot] = handler;
        }

        private void CreateEmptySlot(int index)
        {
            InventorySlotUI slot = InstantiateSlot(index);
            if (slot == null)
                return;

            slot.InitializeEmpty();
            SetSlotSelectable(slot, false);
        }

        private InventorySlotUI InstantiateSlot(int index)
        {
            GameObject slotObject = Instantiate(_slotPrefab, _itemGridContainer);
            InventorySlotUI slot = slotObject.GetComponent<InventorySlotUI>();
            if (slot == null)
            {
                Destroy(slotObject);
                return null;
            }

            _activeSlots.Add(slot);

            int row = index / GridColumns;
            int col = index % GridColumns;
            if (row < _gridRows)
                _gridButtons[row, col] = slot.GetButton();

            return slot;
        }

        private void ClearSlots()
        {
            foreach (InventorySlotUI slot in _activeSlots)
            {
                if (slot == null)
                    continue;

                if (_slotClickHandlers.TryGetValue(slot, out Action handler))
                {
                    slot.OnSlotClicked -= handler;
                    _slotClickHandlers.Remove(slot);
                }

                Destroy(slot.gameObject);
            }

            _activeSlots.Clear();
            _slotClickHandlers.Clear();
        }

        private static void SetSlotSelectable(InventorySlotUI slot, bool isSelectable)
        {
            if (slot == null)
                return;

            Button button = slot.GetButton();
            if (button != null)
                button.interactable = isSelectable;

            CanvasGroup canvasGroup = slot.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = slot.gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = isSelectable ? 1f : 0.5f;
        }

        #region Submission Flow
        private void OnInventorySlotClicked(InventorySlotUI slot, int itemId)
        {
            if (_isSubmitted || itemId != _requiredItemId || slot == null)
                return;

            MenuBarPanel menuBarPanel = UIManager.Instance.OpenPanel<MenuBarPanel>();
            if (menuBarPanel != null)
                menuBarPanel.InitializeSubmissionMenu(itemId, slot.GetSlotScreenPosition(), _ => MarkSubmitted(), null);
        }

        private void MarkSubmitted()
        {
            if (_isSubmitted)
                return;

            _isSubmitted = true;
            RefreshPanel();
        }

        private void ExecuteConfirm()
        {
            if (!_isSubmitted)
                return;

            string failReason;
            bool success = _mode == PlayerShopBuildMode.Upgrade
                ? PlayerShopManager.Instance.TryUpgradeShop(_townKey, out failReason)
                : PlayerShopManager.Instance.TryBuildShop(_townKey, out failReason);

            if (!success)
            {
                Debug.LogWarning($"[{nameof(PlayerShopBuildPanel)}] {_mode} 실패: town={_townKey}, item={_requiredItemId}, reason={failReason}");
                _isSubmitted = false;
                RefreshPanel();
                UIManager.Instance.ShowAlertMessage(failReason);
                return;
            }

            _isSubmitted = false;

            string message = _mode == PlayerShopBuildMode.Upgrade ? "가게를 업그레이드했다" : "가게를 세웠다";

            // 완료 확인 시 열린 패널 모두 닫기
            UIManager.Instance.ShowAlertMessage(message, () => UIManager.Instance.CloseAllPanels(), "확인");
        }
        #endregion

        #region Icon Helpers
        private static void SetImageSprite(Image image, Sprite sprite)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        private void CacheSubmittedIconDefault()
        {
            if (_hasSubmittedIconDefault || _submittedItemIcon == null)
                return;

            _submittedIconDefaultSprite = _submittedItemIcon.sprite;
            _submittedIconDefaultEnabled = _submittedItemIcon.enabled;
            _hasSubmittedIconDefault = true;
        }

        private void RestoreSubmittedIcon()
        {
            if (_submittedItemIcon == null)
                return;

            CacheSubmittedIconDefault();
            _submittedItemIcon.sprite = _submittedIconDefaultSprite;
            _submittedItemIcon.enabled = _submittedIconDefaultEnabled;
        }

        private static Sprite ResolveItemIcon(ItemData itemData)
        {
            if (itemData == null)
                return null;

            if (itemData.Icon != null)
                return itemData.Icon;

            if (!string.IsNullOrEmpty(itemData.Image))
                return UIManager.HasInstance ? UIManager.Instance.LoadItemIcon(itemData.Image) : null;

            return null;
        }
        #endregion
    }
}
