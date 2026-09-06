using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    /// <summary>재고 관리 패널, 판매대 한 칸(slotIndex)의 [현재] 구성과 [변경] 예약을 나란히 표시. 인벤토리 아이템 클릭 시 MenuBarPanel 로 판매가·수량 지정 후, 같은 아이템이면 즉시 옮기고 다른 아이템이면 [변경] 예약 뒤 [변경] 버튼으로 제자리 적용</summary>
    public class PlayerShopStockPanel : UIPanel
    {
        private const int GridColumns = 4;

        [Header("Buttons")]
        [SerializeField] private Button _cancelButton;
        [SerializeField] private Button _applyButton;

        [Header("Current Column")]
        [SerializeField] private Image _currentIcon;
        [SerializeField] private TMP_Text _currentNameText;
        [SerializeField] private TMP_Text _currentStockText;
        [SerializeField] private TMP_Text _currentPriceText;
        [SerializeField] private ShopItemPriceGraphView _currentPriceGraph;

        [Header("Change Column")]
        [SerializeField] private Image _changeIcon;
        [SerializeField] private TMP_Text _changeNameText;
        [SerializeField] private TMP_Text _changeStockText;
        [SerializeField] private TMP_Text _changePriceText;
        [SerializeField] private ShopItemPriceGraphView _changePriceGraph;

        [Header("Weight")]
        [SerializeField] private TMP_Text _weightText;
        [SerializeField] private IncreasableGauge _weightGauge;
        [SerializeField] private Color _weightIncreaseColor = new Color(0.25f, 0.45f, 1f);
        [SerializeField] private Color _weightDecreaseColor = new Color(0.9f, 0.25f, 0.25f);

        [Header("Inventory Grid")]
        [SerializeField] private Transform _itemGridContainer;
        [SerializeField] private GameObject _slotPrefab;

        private TownKey _townKey = TownKey.Unknown;
        private int _slotIndex;
        private int _currentItemId;

        // [변경] 예약(선택하기), 아이템이 0이면 예약 없음
        private int _changeItemId;
        private int _changeQuantity;
        private int _changePrice;

        private Color _weightDefaultColor;
        private bool _weightDefaultCaptured;

        private readonly List<InventorySlotUI> _slots = new List<InventorySlotUI>();
        private readonly Dictionary<InventorySlotUI, Action> _slotClickHandlers = new Dictionary<InventorySlotUI, Action>();

        private InventoryData PlayerInventory => InventoryManager.PlayerInventoryOrNull;

        private PlayerShopStateReadOnly PlayerShopState =>
            PlayerShopManager.HasInstance ? PlayerShopManager.Instance.GetState(_townKey) : null;

        public override void OnOpen()
        {
            base.OnOpen();

            if (InventoryManager.HasInstance)
                InventoryManager.Instance.OnPlayerInventoryChanged += Refresh;

            Refresh();
        }

        public override void OnClose()
        {
            MenuBarPanel menuBarPanel = UIManager.Instance.GetPanel<MenuBarPanel>();
            if (menuBarPanel != null && menuBarPanel.gameObject.activeInHierarchy)
                UIManager.Instance.ClosePanel<MenuBarPanel>();

            if (InventoryManager.HasInstance)
                InventoryManager.Instance.OnPlayerInventoryChanged -= Refresh;

            ClearSlots();
            base.OnClose();
        }

        #region Event Listeners
        protected override void AddListeners()
        {
            base.AddListeners();

            if (_cancelButton != null)
                _cancelButton.onClick.AddListener(Close);

            if (_applyButton != null)
                _applyButton.onClick.AddListener(ApplyChange);
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_cancelButton != null)
                _cancelButton.onClick.RemoveListener(Close);

            if (_applyButton != null)
                _applyButton.onClick.RemoveListener(ApplyChange);
        }
        #endregion

        /// <summary>대상 판매대 칸을 slotIndex 로 지정</summary>
        public void Initialize(TownKey townKey, int slotIndex)
        {
            _townKey = townKey;
            _slotIndex = slotIndex;
            _currentItemId = GetSlotItem()?.ItemId ?? 0;

            ClearChange();
            Refresh();
        }

        #region Inventory Click → MenuBar
        private void OnInventoryItemClicked(InventorySlotUI slot, int itemId)
        {
            if (itemId <= 0 || slot == null)
                return;

            bool isSameItem = _currentItemId > 0 && itemId == _currentItemId;
            string actionLabel = isSameItem ? "옮기기" : "선택하기";
            int defaultPrice = DefaultPriceFor(itemId);
            int maxQuantity = GetOwnedQuantity(itemId);

            MenuBarPanel menuBarPanel = UIManager.Instance.OpenPanel<MenuBarPanel>();
            if (menuBarPanel == null)
                return;

            menuBarPanel.InitializePlayerShopStockMenu(
                itemId,
                slot.GetSlotScreenPosition(),
                defaultPrice,
                maxQuantity,
                actionLabel,
                (quantity, price) =>
                {
                    if (isSameItem)
                        MoveIntoSlot(quantity, price);
                    else
                        StageChange(itemId, quantity, price);
                });
        }

        /// <summary>옮기기: 같은 아이템을 지정 수량만큼 현재 슬롯에 즉시 추가</summary>
        private void MoveIntoSlot(int quantity, int price)
        {
            if (!PlayerShopManager.Instance.TryAddToSlotItem(_townKey, _slotIndex, quantity, price, out string failReason))
            {
                ShowFailure(failReason);
                return;
            }

            _currentItemId = GetSlotItem()?.ItemId ?? 0;
            ClearChange();
            Refresh();
        }

        /// <summary>선택하기: 다른 아이템을 [변경] 칸에 예약(확정은 [변경] 버튼)</summary>
        private void StageChange(int itemId, int quantity, int price)
        {
            _changeItemId = itemId;
            _changeQuantity = quantity;
            _changePrice = price;
            Refresh();
        }

        private void ClearChange()
        {
            _changeItemId = 0;
            _changeQuantity = 0;
            _changePrice = 0;
        }
        #endregion

        #region Apply
        private void ApplyChange()
        {
            if (!PlayerShopManager.HasInstance || _changeItemId <= 0)
            {
                UIManager.Instance.ShowAlertMessage("변경할 아이템을 고르지 않았다");
                return;
            }

            if (!PlayerShopManager.Instance.TrySetSlotItem(_townKey, _slotIndex, _changeItemId, _changeQuantity, _changePrice, out string failReason))
            {
                ShowFailure(failReason);
                return;
            }

            Close();
        }

        private void ShowFailure(string failReason)
        {
            Debug.LogWarning($"[{nameof(PlayerShopStockPanel)}] 재고 변경 실패: town={_townKey}, slot={_slotIndex}, reason={failReason}");
            UIManager.Instance.ShowAlertMessage(failReason);
            Refresh();
        }
        #endregion

        #region Refresh
        private void Refresh()
        {
            PlayerShopItemReadOnly currentListed = GetSlotItem();
            RefreshColumn(currentListed?.ItemId ?? 0, currentListed?.Quantity ?? 0, currentListed?.UnitPrice ?? 0,
                _currentIcon, _currentNameText, _currentStockText, _currentPriceText, _currentPriceGraph);

            RefreshColumn(_changeItemId, _changeQuantity, _changePrice,
                _changeIcon, _changeNameText, _changeStockText, _changePriceText, _changePriceGraph);

            RefreshWeight();
            RefreshControls();
            RefreshInventory();
        }

        private void RefreshColumn(int itemId, int quantity, int unitPrice,
            Image icon, TMP_Text nameText, TMP_Text stockText, TMP_Text priceText, ShopItemPriceGraphView priceGraph)
        {
            ItemData itemData = itemId > 0 ? DataManager.Instance.GetItem(itemId) : null;
            int marketPrice = itemId > 0 ? PlayerShopManager.Instance.GetListedMarketPrice(_townKey, itemId) : 0;

            SetImageSprite(icon, ResolveItemIcon(itemData));

            if (nameText != null)
                nameText.text = itemData != null ? $"이름 : {ResolveItemName(itemData, itemId)}" : "이름 : -";

            if (stockText != null)
                stockText.text = itemId > 0 ? $"재고 : {quantity}개" : "재고 : -";

            if (priceText != null)
                priceText.text = itemId > 0 ? $"판매가 : {unitPrice} G" : "판매가 : -";

            if (priceGraph == null)
                return;

            if (itemData != null)
            {
                string marketTown = PlayerShopState != null
                    ? TownKeyUtility.ToStorageKey(PlayerShopState.TownKey)
                    : string.Empty;
                priceGraph.UpdateGraph(itemData, marketPrice, marketTown);
            }
            else
                priceGraph.Clear();
        }

        /// <summary>변경 확정 시 인벤토리 무게 변화를 텍스트·게이지로 미리 표시(예약이 있을 때만)</summary>
        private void RefreshWeight()
        {
            if (PlayerInventory == null)
                return;

            if (_weightText != null && !_weightDefaultCaptured)
            {
                _weightDefaultColor = _weightText.color;
                _weightDefaultCaptured = true;
            }

            float currentWeight = PlayerInventory.CurrentWeight;
            float delta = 0f;

            if (_changeItemId > 0 && _changeItemId != _currentItemId)
            {
                int currentQuantity = GetSlotItem()?.Quantity ?? 0;
                // 확정 시 현재 슬롯 아이템은 가방으로 복귀(+), 변경 아이템은 가방에서 차감(-)
                delta = GetTotalWeight(_currentItemId, currentQuantity) - GetTotalWeight(_changeItemId, _changeQuantity);
            }

            float projected = Mathf.Round((currentWeight + delta) * 10f) / 10f;

            if (_weightText != null)
            {
                _weightText.text = Mathf.Approximately(delta, 0f)
                    ? $"{projected:0.#}kg"
                    : $"{projected:0.#}kg({(delta > 0f ? "+" : string.Empty)}{delta:0.#})";

                if (Mathf.Approximately(delta, 0f))
                    _weightText.color = _weightDefaultColor;
                else
                    _weightText.color = delta > 0f ? _weightIncreaseColor : _weightDecreaseColor;
            }

            if (_weightGauge != null)
                _weightGauge.UpdateGauge(PlayerInventory.MaxWeight, currentWeight, delta);
        }

        private void RefreshControls()
        {
            // 다른 아이템을 예약한 경우에만 [변경] 확정 가능(같은 아이템 추가는 옮기기로 즉시 처리)
            SetButtonEnabled(_applyButton, _changeItemId > 0 && _changeItemId != _currentItemId);
            EnsureValidSelection();
        }

        private void RefreshInventory()
        {
            ClearSlots();

            if (_itemGridContainer == null || _slotPrefab == null || PlayerInventory == null)
                return;

            int index = 0;
            foreach (KeyValuePair<int, InventoryItem> kvp in PlayerInventory.Items)
            {
                InventoryItem inventoryItem = kvp.Value;
                ItemData itemData = DataManager.Instance.GetItem(inventoryItem.id);
                if (itemData == null || itemData.Unsellable)
                    continue;

                CreateSlot(itemData, inventoryItem.quantity);
                index++;
            }

            int minSlots = Mathf.CeilToInt(index / (float)GridColumns) * GridColumns;
            for (int i = index; i < minSlots; i++)
                CreateEmptySlot();
        }

        private void CreateSlot(ItemData itemData, int quantity)
        {
            InventorySlotUI slot = InstantiateSlot();
            if (slot == null)
                return;

            slot.Initialize(itemData, quantity);

            int itemId = itemData.ID;
            Action handler = () => OnInventoryItemClicked(slot, itemId);
            slot.OnSlotClicked += handler;
            _slotClickHandlers[slot] = handler;
        }

        private void CreateEmptySlot()
        {
            InventorySlotUI slot = InstantiateSlot();
            slot?.InitializeEmpty();
        }

        private InventorySlotUI InstantiateSlot()
        {
            GameObject slotObject = Instantiate(_slotPrefab, _itemGridContainer);
            InventorySlotUI slot = slotObject.GetComponent<InventorySlotUI>();
            if (slot == null)
            {
                Destroy(slotObject);
                return null;
            }

            _slots.Add(slot);
            return slot;
        }

        private void ClearSlots()
        {
            foreach (InventorySlotUI slot in _slots)
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

            _slots.Clear();
            _slotClickHandlers.Clear();
        }
        #endregion

        #region Helpers
        private PlayerShopItemReadOnly GetSlotItem()
            => PlayerShopState?.GetSlotItem(_slotIndex);

        private int GetOwnedQuantity(int itemId)
            => PlayerInventory?.GetItemCount(itemId) ?? 0;

        private static float GetTotalWeight(int itemId, int quantity)
        {
            if (itemId <= 0 || quantity <= 0)
                return 0f;

            ItemData itemData = DataManager.Instance.GetItem(itemId);
            return itemData != null ? Mathf.Round(itemData.Weight * quantity * 10f) / 10f : 0f;
        }

        private int DefaultPriceFor(int itemId)
        {
            int marketPrice = itemId > 0 ? PlayerShopManager.Instance.GetListedMarketPrice(_townKey, itemId) : 0;
            return Mathf.Max(1, marketPrice);
        }

        private static void SetImageSprite(Image image, Sprite sprite)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.enabled = sprite != null;
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
        #endregion
    }
}
