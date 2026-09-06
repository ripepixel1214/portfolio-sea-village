using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Data;
using SeaVillage.UI.Shop;

namespace SeaVillage.UI
{
    /// <summary>
    /// 상점 - 판매 - 정보 패널
    /// </summary>
    public class SellInformationPanel : UIPanel, IContextualPanel
    {
        [Header("Sell Information Settings")]
        [SerializeField] private Image itemImage;
        [SerializeField] private TextMeshProUGUI itemName;
        [SerializeField] private TextMeshProUGUI itemDescription;
        [SerializeField] private ItemCategoryView itemCategoryChipsView;

        [Header("Price Graph")]
        [SerializeField] private ShopItemPriceGraphView itemPriceGraphView;

        [Header("Details")]
        [SerializeField] private TextMeshProUGUI marketPriceText;
        [SerializeField] private TextMeshProUGUI sellQuantityText;
        [SerializeField] private TextMeshProUGUI totalPriceText;
        [SerializeField] private TextMeshProUGUI expectedFeeText;
        [SerializeField] private TextMeshProUGUI purchasedPriceText;
        [SerializeField] private TextMeshProUGUI netProfitText;

        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button minimizeButton;
        [SerializeField] private Button maximizeButton;

        [SerializeField] private Button cancelButton;
        [SerializeField] private Button addToSellListButton;

        [SerializeField] private TextMeshProUGUI quantityControlText;

        // Navigation
        private enum NavigationArea { QuantityRow, ButtonRow }
        private enum QuantityButton { Minimize, Minus, Plus, Maximize }
        private enum ActionButton { Cancel, AddToSellList }

        private NavigationArea currentArea = NavigationArea.QuantityRow;
        private QuantityButton currentQuantityButton = QuantityButton.Minus;
        private ActionButton currentActionButton = ActionButton.AddToSellList;

        private int unitPrice;
        private int sellQuantity = 1;
        private int maxQuantity;
        private ItemData currentItemData;
        private SellPanel sellPanel; // 부모 SellPanel 캐시

        // 인벤토리 슬롯에서 열렸으면 해당 출처, 판매 목록에서 열렸으면 null(총량 기준)
        private SlotType? sourceSlotType;

        private SellPanel GetSellPanel()
        {
            SellPanel resolved = GetComponentInParent<SellPanel>();
            if (resolved == null && UIManager.HasInstance)
                resolved = UIManager.Instance.GetPanel<SellPanel>();

            return resolved;
        }

        private int GetCurrentQuantityInSellList()
        {
            SellPresenter presenter = sellPanel?.Presenter;
            if (presenter == null || currentItemData == null)
                return 0;

            return sourceSlotType.HasValue
                ? presenter.GetQuantityInSellList(currentItemData.ID, sourceSlotType.Value)
                : presenter.GetTotalQuantityInSellList(currentItemData.ID);
        }

        /// <summary>
        /// 전달받은 아이템 정보, 수량으로 판매 정보 패널을 초기화
        /// </summary>
        /// <param name="itemData">아이템 데이터</param>
        /// <param name="maxQuantity">최대 수량</param>
        /// <param name="sourceType">인벤토리 슬롯에서 열렸으면 해당 출처, 판매 목록에서 열렸으면 null</param>
        public void InitializeSellInformation(ItemData itemData, int maxQuantity, SlotType? sourceType, string marketTown)
        {
            if (itemData == null)
            {
                Debug.LogError("Sell Information Panel: Received NULL item data for initialization.");
                return;
            }
            currentItemData = itemData;
            sourceSlotType = sourceType;

            this.maxQuantity = maxQuantity;

            string resolvedMarketTown = string.IsNullOrWhiteSpace(marketTown) ? itemData.Town : marketTown.Trim();
            unitPrice = DataManager.Instance.GetItemPrice(itemData.PriceListID, resolvedMarketTown);

            marketPriceText.text = unitPrice.ToString();
            itemPriceGraphView?.UpdateGraph(currentItemData, unitPrice, resolvedMarketTown);

            // 부모/등록 패널 기준으로 SellPanel 캐시
            sellPanel = GetSellPanel();

            // 이미 담긴 수량을 기본값으로 표시하고, 없으면 1개부터 시작
            int quantityInSellList = GetCurrentQuantityInSellList();
            sellQuantity = quantityInSellList > 0 ? quantityInSellList : 1;

            UpdateItemUIs();
            UpdateQuantityRelatedTexts();
        }

        #region Panel Methods
        public override void OnOpen()
        {
            base.OnOpen();

            UpdateQuantityControlText();

            currentArea = NavigationArea.QuantityRow;
            currentQuantityButton = QuantityButton.Minimize;
            currentActionButton = ActionButton.Cancel;
            SelectCurrentButton();
        }
        #endregion

        #region Event Listeners
        protected override void AddListeners()
        {
            if (minusButton != null)
                minusButton.onClick.AddListener(() => AddQuantityToAdd(-1));
            if (plusButton != null)
                plusButton.onClick.AddListener(() => AddQuantityToAdd(1));
            if (minimizeButton != null)
                minimizeButton.onClick.AddListener(() => SetQuantityToAdd(0));
            if (maximizeButton != null)
                maximizeButton.onClick.AddListener(() => SetQuantityToAddMax());
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelButtonClicked);
            if (addToSellListButton != null)
                addToSellListButton.onClick.AddListener(OnAddToSellListButtonClicked);
        }

        protected override void RemoveListeners()
        {
            if (minusButton != null)
                minusButton.onClick.RemoveAllListeners();
            if (plusButton != null)
                plusButton.onClick.RemoveAllListeners();
            if (minimizeButton != null)
                minimizeButton.onClick.RemoveAllListeners();
            if (maximizeButton != null)
                maximizeButton.onClick.RemoveAllListeners();
            if (cancelButton != null)
                cancelButton.onClick.RemoveAllListeners();
            if (addToSellListButton != null)
                addToSellListButton.onClick.RemoveAllListeners();
        }
        #endregion

        #region Navigation
        public override void NavigateToLeftButton()
        {
            switch (currentArea)
            {
                case NavigationArea.QuantityRow:
                    if (currentQuantityButton > QuantityButton.Minimize)
                        currentQuantityButton--;
                    break;
                case NavigationArea.ButtonRow:
                    if (currentActionButton > ActionButton.Cancel)
                        currentActionButton--;
                    break;
            }
            SelectCurrentButton();
        }

        public override void NavigateToRightButton()
        {
            switch (currentArea)
            {
                case NavigationArea.QuantityRow:
                    if (currentQuantityButton < QuantityButton.Maximize)
                        currentQuantityButton++;
                    break;
                case NavigationArea.ButtonRow:
                    if (currentActionButton < ActionButton.AddToSellList)
                        currentActionButton++;
                    break;
            }
            SelectCurrentButton();
        }

        public override void NavigateToUpperButton()
        {
            if (currentArea == NavigationArea.ButtonRow)
            {
                currentArea = NavigationArea.QuantityRow;
                currentQuantityButton = (currentActionButton == ActionButton.Cancel) ? QuantityButton.Minus : QuantityButton.Plus;
            }
            SelectCurrentButton();
        }

        public override void NavigateToLowerButton()
        {
            if (currentArea == NavigationArea.QuantityRow)
            {
                currentArea = NavigationArea.ButtonRow;
                currentActionButton = (currentQuantityButton <= QuantityButton.Minus) ? ActionButton.Cancel : ActionButton.AddToSellList;
            }
            SelectCurrentButton();
        }
        #endregion

        public override void ClickSelectedButton()
        {
            Button targetButton = GetCurrentButton();
            if (targetButton != null && targetButton.interactable)
                targetButton.onClick.Invoke();
        }

        private Button GetCurrentButton()
        {
            switch (currentArea)
            {
                case NavigationArea.QuantityRow:
                    switch (currentQuantityButton)
                    {
                        case QuantityButton.Minimize: return minimizeButton;
                        case QuantityButton.Minus: return minusButton;
                        case QuantityButton.Plus: return plusButton;
                        case QuantityButton.Maximize: return maximizeButton;
                    }
                    break;
                case NavigationArea.ButtonRow:
                    switch (currentActionButton)
                    {
                        case ActionButton.Cancel: return cancelButton;
                        case ActionButton.AddToSellList: return addToSellListButton;
                    }
                    break;
            }
            return null;
        }

        private void SelectCurrentButton()
        {
            Button targetButton = GetCurrentButton();
            if (targetButton != null && targetButton.interactable)
                targetButton.Select();
        }

        private void UpdateItemUIs()
        {
            if (currentItemData != null && currentItemData.Icon != null)
            {
                if (itemImage != null)
                    itemImage.sprite = currentItemData.Icon;
            }
            else
            {
                if (itemImage != null)
                    itemImage.sprite = UIManager.Instance.LoadItemIcon(currentItemData.Image);
            }

            if (itemName != null)
                itemName.text = DataManager.Instance.GetScriptTextKR(currentItemData.Name);

            if (itemDescription != null)
                itemDescription.text = DataManager.Instance.GetScriptTextKR(currentItemData.Description);

            itemCategoryChipsView?.SetCategoriesWithTown(currentItemData.Type, currentItemData.Town);

            if (itemDescription == null)
                Debug.LogWarning("SellInformationPanel: itemDescription reference is missing. Check prefab binding.");
        }

        public void AddQuantityToAdd(int quantity)
        {
            int addedQuantity = sellQuantity + quantity;
            if (addedQuantity < 0 || addedQuantity > maxQuantity)
                return;

            sellQuantity = addedQuantity;
            UpdateQuantityRelatedTexts();
        }

        public void SetQuantityToAdd(int quantity)
        {
            sellQuantity = quantity;
            UpdateQuantityRelatedTexts();
        }

        public void SetQuantityToAddMax()
        {
            sellQuantity = maxQuantity;
            UpdateQuantityRelatedTexts();
        }

        private void UpdateQuantityRelatedTexts()
        {
            UpdateQuantityControlText();
            UpdateTotalSellPriceTexts();
            UpdateNetProfitText();
        }

        private void UpdateQuantityControlText()
        {
            if (quantityControlText != null)
                quantityControlText.text = sellQuantity.ToString();
        }

        private void UpdateTotalSellPriceTexts()
        {
            if (sellQuantityText == null || totalPriceText == null)
                return;

            sellQuantityText.text = sellQuantity.ToString();
            totalPriceText.text = (unitPrice * sellQuantity).ToString();
        }

        private void UpdateNetProfitText()
        {
            if (expectedFeeText == null || purchasedPriceText == null || netProfitText == null) return;

            int totalSellPrice = unitPrice * sellQuantity;
            int fee = ShopUtility.CalculateTradingFee(totalSellPrice);
            // 판매 패널과 같은 기준(두 인벤토리 합산 가중 평균 구매가)으로 원가를 계산한다
            int unitPurchasePrice = sellPanel?.Presenter?.GetUnitPurchasePrice(currentItemData.ID)
                ?? currentItemData.OriginPrice;
            int purchasePrice = unitPurchasePrice * sellQuantity;
            int netProfit = ShopUtility.CalculateFinalPrice(totalSellPrice, isPurchase: false) - purchasePrice;

            expectedFeeText.text = fee == 0 ? "0" : $"-{fee}";
            purchasedPriceText.text = purchasePrice == 0 ? "0" : $"-{purchasePrice}";
            netProfitText.text = netProfit.ToString();
        }

        /// <summary>
        /// 취소 버튼 클릭 이벤트
        /// </summary>
        private void OnCancelButtonClicked()
        {
            UIManager.Instance.ClosePanel<SellInformationPanel>();
        }

        /// <summary>
        /// 판매 목록 추가 버튼 클릭 이벤트
        /// </summary>
        private void OnAddToSellListButtonClicked()
        {
            if (currentItemData == null)
            {
                Debug.LogError("SellInformationPanel: Current item data is NULL.");
                return;
            }

            AddToSellList(currentItemData.ID, sellQuantity);
        }

        /// <summary>
        /// 판매 목록에 아이템 추가
        /// </summary>
        private void AddToSellList(int itemID, int quantity)
        {
            if (sellPanel == null)
            {
                sellPanel = GetSellPanel();
                if (sellPanel == null)
                {
                    Debug.LogError("SellInformationPanel: Cannot get active SellPanel.");
                    return;
                }
            }

            // 이 패널의 수량은 최종 수량이다 (0이면 제거 처리)
            if (sourceSlotType.HasValue)
                sellPanel.SetQuantityToSellList(itemID, quantity, sourceSlotType.Value);
            else
                sellPanel.SetTotalQuantityToSellList(itemID, quantity);

            // 판매 목록에 추가한 후 정보 패널 닫기
            UIManager.Instance.ClosePanel<SellInformationPanel>();
        }
    }
}
