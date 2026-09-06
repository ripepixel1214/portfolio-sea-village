using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using SeaVillage.Data;
using SeaVillage.Core;
using SeaVillage.UI.Shop;
using TMPro;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    public enum MenuBarContext
    {
        ShipInventory,      // 배 인벤토리
        PlayerInventory,    // 플레이어 인벤토리
        Cart,               // 장바구니
        SellList,           // 판매 목록
        Submission,         // 제출
        ItemSelection,      // 아이템 선택
        PlayerShopStock,    // 내 가게 재고 관리(가격/수량 설정 후 옮기기/선택하기)
    }

    /// <summary>메뉴 바 패널</summary>
    public class MenuBarPanel : UIPanel, IContextualPanel
    {
        [Header("Menu Buttons")]
        [SerializeField] private Button foodRechargeButton;
        [SerializeField] private Button useItemButton;
        [SerializeField] private Button moveAllButton;
        [SerializeField] private Button moveButton;

        // 수량 선택 버튼 (담기)
        [SerializeField] private Button numberSelectionButton;

        [SerializeField] private Button openInformationButton;
        [SerializeField] private Button cancelButton;

        [Header("Information")]
        [SerializeField] private TextMeshProUGUI quantityText;

        [Header("Sub Buttons")]
        // 수량 감소 버튼
        [SerializeField] private Button decreaseButton;
        // 수량 증가 버튼
        [SerializeField] private Button increaseButton;

        [Header("Player Shop - Price Row")]
        // 판매 골드 설정 행(내 가게 재고 관리 전용)
        [SerializeField] private Button priceSelectionButton;
        [SerializeField] private Button priceDecreaseButton;
        [SerializeField] private Button priceIncreaseButton;
        [SerializeField] private TextMeshProUGUI priceText;

        [Header("Panel Settings")]
        [SerializeField] private RectTransform panelRect;

        private bool isFoodItemMenu;
        private bool isTransferMenu;
        private bool isInformationOnlyInventoryMenu;
        private int selectedItemMaxQuantity = 1;
        private MenuBarContext currentContext;
        private Vector2 targetPosition;

        private int currentButtonIndex = 0;
        private int selectedItemID = -1;
        private int currentQuantity = 1;
        private List<Button> buttons;
        private static MenuBarPanel activeInstance;
        private Action<int> submissionAction;
        private Action submissionCancelAction;
        private Action<int> itemSelectionAction;
        private Action itemSelectionCancelAction;
        private string itemSelectionActionLabel = "선택";
        private string marketTown;
        private string defaultMoveButtonLabel;
        private string defaultCancelButtonLabel;

        // 수량 조절 버튼이 선택된 동안에만 좌우 입력을 수량 조절로 사용
        private bool IsQuantitySelectionActive => IsRowSelected(numberSelectionButton);

        // 판매가 조절 행이 선택된 동안에만 좌우 입력을 가격 조절로 사용
        private bool IsPriceSelectionActive => IsRowSelected(priceSelectionButton);

        private bool IsCancelableContext => currentContext == MenuBarContext.Submission
            || currentContext == MenuBarContext.ItemSelection;

        // 내 가게 재고 관리 전용 상태
        private int currentPrice = 1;
        private Action<int, int> playerShopConfirmAction; // (수량, 판매가)
        private const int PriceStep = 1;
        private const int MinPrice = 1;

        private const string UsageFood = "Food";
        private const string UsageCal = "Cal";
        private const string UsageCharm = "Charm";
        private const string UsageStr = "Str";
        private const string UsageDex = "Dex";
        private const string UsageNull = "NULL";

        public int GetSelectedItemID() => selectedItemID;

        #region MonoBehaviour
        private void Awake()
        {
            buttons = new List<Button>();
            CacheDefaultButtonLabels();

            if (panelRect == null)
                panelRect = GetComponent<RectTransform>();
        }
        #endregion

        private void Update()
        {
            // 클릭 감지 (패널 외부 클릭 시 닫기)
            // TODO: Input system에서 모바일 터치 처리로 변경
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                if (!IsPointerOverPanel())
                    ClosePanel(IsCancelableContext);
            }
        }

        // 마우스로 행을 클릭하면 키보드 이동과 동일하게 해당 행을 선택(조절 UI 활성화)
        private void SelectRowByClick(Button row)
        {
            if (buttons == null)
                return;

            int index = buttons.IndexOf(row);
            if (index < 0)
                return;

            currentButtonIndex = index;
            SelectCurrentButton();
        }

        #region UIPanel Overrides
        public override void OnOpen()
        {
            base.OnOpen();

            if (activeInstance != null && activeInstance != this)
                activeInstance.ClosePanel();

            activeInstance = this;
        }

        public override void OnClose()
        {
            base.OnClose();

            if (activeInstance == this)
                activeInstance = null;

            ResetContextCallbacks();
        }
        #endregion

        #region Event Listeners
        protected override void AddListeners()
        {
            if (foodRechargeButton != null)
                foodRechargeButton.onClick.AddListener(OnUseItemAsFood);

            if (useItemButton != null)
                useItemButton.onClick.AddListener(OnUseItemAsStat);

            if (moveAllButton != null)
                moveAllButton.onClick.AddListener(OnMoveAllButtonClicked);

            if (moveButton != null)
                moveButton.onClick.AddListener(OnMoveButtonClicked);

            if (numberSelectionButton != null)
            {
                numberSelectionButton.onClick.AddListener(ConfirmQuantitySelection);
                numberSelectionButton.onClick.AddListener(() => SelectRowByClick(numberSelectionButton));
            }

            if (priceSelectionButton != null)
                priceSelectionButton.onClick.AddListener(() => SelectRowByClick(priceSelectionButton));

            if (decreaseButton != null)
                decreaseButton.onClick.AddListener(() => DecreaseQuantity());

            if (increaseButton != null)
                increaseButton.onClick.AddListener(() => IncreaseQuantity());

            if (priceDecreaseButton != null)
                priceDecreaseButton.onClick.AddListener(() => DecreasePrice());

            if (priceIncreaseButton != null)
                priceIncreaseButton.onClick.AddListener(() => IncreasePrice());

            if (openInformationButton != null)
                openInformationButton.onClick.AddListener(OnOpenInformationClicked);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelButtonClicked);
        }

        protected override void RemoveListeners()
        {
            if (foodRechargeButton != null) foodRechargeButton.onClick.RemoveAllListeners();
            if (useItemButton != null) useItemButton.onClick.RemoveAllListeners();
            if (moveAllButton != null) moveAllButton.onClick.RemoveAllListeners();
            if (moveButton != null) moveButton.onClick.RemoveAllListeners();
            if (numberSelectionButton != null) numberSelectionButton.onClick.RemoveAllListeners();
            if (priceSelectionButton != null) priceSelectionButton.onClick.RemoveAllListeners();
            if (decreaseButton != null) decreaseButton.onClick.RemoveAllListeners();
            if (increaseButton != null) increaseButton.onClick.RemoveAllListeners();
            if (priceDecreaseButton != null) priceDecreaseButton.onClick.RemoveAllListeners();
            if (priceIncreaseButton != null) priceIncreaseButton.onClick.RemoveAllListeners();
            if (openInformationButton != null) openInformationButton.onClick.RemoveAllListeners();
            if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();
        }
        #endregion

        #region Menu Configuration
        /// <summary>아이템 정보 및 컨텍스트와 함께 패널 열기</summary>
        public void InitializeMenu(int itemID, MenuBarContext context, Vector2 position, bool isFoodItem = false, int maxQuantity = 1, bool enableTransferActions = false)
        {
            InitializeMenu(itemID, context, position, isFoodItem, maxQuantity, enableTransferActions, null);
        }

        public void InitializeMenu(int itemID, MenuBarContext context, Vector2 position, bool isFoodItem, int maxQuantity, bool enableTransferActions, string marketTown)
        {
            InitializeMenu(itemID, context, position, isFoodItem, maxQuantity, enableTransferActions, marketTown, false);
        }

        private void InitializeMenu(
            int itemID,
            MenuBarContext context,
            Vector2 position,
            bool isFoodItem,
            int maxQuantity,
            bool enableTransferActions,
            string marketTown,
            bool informationOnlyInventoryMenu)
        {
            selectedItemID = itemID;
            currentContext = context;
            isFoodItemMenu = isFoodItem;
            isInformationOnlyInventoryMenu = informationOnlyInventoryMenu;
            selectedItemMaxQuantity = Mathf.Max(0, maxQuantity);
            currentQuantity = selectedItemMaxQuantity > 0 ? 1 : 0;
            isTransferMenu = enableTransferActions;
            targetPosition = position;
            this.marketTown = string.IsNullOrWhiteSpace(marketTown) ? string.Empty : marketTown.Trim();

            ConfigureButtons();
            SetPanelPosition();
        }

        public void InitializeInventoryMenu(int itemID, MenuBarContext sourceContext, Vector2 position, bool isFoodItem, int maxQuantity)
        {
            if (sourceContext != MenuBarContext.ShipInventory && sourceContext != MenuBarContext.PlayerInventory)
            {
                Debug.LogWarning($"[MenuBarPanel] Invalid inventory context: {sourceContext}");
                sourceContext = MenuBarContext.ShipInventory;
            }

            InitializeMenu(itemID, sourceContext, position, isFoodItem, maxQuantity, true);
            TutorialUIBinding.Bind(foodRechargeButton, TutorialAnchorKeys.FoodRechargeButton);
        }

        public void InitializeCartMenu(int itemID, Vector2 position, int quantity, int maxQuantity, string marketTown)
        {
            InitializeMenu(itemID, MenuBarContext.Cart, position, false, maxQuantity, false, marketTown);
            currentQuantity = quantity; // 현재 장바구니 수량으로 초기화
            UpdateQuantityDisplay();
        }

        public void InitializePlayerInventoryMenu(int itemID, Vector2 position, bool isFoodItem)
        {
            InitializeMenu(itemID, MenuBarContext.PlayerInventory, position, isFoodItem);
        }

        public void InitializePlayerInventoryInformationMenu(int itemID, Vector2 position)
        {
            InitializeMenu(
                itemID,
                MenuBarContext.PlayerInventory,
                position,
                false,
                1,
                false,
                null,
                true);
        }

        public void InitializeSellListMenu(int itemID, Vector2 position, int quantity, int maxQuantity, string marketTown)
        {
            InitializeMenu(itemID, MenuBarContext.SellList, position, false, maxQuantity, false, marketTown);
            currentQuantity = quantity; // 현재 판매 목록 수량으로 초기화
            UpdateQuantityDisplay();
        }

        /// <summary>내 가게 재고 관리 메뉴, 판매가·수량 조절 후 액션 버튼(옮기기/선택하기)으로 확정</summary>
        public void InitializePlayerShopStockMenu(int itemID, Vector2 position, int defaultPrice, int maxQuantity, string actionLabel, Action<int, int> onConfirm)
        {
            playerShopConfirmAction = onConfirm;
            currentPrice = Mathf.Max(MinPrice, defaultPrice);
            InitializeMenu(itemID, MenuBarContext.PlayerShopStock, position, false, maxQuantity, false);
            SetButtonLabel(moveButton, actionLabel);
            UpdatePriceDisplay();
            UpdateQuantityDisplay();
        }

        public void InitializeSubmissionMenu(int itemID, Vector2 position, Action<int> onSubmit)
        {
            InitializeSubmissionMenu(itemID, position, onSubmit, null);
        }

        public void InitializeSubmissionMenu(int itemID, Vector2 position, Action<int> onSubmit, Action onCancel)
        {
            submissionAction = onSubmit;
            submissionCancelAction = onCancel;
            InitializeMenu(itemID, MenuBarContext.Submission, position);
        }

        public void InitializeItemSelectionMenu(
            int itemID,
            Vector2 position,
            Action<int> onSelected,
            Action onCancel = null,
            string actionLabel = "선택")
        {
            itemSelectionAction = onSelected;
            itemSelectionCancelAction = onCancel;
            itemSelectionActionLabel = string.IsNullOrWhiteSpace(actionLabel) ? "선택" : actionLabel;
            InitializeMenu(itemID, MenuBarContext.ItemSelection, position);
        }

        /// <summary>패널 위치 설정(화면 경계 보정 포함)</summary>
        private void SetPanelPosition()
        {
            if (panelRect == null) return;

            // 부모 Canvas의 RectTransform 가져오기
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Vector2 canvasSize = canvasRect.sizeDelta;
            Vector2 panelSize = panelRect.sizeDelta;

            // 목표 위치 설정
            Vector2 adjustedPosition = targetPosition;

            // 화면 오른쪽 경계 보정
            if (adjustedPosition.x + panelSize.x > canvasSize.x / 2)
                adjustedPosition.x -= panelSize.x;

            // 화면 아래쪽 경계 보정
            if (adjustedPosition.y - panelSize.y < -canvasSize.y / 2)
                adjustedPosition.y = -canvasSize.y / 2 + panelSize.y;

            // 화면 위쪽 경계 보정
            if (adjustedPosition.y > canvasSize.y / 2)
                adjustedPosition.y = canvasSize.y / 2;

            // 화면 왼쪽 경계 보정
            if (adjustedPosition.x < -canvasSize.x / 2)
                adjustedPosition.x = -canvasSize.x / 2;

            panelRect.anchoredPosition = adjustedPosition;
        }

        /// <summary>컨텍스트에 따라 버튼 구성</summary>
        private void ConfigureButtons()
        {
            buttons.Clear();
            currentButtonIndex = 0;
            RestoreDefaultButtonLabels();

            // 모든 버튼 비활성화
            SetButtonActive(foodRechargeButton, false);
            SetButtonActive(useItemButton, false);
            SetButtonActive(moveAllButton, false);
            SetButtonActive(moveButton, false);
            SetButtonActive(numberSelectionButton, false);
            SetButtonActive(priceSelectionButton, false);
            SetButtonActive(openInformationButton, false);
            SetButtonActive(cancelButton, false);

            switch (currentContext)
            {
                case MenuBarContext.ShipInventory:
                    if (isFoodItemMenu) AddButton(foodRechargeButton);
                    AddButton(moveAllButton);
                    AddButton(moveButton);
                    AddButton(openInformationButton);
                    break;
                case MenuBarContext.PlayerInventory:
                    if (isInformationOnlyInventoryMenu)
                    {
                        AddButton(openInformationButton);
                        break;
                    }

                    if (isTransferMenu)
                    {
                        if (isFoodItemMenu) AddButton(foodRechargeButton);
                        AddButton(moveAllButton);
                        AddButton(moveButton);
                        AddButton(openInformationButton);
                        break;
                    }

                    // 플레이어 인벤토리: Food 또는 Stat item 처리
                    // 사용 불가능한 아이템이면 useItemButton을 추가하지 않음
                    ItemData itemData = GetSelectedItemData();
                    bool isUsable = IsUsableUsage(itemData?.Usage);

                    if (isFoodItemMenu)
                    {
                        AddButton(foodRechargeButton);
                    }
                    else
                    {
                        if (isUsable)
                            AddButton(useItemButton);
                    }
                    break;
                // 수량 조절 UI 활성화(currentQuantity는 Initialize*Menu에서 현재 담긴 수량으로 설정)
                case MenuBarContext.Cart:
                case MenuBarContext.SellList:
                    AddButton(numberSelectionButton);
                    AddButton(openInformationButton);
                    UpdateQuantityDisplay();
                    break;
                case MenuBarContext.Submission:
                    SetButtonLabel(moveButton, "제출하기");
                    SetButtonLabel(cancelButton, "취소");
                    AddButton(moveButton);
                    break;
                case MenuBarContext.ItemSelection:
                    SetButtonLabel(moveButton, itemSelectionActionLabel);
                    SetButtonLabel(cancelButton, "취소");
                    AddButton(moveButton);
                    break;
                // 판매가·수량 조절 행 + 확정 버튼(옮기기/선택하기), 두 조절 행은 좌우 입력으로만 조절
                case MenuBarContext.PlayerShopStock:
                    AddButton(priceSelectionButton);
                    AddButton(numberSelectionButton);
                    AddButton(moveButton);
                    UpdatePriceDisplay();
                    UpdateQuantityDisplay();
                    break;
            }

            // 취소 버튼은 항상 마지막에 추가
            AddButton(cancelButton);

            SelectCurrentButton();
        }
        #endregion

        /// <summary>수량 선택 모드 진입 / 담기 버튼 클릭 이벤트</summary>
        private void ConfirmQuantitySelection()
        {
            if (currentContext == MenuBarContext.Cart)
                AddToCart();
            else if (currentContext == MenuBarContext.SellList)
                AddToSellList();
        }

        private void OnMoveButtonClicked()
        {
            if (currentContext == MenuBarContext.Submission)
            {
                submissionAction?.Invoke(selectedItemID);
                ClosePanel();
                return;
            }

            if (currentContext == MenuBarContext.ItemSelection)
            {
                itemSelectionAction?.Invoke(selectedItemID);
                if (gameObject.activeInHierarchy)
                    ClosePanel();
                return;
            }

            if (currentContext == MenuBarContext.PlayerShopStock)
            {
                playerShopConfirmAction?.Invoke(currentQuantity, currentPrice);
                ClosePanel();
                return;
            }

            MoveSelectedItem(1);
        }

        private void OnCancelButtonClicked()
        {
            ClosePanel(IsCancelableContext);
        }

        private void OnMoveAllButtonClicked()
        {
            MoveSelectedItem(selectedItemMaxQuantity);
        }

        private void MoveSelectedItem(int quantity)
        {
            if (!isTransferMenu || selectedItemID < 0)
            {
                ClosePanel();
                return;
            }

            if (quantity <= 0)
            {
                UIManager.Instance?.ShowAlertMessage("옮길 아이템이 없다");
                ClosePanel();
                return;
            }

            InventoryManager inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null || inventoryManager.PlayerInventory == null || inventoryManager.ShipInventory == null)
            {
                UIManager.Instance?.ShowAlertMessage("[Error] 인벤토리 정보를 찾을 수 없습니다");
                ClosePanel();
                return;
            }

            int availableQuantity = GetSelectedItemQuantity();
            int moveQuantity = Mathf.Min(quantity, availableQuantity);
            if (moveQuantity <= 0)
            {
                UIManager.Instance?.ShowAlertMessage("옮길 아이템이 없다");
                ClosePanel();
                return;
            }

            bool success = currentContext switch
            {
                MenuBarContext.ShipInventory => inventoryManager.TransferToPlayer(selectedItemID, moveQuantity),
                MenuBarContext.PlayerInventory => inventoryManager.TransferToShip(selectedItemID, moveQuantity),
                _ => false
            };

            UIManager.Instance?.ShowAlertMessage(success ? "아이템을 옮겼다" : "아이템을 옮길 수 없다");
            ClosePanel();
        }

        /// <summary>플레이어 인벤토리의 Food 아이템 사용(배 식량 충전)</summary>
        private void OnUseItemAsFood()
        {
            bool consumeFromShipInventory = isTransferMenu && currentContext == MenuBarContext.ShipInventory;
            bool success = ItemUseService.ConsumeFoodForShip(selectedItemID, currentQuantity, consumeFromShipInventory);

            if (success)
            {
                UIManager.Instance?.ShowAlertMessage(
                    "식량을 충전했다",
                    () => TutorialEventReporter.Report(TutorialEventType.UiElementActivated, TutorialTargetIds.FoodRechargeMessage, source: TutorialEventSource.UserInterface));
                TutorialEventReporter.Report(TutorialEventType.FoodRechargeCompleted, TutorialTargetIds.Potato, source: TutorialEventSource.Domain);
            }
            else
                UIManager.Instance?.ShowAlertMessage("식량을 충전할 수 없다");

            ClosePanel();
        }

        /// <summary>플레이어 인벤토리의 Stat 아이템 사용(Cal/Charm/Str/Dex)</summary>
        private void OnUseItemAsStat()
        {
            ItemData itemData = GetSelectedItemData();
            if (itemData == null)
            {
                UIManager.Instance?.ShowAlertMessage("[Error] 아이템 정보를 찾을 수 없습니다");
                ClosePanel();
                return;
            }

            if (!IsUsableUsage(itemData.Usage))
            {
                UIManager.Instance?.ShowAlertMessage("이 아이템은 사용할 수 없다");
                ClosePanel();
                return;
            }

            string usage = itemData.Usage;

            // Cal과 Charm은 적용 대상(직원/플레이어) 선택 필요
            if (usage == UsageCal || usage == UsageCharm)
                OpenStaffSelectionPanel(selectedItemID);
            else if (usage == UsageStr || usage == UsageDex)
            {
                ItemUseService.TryUseOnPlayer(selectedItemID, out string resultMessage);
                ClosePanelThen(() => UIManager.Instance?.ShowAlertMessage(resultMessage));
            }
            else
            {
                UIManager.Instance?.ShowAlertMessage("이 아이템은 사용할 수 없다");
                ClosePanel();
            }
        }

        /// <summary>직원 선택 패널 열기(Cal/Charm 아이템용)</summary>
        private void OpenStaffSelectionPanel(int itemID)
        {
            ClosePanelThen(() =>
            {
                StaffChangePanel panel = UIManager.Instance.OpenPanel<StaffChangePanel>();
                panel?.InitializeStatItem(itemID);
            });
        }

        /// <summary>수량 감소</summary>
        private void DecreaseQuantity()
        {
            if (currentQuantity >= 1)
            {
                currentQuantity--;
                UpdateQuantityDisplay();
            }
        }

        /// <summary>수량 증가</summary>
        private void IncreaseQuantity()
        {
            if (currentQuantity < selectedItemMaxQuantity)
            {
                currentQuantity++;
                UpdateQuantityDisplay();
            }
        }

        /// <summary>수량 표시 업데이트</summary>
        private void UpdateQuantityDisplay()
        {
            if (quantityText == null)
                return;

            string number = IsQuantitySelectionActive
                ? WithSelectionMark(currentQuantity.ToString())
                : currentQuantity.ToString();

            quantityText.text = currentContext == MenuBarContext.PlayerShopStock
                ? $"{number}개"
                : $"{number}개 담기";
        }

        private void DecreasePrice()
        {
            if (currentPrice > MinPrice)
            {
                currentPrice = Mathf.Max(MinPrice, currentPrice - PriceStep);
                UpdatePriceDisplay();
            }
        }

        private void IncreasePrice()
        {
            currentPrice += PriceStep;
            UpdatePriceDisplay();
        }

        private void UpdatePriceDisplay()
        {
            if (priceText == null)
                return;

            string number = IsPriceSelectionActive
                ? WithSelectionMark(currentPrice.ToString())
                : currentPrice.ToString();

            priceText.text = $"{number}G";
        }

        private static string WithSelectionMark(string content) =>
            $"<mark=#00000080 padding=\"10,10,5,5\">{content}</mark>";

        // 현재 선택된 조절 행에만 좌우 화살표와 텍스트 선택 배경 표시
        private void RefreshAdjustRowVisuals()
        {
            SetButtonActive(decreaseButton, IsQuantitySelectionActive);
            SetButtonActive(increaseButton, IsQuantitySelectionActive);
            SetButtonActive(priceDecreaseButton, IsPriceSelectionActive);
            SetButtonActive(priceIncreaseButton, IsPriceSelectionActive);

            UpdateQuantityDisplay();
            UpdatePriceDisplay();
        }

        /// <summary>장바구니에 아이템 추가</summary>
        private void AddToCart()
        {
            if (currentContext != MenuBarContext.Cart)
            {
                ClosePanel();
                return;
            }

            // 0개면 SetCartQuantity가 제거 처리
            UIManager.Instance.GetPanel<PurchasePanel>()?.Presenter?.SetCartQuantity(selectedItemID, currentQuantity);

            ClosePanel();
        }

        /// <summary>판매 목록에 아이템 추가/수정</summary>
        private void AddToSellList()
        {
            if (currentContext != MenuBarContext.SellList)
            {
                ClosePanel();
                return;
            }

            // 판매 목록은 출처가 병합돼 있으므로 총량으로 조절(0개면 제거 처리)
            UIManager.Instance.GetPanel<SellPanel>()?.Presenter?.SetSellTotalQuantity(selectedItemID, currentQuantity);

            ClosePanel();
        }

        /// <summary>정보 보기 클릭 이벤트</summary>
        private void OnOpenInformationClicked()
        {
            ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(selectedItemID);

            if (itemData == null) return;

            if (currentContext == MenuBarContext.ShipInventory || currentContext == MenuBarContext.PlayerInventory)
            {
                if (!TryGetSelectedInventoryItem(out InventoryItem inventoryItem))
                {
                    Debug.LogWarning($"[MenuBarPanel] 인벤토리에서 아이템을 찾을 수 없습니다: {selectedItemID}");
                    ClosePanel();
                    return;
                }

                ClosePanelThen(() => UIManager.Instance
                    .OpenPanel<ItemInformationPanel>()?
                    .InitializeItemInformation(
                        itemData,
                        inventoryItem.quantity,
                        inventoryItem.averagePurchasePrice));
            }
            else if (currentContext == MenuBarContext.Cart)
            {
                PurchasePresenter presenter = UIManager.Instance.GetPanel<PurchasePanel>()?.Presenter;
                if (presenter == null) return;

                int quantity = presenter.GetOriginalMaxQuantity(selectedItemID);
                ClosePanelThen(() => UIManager.Instance.OpenPanel<PurchaseInformationPanel>()?.InitializePurchaseInformation(itemData, quantity, marketTown));
            }
            else if (currentContext == MenuBarContext.SellList)
            {
                SellPresenter presenter = UIManager.Instance.GetPanel<SellPanel>()?.Presenter;
                if (presenter == null) return;

                // 판매 목록에서 열므로 출처 없이(총량 기준) 두 인벤토리 보유 합계까지 조절 가능
                int quantity = presenter.GetOwnedQuantity(selectedItemID);
                ClosePanelThen(() => UIManager.Instance.OpenPanel<SellInformationPanel>()?.InitializeSellInformation(itemData, quantity, null, marketTown));
            }
        }

        private void SetButtonActive(Button btn, bool active)
        {
            if (btn != null) btn.gameObject.SetActive(active);
        }

        private void AddButton(Button btn)
        {
            if (btn != null)
            {
                buttons.Add(btn);
                btn.gameObject.SetActive(true);
            }
        }

        #region Navigation
        public override void NavigateToUpperButton()
        {
            if (buttons == null || buttons.Count == 0) return;

            if (--currentButtonIndex < 0)
                currentButtonIndex = buttons.Count - 1;
            SelectCurrentButton();
        }

        public override void NavigateToLowerButton()
        {
            if (buttons == null || buttons.Count == 0) return;

            currentButtonIndex = (currentButtonIndex + 1) % buttons.Count;
            SelectCurrentButton();
        }

        private bool IsRowSelected(Button row) =>
            row != null && buttons != null
            && currentButtonIndex >= 0 && currentButtonIndex < buttons.Count
            && buttons[currentButtonIndex] == row;

        public override void NavigateToLeftButton()
        {
            if (IsQuantitySelectionActive)
                DecreaseQuantity();
            else if (IsPriceSelectionActive)
                DecreasePrice();
        }

        public override void NavigateToRightButton()
        {
            if (IsQuantitySelectionActive)
                IncreaseQuantity();
            else if (IsPriceSelectionActive)
                IncreasePrice();
        }
        #endregion

        private void SelectCurrentButton()
        {
            if (buttons == null || buttons.Count == 0) return;

            if (buttons[currentButtonIndex] != null && buttons[currentButtonIndex].interactable)
                buttons[currentButtonIndex].Select();

            RefreshAdjustRowVisuals();
        }

        public override void ClickSelectedButton()
        {
            if (buttons == null || buttons.Count == 0) return;

            Button currentButton = buttons[currentButtonIndex];
            if (currentButton != null && currentButton.interactable)
                currentButton.onClick.Invoke();
        }

        private void ClosePanel(bool notifyContextCancel = false)
        {
            if (notifyContextCancel)
            {
                if (currentContext == MenuBarContext.Submission)
                    submissionCancelAction?.Invoke();
                else if (currentContext == MenuBarContext.ItemSelection)
                    itemSelectionCancelAction?.Invoke();
            }

            ResetContextCallbacks();
            UIManager.Instance?.ClosePanel<MenuBarPanel>();
        }

        private void ResetContextCallbacks()
        {
            submissionAction = null;
            submissionCancelAction = null;
            itemSelectionAction = null;
            itemSelectionCancelAction = null;
            itemSelectionActionLabel = "선택";
            playerShopConfirmAction = null;
            marketTown = string.Empty;
            isInformationOnlyInventoryMenu = false;
        }

        private void ClosePanelThen(Action afterClosed)
        {
            UIManager manager = UIManager.Instance;
            if (manager == null)
                return;

            void HandlePanelClosed()
            {
                manager.OnPanelClosed -= HandlePanelClosed;
                afterClosed?.Invoke();
            }

            manager.OnPanelClosed += HandlePanelClosed;
            ClosePanel();
        }

        private void CacheDefaultButtonLabels()
        {
            defaultMoveButtonLabel = GetButtonLabel(moveButton);
            defaultCancelButtonLabel = GetButtonLabel(cancelButton);
        }

        private void RestoreDefaultButtonLabels()
        {
            SetButtonLabel(moveButton, defaultMoveButtonLabel);
            SetButtonLabel(cancelButton, defaultCancelButtonLabel);
        }

        private static string GetButtonLabel(Button button)
        {
            TextMeshProUGUI label = button != null ? button.GetComponentInChildren<TextMeshProUGUI>() : null;
            return label != null ? label.text : null;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            TextMeshProUGUI text = button != null ? button.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (text != null && label != null)
                text.text = label;
        }

        private ItemData GetSelectedItemData()
        {
            return DataManager.Instance?.ItemDatabase.GetItem(selectedItemID);
        }

        private int GetSelectedItemQuantity()
        {
            InventoryData inventory = GetCurrentContextInventory();
            if (inventory != null)
                return inventory.GetItemCount(selectedItemID);

            return selectedItemMaxQuantity;
        }

        private InventoryData GetCurrentContextInventory()
        {
            if (!InventoryManager.HasInstance)
                return null;

            InventoryManager inventoryManager = InventoryManager.Instance;

            return currentContext switch
            {
                MenuBarContext.ShipInventory => inventoryManager.ShipInventory,
                MenuBarContext.PlayerInventory => inventoryManager.PlayerInventory,
                _ => null
            };
        }

        private bool TryGetSelectedInventoryItem(out InventoryItem inventoryItem)
        {
            inventoryItem = default;
            InventoryData inventory = GetCurrentContextInventory();
            return inventory != null
                && inventory.Items.TryGetValue(selectedItemID, out inventoryItem);
        }

        private static bool IsUsableUsage(string usage)
        {
            return !string.IsNullOrWhiteSpace(usage) && usage != UsageNull;
        }

        /// <summary>마우스 포인터가 패널 위에 있는지 확인</summary>
        private bool IsPointerOverPanel()
        {
            if (panelRect == null) return false;

            if (EventSystem.current == null) return false;

            // New Input System을 사용하여 마우스 위치 가져오기
            Vector2 mousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

            // 현재 포인터 위치에 있는 모든 UI 요소 가져오기
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = mousePosition
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            // 현재 패널이나 자식 요소가 클릭되었는지 확인
            foreach (var result in results)
                if (result.gameObject.transform.IsChildOf(transform) || result.gameObject == gameObject)
                    return true;

            return false;
        }
    }
}
