using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;
using SeaVillage.Data;
using SeaVillage.Event;
using SeaVillage.UI;
using TMPro;

/// <summary>
/// 퀘스트 제출 패널
/// </summary>
public class SubmissionPanel : UIPanel, IContextualPanel
{
    [Header("Buttons")]
    [SerializeField] private Button cancelButton;

    [Header("Inventory Grid")]
    [SerializeField] private Transform itemGridContainer;
    [SerializeField] private GameObject slotPrefab;

    [Header("Quest Info")]
    [SerializeField] private TextMeshProUGUI _requiredText;
    [SerializeField] private Image questItemIcon;
    [SerializeField] private Image submittedItemIcon;

    private const int GridColumns = 8;
    private const int DefaultGridRows = 4;

    private BoardData _targetBoard;
    private string _targetQuestKey;
    private int _gridRows = DefaultGridRows;
    private Button[,] _gridButtons;
    private readonly List<InventorySlotUI> _activeSlots = new List<InventorySlotUI>();
    private readonly Dictionary<InventorySlotUI, Action> _slotClickHandlers = new Dictionary<InventorySlotUI, Action>();
    private readonly HashSet<int> _selectableItemIds = new HashSet<int>();
    private NavigationArea _currentArea = NavigationArea.SubmissionInventory;
    private Vector2Int _currentGridPos = Vector2Int.zero;
    private Vector2Int _firstSelectableGridPos = new Vector2Int(-1, -1);
    private InventorySlotUI _selectedSlot;
    private Sprite _submittedItemDefaultSprite;
    private bool _submittedItemDefaultEnabled;
    private bool _hasSubmittedItemDefault;
    private Action<int> _itemSelectionAction;
    private string _requiredTextDefaultValue;
    private string _requiredTextOverride;
    private bool _hasRequiredTextDefault;
    private bool _isItemSelectionMode;

    private InventoryData PlayerInventory => InventoryManager.PlayerInventoryOrNull;
    private int MaxSlotsInGrid => _gridRows * GridColumns;

    public void Configure(BoardQuestSlot slot)
    {
        ResetItemSelectionContext();
        _targetBoard = slot?.Board;
        _targetQuestKey = slot?.QuestKey;

        if (isActiveAndEnabled)
            RefreshPanel();
    }

    public void ConfigureItemSelection(
        IReadOnlyCollection<int> allowedItemIds,
        Action<int> onSelected,
        string requiredText)
    {
        _targetBoard = null;
        _targetQuestKey = null;
        _selectableItemIds.Clear();

        if (allowedItemIds != null)
        {
            foreach (int itemId in allowedItemIds)
            {
                if (itemId > 0)
                    _selectableItemIds.Add(itemId);
            }
        }

        _itemSelectionAction = onSelected;
        _requiredTextOverride = requiredText ?? string.Empty;
        _isItemSelectionMode = onSelected != null;

        if (isActiveAndEnabled)
            RefreshPanel();
    }

    public override void OnOpen()
    {
        base.OnOpen();

        CacheSubmittedItemDefault();
        CacheRequiredTextDefault();

        if (InventoryManager.HasInstance)
            InventoryManager.Instance.OnPlayerInventoryChanged += RefreshInventory;

        RefreshPanel();
    }

    public override void OnClose()
    {
        MenuBarPanel menuBarPanel = UIManager.Instance?.GetPanel<MenuBarPanel>();
        if (menuBarPanel != null && menuBarPanel.gameObject.activeInHierarchy)
            UIManager.Instance.ClosePanel<MenuBarPanel>();

        if (InventoryManager.HasInstance)
            InventoryManager.Instance.OnPlayerInventoryChanged -= RefreshInventory;

        ClearSlots();
        ClearSelection();
        ResetItemSelectionContext();
        base.OnClose();
    }

    #region Event Listeners
    protected override void AddListeners()
    {
        if (cancelButton != null)
            cancelButton.onClick.AddListener(Close);
    }

    protected override void RemoveListeners()
    {
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(Close);
    }
    #endregion

    private void RefreshPanel()
    {
        ClearSelection();
        ApplyRequiredText();
        RefreshQuestInfo();
        RefreshInventory();
    }

    private void RefreshQuestInfo()
    {
        if (_isItemSelectionMode)
        {
            SetImageSprite(questItemIcon, null);
            RestoreSubmittedItemIcon();
            return;
        }

        if (_targetBoard == null)
        {
            SetImageSprite(questItemIcon, null);
            RestoreSubmittedItemIcon();

            return;
        }

        ItemData itemData = DataManager.Instance?.GetItem(_targetBoard.ItemID);
        Sprite iconSprite = ResolveItemIcon(itemData);

        SetImageSprite(questItemIcon, iconSprite);
        RestoreSubmittedItemIcon();
    }

    private void RefreshInventory()
    {
        ClearSlots();

        if (itemGridContainer == null || slotPrefab == null || PlayerInventory == null)
        {
            _gridRows = DefaultGridRows;
            _gridButtons = new Button[_gridRows, GridColumns];
            _currentArea = NavigationArea.CloseButton;
            SelectCurrentButton();
            return;
        }

        _gridRows = Mathf.Max(Mathf.CeilToInt((float)PlayerInventory.ItemCount / GridColumns), DefaultGridRows);
        _gridButtons = new Button[_gridRows, GridColumns];
        _firstSelectableGridPos = new Vector2Int(-1, -1);

        int slotIndex = 0;
        foreach (var kvp in PlayerInventory.Items)
        {
            var inventoryItem = kvp.Value;
            ItemData itemData = DataManager.Instance?.GetItem(inventoryItem.id);
            if (itemData == null)
                continue;

            bool isSelectable = inventoryItem.quantity > 0
                && (_isItemSelectionMode
                    ? _selectableItemIds.Contains(itemData.ID)
                    : _targetBoard != null && itemData.ID == _targetBoard.ItemID);
            CreateInventorySlot(slotIndex++, itemData, inventoryItem.quantity, isSelectable);
        }

        for (int i = slotIndex; i < MaxSlotsInGrid; i++)
            CreateEmptySlot(i);

        if (IsSelectableGridPosition(_firstSelectableGridPos))
        {
            _currentArea = NavigationArea.SubmissionInventory;
            _currentGridPos = _firstSelectableGridPos;
        }
        else
        {
            _currentArea = NavigationArea.CloseButton;
            _currentGridPos = Vector2Int.zero;
        }

        SelectCurrentButton();
    }

    private void CreateInventorySlot(int index, ItemData itemData, int quantity, bool isSelectable)
    {
        GameObject slotObject = Instantiate(slotPrefab, itemGridContainer);
        InventorySlotUI slot = slotObject.GetComponent<InventorySlotUI>();
        if (slot == null)
            return;

        slot.Initialize(itemData, quantity);
        _activeSlots.Add(slot);

        int row = index / GridColumns;
        int col = index % GridColumns;
        if (row < _gridRows)
            _gridButtons[row, col] = slot.GetButton();

        SetSlotSelectable(slot, isSelectable);
        if (!isSelectable)
            return;

        if (_firstSelectableGridPos.x < 0)
            _firstSelectableGridPos = new Vector2Int(row, col);

        int itemId = itemData.ID;
        Action handler = () => OnInventorySlotClicked(slot, itemId);
        slot.OnSlotClicked += handler;
        _slotClickHandlers[slot] = handler;
    }

    private void CreateEmptySlot(int index)
    {
        GameObject slotObject = Instantiate(slotPrefab, itemGridContainer);
        InventorySlotUI slot = slotObject.GetComponent<InventorySlotUI>();
        if (slot == null)
            return;

        slot.InitializeEmpty();
        _activeSlots.Add(slot);

        int row = index / GridColumns;
        int col = index % GridColumns;
        if (row < _gridRows)
            _gridButtons[row, col] = slot.GetButton();

        SetSlotSelectable(slot, false);
    }

    private void ClearSlots()
    {
        foreach (var slot in _activeSlots)
        {
            if (slot == null)
                continue;

            if (_slotClickHandlers.TryGetValue(slot, out var handler))
            {
                slot.OnSlotClicked -= handler;
                _slotClickHandlers.Remove(slot);
            }

            Destroy(slot.gameObject);
        }

        _activeSlots.Clear();
        _slotClickHandlers.Clear();
    }

    private void SetSlotSelectable(InventorySlotUI slot, bool isSelectable)
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

    private void OnInventorySlotClicked(InventorySlotUI slot, int itemId)
    {
        if (_isItemSelectionMode)
        {
            if (!_selectableItemIds.Contains(itemId))
                return;

            _selectedSlot = slot;
            SetImageSprite(submittedItemIcon, slot != null ? slot.GetItemIcon().sprite : null);

            MenuBarPanel selectionMenu = UIManager.Instance?.OpenPanel<MenuBarPanel>();
            if (selectionMenu != null)
            {
                selectionMenu.InitializeItemSelectionMenu(
                    itemId,
                    slot.GetSlotScreenPosition(),
                    ConfirmItemSelection,
                    ClearSelection);
            }

            return;
        }

        if (_targetBoard == null || itemId != _targetBoard.ItemID)
            return;

        _selectedSlot = slot;
        SetImageSprite(submittedItemIcon, slot != null ? slot.GetItemIcon().sprite : null);

        MenuBarPanel menuBarPanel = UIManager.Instance?.OpenPanel<MenuBarPanel>();
        if (menuBarPanel != null)
            menuBarPanel.InitializeSubmissionMenu(itemId, slot.GetSlotScreenPosition(), _ => ExecuteSubmission(), ClearSelection);
    }

    private void ClearSelection()
    {
        _selectedSlot = null;
        RestoreSubmittedItemIcon();
    }

    #region Submission Flow
    private void ConfirmItemSelection(int itemId)
    {
        if (!_isItemSelectionMode
            || !_selectableItemIds.Contains(itemId)
            || PlayerInventory == null
            || !PlayerInventory.HasItem(itemId, 1)
            || !UIManager.HasInstance)
        {
            return;
        }

        Action<int> confirmedAction = _itemSelectionAction;
        _itemSelectionAction = null;
        confirmedAction?.Invoke(itemId);
        UIManager.Instance.ClosePanel<SubmissionPanel>();
    }

    private void ExecuteSubmission()
    {
        if (_targetBoard == null || _selectedSlot == null || PlayerInventory == null)
            return;

        if (!PlayerInventory.RemoveItem(_targetBoard.ItemID, 1))
            return;

        EventManager eventManager = EventManager.Instance;
        if (!eventManager.CompleteBoardQuest(_targetBoard))
        {
            PlayerInventory.AddItem(_targetBoard.ItemID, 1);
            return;
        }

        Debug.Log($"[SubmissionPanel] 제출 완료: quest={_targetQuestKey}, itemId={_targetBoard.ItemID}");
        UIManager.Instance.ShowAlertMessage($"의뢰를 완료하여 {_targetBoard.Reward}골드를 얻었다", CloseSubmissionPanel, "확인");
    }
    #endregion

    #region Navigation
    public override void NavigateToUpperButton()
    {
        switch (_currentArea)
        {
            case NavigationArea.SubmissionInventory:
                if (_currentGridPos.x > 0)
                {
                    Vector2Int nextPos = new Vector2Int(_currentGridPos.x - 1, _currentGridPos.y);
                    if (IsSelectableGridPosition(nextPos))
                        _currentGridPos = nextPos;
                }
                else
                    _currentArea = NavigationArea.CloseButton;
                break;
        }

        SelectCurrentButton();
    }

    public override void NavigateToLowerButton()
    {
        switch (_currentArea)
        {
            case NavigationArea.CloseButton:
                if (IsSelectableGridPosition(_firstSelectableGridPos))
                {
                    _currentArea = NavigationArea.SubmissionInventory;
                    _currentGridPos = _firstSelectableGridPos;
                }
                break;
            case NavigationArea.SubmissionInventory:
                Vector2Int nextPos = new Vector2Int(_currentGridPos.x + 1, _currentGridPos.y);
                if (IsSelectableGridPosition(nextPos))
                    _currentGridPos = nextPos;
                else
                    _currentArea = NavigationArea.CloseButton;
                break;
        }

        SelectCurrentButton();
    }

    public override void NavigateToLeftButton()
    {
        if (_currentArea != NavigationArea.SubmissionInventory)
            return;

        Vector2Int nextPos = new Vector2Int(_currentGridPos.x, _currentGridPos.y - 1);
        if (IsSelectableGridPosition(nextPos))
            _currentGridPos = nextPos;

        SelectCurrentButton();
    }

    public override void NavigateToRightButton()
    {
        if (_currentArea != NavigationArea.SubmissionInventory)
            return;

        Vector2Int nextPos = new Vector2Int(_currentGridPos.x, _currentGridPos.y + 1);
        if (IsSelectableGridPosition(nextPos))
            _currentGridPos = nextPos;

        SelectCurrentButton();
    }

    public override void ClickSelectedButton()
    {
        Button button = GetCurrentButton();
        if (button != null && button.interactable)
            button.onClick.Invoke();
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
            case NavigationArea.SubmissionInventory:
                if (IsSelectableGridPosition(_currentGridPos))
                    return _gridButtons[_currentGridPos.x, _currentGridPos.y];
                break;
            case NavigationArea.CloseButton:
                return cancelButton;
        }

        return cancelButton;
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

    private static void SetImageSprite(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private void CacheSubmittedItemDefault()
    {
        if (_hasSubmittedItemDefault || submittedItemIcon == null)
            return;

        _submittedItemDefaultSprite = submittedItemIcon.sprite;
        _submittedItemDefaultEnabled = submittedItemIcon.enabled;
        _hasSubmittedItemDefault = true;
    }

    private void RestoreSubmittedItemIcon()
    {
        if (submittedItemIcon == null)
            return;

        CacheSubmittedItemDefault();
        submittedItemIcon.sprite = _submittedItemDefaultSprite;
        submittedItemIcon.enabled = _submittedItemDefaultEnabled;
    }

    private void CacheRequiredTextDefault()
    {
        if (_hasRequiredTextDefault || _requiredText == null)
            return;

        _requiredTextDefaultValue = _requiredText.text;
        _hasRequiredTextDefault = true;
    }

    private void ApplyRequiredText()
    {
        if (_requiredText == null)
            return;

        CacheRequiredTextDefault();
        _requiredText.text = _isItemSelectionMode
            ? _requiredTextOverride
            : _requiredTextDefaultValue;
    }

    private void ResetItemSelectionContext()
    {
        _isItemSelectionMode = false;
        _itemSelectionAction = null;
        _requiredTextOverride = null;
        _selectableItemIds.Clear();
        ApplyRequiredText();
    }

    private static void CloseSubmissionPanel()
    {
        UIManager.Instance?.ClosePanel<SubmissionPanel>();
    }

    private static string ResolveItemName(ItemData itemData, int fallbackItemId)
    {
        if (itemData != null)
        {
            string itemName = DataManager.Instance.GetScriptTextKR(itemData.Name);
            if (!string.IsNullOrWhiteSpace(itemName))
                return itemName;
        }

        return $"Item {fallbackItemId}";
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
}
