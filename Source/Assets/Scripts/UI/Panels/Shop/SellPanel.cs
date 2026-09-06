using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.Data;
using SeaVillage.UI.Shop;

namespace SeaVillage.UI
{
    /// <summary>
    /// 상점 - 판매 패널 (View)
    /// </summary>
    public class SellPanel : UIPanel, ISellView, IContextualPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button sellButton;
        [SerializeField] private Button changeInfoTypeButton;
        
        [Header("Inventory Grids")]
        [SerializeField] private Transform playerInventoryGridContainer;
        [SerializeField] private Transform shipInventoryGridContainer;
        [SerializeField] private Transform sellListGridContainer;
        [SerializeField, Space(5)] private GameObject slotPrefab;

        [Space(10)]
        [SerializeField] private TextMeshProUGUI sellPriceText;
        [SerializeField] private TextMeshProUGUI tradingFeeText;
        [SerializeField] private TextMeshProUGUI totalGainedAmountText;
        [SerializeField] private TextMeshProUGUI netProfitText;

        // Grid Configuration
        private const int PlayerInventoryGridRows = 2;
        private const int PlayerInventoryGridCols = 5;
        private const int ShipInventoryGridRows = 2;
        private const int ShipInventoryGridCols = 5;
        private const int SellListGridRows = 2;
        private const int SellListGridCols = 3;
        private const int MaxPlayerSlots = PlayerInventoryGridRows * PlayerInventoryGridCols;
        private const int MaxShipSlots = ShipInventoryGridRows * ShipInventoryGridCols;
        private const int MaxSellListSlots = SellListGridRows * SellListGridCols;

        // MVP
        private SellPresenter presenter;
        public SellPresenter Presenter => presenter;

        // Slot Management
        private Button[,] playerInventoryGridButtons;
        private Button[,] shipInventoryGridButtons;
        private Button[,] sellListGridButtons;
        private readonly List<ShopSlotUI> playerInventorySlots = new List<ShopSlotUI>();
        private readonly List<ShopSlotUI> shipInventorySlots = new List<ShopSlotUI>();
        private readonly List<ShopSlotUI> sellListSlots = new List<ShopSlotUI>();
        private readonly Dictionary<ShopSlotUI, Action> slotClickHandlers = new Dictionary<ShopSlotUI, Action>();

        // Navigation State
        private Vector2Int currentGridPos = Vector2Int.zero;
        private NavigationArea currentArea = NavigationArea.PlayerInventory;
        private int playerInventoryItemCount;
        private int shipInventoryItemCount;
        private int sellListItemCount;
        private string contextMarketTown;
        public string MarketTown => string.IsNullOrEmpty(contextMarketTown) ? ShopUtility.GetCurrentTownStorageKey() : contextMarketTown;

        #region ISellView Events
        public event Action OnConfirmButtonClicked;
        public event Action<int, Vector2> OnCartSlotClicked;
        #endregion

        public void SetMarketTown(string marketTown)
        {
            contextMarketTown = TownKeyUtility.NormalizeStorageKey(marketTown);
            presenter?.SetMarketTown(MarketTown);
        }

        #region Panel Methods
        public override void OnOpen()
        {
            base.OnOpen();
            
            // 그리드 버튼 배열 초기화
            playerInventoryGridButtons = new Button[PlayerInventoryGridRows, PlayerInventoryGridCols];
            shipInventoryGridButtons = new Button[ShipInventoryGridRows, ShipInventoryGridCols];
            sellListGridButtons = new Button[SellListGridRows, SellListGridCols];
            
            currentArea = NavigationArea.PlayerInventory;
            currentGridPos = Vector2Int.zero;
            
            presenter = new SellPresenter(this, MarketTown);
            presenter.Initialize();
        }

        public override void OnClose()
        {
            base.OnClose();

            ClearAllSlots();
            presenter?.Dispose();
            presenter = null;
            contextMarketTown = string.Empty;
        }
        #endregion

        #region Listeners
        protected override void AddListeners()
        {
            if (cancelButton != null)
                cancelButton.onClick.AddListener(Close);

            if (sellButton != null)
                sellButton.onClick.AddListener(HandleSellButtonClicked);

            if (changeInfoTypeButton != null)
                changeInfoTypeButton.onClick.AddListener(ChangeSlotsInfoType);
        }

        protected override void RemoveListeners()
        {
            if (cancelButton != null)
                cancelButton.onClick.RemoveAllListeners();
                
            if (sellButton != null)
                sellButton.onClick.RemoveAllListeners();

            if (changeInfoTypeButton != null)
                changeInfoTypeButton.onClick.RemoveListener(ChangeSlotsInfoType);
        }

        private void HandleSellButtonClicked()
        {
            OnConfirmButtonClicked?.Invoke();
        }
        #endregion

        #region ISellView Implementation
        public void RefreshPlayerInventorySlots(IReadOnlyDictionary<int, int> items)
        {
            ClearSlotsFromList(playerInventorySlots);
            
            int index = 0;
            foreach (var kvp in items)
            {
                if (index >= MaxPlayerSlots) break;
                
                ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(kvp.Key);
                if (itemData != null)
                {
                    int itemID = kvp.Key;
                    CreateSlotInternal(playerInventoryGridContainer, itemData, kvp.Value, false, 
                        () => HandleInventorySlotClicked(itemID, SlotType.PlayerInventory), playerInventorySlots);
                    
                    int row = index / PlayerInventoryGridCols;
                    int col = index % PlayerInventoryGridCols;
                    if (row < PlayerInventoryGridRows && playerInventorySlots.Count > 0)
                        playerInventoryGridButtons[row, col] = playerInventorySlots[playerInventorySlots.Count - 1].GetButton();
                    
                    index++;
                }
            }
            
            // 빈 슬롯 채우기
            for (int i = index; i < MaxPlayerSlots; i++)
            {
                CreateEmptySlotInternal(playerInventoryGridContainer, playerInventorySlots);
                int row = i / PlayerInventoryGridCols;
                int col = i % PlayerInventoryGridCols;
                if (row < PlayerInventoryGridRows && playerInventorySlots.Count > 0)
                    playerInventoryGridButtons[row, col] = playerInventorySlots[playerInventorySlots.Count - 1].GetButton();
            }
            
            playerInventoryItemCount = items.Count;

            // 슬롯 재생성 후 선택 복원
            SelectCurrentButton();
        }

        public void RefreshShipInventorySlots(IReadOnlyDictionary<int, int> items)
        {
            ClearSlotsFromList(shipInventorySlots);
            
            int index = 0;
            foreach (var kvp in items)
            {
                if (index >= MaxShipSlots) break;
                
                ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(kvp.Key);
                if (itemData != null)
                {
                    int itemID = kvp.Key;
                    CreateSlotInternal(shipInventoryGridContainer, itemData, kvp.Value, false,
                        () => HandleInventorySlotClicked(itemID, SlotType.ShipInventory), shipInventorySlots);
                    
                    int row = index / ShipInventoryGridCols;
                    int col = index % ShipInventoryGridCols;
                    if (row < ShipInventoryGridRows && shipInventorySlots.Count > 0)
                        shipInventoryGridButtons[row, col] = shipInventorySlots[shipInventorySlots.Count - 1].GetButton();
                    
                    index++;
                }
            }
            
            // 빈 슬롯 채우기
            for (int i = index; i < MaxShipSlots; i++)
            {
                CreateEmptySlotInternal(shipInventoryGridContainer, shipInventorySlots);
                int row = i / ShipInventoryGridCols;
                int col = i % ShipInventoryGridCols;
                if (row < ShipInventoryGridRows && shipInventorySlots.Count > 0)
                    shipInventoryGridButtons[row, col] = shipInventorySlots[shipInventorySlots.Count - 1].GetButton();
            }
            
            shipInventoryItemCount = items.Count;

            // 슬롯 재생성 후 선택 복원
            SelectCurrentButton();
        }

        public void RefreshSellListSlots(IReadOnlyDictionary<int, int> items)
        {
            ClearSlotsFromList(sellListSlots);
            
            int index = 0;
            foreach (var kvp in items)
            {
                if (index >= MaxSellListSlots) break;
                
                ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(kvp.Key);
                if (itemData != null)
                {
                    int itemID = kvp.Key;
                    // 슬롯 생성 (핸들러 없이)
                    var slot = CreateSlotInternalWithoutHandler(sellListGridContainer, itemData, kvp.Value, true, sellListSlots);
                    
                    // 이제 슬롯이 존재하므로 핸들러 등록
                    if (slot != null)
                    {
                        Action handler = () => HandleSellListSlotClicked(itemID, slot);
                        slot.OnSlotClicked += handler;
                        slotClickHandlers[slot] = handler;
                    }
                    
                    int row = index / SellListGridCols;
                    int col = index % SellListGridCols;
                    if (row < SellListGridRows && slot != null)
                        sellListGridButtons[row, col] = slot.GetButton();
                    
                    index++;
                }
            }
            
            // 빈 슬롯 채우기
            for (int i = index; i < MaxSellListSlots; i++)
            {
                CreateEmptySlotInternal(sellListGridContainer, sellListSlots);
                int row = i / SellListGridCols;
                int col = i % SellListGridCols;
                if (row < SellListGridRows && sellListSlots.Count > 0)
                    sellListGridButtons[row, col] = sellListSlots[sellListSlots.Count - 1].GetButton();
            }
            
            sellListItemCount = items.Count;

            // 슬롯 재생성 후 선택 복원
            SelectCurrentButton();
        }

        public void UpdatePriceDisplay(int totalPrice, int tradingFee, int finalPrice, int purchasePrice)
        {
            if (sellPriceText != null)
                sellPriceText.text = totalPrice.ToString();
            if (tradingFeeText != null)
                tradingFeeText.text = tradingFee.ToString();
            if (totalGainedAmountText != null)
                totalGainedAmountText.text = finalPrice.ToString();
            if (netProfitText != null)
            {
                int netProfit = finalPrice - purchasePrice;
                netProfitText.text = netProfit.ToString();
            }
        }

        public void ShowMessage(string message, Action onConfirm = null)
        {
            UIManager.Instance.OpenPanel<SystemMessagePanel>()?.SetupAlert(message, onConfirm);
        }

        #endregion

        #region Slot Management
        private ShopSlotUI CreateSlotInternal(Transform container, ItemData itemData, int quantity, bool isTargetSlot, Action onClick, List<ShopSlotUI> slotList)
        {
            GameObject slotObj = Instantiate(slotPrefab, container);
            ShopSlotUI slot = slotObj.GetComponent<ShopSlotUI>();
            
            if (slot != null)
            {
                slot.Initialize(itemData, quantity, isTargetSlot, MarketTown);
                
                Action handler = onClick;
                slot.OnSlotClicked += handler;
                slotClickHandlers[slot] = handler;
                
                slotList.Add(slot);
            }
            
            return slot;
        }

        /// <summary>
        /// 핸들러 없이 슬롯 생성 (나중에 핸들러 추가 필요)
        /// </summary>
        private ShopSlotUI CreateSlotInternalWithoutHandler(Transform container, ItemData itemData, int quantity, bool isTargetSlot, List<ShopSlotUI> slotList)
        {
            GameObject slotObj = Instantiate(slotPrefab, container);
            ShopSlotUI slot = slotObj.GetComponent<ShopSlotUI>();
            
            if (slot != null)
            {
                slot.Initialize(itemData, quantity, isTargetSlot, MarketTown);
                slotList.Add(slot);
            }
            
            return slot;
        }

        private void CreateEmptySlotInternal(Transform container, List<ShopSlotUI> slotList)
        {
            GameObject slotObj = Instantiate(slotPrefab, container);
            ShopSlotUI slot = slotObj.GetComponent<ShopSlotUI>();
            
            if (slot != null)
            {
                slot.InitializeEmpty();
                slotList.Add(slot);
            }
        }

        private void ClearSlotsFromList(List<ShopSlotUI> slots)
        {
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
            ClearSlotsFromList(playerInventorySlots);
            ClearSlotsFromList(shipInventorySlots);
            ClearSlotsFromList(sellListSlots);
            slotClickHandlers.Clear();
        }
        #endregion

        #region Slot Click Handling
        // 인벤토리 슬롯에서는 해당 인벤토리의 보유 수량까지만 담을 수 있다
        private void HandleInventorySlotClicked(int itemID, SlotType sourceType)
        {
            ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(itemID);
            int maxQuantity = presenter?.GetOriginalMaxQuantity(itemID, sourceType) ?? 0;

            if (itemData != null)
            {
                UIManager.Instance.OpenPanel<SellInformationPanel>()?.InitializeSellInformation(itemData, maxQuantity, sourceType, MarketTown);
            }
        }

        private void HandleSellListSlotClicked(int itemID, ShopSlotUI slot)
        {
            Vector2 position = slot != null ? slot.GetSlotScreenPosition() : Vector2.zero;
            OnCartSlotClicked?.Invoke(itemID, position);
        }

        /// <summary>
        /// 지정한 출처 인벤토리에서 담을 수량 설정
        /// </summary>
        public void SetQuantityToSellList(int itemID, int quantity, SlotType sourceType)
        {
            presenter?.SetSellQuantity(itemID, quantity, sourceType);
        }

        /// <summary>
        /// 판매 목록의 총 수량 설정
        /// </summary>
        public void SetTotalQuantityToSellList(int itemID, int quantity)
        {
            presenter?.SetSellTotalQuantity(itemID, quantity);
        }
        #endregion

        public void ChangeSlotsInfoType()
        {
            foreach (var slot in playerInventorySlots)
                slot?.ChangeInfoType();

            foreach (var slot in shipInventorySlots)
                slot?.ChangeInfoType();
        }

        #region Navigation Helpers
        private int GetPlayerInventoryGridIndex(Vector2Int pos) => pos.x * PlayerInventoryGridCols + pos.y;
        private int GetShipInventoryGridIndex(Vector2Int pos) => pos.x * ShipInventoryGridCols + pos.y;
        private int GetSellListGridIndex(Vector2Int pos) => pos.x * SellListGridCols + pos.y;

        private void SelectCurrentButton()
        {
            Button targetButton = null;

            switch (currentArea)
            {
                case NavigationArea.PlayerInventory:
                    if (IsValidPlayerInventoryGridPosition(currentGridPos))
                        targetButton = playerInventoryGridButtons[currentGridPos.x, currentGridPos.y];
                    break;
                case NavigationArea.ShipInventory:
                    if (IsValidShipInventoryGridPosition(currentGridPos))
                        targetButton = shipInventoryGridButtons[currentGridPos.x, currentGridPos.y];
                    break;
                case NavigationArea.SellListGrid:
                    if (IsValidSellListGridPosition(currentGridPos))
                        targetButton = sellListGridButtons[currentGridPos.x, currentGridPos.y];
                    break;
                case NavigationArea.CancelButton:
                    targetButton = cancelButton;
                    break;
                case NavigationArea.SellButton:
                    targetButton = sellButton;
                    break;
            }

            if (targetButton != null && targetButton.interactable)
                targetButton.Select();
        }

        private bool IsValidPlayerInventoryGridPosition(Vector2Int pos)
        {
            if (pos.x < 0 || pos.x >= PlayerInventoryGridRows) return false;
            if (pos.y < 0 || pos.y >= PlayerInventoryGridCols) return false;
            return GetPlayerInventoryGridIndex(pos) < playerInventoryItemCount;
        }

        private bool IsValidShipInventoryGridPosition(Vector2Int pos)
        {
            if (pos.x < 0 || pos.x >= ShipInventoryGridRows) return false;
            if (pos.y < 0 || pos.y >= ShipInventoryGridCols) return false;
            return GetShipInventoryGridIndex(pos) < shipInventoryItemCount;
        }

        private bool IsValidSellListGridPosition(Vector2Int pos)
        {
            if (pos.x < 0 || pos.x >= SellListGridRows) return false;
            if (pos.y < 0 || pos.y >= SellListGridCols) return false;
            return GetSellListGridIndex(pos) < sellListItemCount;
        }
        #endregion

        #region Navigation
        public override void NavigateToUpperButton()
        {
            switch (currentArea)
            {
                case NavigationArea.PlayerInventory:
                    if (currentGridPos.x > 0)
                        currentGridPos.x--;
                    break;
                case NavigationArea.ShipInventory:
                    if (currentGridPos.x == 0)
                    {
                        if (playerInventoryItemCount > 0)
                        {
                            currentArea = NavigationArea.PlayerInventory;
                            currentGridPos.x = PlayerInventoryGridRows - 1;
                            if (!IsValidPlayerInventoryGridPosition(currentGridPos))
                            {
                                int lastIndex = playerInventoryItemCount - 1;
                                currentGridPos.x = lastIndex / PlayerInventoryGridCols;
                                currentGridPos.y = lastIndex % PlayerInventoryGridCols;
                            }
                        }
                    }
                    else
                        currentGridPos.x--;
                    break;
                case NavigationArea.SellListGrid:
                    if (currentGridPos.x > 0)
                        currentGridPos.x--;
                    break;
                case NavigationArea.CancelButton:
                case NavigationArea.SellButton:
                    if (sellListItemCount > 0)
                    {
                        currentArea = NavigationArea.SellListGrid;
                        int lastIndex = sellListItemCount - 1;
                        currentGridPos.x = lastIndex / SellListGridCols;
                        currentGridPos.y = lastIndex % SellListGridCols;
                    }
                    else if (shipInventoryItemCount > 0)
                    {
                        currentArea = NavigationArea.ShipInventory;
                        int lastIndex = shipInventoryItemCount - 1;
                        currentGridPos.x = lastIndex / ShipInventoryGridCols;
                        currentGridPos.y = lastIndex % ShipInventoryGridCols;
                    }
                    else if (playerInventoryItemCount > 0)
                    {
                        currentArea = NavigationArea.PlayerInventory;
                        int lastIndex = playerInventoryItemCount - 1;
                        currentGridPos.x = lastIndex / PlayerInventoryGridCols;
                        currentGridPos.y = lastIndex % PlayerInventoryGridCols;
                    }
                    break;
            }
            SelectCurrentButton();
        }

        public override void NavigateToLowerButton()
        {
            switch (currentArea)
            {
                case NavigationArea.PlayerInventory:
                    if (currentGridPos.x == PlayerInventoryGridRows - 1)
                    {
                        if (shipInventoryItemCount > 0)
                        {
                            currentArea = NavigationArea.ShipInventory;
                            currentGridPos.x = 0;
                            if (!IsValidShipInventoryGridPosition(currentGridPos))
                                currentGridPos = Vector2Int.zero;
                        }
                        else
                            currentArea = NavigationArea.CancelButton;
                    }
                    else
                    {
                        Vector2Int nextPos = new Vector2Int(currentGridPos.x + 1, currentGridPos.y);
                        if (IsValidPlayerInventoryGridPosition(nextPos))
                            currentGridPos.x++;
                        else if (shipInventoryItemCount > 0)
                        {
                            currentArea = NavigationArea.ShipInventory;
                            currentGridPos.x = 0;
                            if (!IsValidShipInventoryGridPosition(currentGridPos))
                                currentGridPos = Vector2Int.zero;
                        }
                        else
                            currentArea = NavigationArea.CancelButton;
                    }
                    break;
                case NavigationArea.ShipInventory:
                    if (currentGridPos.x < ShipInventoryGridRows - 1)
                    {
                        Vector2Int nextPos = new Vector2Int(currentGridPos.x + 1, currentGridPos.y);
                        if (IsValidShipInventoryGridPosition(nextPos))
                            currentGridPos.x++;
                        else
                            currentArea = NavigationArea.CancelButton;
                    }
                    else
                        currentArea = NavigationArea.CancelButton;
                    break;
                case NavigationArea.SellListGrid:
                    if (currentGridPos.x < SellListGridRows - 1)
                    {
                        Vector2Int nextPos = new Vector2Int(currentGridPos.x + 1, currentGridPos.y);
                        if (IsValidSellListGridPosition(nextPos))
                            currentGridPos.x++;
                        else
                            currentArea = NavigationArea.CancelButton;
                    }
                    else
                        currentArea = NavigationArea.CancelButton;
                    break;
            }
            SelectCurrentButton();
        }

        public override void NavigateToLeftButton()
        {
            switch (currentArea)
            {
                case NavigationArea.PlayerInventory:
                    if (currentGridPos.y > 0)
                        currentGridPos.y--;
                    break;
                case NavigationArea.ShipInventory:
                    if (currentGridPos.y > 0)
                        currentGridPos.y--;
                    break;
                case NavigationArea.SellListGrid:
                    if (currentGridPos.y == 0)
                    {
                        currentArea = NavigationArea.PlayerInventory;
                        currentGridPos.x = 0;
                        currentGridPos.y = PlayerInventoryGridCols - 1;
                    }
                    else
                        currentGridPos.y--;
                    break;
                case NavigationArea.CancelButton:
                    if (shipInventoryItemCount > 0)
                    {
                        currentArea = NavigationArea.ShipInventory;
                        int lastIndex = shipInventoryItemCount - 1;
                        currentGridPos.x = lastIndex / ShipInventoryGridCols;
                        currentGridPos.y = lastIndex % ShipInventoryGridCols;
                    }
                    else if (playerInventoryItemCount > 0)
                    {
                        currentArea = NavigationArea.PlayerInventory;
                        int lastIndex = playerInventoryItemCount - 1;
                        currentGridPos.x = lastIndex / PlayerInventoryGridCols;
                        currentGridPos.y = lastIndex % PlayerInventoryGridCols;
                    }
                    break;
                case NavigationArea.SellButton:
                    currentArea = NavigationArea.CancelButton;
                    break;
            }
            SelectCurrentButton();
        }

        public override void NavigateToRightButton()
        {
            switch (currentArea)
            {
                case NavigationArea.PlayerInventory:
                    if (currentGridPos.y == PlayerInventoryGridCols - 1)
                    {
                        if (sellListItemCount > 0)
                        {
                            currentArea = NavigationArea.SellListGrid;
                            currentGridPos = Vector2Int.zero;
                        }
                    }
                    else
                    {
                        Vector2Int nextPos = new Vector2Int(currentGridPos.x, currentGridPos.y + 1);
                        if (IsValidPlayerInventoryGridPosition(nextPos))
                            currentGridPos.y++;
                    }
                    break;
                case NavigationArea.ShipInventory:
                    if (currentGridPos.y == ShipInventoryGridCols - 1)
                    {
                        if (sellListItemCount > 0)
                        {
                            currentArea = NavigationArea.SellListGrid;
                            currentGridPos = Vector2Int.zero;
                        }
                    }
                    else
                    {
                        Vector2Int nextPos = new Vector2Int(currentGridPos.x, currentGridPos.y + 1);
                        if (IsValidShipInventoryGridPosition(nextPos))
                            currentGridPos.y++;
                    }
                    break;
                case NavigationArea.SellListGrid:
                    if (currentGridPos.y < SellListGridCols - 1)
                    {
                        Vector2Int nextPos = new Vector2Int(currentGridPos.x, currentGridPos.y + 1);
                        if (IsValidSellListGridPosition(nextPos))
                            currentGridPos.y++;
                    }
                    break;
                case NavigationArea.CancelButton:
                    currentArea = NavigationArea.SellButton;
                    break;
            }
            SelectCurrentButton();
        }
        #endregion

        public override void ClickSelectedButton()
        {
            Button targetButton = null;

            switch (currentArea)
            {
                case NavigationArea.PlayerInventory:
                    if (IsValidPlayerInventoryGridPosition(currentGridPos))
                        targetButton = playerInventoryGridButtons[currentGridPos.x, currentGridPos.y];
                    break;
                case NavigationArea.ShipInventory:
                    if (IsValidShipInventoryGridPosition(currentGridPos))
                        targetButton = shipInventoryGridButtons[currentGridPos.x, currentGridPos.y];
                    break;
                case NavigationArea.SellListGrid:
                    if (IsValidSellListGridPosition(currentGridPos))
                        targetButton = sellListGridButtons[currentGridPos.x, currentGridPos.y];
                    break;
                case NavigationArea.CancelButton:
                    targetButton = cancelButton;
                    break;
                case NavigationArea.SellButton:
                    targetButton = sellButton;
                    break;
            }

            if (targetButton != null && targetButton.interactable)
                targetButton.onClick.Invoke();
        }


    }
}
