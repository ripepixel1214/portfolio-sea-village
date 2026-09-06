using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Data;
using SeaVillage.Core;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    /// <summary>
    /// 상점 - 구매 - 정보 패널
    /// </summary>
    public class PurchaseInformationPanel : UIPanel, IContextualPanel
    {
        [Header("Purchase Information Settings")]
        [SerializeField] private Image itemImage;
        [SerializeField] private TextMeshProUGUI itemName;
        [SerializeField] private TextMeshProUGUI itemDescription;
        [SerializeField] private ItemCategoryView itemCategoryChipsView;

        [Header("Price Graph")]
        [SerializeField] private ShopItemPriceGraphView itemPriceGraphView;

        [Header("Details")]
        [SerializeField] private TextMeshProUGUI marketPriceText;
        [SerializeField] private TextMeshProUGUI purchaseQuantityText;
        [SerializeField] private TextMeshProUGUI totalPriceText;
        [SerializeField] private TextMeshProUGUI unitWeightText;
        [SerializeField] private TextMeshProUGUI totalWeightText;
        [SerializeField] private TextMeshProUGUI weightGainedText;
        [SerializeField] private IncreasableGauge weightGauge;

        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button minimizeButton;
        [SerializeField] private Button maximizeButton;

        [SerializeField] private Button cancelButton;
        [SerializeField] private Button addToCartButton;

        [SerializeField] private TextMeshProUGUI quantityControlText;

        // Navigation
        private enum NavigationArea { QuantityRow, ButtonRow }
        private enum QuantityButton { Minimize, Minus, Plus, Maximize }
        private enum ActionButton { Cancel, AddToCart }

        private NavigationArea currentArea = NavigationArea.QuantityRow;
        private QuantityButton currentQuantityButton = QuantityButton.Minus;
        private ActionButton currentActionButton = ActionButton.AddToCart;

        private int unitPrice;
        private float unitWeight;
        private int purchaseQuantity = 1;
        private int maxQuantity;
        private int quantityInCart;
        private ItemData currentItemData;
        private string marketTown;

        private PurchasePanel purchasePanel;

        private PurchasePanel GetPurchasePanel()
        {
            PurchasePanel resolved = GetComponentInParent<PurchasePanel>();
            if (resolved == null && UIManager.HasInstance)
                resolved = UIManager.Instance.GetPanel<PurchasePanel>();

            return resolved;
        }

        #region Panel Methods
        public override void OnOpen()
        {
            base.OnOpen();

            currentArea = NavigationArea.QuantityRow;
            currentQuantityButton = QuantityButton.Minimize;
            currentActionButton = ActionButton.Cancel;
            SelectCurrentButton();
            TutorialUIBinding.Bind(addToCartButton, TutorialAnchorKeys.PurchaseAddToCartButton);
        }
        #endregion

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
            if (addToCartButton != null)
                addToCartButton.onClick.AddListener(OnAddToCartButtonClicked);
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
            if (addToCartButton != null)
                addToCartButton.onClick.RemoveAllListeners();
        }

        /// <summary>
        /// 전달받은 아이템 정보, 수량으로 구매 정보 패널을 초기화
        /// </summary>
        /// <param name="itemData"></param>
        public void InitializePurchaseInformation(ItemData itemData, int maxQuantity)
        {
            InitializePurchaseInformation(itemData, maxQuantity, null);
        }

        public void InitializePurchaseInformation(ItemData itemData, int maxQuantity, string marketTown)
        {
            if (itemData == null)
            {
                Debug.LogError("Purchase Information Panel: Received NULL item data for initialization.");
                return;
            }
            currentItemData = itemData;
            this.marketTown = string.IsNullOrWhiteSpace(marketTown) ? itemData.Town : marketTown.Trim();

            this.maxQuantity = maxQuantity;

            unitPrice = DataManager.Instance.GetItemPrice(itemData.PriceListID, this.marketTown);
            unitWeight = Mathf.Round(itemData.Weight * 10f) / 10f;

            marketPriceText.text = unitPrice.ToString();
            itemPriceGraphView?.UpdateGraph(currentItemData, unitPrice, this.marketTown);
            unitWeightText.text = unitWeight.ToString("F1");

            purchasePanel = GetPurchasePanel();

            // 이미 장바구니에 담긴 수량을 기본값으로 표시하고, 없으면 1개부터 시작
            quantityInCart = purchasePanel?.Presenter?.GetQuantityInCart(itemData.ID) ?? 0;
            purchaseQuantity = quantityInCart > 0 ? quantityInCart : 1;

            UpdateItemUIs();
            UpdateQuantityRelatedTexts();
            UpdateWeightGaugeDisplay();
        }

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
                    if (currentActionButton < ActionButton.AddToCart)
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
                currentActionButton = (currentQuantityButton <= QuantityButton.Minus) ? ActionButton.Cancel : ActionButton.AddToCart;
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
                        case ActionButton.AddToCart: return addToCartButton;
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
                Debug.LogWarning("PurchaseInformationPanel: itemDescription reference is missing. Check prefab binding.");
        }

        public void AddQuantityToAdd(int quantity)
        {
            int AddedQuantity = purchaseQuantity + quantity;
            if (AddedQuantity < 0 || AddedQuantity > maxQuantity)
                return;

            purchaseQuantity = AddedQuantity;
            UpdateQuantityRelatedTexts();
        }

        public void SetQuantityToAdd(int quantity)
        {
            purchaseQuantity = quantity;
            UpdateQuantityRelatedTexts();
        }

        public void SetQuantityToAddMax()
        {
            purchaseQuantity = maxQuantity;
            UpdateQuantityRelatedTexts();
        }

        private void UpdateQuantityRelatedTexts()
        {
            UpdateQuantityControlText();
            UpdatePriceTexts();
            UpdateWeightTexts();
            UpdateWeightGaugeDisplay();
        }

        private void UpdateQuantityControlText()
        {
            if (quantityControlText != null)
                quantityControlText.text = purchaseQuantity.ToString();
        }

        private void UpdatePriceTexts()
        {
            if (purchaseQuantityText == null || totalPriceText == null)
                return;

            purchaseQuantityText.text = purchaseQuantity.ToString();
            totalPriceText.text = (unitPrice * purchaseQuantity).ToString();
        }

        private void UpdateWeightTexts()
        {
            if (totalWeightText != null)
                totalWeightText.text = (Mathf.Round(unitWeight * purchaseQuantity * 10f) / 10f).ToString("F1");
        }

        private void UpdateWeightGaugeDisplay()
        {
            if (currentItemData == null) return;

            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;
            if (playerInventory == null) return;

            // 정보 패널의 무게 게이지 업데이트
            float itemTotalWeight = Mathf.Round(unitWeight * purchaseQuantity * 10f) / 10f;
            float maxWeight = playerInventory.MaxWeight;
            float currentWeight = playerInventory.CurrentWeight;

            // 장바구니에 담긴 무게 추가
            float cartWeight = 0f;
            if (purchasePanel != null && purchasePanel.Presenter != null)
            {
                cartWeight = purchasePanel.Presenter.CalculateCurrentCartWeight();
            }

            if (weightGainedText != null && weightGauge != null)
            {
                // 대상 아이템의 기존 담긴 수량은 purchaseQuantity로 대체되므로 장바구니 무게에서 제외한다.
                // 장바구니 무게는 아직 보유분이 아니므로 증가분에만 포함한다.
                float otherItemsCartWeight = cartWeight - (Mathf.Round(unitWeight * quantityInCart * 10f) / 10f);
                float increasedWeight = Mathf.Round((otherItemsCartWeight + itemTotalWeight) * 10f) / 10f;
                weightGainedText.text = $"{currentWeight:F1}<#00a6ff>(+{increasedWeight:F1})</color> / {maxWeight:F1}kg";
                weightGauge.UpdateGauge(maxWeight, currentWeight, increasedWeight);
            }
        }

        /// <summary>
        /// 취소 버튼 클릭 이벤트
        /// </summary>
        private void OnCancelButtonClicked()
        {
            UIManager.Instance.ClosePanel<PurchaseInformationPanel>();
        }

        /// <summary>
        /// 장바구니 추가 버튼 클릭 이벤트
        /// </summary>
        private void OnAddToCartButtonClicked()
        {
            if (currentItemData == null)
            {
                Debug.LogError("PurchaseInformationPanel: Current item data is NULL.");
                return;
            }

            AddToCart(currentItemData.ID, purchaseQuantity);
        }

        /// <summary>
        /// 장바구니에 아이템 추가
        /// </summary>
        private void AddToCart(int itemID, int quantity)
        {
            if (purchasePanel == null)
            {
                purchasePanel = GetPurchasePanel();
                if (purchasePanel == null)
                {
                    Debug.LogError("PurchaseInformationPanel: Cannot get active PurchasePanel.");
                    return;
                }
            }

            // 이 패널의 수량은 장바구니의 최종 수량이다 (0이면 SetQuantityToTarget이 제거 처리)
            bool? success = purchasePanel.SetQuantityToCart(itemID, quantity);
            if (success == true)
                TutorialEventReporter.Report(TutorialEventType.CartUpdated, TutorialTargetIds.Potato, source: TutorialEventSource.Domain);
            UIManager.Instance.ClosePanel<PurchaseInformationPanel>();
        }
    }
}
