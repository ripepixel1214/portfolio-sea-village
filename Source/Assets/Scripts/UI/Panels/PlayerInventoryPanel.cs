using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    public sealed class PlayerInventoryPanel : UIPanel
    {
        private const int GridColumns = 8;

        [Serializable]
        private sealed class OriginFilterBinding
        {
            [SerializeField] private TownKey townKey = TownKey.Unknown;
            [SerializeField] private Button button;

            public TownKey TownKey => townKey;
            public Button Button => button;
        }

        [Header("Navigation")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button showAllButton;
        [SerializeField] private OriginFilterBinding[] originFilters = Array.Empty<OriginFilterBinding>();

        [Header("Inventory")]
        [SerializeField] private ScrollRect itemScrollRect;
        [SerializeField] private Transform itemGridContainer;
        [SerializeField] private GameObject slotPrefab;

        [Header("Filter Visuals")]
        [SerializeField] private Color showAllColor = new Color32(188, 170, 142, 255);
        [SerializeField] private Color originVisibleColor = new Color32(235, 224, 204, 255);
        [SerializeField] private Color originHiddenColor = new Color32(158, 137, 111, 255);

        private readonly List<PlayerInventoryItemSlotUI> _activeSlots = new();
        private readonly Dictionary<PlayerInventoryItemSlotUI, Action> _slotHandlers = new();
        private readonly Dictionary<Button, UnityAction> _filterHandlers = new();
        private readonly List<Button> _filterNavigationButtons = new();
        private readonly HashSet<TownKey> _hiddenOrigins = new();
        private Button[,] _gridButtons;
        private NavigationArea _currentArea = NavigationArea.PlayerInventory;
        private Vector2Int _currentGridPosition = Vector2Int.zero;
        private int _currentFilterIndex;
        private int _gridRows = 1;
        private int _visibleItemCount;

        private InventoryData PlayerInventory => InventoryManager.PlayerInventoryOrNull;

        #region UIPanel
        public override void OnOpen()
        {
            _hiddenOrigins.Clear();
            RebuildFilterNavigationButtons();
            UpdateFilterVisuals();
            base.OnOpen();

            if (InventoryManager.HasInstance)
                InventoryManager.Instance.OnPlayerInventoryChanged += RefreshInventory;

            RefreshInventory();
        }

        public override void OnClose()
        {
            if (InventoryManager.HasInstance)
                InventoryManager.Instance.OnPlayerInventoryChanged -= RefreshInventory;

            ClearSlots();
            base.OnClose();
        }

        protected override void AddListeners()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            if (showAllButton != null)
                showAllButton.onClick.AddListener(HandleShowAllClicked);

            _filterHandlers.Clear();
            OriginFilterBinding[] filters = originFilters ?? Array.Empty<OriginFilterBinding>();
            for (int i = 0; i < filters.Length; i++)
            {
                OriginFilterBinding binding = filters[i];
                if (binding == null || binding.Button == null || binding.TownKey == TownKey.Unknown)
                    continue;

                TownKey townKey = binding.TownKey;
                UnityAction handler = () => HandleOriginFilterClicked(townKey);
                binding.Button.onClick.AddListener(handler);
                _filterHandlers[binding.Button] = handler;
            }
        }

        protected override void RemoveListeners()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
            if (showAllButton != null)
                showAllButton.onClick.RemoveListener(HandleShowAllClicked);

            foreach (KeyValuePair<Button, UnityAction> pair in _filterHandlers)
                if (pair.Key != null)
                    pair.Key.onClick.RemoveListener(pair.Value);

            _filterHandlers.Clear();
        }
        #endregion

        #region Inventory
        private void RefreshInventory()
        {
            ClearSlots();

            if (slotPrefab == null || itemGridContainer == null)
            {
                Debug.LogError("[PlayerInventoryPanel] 슬롯 프리팹 또는 그리드 참조가 없습니다");
                return;
            }

            if (PlayerInventory == null || !DataManager.HasInstance)
            {
                _visibleItemCount = 0;
                _gridRows = 1;
                _gridButtons = new Button[_gridRows, GridColumns];
                ResetScrollPosition();
                MoveNavigationToAvailableTarget();
                return;
            }

            _visibleItemCount = CountVisibleItems();
            _gridRows = Mathf.Max(
                Mathf.CeilToInt(_visibleItemCount / (float)GridColumns),
                1);
            _gridButtons = new Button[_gridRows, GridColumns];

            int slotIndex = 0;
            foreach (KeyValuePair<int, InventoryItem> pair in PlayerInventory.Items)
            {
                ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(pair.Value.id);
                if (!PlayerInventoryViewPolicy.ShouldDisplay(itemData, _hiddenOrigins))
                    continue;

                CreateItemSlot(slotIndex++, itemData, pair.Value.quantity);
            }

            ResetScrollPosition();
            MoveNavigationToAvailableTarget();
        }

        private int CountVisibleItems()
        {
            int count = 0;
            foreach (KeyValuePair<int, InventoryItem> pair in PlayerInventory.Items)
            {
                ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(pair.Value.id);
                if (PlayerInventoryViewPolicy.ShouldDisplay(itemData, _hiddenOrigins))
                    count++;
            }

            return count;
        }

        private void CreateItemSlot(int index, ItemData itemData, int quantity)
        {
            GameObject slotObject = Instantiate(slotPrefab, itemGridContainer);
            slotObject.name = $"PlayerItemSlot_{index}";
            PlayerInventoryItemSlotUI slot = slotObject.GetComponent<PlayerInventoryItemSlotUI>();
            if (slot == null)
            {
                Debug.LogError("[PlayerInventoryPanel] 전용 인벤토리 슬롯 컴포넌트가 없습니다");
                Destroy(slotObject);
                return;
            }

            slot.Initialize(itemData, quantity);
            Action handler = () => HandleSlotClicked(slot);
            slot.OnSlotClicked += handler;
            _slotHandlers[slot] = handler;
            _activeSlots.Add(slot);
            SetGridButton(index, slot.GetButton());
        }

        private void SetGridButton(int index, Button button)
        {
            int row = index / GridColumns;
            int column = index % GridColumns;
            if (row < _gridRows)
                _gridButtons[row, column] = button;
        }

        private void ClearSlots()
        {
            for (int i = 0; i < _activeSlots.Count; i++)
            {
                PlayerInventoryItemSlotUI slot = _activeSlots[i];
                if (slot == null)
                    continue;

                if (_slotHandlers.TryGetValue(slot, out Action handler))
                    slot.OnSlotClicked -= handler;
                Destroy(slot.gameObject);
            }

            _activeSlots.Clear();
            _slotHandlers.Clear();
        }

        private void HandleSlotClicked(PlayerInventoryItemSlotUI slot)
        {
            if (slot == null || slot.GetItemData() == null)
                return;

            MenuBarPanel menu = UIManager.Instance?.OpenPanel<MenuBarPanel>();
            menu?.InitializePlayerInventoryInformationMenu(
                slot.GetItemData().ID,
                slot.GetSlotScreenPosition());
        }
        #endregion

        #region Filters
        private void HandleShowAllClicked()
        {
            _hiddenOrigins.Clear();
            _currentFilterIndex = 0;
            RefreshAfterFilterChanged();
        }

        private void HandleOriginFilterClicked(TownKey townKey)
        {
            if (!_hiddenOrigins.Add(townKey))
                _hiddenOrigins.Remove(townKey);

            _currentFilterIndex = FindFilterNavigationIndex(townKey);
            RefreshAfterFilterChanged();
        }

        private void RefreshAfterFilterChanged()
        {
            UpdateFilterVisuals();
            RefreshInventory();
            _currentArea = NavigationArea.PlayerInventoryFilter;
            SelectCurrentButton();
        }

        private void RebuildFilterNavigationButtons()
        {
            _filterNavigationButtons.Clear();
            if (showAllButton != null)
                _filterNavigationButtons.Add(showAllButton);

            OriginFilterBinding[] filters = originFilters ?? Array.Empty<OriginFilterBinding>();
            for (int i = 0; i < filters.Length; i++)
            {
                Button button = filters[i]?.Button;
                if (button != null)
                    _filterNavigationButtons.Add(button);
            }
        }

        private void UpdateFilterVisuals()
        {
            SetButtonGraphicColor(showAllButton, showAllColor);
            OriginFilterBinding[] filters = originFilters ?? Array.Empty<OriginFilterBinding>();
            for (int i = 0; i < filters.Length; i++)
            {
                OriginFilterBinding binding = filters[i];
                if (binding?.Button == null)
                    continue;

                Color color = _hiddenOrigins.Contains(binding.TownKey)
                    ? originHiddenColor
                    : originVisibleColor;
                SetButtonGraphicColor(binding.Button, color);
            }
        }

        private int FindFilterNavigationIndex(TownKey townKey)
        {
            OriginFilterBinding[] filters = originFilters ?? Array.Empty<OriginFilterBinding>();
            for (int i = 0; i < filters.Length; i++)
                if (filters[i]?.TownKey == townKey)
                    return showAllButton != null ? i + 1 : i;

            return 0;
        }

        private static void SetButtonGraphicColor(Button button, Color color)
        {
            if (button?.targetGraphic != null)
                button.targetGraphic.color = color;
        }
        #endregion

        #region Navigation
        public override void NavigateToUpperButton()
        {
            if (_currentArea == NavigationArea.PlayerInventory)
            {
                if (_currentGridPosition.x > 0)
                    _currentGridPosition.x--;
                else
                    _currentArea = NavigationArea.PlayerInventoryFilter;
            }
            else if (_currentArea == NavigationArea.PlayerInventoryFilter)
            {
                _currentArea = NavigationArea.CloseButton;
            }

            SelectCurrentButton();
        }

        public override void NavigateToLowerButton()
        {
            if (_currentArea == NavigationArea.CloseButton)
            {
                _currentArea = NavigationArea.PlayerInventoryFilter;
            }
            else if (_currentArea == NavigationArea.PlayerInventoryFilter && _visibleItemCount > 0)
            {
                _currentArea = NavigationArea.PlayerInventory;
                _currentGridPosition = Vector2Int.zero;
            }
            else if (_currentArea == NavigationArea.PlayerInventory
                && IsValidGridPosition(_currentGridPosition + Vector2Int.right))
            {
                _currentGridPosition.x++;
            }

            SelectCurrentButton();
        }

        public override void NavigateToLeftButton()
        {
            if (_currentArea == NavigationArea.PlayerInventoryFilter)
            {
                MoveFilterSelection(-1);
            }
            else if (_currentArea == NavigationArea.PlayerInventory
                && IsValidGridPosition(_currentGridPosition + Vector2Int.down))
            {
                _currentGridPosition.y--;
            }

            SelectCurrentButton();
        }

        public override void NavigateToRightButton()
        {
            if (_currentArea == NavigationArea.PlayerInventoryFilter)
            {
                MoveFilterSelection(1);
            }
            else if (_currentArea == NavigationArea.PlayerInventory
                && IsValidGridPosition(_currentGridPosition + Vector2Int.up))
            {
                _currentGridPosition.y++;
            }

            SelectCurrentButton();
        }

        public override void ClickSelectedButton()
        {
            Button button = GetCurrentButton();
            if (button != null && button.interactable)
                button.onClick.Invoke();
        }

        private void MoveNavigationToAvailableTarget()
        {
            if (_visibleItemCount > 0)
            {
                _currentArea = NavigationArea.PlayerInventory;
                _currentGridPosition = Vector2Int.zero;
            }
            else
            {
                _currentArea = NavigationArea.PlayerInventoryFilter;
                _currentFilterIndex = Mathf.Clamp(
                    _currentFilterIndex,
                    0,
                    Mathf.Max(0, _filterNavigationButtons.Count - 1));
            }

            SelectCurrentButton();
        }

        private void MoveFilterSelection(int direction)
        {
            int count = _filterNavigationButtons.Count;
            if (count == 0)
                return;

            _currentFilterIndex = (_currentFilterIndex + direction + count) % count;
        }

        private void SelectCurrentButton()
        {
            Button button = GetCurrentButton();
            if (button != null && button.interactable)
            {
                button.Select();
                ScrollToCurrentGridRow();
            }
        }

        private void ResetScrollPosition()
        {
            if (itemScrollRect == null)
                return;

            itemScrollRect.StopMovement();
            itemScrollRect.verticalNormalizedPosition = 1f;
        }

        private void ScrollToCurrentGridRow()
        {
            if (_currentArea != NavigationArea.PlayerInventory || itemScrollRect == null)
                return;

            float normalizedPosition = _gridRows <= 1
                ? 1f
                : 1f - _currentGridPosition.x / (float)(_gridRows - 1);
            itemScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalizedPosition);
        }

        private Button GetCurrentButton()
        {
            return _currentArea switch
            {
                NavigationArea.PlayerInventory when IsValidGridPosition(_currentGridPosition)
                    => _gridButtons[_currentGridPosition.x, _currentGridPosition.y],
                NavigationArea.PlayerInventoryFilter when _currentFilterIndex >= 0
                    && _currentFilterIndex < _filterNavigationButtons.Count
                    => _filterNavigationButtons[_currentFilterIndex],
                NavigationArea.CloseButton => closeButton,
                _ => null,
            };
        }

        private bool IsValidGridPosition(Vector2Int position)
        {
            if (position.x < 0 || position.x >= _gridRows
                || position.y < 0 || position.y >= GridColumns)
            {
                return false;
            }

            int index = position.x * GridColumns + position.y;
            return index < _visibleItemCount;
        }
        #endregion
    }
}
