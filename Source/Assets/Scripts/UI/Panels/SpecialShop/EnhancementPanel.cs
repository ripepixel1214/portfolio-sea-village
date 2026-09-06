using System;
using System.Collections.Generic;
using SeaVillage.Core;
using SeaVillage.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    public class EnhancementPanel : UIPanel, IContextualPanel
    {
        private const int EnhancementGoldCost = 0;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _headerText;
        [SerializeField] private Button _closeButton;

        [Header("Current Item")]
        [SerializeField] private Button _targetSlotButton;
        [SerializeField] private Image _currentItemIcon;
        [SerializeField] private TextMeshProUGUI _currentLevelText;
        [SerializeField] private TextMeshProUGUI _currentPriceText;
        [SerializeField] private TextMeshProUGUI _selectItemText;

        [Header("Result Item")]
        [SerializeField] private Image _resultItemIcon;
        [SerializeField] private TextMeshProUGUI _resultLevelText;
        [SerializeField] private TextMeshProUGUI _resultPriceText;

        [Header("Requirement")]
        [SerializeField] private TextMeshProUGUI _crystalRequirementText;

        [Header("Actions")]
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _enhanceButton;
        [SerializeField] private Sprite _emptySlotSprite;

        private readonly List<int> _selectableItemIds = new();
        private int _selectedItemId;

        #region Public API

        public void Initialize()
        {
            _selectedItemId = 0;
            SpecialShopPanelUtility.SetText(_headerText, "강화");
            RefreshState();
        }

        public override void OnClose()
        {
            _selectedItemId = 0;
            _selectableItemIds.Clear();
            base.OnClose();
        }

        #endregion

        #region Event Handlers

        protected override void AddListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
            if (_targetSlotButton != null)
                _targetSlotButton.onClick.AddListener(OpenItemSelection);
            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Close);
            if (_enhanceButton != null)
                _enhanceButton.onClick.AddListener(ConfirmEnhancement);
        }

        protected override void RemoveListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);
            if (_targetSlotButton != null)
                _targetSlotButton.onClick.RemoveListener(OpenItemSelection);
            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(Close);
            if (_enhanceButton != null)
                _enhanceButton.onClick.RemoveListener(ConfirmEnhancement);
        }

        private void OpenItemSelection()
        {
            if (!SpecialShopAccessPolicy.CanUseCurrentTown(SpecialShopFeature.SpecialContent))
            {
                UIManager.Instance?.ShowAlertMessage(
                    $"호감도 {SpecialShopAccessPolicy.GetRequiredAffinity(SpecialShopFeature.SpecialContent)} 이상 필요");
                return;
            }

            if (!TryGetRuntimeData(
                    out SpecialShopContentCatalog catalog,
                    out ItemDatabase itemDatabase,
                    out InventoryData inventory))
            {
                UIManager.Instance?.ShowAlertMessage("강화 정보를 불러올 수 없다");
                return;
            }

            BuildSelectableItemIds(catalog, itemDatabase, inventory);
            SubmissionPanel submissionPanel = UIManager.Instance?.OpenPanel<SubmissionPanel>();
            if (submissionPanel == null)
            {
                UIManager.Instance?.ShowAlertMessage("아이템 선택창을 열 수 없다");
                return;
            }

            submissionPanel.ConfigureItemSelection(
                _selectableItemIds,
                OnEnhancementItemSelected,
                "강화 아이템");
        }

        private void OnEnhancementItemSelected(int itemId)
        {
            if (!gameObject.activeInHierarchy
                || !SpecialShopAccessPolicy.CanUseCurrentTown(SpecialShopFeature.SpecialContent)
                || !TryGetRuntimeData(
                    out SpecialShopContentCatalog catalog,
                    out ItemDatabase itemDatabase,
                    out InventoryData inventory))
            {
                return;
            }

            BuildSelectableItemIds(catalog, itemDatabase, inventory);
            if (!_selectableItemIds.Contains(itemId))
            {
                UIManager.Instance?.ShowAlertMessage("선택한 아이템은 강화할 수 없다", RefreshState);
                return;
            }

            _selectedItemId = itemId;
            RefreshState();
        }

        private void ConfirmEnhancement()
        {
            if (!TryResolveCurrentSelection(
                    out SpecialShopContentCatalog catalog,
                    out ItemDatabase itemDatabase,
                    out InventoryData inventory,
                    out int nextItemId,
                    out EnhancementLevelDefinition levelDefinition))
            {
                UIManager.Instance?.ShowAlertMessage("강화할 아이템을 다시 선택해야 한다", RefreshState);
                return;
            }

            int crystalCount = inventory.GetItemCount(catalog.CrystalItemId);
            if (crystalCount < levelDefinition.CrystalCount)
            {
                UIManager.Instance?.ShowAlertMessage("수정이 부족하다", RefreshState);
                return;
            }

            string currentItemName = GetDisplayName(itemDatabase, _selectedItemId);
            string nextItemName = GetDisplayName(itemDatabase, nextItemId);
            UIManager.Instance?.ShowConfirmMessage(
                $"{currentItemName} → {nextItemName}\n"
                + $"수정 {levelDefinition.CrystalCount}개 · 비용 {EnhancementGoldCost} G\n"
                + $"성공 확률 {levelDefinition.SuccessRatePercent}%",
                ExecuteEnhancement,
                null,
                "강화 확인");
        }

        private void ExecuteEnhancement()
        {
            if (!SpecialShopAccessPolicy.CanUseCurrentTown(SpecialShopFeature.SpecialContent))
            {
                ShowMessageAfterCurrentDialog(
                    $"호감도 {SpecialShopAccessPolicy.GetRequiredAffinity(SpecialShopFeature.SpecialContent)} 이상 필요",
                    RefreshState);
                return;
            }

            if (!TryResolveCurrentSelection(
                    out SpecialShopContentCatalog catalog,
                    out ItemDatabase itemDatabase,
                    out _,
                    out int nextItemId,
                    out EnhancementLevelDefinition levelDefinition))
            {
                ShowMessageAfterCurrentDialog("강화할 아이템을 다시 선택해야 한다", RefreshState);
                return;
            }

            int currentItemId = _selectedItemId;
            var rule = new EnhancementRule(
                levelDefinition.CrystalCount,
                EnhancementGoldCost,
                levelDefinition.SuccessRatePercent);
            bool processed = EnhancementProcessor.TryEnhance(
                currentItemId,
                nextItemId,
                catalog.CrystalItemId,
                rule,
                out bool success,
                out string failReason);

            if (processed)
                _selectedItemId = success ? nextItemId : currentItemId;

            string message = processed
                ? success
                    ? $"{GetDisplayName(itemDatabase, nextItemId)} 강화에 성공했다"
                    : "강화에 실패했다"
                : failReason;
            string title = processed && success ? "강화 성공" : "강화 결과";
            ShowMessageAfterCurrentDialog(message, RefreshState, title);
        }

        #endregion

        #region Private Helpers

        private void RefreshState()
        {
            ConfigureNavigation();

            bool canUseEnhancement = SpecialShopAccessPolicy.CanUseCurrentTown(SpecialShopFeature.SpecialContent);
            bool hasRuntimeData = TryGetRuntimeData(
                out SpecialShopContentCatalog catalog,
                out ItemDatabase itemDatabase,
                out InventoryData inventory);
            SetButtonEnabled(_targetSlotButton, canUseEnhancement && hasRuntimeData);

            if (!hasRuntimeData)
            {
                _selectedItemId = 0;
                ShowEmptyState();
                return;
            }

            BuildSelectableItemIds(catalog, itemDatabase, inventory);
            if (!TryResolveSelectedItem(
                    catalog,
                    itemDatabase,
                    inventory,
                    out int nextItemId,
                    out EnhancementLevelDefinition levelDefinition))
            {
                _selectedItemId = 0;
                ShowEmptyState();
                return;
            }

            ItemData currentItem = itemDatabase.GetItem(_selectedItemId);
            ItemData nextItem = itemDatabase.GetItem(nextItemId);
            int currentLevel = Mathf.Max(0, levelDefinition.TargetLevel - 1);
            int crystalCount = inventory.GetItemCount(catalog.CrystalItemId);
            bool hasEnoughCrystal = crystalCount >= levelDefinition.CrystalCount;

            SetSelectItemTextVisible(false);
            SetItemIcon(_currentItemIcon, _selectedItemId, null);
            SetItemIcon(_resultItemIcon, nextItemId, _currentItemIcon != null ? _currentItemIcon.sprite : null);
            SpecialShopPanelUtility.SetText(_currentLevelText, $"+{currentLevel}");
            SpecialShopPanelUtility.SetText(_resultLevelText, $"+{levelDefinition.TargetLevel}");
            SpecialShopPanelUtility.SetText(_currentPriceText, $"{currentItem.OriginPrice:N0} G");
            SpecialShopPanelUtility.SetText(_resultPriceText, $"{nextItem.OriginPrice:N0} G");
            SetRequirementText(crystalCount, levelDefinition.CrystalCount, hasEnoughCrystal);
            SetButtonEnabled(_enhanceButton, canUseEnhancement && hasEnoughCrystal);
            EnsureValidSelection();
        }

        private void ConfigureNavigation()
        {
            navigableButtons.Clear();
            if (_closeButton != null)
                navigableButtons.Add(_closeButton);
            if (_targetSlotButton != null)
                navigableButtons.Add(_targetSlotButton);
            if (_cancelButton != null)
                navigableButtons.Add(_cancelButton);
            if (_enhanceButton != null)
                navigableButtons.Add(_enhanceButton);

            defaultSelectedButtonIndex = navigableButtons.Count > 1 ? 1 : 0;
            currentSelectedButtonIndex = defaultSelectedButtonIndex;
            EnsureValidSelection();
        }

        private void ShowEmptyState()
        {
            SetEmptyIcon(_currentItemIcon);
            SetEmptyIcon(_resultItemIcon);
            SetSelectItemTextVisible(true);
            SpecialShopPanelUtility.SetText(_currentLevelText, string.Empty);
            SpecialShopPanelUtility.SetText(_currentPriceText, string.Empty);
            SpecialShopPanelUtility.SetText(_resultLevelText, string.Empty);
            SpecialShopPanelUtility.SetText(_resultPriceText, string.Empty);
            SpecialShopPanelUtility.SetText(_crystalRequirementText, "0 / 0");
            SetButtonEnabled(_enhanceButton, false);
            EnsureValidSelection();
        }

        private void SetSelectItemTextVisible(bool visible)
        {
            if (_selectItemText != null)
                _selectItemText.gameObject.SetActive(visible);
        }

        private void SetEmptyIcon(Image image)
        {
            if (image == null)
                return;

            image.sprite = _emptySlotSprite;
            image.enabled = _emptySlotSprite != null;
            image.preserveAspect = true;
        }

        private static void SetItemIcon(Image image, int itemId, Sprite fallback)
        {
            if (image == null)
                return;

            SpecialShopPanelUtility.SetItemIcon(image, itemId);
            if (image.enabled || fallback == null)
                return;

            image.sprite = fallback;
            image.enabled = true;
            image.preserveAspect = true;
        }

        private void SetRequirementText(int ownedCount, int requiredCount, bool sufficient)
        {
            if (_crystalRequirementText == null)
                return;

            string color = sufficient ? "#419665" : "#DC4B4B";
            _crystalRequirementText.text = $"<color={color}>{ownedCount}</color> / {requiredCount}";
        }

        private bool TryResolveCurrentSelection(
            out SpecialShopContentCatalog catalog,
            out ItemDatabase itemDatabase,
            out InventoryData inventory,
            out int nextItemId,
            out EnhancementLevelDefinition levelDefinition)
        {
            nextItemId = 0;
            levelDefinition = null;
            if (!TryGetRuntimeData(out catalog, out itemDatabase, out inventory))
                return false;

            return TryResolveSelectedItem(
                catalog,
                itemDatabase,
                inventory,
                out nextItemId,
                out levelDefinition);
        }

        private void BuildSelectableItemIds(
            SpecialShopContentCatalog catalog,
            ItemDatabase itemDatabase,
            InventoryData inventory)
        {
            _selectableItemIds.Clear();
            if (catalog == null
                || itemDatabase == null
                || inventory == null
                || !IsValidItem(itemDatabase, catalog.CrystalItemId))
            {
                return;
            }

            foreach (var pair in inventory.Items)
            {
                int itemId = pair.Key;
                if (pair.Value.quantity <= 0
                    || !IsValidItem(itemDatabase, itemId)
                    || !catalog.TryResolveEnhancement(
                        itemId,
                        out int nextItemId,
                        out EnhancementLevelDefinition levelDefinition)
                    || !IsValidItem(itemDatabase, nextItemId)
                    || levelDefinition == null)
                {
                    continue;
                }

                _selectableItemIds.Add(itemId);
            }
        }

        private bool TryResolveSelectedItem(
            SpecialShopContentCatalog catalog,
            ItemDatabase itemDatabase,
            InventoryData inventory,
            out int nextItemId,
            out EnhancementLevelDefinition levelDefinition)
        {
            nextItemId = 0;
            levelDefinition = null;
            return _selectedItemId > 0
                && inventory.HasItem(_selectedItemId, 1)
                && IsValidItem(itemDatabase, catalog.CrystalItemId)
                && IsValidItem(itemDatabase, _selectedItemId)
                && catalog.TryResolveEnhancement(_selectedItemId, out nextItemId, out levelDefinition)
                && IsValidItem(itemDatabase, nextItemId)
                && levelDefinition != null;
        }

        private static bool TryGetRuntimeData(
            out SpecialShopContentCatalog catalog,
            out ItemDatabase itemDatabase,
            out InventoryData inventory)
        {
            catalog = null;
            itemDatabase = null;
            inventory = InventoryManager.PlayerInventoryOrNull;

            if (!DataManager.HasInstance)
                return false;

            catalog = DataManager.Instance.SpecialShopContentCatalog;
            itemDatabase = DataManager.Instance.ItemDatabase;
            return catalog != null && itemDatabase != null && inventory != null;
        }

        private static bool IsValidItem(ItemDatabase itemDatabase, int itemId)
        {
            return itemId > 0 && itemDatabase.GetItem(itemId) != null;
        }

        private static string GetDisplayName(ItemDatabase itemDatabase, int itemId)
        {
            return IsValidItem(itemDatabase, itemId)
                ? SpecialShopPanelUtility.GetItemName(itemId)
                : string.Empty;
        }

        private static void ShowMessageAfterCurrentDialog(
            string message,
            Action onConfirm = null,
            string title = "알림")
        {
            if (!UIManager.HasInstance)
                return;

            UIManager manager = UIManager.Instance;
            void Handler()
            {
                manager.OnPanelClosed -= Handler;
                manager.ShowAlertMessage(message, onConfirm, title);
            }

            manager.OnPanelClosed += Handler;
        }

        #endregion
    }
}
