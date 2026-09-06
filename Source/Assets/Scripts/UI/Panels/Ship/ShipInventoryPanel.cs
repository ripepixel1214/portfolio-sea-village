using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.Data;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    /// <summary>
    /// 배 - 인벤토리 패널
    /// </summary>
    public class ShipInventoryPanel : UIPanel, IContextualPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        [Header("Inventory Grids")]
        [SerializeField] private Transform shipInventoryContainer;
        [SerializeField] private Transform playerInventoryContainer;
        [SerializeField, Space(5)] private GameObject slotPrefab;

        [Header("Inventory Weight Texts")]
        [SerializeField] private TextMeshProUGUI shipInventoryWeigthText;
        [SerializeField] private TextMeshProUGUI foodText;
        [SerializeField] private TextMeshProUGUI playerInventoryWeigthText;

        [Header("Gauges")]
        [SerializeField] private Gauge shipInventoryGauge;
        [SerializeField] private Gauge foodGauge;
        [SerializeField] private Gauge playerInventoryGauge;

        [Header("Doll Visuals")]
        [SerializeField] private Image playerImage;

        // References - InventoryManager를 통해 접근
        private InventoryData ShipInventory => InventoryManager.ShipInventoryOrNull;
        private InventoryData PlayerInventory => InventoryManager.PlayerInventoryOrNull;

        [SerializeField] private Button changeInfoTypeButton;

        // Grid Configuration
        private const int DefaultGridRows = 4;
        private const int DefaultGridCols = 4;
        private int shipGridRows = DefaultGridRows;
        private int playerGridRows = DefaultGridRows;

        private Button selectedButton;

        // Slot Management
        private int shipItemCount;
        private int playerItemCount;
        private Button[,] shipInventoryButtons;
        private Button[,] playerInventoryButtons;
        private List<InventorySlotUI> shipInventorySlots = new List<InventorySlotUI>();
        private List<InventorySlotUI> playerInventorySlots = new List<InventorySlotUI>();
        private bool isSubscribedToInventoryEvents;
        private Sprite defaultPlayerImage;

        // Navigation State
        private Vector2Int currentGridPos = Vector2Int.zero;
        private NavigationArea currentArea = NavigationArea.ShipInventory;

        // Properties
        private int MaxShipSlots => shipGridRows * DefaultGridCols;
        private int MaxPlayerSlots => playerGridRows * DefaultGridCols;

        protected override void AddListeners()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseInventory);

            if (changeInfoTypeButton != null)
                changeInfoTypeButton.onClick.AddListener(ChangeSlotsInfoType);
        }

        protected override void RemoveListeners()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(CloseInventory);

            if (changeInfoTypeButton != null)
                changeInfoTypeButton.onClick.RemoveListener(ChangeSlotsInfoType);
        }

        #region Panel Methods
        public override void OnOpen()
        {
            base.OnOpen();
            TutorialUIBinding.Bind(closeButton, TutorialAnchorKeys.ShipInventoryCloseButton);

            if (playerImage != null && defaultPlayerImage == null)
                defaultPlayerImage = playerImage.sprite;

            SubscribeToInventoryEvents();

            // Ensure gauges are bound to current inventories
            var invManager = InventoryManager.Instance;
            if (shipInventoryGauge != null)
                shipInventoryGauge.SetInventory(invManager != null ? invManager.ShipInventory : null);
            if (playerInventoryGauge != null)
                playerInventoryGauge.SetInventory(invManager != null ? invManager.PlayerInventory : null);

            RefreshAllInventoryDisplay();
        }

        public override void OnClose()
        {
            UnsubscribeFromInventoryEvents();
            base.OnClose();
            ClearShipInventorySlots();
            ClearPlayerInventorySlots();
            // Unbind gauges to avoid stale listeners
            if (shipInventoryGauge != null)
                shipInventoryGauge.SetInventory(null);
            if (playerInventoryGauge != null)
                playerInventoryGauge.SetInventory(null);
        }

        public override void OnFocusRestored()
        {
            base.OnFocusRestored();
            SelectCurrentButton();
        }
        #endregion

        private void SubscribeToInventoryEvents()
        {
            if (isSubscribedToInventoryEvents)
                return;

            var invManager = InventoryManager.Instance;
            if (invManager == null)
                return;

            invManager.OnShipInventoryChanged += RefreshAllInventoryDisplay;
            invManager.OnPlayerInventoryChanged += RefreshAllInventoryDisplay;
            invManager.OnShipFoodStorageChanged += UpdateInventoryWeights;
            invManager.OnShipInventoryReady += RefreshAllInventoryDisplay;
            invManager.OnPlayerInventoryReady += RefreshAllInventoryDisplay;
            isSubscribedToInventoryEvents = true;
        }

        private void UnsubscribeFromInventoryEvents()
        {
            if (!isSubscribedToInventoryEvents)
                return;

            var invManager = InventoryManager.Instance;
            if (invManager != null)
            {
                invManager.OnShipInventoryChanged -= RefreshAllInventoryDisplay;
                invManager.OnPlayerInventoryChanged -= RefreshAllInventoryDisplay;
                invManager.OnShipFoodStorageChanged -= UpdateInventoryWeights;
                invManager.OnShipInventoryReady -= RefreshAllInventoryDisplay;
                invManager.OnPlayerInventoryReady -= RefreshAllInventoryDisplay;
            }

            isSubscribedToInventoryEvents = false;
        }

        private void RefreshAllInventoryDisplay()
        {
            UpdateInventoryWeights();
            InitializeGrid();
            RefreshDollVisuals();
        }

        private void RefreshDollVisuals()
        {
            if (playerImage == null)
                return;

            Sprite dollSprite = DollEffectPolicy.PlayerInventorySpriteOrNull;
            playerImage.sprite = dollSprite != null ? dollSprite : defaultPlayerImage;
        }

        private void UpdateInventoryWeights()
        {
            var invManager = InventoryManager.Instance;
            if (invManager == null)
                return;

            if (shipInventoryWeigthText != null && invManager.ShipInventory != null)
                shipInventoryWeigthText.text = $"{invManager.ShipInventory.CurrentWeight:F1} / {invManager.ShipMaxCapacity:F1} kg";

            // 게이지들 즉시 갱신
            if (shipInventoryGauge != null)
                shipInventoryGauge.RefreshGauge();

            // 배 식량 저장값 업데이트
            UpdateFoodStorage(invManager);

            if (playerInventoryWeigthText != null && invManager.PlayerInventory != null)
                playerInventoryWeigthText.text = $"{invManager.PlayerInventory.CurrentWeight:F1} / {invManager.PlayerInventory.MaxWeight:F1} kg";
            if (playerInventoryGauge != null)
                playerInventoryGauge.RefreshGauge();
        }

        private void UpdateFoodStorage(InventoryManager invManager)
        {
            if (invManager == null) return;

            if (foodText != null)
                foodText.text = $"{invManager.ShipFoodDays}일";

            // 게이지 업데이트 (Gauge.RefreshGauge() 호출)
            if (foodGauge != null)
                foodGauge.RefreshGauge();
        }

        /// <summary>
        /// 배/플레이어 인벤토리 슬롯의 정보 표시 타입을 순환 변경한다.
        /// </summary>
        public void ChangeSlotsInfoType()
        {
            foreach (var slot in shipInventorySlots)
                slot?.ChangeInfoType();

            foreach (var slot in playerInventorySlots)
                slot?.ChangeInfoType();
        }

        #region Initialization
        private void InitializeGrid()
        {
            shipGridRows = DefaultGridRows;
            playerGridRows = DefaultGridRows;

            RefreshShipInventory();
            RefreshPlayerInventory();

            RebuildGridButtons();
            ResolveCurrentNavigationTarget();
            SelectCurrentButton();
        }

        private void RebuildGridButtons()
        {
            shipInventoryButtons = new Button[shipGridRows, DefaultGridCols];
            for (int i = 0; i < shipInventorySlots.Count && i < MaxShipSlots; i++)
            {
                int row = i / DefaultGridCols;
                int col = i % DefaultGridCols;
                if (row < shipGridRows)
                    shipInventoryButtons[row, col] = shipInventorySlots[i]?.GetButton();
            }

            playerInventoryButtons = new Button[playerGridRows, DefaultGridCols];
            for (int i = 0; i < playerInventorySlots.Count && i < MaxPlayerSlots; i++)
            {
                int row = i / DefaultGridCols;
                int col = i % DefaultGridCols;
                if (row < playerGridRows)
                    playerInventoryButtons[row, col] = playerInventorySlots[i]?.GetButton();
            }
        }

        private void ResolveCurrentNavigationTarget()
        {
            if (shipItemCount > 0)
                currentArea = NavigationArea.ShipInventory;
            else if (playerItemCount > 0)
                currentArea = NavigationArea.PlayerInventory;
            else
                currentArea = NavigationArea.CloseButton;

            currentGridPos = Vector2Int.zero;
        }
        #endregion

        #region Ship Inventory
        private void RefreshShipInventory()
        {
            ClearShipInventorySlots();

            if (ShipInventory == null)
            {
                for (int i = 0; i < MaxShipSlots; i++)
                    CreateEmptyShipInventorySlot(i);
                shipItemCount = 0;
                return;
            }

            shipItemCount = ShipInventory.ItemCount;

            if (shipItemCount <= DefaultGridCols * DefaultGridRows)
                shipGridRows = DefaultGridRows;
            else
                shipGridRows = Mathf.CeilToInt((float)shipItemCount / DefaultGridCols);

            int slotIndex = 0;
            foreach (var kvp in ShipInventory.Items)
            {
                if (slotIndex >= MaxShipSlots) break;

                var invItem = kvp.Value;
                ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(invItem.id);

                if (itemData != null)
                    CreateShipInventorySlot(slotIndex++, itemData, invItem.quantity);
            }

            for (int i = slotIndex; i < MaxShipSlots; i++)
                CreateEmptyShipInventorySlot(i);

            shipItemCount = slotIndex;
        }

        private void CreateShipInventorySlot(int index, ItemData itemData, int quantity)
        {
            GameObject slotObj = Instantiate(slotPrefab, shipInventoryContainer);
            slotObj.name = $"ShipItemSlot_{index}";

            InventorySlotUI slot = slotObj.GetComponent<InventorySlotUI>();
            if (slot != null)
            {
                slot.Initialize(itemData, quantity);
                slot.OnSlotClicked += () => OnSlotClicked(slot, MenuBarContext.ShipInventory);
                shipInventorySlots.Add(slot);
            }
        }

        private void CreateEmptyShipInventorySlot(int index)
        {
            GameObject slotObj = Instantiate(slotPrefab, shipInventoryContainer);
            slotObj.name = $"ShipItemSlot_{index}";

            InventorySlotUI slot = slotObj.GetComponent<InventorySlotUI>();
            if (slot != null)
            {
                slot.InitializeEmpty();
                slot.OnSlotClicked += () => OnSlotClicked(slot, MenuBarContext.ShipInventory);
                shipInventorySlots.Add(slot);
            }
        }

        private void ClearShipInventorySlots()
        {
            foreach (var slot in shipInventorySlots)
                if (slot != null)
                    Destroy(slot.gameObject);

            shipInventorySlots.Clear();
            shipGridRows = DefaultGridRows;
        }
        #endregion

        #region Player Inventory
        private void RefreshPlayerInventory()
        {
            ClearPlayerInventorySlots();

            if (PlayerInventory == null)
            {
                for (int i = 0; i < MaxPlayerSlots; i++)
                    CreateEmptyPlayerInventorySlot(i);
                playerItemCount = 0;
                return;
            }

            playerItemCount = PlayerInventory.ItemCount;

            if (playerItemCount <= DefaultGridCols * DefaultGridRows)
                playerGridRows = DefaultGridRows;
            else
                playerGridRows = Mathf.CeilToInt((float)playerItemCount / DefaultGridCols);

            int slotIndex = 0;
            foreach (var kvp in PlayerInventory.Items)
            {
                if (slotIndex >= MaxPlayerSlots) break;

                var invItem = kvp.Value;
                ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(invItem.id);

                if (itemData != null)
                    CreatePlayerInventorySlot(slotIndex++, itemData, invItem.quantity);
            }

            for (int i = slotIndex; i < MaxPlayerSlots; i++)
                CreateEmptyPlayerInventorySlot(i);

            playerItemCount = slotIndex;
        }

        private void CreatePlayerInventorySlot(int index, ItemData itemData, int quantity)
        {
            GameObject slotObj = Instantiate(slotPrefab, playerInventoryContainer);
            slotObj.name = $"PlayerItemSlot_{index}";

            InventorySlotUI slot = slotObj.GetComponent<InventorySlotUI>();
            if (slot != null)
            {
                slot.Initialize(itemData, quantity);
                slot.OnSlotClicked += () => OnSlotClicked(slot, MenuBarContext.PlayerInventory);
                playerInventorySlots.Add(slot);

                if (itemData.ID == TutorialItemIds.Potato)
                    TutorialUIBinding.Bind(slot, TutorialAnchorKeys.InventoryPotatoSlot);
            }
        }

        private void CreateEmptyPlayerInventorySlot(int index)
        {
            GameObject slotObj = Instantiate(slotPrefab, playerInventoryContainer);
            slotObj.name = $"PlayerItemSlot_{index}";

            InventorySlotUI slot = slotObj.GetComponent<InventorySlotUI>();
            if (slot != null)
            {
                slot.InitializeEmpty();
                slot.OnSlotClicked += () => OnSlotClicked(slot, MenuBarContext.PlayerInventory);
                playerInventorySlots.Add(slot);
            }
        }

        private void ClearPlayerInventorySlots()
        {
            foreach (var slot in playerInventorySlots)
                if (slot != null)
                    Destroy(slot.gameObject);

            playerInventorySlots.Clear();
            playerGridRows = DefaultGridRows;
        }
        #endregion

        private void OnSlotClicked(InventorySlotUI slot, MenuBarContext sourceContext)
        {
            if (slot == null || slot.GetItemData() == null) return;

            ItemData itemData = slot.GetItemData();
            if (sourceContext == MenuBarContext.PlayerInventory && itemData.ID == TutorialItemIds.Potato)
                TutorialEventReporter.Report(TutorialEventType.ItemSelected, TutorialTargetIds.InventoryPotato, source: TutorialEventSource.UserInterface);

            bool isFoodItem = itemData.Usage == "Food";

            var menuBarPanel = UIManager.Instance.OpenPanel<MenuBarPanel>();
            if (menuBarPanel != null)
                menuBarPanel.InitializeInventoryMenu(itemData.ID, sourceContext, slot.GetSlotScreenPosition(), isFoodItem, slot.GetQuantity());
            else
                Debug.LogWarning("MenuBarPanel을 열 수 없습니다.");
        }

        private void CloseInventory()
        {
            Close();
            TutorialEventReporter.Report(TutorialEventType.PanelClosed, TutorialTargetIds.ShipInventoryPanel, source: TutorialEventSource.UserInterface);
        }

        private void SelectCurrentButton()
        {
            switch (currentArea)
            {
                case NavigationArea.ShipInventory:
                    if (IsValidGridPosition(currentGridPos, shipGridRows))
                        selectedButton = shipInventoryButtons[currentGridPos.x, currentGridPos.y];
                    break;
                case NavigationArea.PlayerInventory:
                    if (IsValidGridPosition(currentGridPos, playerGridRows))
                        selectedButton = playerInventoryButtons[currentGridPos.x, currentGridPos.y];
                    break;
                case NavigationArea.CloseButton:
                    selectedButton = closeButton;
                    break;
            }

            if (selectedButton != null && selectedButton.interactable)
                selectedButton.Select();
        }

        private bool IsValidGridPosition(Vector2Int pos, int maxRows)
        {
            return pos.x >= 0 && pos.x < maxRows && pos.y >= 0 && pos.y < DefaultGridCols;
        }

        #region Navigation
        public override void NavigateToUpperButton()
        {
            switch (currentArea)
            {
                case NavigationArea.ShipInventory:
                case NavigationArea.PlayerInventory:
                    if (currentGridPos.x > 0)
                        currentGridPos.x--;
                    break;
                case NavigationArea.CloseButton:
                    if (shipItemCount > 0)
                    {
                        currentArea = NavigationArea.ShipInventory;
                        currentGridPos.x = shipGridRows - 1;
                    }
                    else if (playerItemCount > 0)
                    {
                        currentArea = NavigationArea.PlayerInventory;
                        currentGridPos.x = playerGridRows - 1;
                    }
                    break;
            }

            SelectCurrentButton();
        }

        public override void NavigateToLowerButton()
        {
            switch (currentArea)
            {
                case NavigationArea.ShipInventory:
                    if (currentGridPos.x < shipGridRows - 1)
                        currentGridPos.x++;
                    else
                        currentArea = NavigationArea.CloseButton;
                    break;
                case NavigationArea.PlayerInventory:
                    if (currentGridPos.x < playerGridRows - 1)
                        currentGridPos.x++;
                    else
                        currentArea = NavigationArea.CloseButton;
                    break;
                case NavigationArea.CloseButton:
                    break;
            }

            SelectCurrentButton();
        }

        public override void NavigateToLeftButton()
        {
            switch (currentArea)
            {
                case NavigationArea.ShipInventory:
                case NavigationArea.PlayerInventory:
                    if (currentGridPos.y > 0)
                        currentGridPos.y--;
                    else if (currentArea == NavigationArea.PlayerInventory)
                    {
                        if (shipItemCount == 0) break;

                        // 플레이어 인벤토리 좌측 끝 => 배 인벤토리 우측 끝
                        currentArea = NavigationArea.ShipInventory;
                        currentGridPos.y = DefaultGridCols - 1;
                    }
                    break;
                case NavigationArea.CloseButton:
                    break;
            }

            SelectCurrentButton();
        }

        public override void NavigateToRightButton()
        {
            switch (currentArea)
            {
                case NavigationArea.PlayerInventory:
                case NavigationArea.ShipInventory:
                    if (currentGridPos.y < DefaultGridCols - 1)
                        currentGridPos.y++;
                    else if (currentArea == NavigationArea.ShipInventory)
                    {
                        if (playerItemCount == 0) break;

                        // 배 인벤토리 우측 끝 => 플레이어 인벤토리 좌측 끝
                        currentArea = NavigationArea.PlayerInventory;
                        currentGridPos.y = 0;
                    }
                    break;
                case NavigationArea.CloseButton:
                    break;
            }

            SelectCurrentButton();
        }
        #endregion

        public override void ClickSelectedButton()
        {
            if (selectedButton != null && selectedButton.interactable)
                selectedButton.onClick.Invoke();
        }
    }
}
