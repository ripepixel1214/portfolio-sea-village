using System;
using System.Collections.Generic;
using UnityEngine;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.UI.Shop
{
    /// <summary>
    /// 구매 패널 Presenter - PurchasePanel(View)와 Model 관리 및 비즈니스 로직 처리
    /// </summary>
    public class PurchasePresenter : IDisposable
    {
        private readonly IPurchaseView view;
        private readonly ShopModel model;
        
        private int currentShopID;
        private bool isDisposed;
        public string MarketTown => model.MarketTown;

        public PurchasePresenter(IPurchaseView view, string marketTown = null)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.model = new ShopModel(marketTown, enforcesPlayerWeightLimit: true);
            
            AddListeners();
        }

        public void SetMarketTown(string marketTown)
        {
            model.SetMarketTown(marketTown);
        }

        #region Initialization
        private void AddListeners()
        {
            model.OnDataChanged += HandleDataChanged;
            view.OnConfirmButtonClicked += HandlePurchaseClicked;
            view.OnSlotClicked += HandleShopSlotClicked;
            view.OnCartSlotClicked += HandleCartSlotClickedWithPosition;
        }

        private void RemoveListeners()
        {
            model.OnDataChanged -= HandleDataChanged;
            view.OnConfirmButtonClicked -= HandlePurchaseClicked;
            view.OnSlotClicked -= HandleShopSlotClicked;
            view.OnCartSlotClicked -= HandleCartSlotClickedWithPosition;
        }

        public void Initialize(int shopID)
        {
            currentShopID = shopID;
            LoadShopData();
        }

        private void LoadShopData()
        {
            var shopDataList = DataManager.Instance.ShopDatabase.GetShopsById(currentShopID);
            
            if (shopDataList == null || shopDataList.Count == 0)
            {
                Debug.LogWarning($"PurchasePresenter: 상점 ID {currentShopID}에 판매 중인 아이템이 없습니다.");
                model.Clear();
                return;
            }

            // 진열 수량은 저작 수량이 아니라 당일 판매량을 뺀 남은 재고를 쓴다. 매진된 아이템은 진열하지 않는다
            var items = new Dictionary<int, int>();
            TownKey marketTownKey = ResolveMarketTownKey();
            foreach (var shopData in shopDataList)
            {
                if (!GoldPurchasePolicy.CanPurchase(shopData.ItemID))
                    continue;

                if (items.ContainsKey(shopData.ItemID))
                    continue;

                int remaining = ShopStockManager.Instance.GetRemainingStock(currentShopID, shopData.ItemID, marketTownKey);
                if (remaining > 0)
                    items.Add(shopData.ItemID, remaining);
            }

            model.InitializeSourceItems(items);
            Debug.Log($"PurchasePresenter: 상점 ID {currentShopID}에서 {items.Count}개의 아이템을 로드했습니다.");
        }

        public void Clear()
        {
            model.Clear();
        }
        #endregion

        #region Cart Operations
        public bool SetCartQuantity(int itemID, int quantity)
        {
            if (!model.SetQuantityToTarget(itemID, quantity, out string failReason))
            {
                view.ShowMessage(failReason);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 상점의 원본 최대 재고 반환 (장바구니 무관)
        /// </summary>
        public int GetOriginalMaxQuantity(int itemID)
        {
            return model.GetOriginalMaxQuantity(itemID);
        }

        /// <summary>
        /// 현재 장바구니에 담긴 아이템의 수량 반환
        /// </summary>
        public int GetQuantityInCart(int itemID)
        {
            return model.GetTargetQuantity(itemID);
        }

        /// <summary>
        /// 현재 장바구니의 총 무게 계산
        /// </summary>
        public float CalculateCurrentCartWeight()
        {
            return model.CalculateTotalWeight();
        }

        #endregion

        #region Event Handlers
        private void HandleDataChanged()
        {
            RefreshView();
            UpdatePriceDisplay();
            UpdateWeightGauge();
        }

        private void HandlePurchaseClicked()
        {
            ExecutePurchase();
        }

        private void HandleShopSlotClicked(int itemID)
        {
            int maxQuantity = model.GetOriginalMaxQuantity(itemID);
            ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(itemID);
            
            if (itemData != null)
            {
                // PurchaseInformationPanel 열기
                PurchaseInformationPanel panel = UIManager.Instance.OpenPanel<PurchaseInformationPanel>();
                if (panel != null)
                {
                    panel.InitializePurchaseInformation(itemData, maxQuantity, MarketTown);
                    if (itemID == TutorialItemIds.Potato)
                        TutorialEventReporter.Report(TutorialEventType.ItemSelected, TutorialTargetIds.ShopPotato, source: TutorialEventSource.UserInterface);
                }
            }
        }

        private void HandleCartSlotClickedWithPosition(int itemID, Vector2 slotPosition)
        {
            // 장바구니 슬롯에서는 상점의 원본 최대 재고 기준으로 수량 조절
            int maxQuantity = model.GetOriginalMaxQuantity(itemID);
            int quantity = model.GetTargetQuantity(itemID);
            UIManager.Instance.OpenPanel<MenuBarPanel>()?.InitializeCartMenu(itemID, slotPosition, quantity, maxQuantity, MarketTown);
        }
        #endregion

        #region View Update
        private void RefreshView()
        {
            view.RefreshSourceSlots(model.SourceItems);
            view.RefreshTargetSlots(model.TargetItems);
        }

        private void UpdatePriceDisplay()
        {
            int totalPrice = model.CalculateTotalPrice();
            int tradingFee = ShopUtility.CalculateTradingFee(totalPrice);
            int finalPrice = ShopUtility.CalculateFinalPrice(totalPrice, isPurchase: true);

            view.UpdatePriceDisplay(totalPrice, tradingFee, finalPrice);
        }

        private void UpdateWeightGauge()
        {
            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;
            if (playerInventory == null)
            {
                Debug.LogWarning("PurchasePresenter: Player inventory is not available.");
                return;
            }

            float maxWeight = playerInventory.MaxWeight;
            float currentWeight = playerInventory.CurrentWeight;
            float cartWeight = model.CalculateTotalWeight();

            view.UpdateWeightGauge(maxWeight, currentWeight, cartWeight);
        }
        #endregion

        #region Transaction
        private void ExecutePurchase()
        {
            if (!model.HasTargetItems)
            {
                view.ShowMessage("장바구니가 비어서 구매할 수 없다");
                return;
            }

            if (!model.AllTargetItemsMatch(GoldPurchasePolicy.CanPurchase))
            {
                view.ShowMessage("이 아이템은 골드로 구매할 수 없다");
                return;
            }

            int totalPrice = model.CalculateTotalPrice();
            int finalPrice = ShopUtility.CalculateFinalPrice(totalPrice, isPurchase: true);

            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;
            if (playerInventory == null)
            {
                view.ShowMessage("[Error] 가방을 찾을 수 없습니다");
                return;
            }

            long purchaseCost = Math.Max(0L, finalPrice);
            if (!CurrencyManager.Instance.CanPlayerSpend(CurrencyType.Gold, purchaseCost))
            {
                view.ShowMessage("골드가 부족하다");
                return;
            }

            if (!CurrencyManager.Instance.TrySpendPlayer(CurrencyType.Gold, purchaseCost))
            {
                view.ShowMessage("[Error] 결제에 실패해 구매를 취소했습니다");
                return;
            }

            var addedItems = new List<KeyValuePair<int, int>>();

            foreach (var kvp in model.TargetItems)
            {
                ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(kvp.Key);
                if (itemData != null)
                {
                    string marketTown = ResolveMarketTown(itemData.Town);
                    int unitPrice = DataManager.Instance.GetItemPrice(itemData.PriceListID, marketTown);
                    Debug.Log($"구매 처리: ItemID={kvp.Key}, Quantity={kvp.Value}, UnitPrice={unitPrice}");

                    bool addResult = playerInventory.AddItem(kvp.Key, kvp.Value, unitPrice);
                    if (!addResult)
                    {
                        foreach (var added in addedItems)
                            playerInventory.RemoveItem(added.Key, added.Value);

                        CurrencyManager.Instance.TryAddPlayer(CurrencyType.Gold, purchaseCost);
                        view.ShowMessage("가방이 가득 차서 구매할 수 없다");
                        return;
                    }

                    addedItems.Add(new KeyValuePair<int, int>(kvp.Key, kvp.Value));
                }
            }

            // 롤백 가능성이 사라진 뒤에 재고를 차감한다
            foreach (var added in addedItems)
                ShopStockManager.Instance.RecordSale(currentShopID, added.Key, added.Value);

            RecordPurchasedItems(addedItems);

            Audio.AudioManager.TryPlaySfx(Audio.AudioManager.SfxCash);
            view.ShowMessage(
                $"{purchaseCost}G를 지불했다",
                () => TutorialEventReporter.Report(TutorialEventType.UiElementActivated, TutorialTargetIds.PurchaseMessage, source: TutorialEventSource.UserInterface));
            TutorialEventReporter.Report(TutorialEventType.PurchaseCompleted, TutorialTargetIds.Potato, source: TutorialEventSource.Domain);
            UIManager.Instance.ClosePanel<PurchasePanel>();
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (isDisposed) return;
            
            RemoveListeners();
            model.Clear();
            isDisposed = true;
        }
        #endregion

        private string ResolveMarketTown(string fallbackTown)
        {
            return ShopUtility.ResolveMarketTown(MarketTown, fallbackTown);
        }

        private void RecordPurchasedItems(IReadOnlyList<KeyValuePair<int, int>> purchasedItems)
        {
            if (!TownProgressionManager.HasInstance || purchasedItems == null)
                return;

            int totalQuantity = 0;
            for (int i = 0; i < purchasedItems.Count; i++)
            {
                int quantity = Math.Max(0, purchasedItems[i].Value);
                totalQuantity = totalQuantity > int.MaxValue - quantity
                    ? int.MaxValue
                    : totalQuantity + quantity;
            }

            if (totalQuantity <= 0)
                return;

            TownProgressionManager.Instance.RecordPurchasedItems(ResolveMarketTownKey(), totalQuantity);
        }

        private TownKey ResolveMarketTownKey()
        {
            if (TownKeyUtility.TryParse(MarketTown, out TownKey townKey))
                return townKey;

            return GameManager.HasInstance
                ? GameManager.Instance.CurrentTownKey
                : TownKey.Unknown;
        }
    }
}
