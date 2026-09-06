using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.Data;
using SeaVillage.UI.Shop;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    /// <summary>
    /// 상점 - 구매 패널 (View)
    /// </summary>
    public class PurchasePanel : UIPanel, IPurchaseView, IContextualPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button purchaseButton;

        [Header("Inventory Grids")]
        [SerializeField] private Transform shopGridContainer;
        [SerializeField] private Transform cartGridContainer;
        [SerializeField] private GameObject slotPrefab;

        [Header("Price Texts")]
        [SerializeField] private TextMeshProUGUI totalPriceText;
        [SerializeField] private TextMeshProUGUI tradingFeeText;
        [SerializeField] private TextMeshProUGUI finalPriceText;

        [Header("Weight Gauge")]
        [SerializeField] private IncreasableGauge weightGauge;

        // Grid Configuration
        private const int ShopGridRows = 4;
        private const int ShopGridCols = 5;
        private const int CartGridRows = 2;
        private const int CartGridCols = 3;
        private const int MaxShopSlots = ShopGridRows * ShopGridCols;
        private const int MaxCartSlots = CartGridRows * CartGridCols;

        // MVP
        private PurchasePresenter presenter;
        public PurchasePresenter Presenter => presenter;

        [SerializeField] private Button changeInfoTypeButton;

        // Slot Management
        private Button[,] shopGridButtons;
        private Button[,] cartGridButtons;
        private readonly List<ShopSlotUI> shopSlots = new List<ShopSlotUI>();
        private readonly List<ShopSlotUI> cartSlots = new List<ShopSlotUI>();
        private readonly Dictionary<ShopSlotUI, Action> slotClickHandlers = new Dictionary<ShopSlotUI, Action>();

        // Navigation State
        private Vector2Int currentGridPos = Vector2Int.zero;
        private NavigationArea currentArea = NavigationArea.ShopGrid;
        private int shopItemCount;
        private int cartItemCount;
        private int contextShopId;
        private bool hasContextShopId;
        private string contextMarketTown;
        public string MarketTown => string.IsNullOrEmpty(contextMarketTown) ? ShopUtility.GetCurrentTownStorageKey() : contextMarketTown;

        #region IPurchaseView Events
        public event Action OnConfirmButtonClicked;
        public event Action<int> OnSlotClicked;
        public event Action<int, Vector2> OnCartSlotClicked;
        #endregion

        #region Initialization
        public void SetShopContext(int shopId)
        {
            SetShopContext(shopId, null);
        }

        public void SetShopContext(int shopId, string marketTown)
        {
            if (shopId <= 0)
                return;

            contextShopId = shopId;
            hasContextShopId = true;
            contextMarketTown = TownKeyUtility.NormalizeStorageKey(marketTown);

            if (presenter != null)
            {
                presenter.SetMarketTown(MarketTown);
                presenter.Initialize(shopId);
            }
        }

        #endregion

        #region Panel Methods
        public override void OnOpen()
        {
            base.OnOpen();

            // 그리드 초기화
            shopGridButtons = new Button[ShopGridRows, ShopGridCols];
            cartGridButtons = new Button[CartGridRows, CartGridCols];

            currentArea = NavigationArea.ShopGrid;
            currentGridPos = Vector2Int.zero;

            presenter = new PurchasePresenter(this, MarketTown);

            // 상점 ID 가져오기
            ShopPanel shopPanel = GetComponentInParent<ShopPanel>();
            if (shopPanel == null)
                shopPanel = UIManager.HasInstance ? UIManager.Instance.GetPanel<ShopPanel>() : null;

            int shopId = hasContextShopId ? contextShopId : (shopPanel != null ? shopPanel.GetShopID() : 0);
            if (shopId > 0)
                presenter.Initialize(shopId);

            TutorialUIBinding.Bind(purchaseButton, TutorialAnchorKeys.PurchaseConfirmButton);
        }

        public override void OnClose()
        {
            base.OnClose();

            ClearAllSlots();
            presenter?.Dispose();
            presenter = null;
            hasContextShopId = false;
            contextShopId = 0;
            contextMarketTown = string.Empty;
        }
        #endregion

        #region Listeners
        protected override void AddListeners()
        {
            if (cancelButton != null)
                cancelButton.onClick.AddListener(Close);

            if (purchaseButton != null)
                purchaseButton.onClick.AddListener(HandlePurchaseButtonClicked);

            if (changeInfoTypeButton != null)
                changeInfoTypeButton.onClick.AddListener(ChangeSlotsInfoType);
        }

        protected override void RemoveListeners()
        {
            if (cancelButton != null)
                cancelButton.onClick.RemoveAllListeners();

            if (purchaseButton != null)
                purchaseButton.onClick.RemoveAllListeners();

            if (changeInfoTypeButton != null)
                changeInfoTypeButton.onClick.RemoveListener(ChangeSlotsInfoType);
        }

        private void HandlePurchaseButtonClicked()
        {
            OnConfirmButtonClicked?.Invoke();
        }
        #endregion

        #region IPurchaseView Implementation
        public void RefreshSourceSlots(IReadOnlyDictionary<int, int> items)
        {
            ClearSlots(isTargetSlots: false);

            int index = 0;
            foreach (var kvp in items)
            {
                if (index >= MaxShopSlots) break;

                ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(kvp.Key);
                if (itemData != null)
                {
                    int itemID = kvp.Key;
                    CreateSlot(shopGridContainer, itemData, kvp.Value, false, () => HandleSlotClicked(itemID, false));

                    if (itemID == TutorialItemIds.Potato && shopSlots.Count > 0)
                        TutorialUIBinding.Bind(shopSlots[shopSlots.Count - 1], TutorialAnchorKeys.ShopPotatoSlot);

                    int row = index / ShopGridCols;
                    int col = index % ShopGridCols;
                    if (row < ShopGridRows && shopSlots.Count > 0)
                        shopGridButtons[row, col] = shopSlots[shopSlots.Count - 1].GetButton();

                    index++;
                }
            }

            for (int i = index; i < MaxShopSlots; i++)
            {
                CreateEmptySlot(shopGridContainer, false);
                int row = i / ShopGridCols;
                int col = i % ShopGridCols;
                if (row < ShopGridRows && shopSlots.Count > 0)
                    shopGridButtons[row, col] = shopSlots[shopSlots.Count - 1].GetButton();
            }

            shopItemCount = items.Count;

            // 슬롯 재생성 후 선택 복원
            SelectCurrentButton();
        }

        public void RefreshTargetSlots(IReadOnlyDictionary<int, int> items)
        {
            ClearSlots(isTargetSlots: true);

            int index = 0;
            foreach (var kvp in items)
            {
                if (index >= MaxCartSlots) break;

                ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(kvp.Key);
                if (itemData != null)
                {
                    int itemID = kvp.Key;
                    // 슬롯 생성 (핸들러 없이)
                    var slot = CreateSlotWithoutHandler(cartGridContainer, itemData, kvp.Value, true);

                    // 이제 슬롯이 존재하므로 핸들러 등록
                    if (slot != null)
                    {
                        Action handler = () => HandleSlotClicked(itemID, true, slot);
                        slot.OnSlotClicked += handler;
                        slotClickHandlers[slot] = handler;
                    }

                    int row = index / CartGridCols;
                    int col = index % CartGridCols;
                    if (row < CartGridRows && slot != null)
                        cartGridButtons[row, col] = slot.GetButton();

                    index++;
                }
            }

            for (int i = index; i < MaxCartSlots; i++)
            {
                CreateEmptySlot(cartGridContainer, true);
                int row = i / CartGridCols;
                int col = i % CartGridCols;
                if (row < CartGridRows && cartSlots.Count > 0)
                    cartGridButtons[row, col] = cartSlots[cartSlots.Count - 1].GetButton();
            }

            cartItemCount = items.Count;

            // 슬롯 재생성 후 선택 복원
            SelectCurrentButton();
        }

        public void UpdatePriceDisplay(int totalPrice, int tradingFee, int finalPrice)
        {
            if (totalPriceText != null)
                totalPriceText.text = totalPrice.ToString();
            if (tradingFeeText != null)
                tradingFeeText.text = $"+{tradingFee}";
            if (finalPriceText != null)
                finalPriceText.text = finalPrice.ToString();
        }

        public void UpdateWeightGauge(float maxWeight, float currentWeight, float increasedWeight)
        {
            if (weightGauge != null)
            {
                weightGauge.UpdateGauge(maxWeight, currentWeight, increasedWeight);
            }
        }

        public void ShowMessage(string message, Action onConfirm = null)
        {
            UIManager.Instance.OpenPanel<SystemMessagePanel>()?.SetupAlert(message, onConfirm);
        }
        #endregion

        #region Slot Management
        private void CreateSlot(Transform container, ItemData itemData, int quantity, bool isTargetSlot, Action onClick)
        {
            var slot = CreateSlotWithoutHandler(container, itemData, quantity, isTargetSlot);
            if (slot != null && onClick != null)
            {
                slot.OnSlotClicked += onClick;
                slotClickHandlers[slot] = onClick;
            }
        }

        /// <summary>
        /// 핸들러 없이 슬롯 생성 (나중에 핸들러 추가 필요)
        /// </summary>
        private ShopSlotUI CreateSlotWithoutHandler(Transform container, ItemData itemData, int quantity, bool isTargetSlot)
        {
            GameObject slotObj = Instantiate(slotPrefab, container);
            ShopSlotUI slot = slotObj.GetComponent<ShopSlotUI>();

            if (slot != null)
            {
                slot.Initialize(itemData, quantity, isTargetSlot, MarketTown);

                if (isTargetSlot)
                    cartSlots.Add(slot);
                else
                    shopSlots.Add(slot);
            }

            return slot;
        }

        private void CreateEmptySlot(Transform container, bool isTargetSlot)
        {
            GameObject slotObj = Instantiate(slotPrefab, container);
            ShopSlotUI slot = slotObj.GetComponent<ShopSlotUI>();

            if (slot != null)
            {
                slot.InitializeEmpty();

                if (isTargetSlot)
                    cartSlots.Add(slot);
                else
                    shopSlots.Add(slot);
            }
        }

        public void ChangeSlotsInfoType()
        {
            foreach (var slot in shopSlots)
                slot?.ChangeInfoType();
        }

        private void ClearSlots(bool isTargetSlots)
        {
            List<ShopSlotUI> slots = isTargetSlots ? cartSlots : shopSlots;

            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    if (slotClickHandlers.TryGetValue(slot, out Action handler))
                    {
                        slot.OnSlotClicked -= handler;
                        slotClickHandlers.Remove(slot);
                    }
                    Destroy(slot.gameObject);
                }
            }

            slots.Clear();
        }

        private void ClearAllSlots()
        {
            // 소스, 타겟 슬롯 모두 제거
            ClearSlots(false);
            ClearSlots(true);
            slotClickHandlers.Clear();
        }

        private void HandleSlotClicked(int itemID, bool isCartSlot, ShopSlotUI slot = null)
        {
            if (isCartSlot)
            {
                Vector2 position = slot != null ? slot.GetSlotScreenPosition() : Vector2.zero;
                OnCartSlotClicked?.Invoke(itemID, position);
            }
            else
                OnSlotClicked?.Invoke(itemID);
        }

        public bool? SetQuantityToCart(int itemID, int quantity)
        {
            return presenter?.SetCartQuantity(itemID, quantity);
        }
        #endregion

        #region Navigation
        private int GetShopGridIndex(Vector2Int pos) => pos.x * ShopGridCols + pos.y;
        private int GetCartGridIndex(Vector2Int pos) => pos.x * CartGridCols + pos.y;

        private void SelectCurrentButton()
        {
            Button targetButton = null;

            switch (currentArea)
            {
                case NavigationArea.ShopGrid:
                    if (IsValidShopGridPosition(currentGridPos))
                        targetButton = shopGridButtons[currentGridPos.x, currentGridPos.y];
                    break;
                case NavigationArea.CartGrid:
                    if (IsValidCartGridPosition(currentGridPos))
                        targetButton = cartGridButtons[currentGridPos.x, currentGridPos.y];
                    break;
                case NavigationArea.CloseButton:
                    targetButton = cancelButton;
                    break;
                case NavigationArea.PurchaseButton:
                    targetButton = purchaseButton;
                    break;
            }

            if (targetButton != null && targetButton.interactable)
                targetButton.Select();
        }

        private bool IsValidShopGridPosition(Vector2Int pos)
        {
            if (pos.x < 0 || pos.x >= ShopGridRows) return false;
            if (pos.y < 0 || pos.y >= ShopGridCols) return false;
            return GetShopGridIndex(pos) < shopItemCount;
        }

        private bool IsValidCartGridPosition(Vector2Int pos)
        {
            if (pos.x < 0 || pos.x >= CartGridRows) return false;
            if (pos.y < 0 || pos.y >= CartGridCols) return false;
            return GetCartGridIndex(pos) < cartItemCount;
        }

        public override void NavigateToUpperButton()
        {
            switch (currentArea)
            {
                case NavigationArea.ShopGrid:
                    if (currentGridPos.x > 0)
                        currentGridPos.x--;
                    break;
                case NavigationArea.CartGrid:
                    if (currentGridPos.x > 0)
                        currentGridPos.x--;
                    break;
                case NavigationArea.CloseButton:
                case NavigationArea.PurchaseButton:
                    if (cartItemCount > 0)
                    {
                        currentArea = NavigationArea.CartGrid;
                        int lastIndex = cartItemCount - 1;
                        currentGridPos.x = lastIndex / CartGridCols;
                        currentGridPos.y = lastIndex % CartGridCols;
                    }
                    else if (shopItemCount > 0)
                    {
                        currentArea = NavigationArea.ShopGrid;
                        int lastIndex = shopItemCount - 1;
                        currentGridPos.x = lastIndex / ShopGridCols;
                        currentGridPos.y = lastIndex % ShopGridCols;
                    }
                    break;
            }
            SelectCurrentButton();
        }

        public override void NavigateToLowerButton()
        {
            switch (currentArea)
            {
                case NavigationArea.ShopGrid:
                    int lastItemRow = shopItemCount > 0 ? (shopItemCount - 1) / ShopGridCols : 0;
                    if (currentGridPos.x < lastItemRow)
                        currentGridPos.x++;
                    else
                        currentArea = NavigationArea.CloseButton;
                    break;
                case NavigationArea.CartGrid:
                    lastItemRow = cartItemCount > 0 ? (cartItemCount - 1) / CartGridCols : 0;
                    if (currentGridPos.x < lastItemRow)
                        currentGridPos.x++;
                    else
                        currentArea = NavigationArea.CloseButton;
                    break;
            }
            SelectCurrentButton();
        }

        public override void NavigateToLeftButton()
        {
            switch (currentArea)
            {
                case NavigationArea.ShopGrid:
                    if (currentGridPos.y > 0)
                        currentGridPos.y--;
                    break;
                case NavigationArea.CartGrid:
                    if (currentGridPos.y == 0)
                    {
                        currentArea = NavigationArea.ShopGrid;
                        currentGridPos.x = 0;
                        currentGridPos.y = ShopGridCols - 1;
                    }
                    else
                        currentGridPos.y--;
                    break;
                case NavigationArea.CloseButton:
                    if (shopItemCount > 0)
                    {
                        currentArea = NavigationArea.ShopGrid;
                        int lastShopIndex = shopItemCount - 1;
                        currentGridPos.x = lastShopIndex / ShopGridCols;
                        currentGridPos.y = lastShopIndex % ShopGridCols;
                    }
                    break;
                case NavigationArea.PurchaseButton:
                    currentArea = NavigationArea.CloseButton;
                    break;
            }
            SelectCurrentButton();
        }

        public override void NavigateToRightButton()
        {
            switch (currentArea)
            {
                case NavigationArea.ShopGrid:
                    if (currentGridPos.y == ShopGridCols - 1)
                    {
                        if (cartItemCount > 0)
                        {
                            currentArea = NavigationArea.CartGrid;
                            currentGridPos = Vector2Int.zero;
                        }
                    }
                    else
                        currentGridPos.y++;
                    break;
                case NavigationArea.CartGrid:
                    if (currentGridPos.y < CartGridCols - 1)
                        currentGridPos.y++;
                    break;
                case NavigationArea.CloseButton:
                    currentArea = NavigationArea.PurchaseButton;
                    break;
            }
            SelectCurrentButton();
        }

        public override void ClickSelectedButton()
        {
            Button targetButton = null;

            switch (currentArea)
            {
                case NavigationArea.ShopGrid:
                    if (IsValidShopGridPosition(currentGridPos))
                        targetButton = shopGridButtons[currentGridPos.x, currentGridPos.y];
                    break;
                case NavigationArea.CartGrid:
                    if (IsValidCartGridPosition(currentGridPos))
                        targetButton = cartGridButtons[currentGridPos.x, currentGridPos.y];
                    break;
                case NavigationArea.CloseButton:
                    targetButton = cancelButton;
                    break;
                case NavigationArea.PurchaseButton:
                    targetButton = purchaseButton;
                    break;
            }

            if (targetButton != null && targetButton.interactable)
                targetButton.onClick.Invoke();
        }
        #endregion


    }
}
