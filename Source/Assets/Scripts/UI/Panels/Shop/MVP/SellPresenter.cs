using System;
using System.Collections.Generic;
using UnityEngine;
using SeaVillage.Data;
using SeaVillage.Core;

namespace SeaVillage.UI.Shop
{
    /// <summary>
    /// 판매 패널 Presenter - 다중 소스(플레이어/배 인벤토리) 관리 및 판매 비즈니스 로직 처리
    /// </summary>
    public class SellPresenter : IDisposable
    {
        private readonly ISellView view;
        
        // 각 인벤토리별 Model. 판매 목록은 두 모델의 TargetItems가 단일 진리원이며,
        // 같은 아이템은 출처와 무관하게 하나의 슬롯으로 병합해 다룬다.
        private readonly ShopModel playerInventoryModel;
        private readonly ShopModel shipInventoryModel;

        private bool isDisposed;

        public string MarketTown => playerInventoryModel.MarketTown;

        public SellPresenter(ISellView view, string marketTown = null)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            // 판매는 보유 중인 물건을 내보내므로 플레이어 무게 한계를 적용하지 않는다
            this.playerInventoryModel = new ShopModel(marketTown, enforcesPlayerWeightLimit: false);
            this.shipInventoryModel = new ShopModel(marketTown, enforcesPlayerWeightLimit: false);
            
            SubscribeToEvents();
        }

        public void SetMarketTown(string marketTown)
        {
            playerInventoryModel.SetMarketTown(marketTown);
            shipInventoryModel.SetMarketTown(marketTown);
        }

        #region Initialization
        private void SubscribeToEvents()
        {
            playerInventoryModel.OnDataChanged += HandlePlayerDataChanged;
            shipInventoryModel.OnDataChanged += HandleShipDataChanged;
            view.OnConfirmButtonClicked += HandleSellClicked;
            view.OnCartSlotClicked += HandleSellListSlotClickedWithPosition;
        }

        private void UnsubscribeFromEvents()
        {
            playerInventoryModel.OnDataChanged -= HandlePlayerDataChanged;
            shipInventoryModel.OnDataChanged -= HandleShipDataChanged;
            view.OnConfirmButtonClicked -= HandleSellClicked;
            view.OnCartSlotClicked -= HandleSellListSlotClickedWithPosition;
        }

        /// <summary>
        /// 인벤토리 데이터 로드
        /// </summary>
        public void Initialize()
        {
            LoadPlayerInventory();
            LoadShipInventory();
            RefreshAllViews();
        }

        private void LoadPlayerInventory()
        {
            var playerInventory = InventoryManager.PlayerInventoryOrNull;

            if (playerInventory == null)
            {
                Debug.LogWarning("SellPresenter: PlayerInventory가 null입니다.");
                playerInventoryModel.Clear();
                return;
            }

            var items = new Dictionary<int, int>();
            foreach (var kvp in playerInventory.Items)
            {
                var invItem = kvp.Value;
                if (IsUnsellable(invItem.id))
                    continue;
                if (!items.ContainsKey(invItem.id))
                    items.Add(invItem.id, invItem.quantity);
            }

            playerInventoryModel.InitializeSourceItems(items);
        }

        private void LoadShipInventory()
        {
            var shipInventory = InventoryManager.ShipInventoryOrNull;

            if (shipInventory == null)
            {
                shipInventoryModel.Clear();
                return;
            }

            var items = new Dictionary<int, int>();
            foreach (var kvp in shipInventory.Items)
            {
                var invItem = kvp.Value;
                if (IsUnsellable(invItem.id))
                    continue;
                if (!items.ContainsKey(invItem.id))
                    items.Add(invItem.id, invItem.quantity);
            }

            shipInventoryModel.InitializeSourceItems(items);
        }

        /// <summary>판매 불가 아이템(가게 아이템 등)인지</summary>
        private static bool IsUnsellable(int itemId)
        {
            ItemData itemData = DataManager.HasInstance ? DataManager.Instance.GetItem(itemId) : null;
            return itemData != null && itemData.Unsellable;
        }

        public void Clear()
        {
            playerInventoryModel.Clear();
            shipInventoryModel.Clear();
        }
        #endregion

        #region Sell List Operations
        /// <summary>
        /// 지정한 출처 인벤토리에서 담을 수량 설정 (0개면 해당 출처분 제거).
        /// 다른 출처에 담긴 수량은 건드리지 않으므로 판매 목록에서 합산된다
        /// </summary>
        public bool SetSellQuantity(int itemID, int quantity, SlotType sourceType)
        {
            ShopModel sourceModel = GetModelBySlotType(sourceType);
            if (sourceModel == null)
            {
                Debug.LogWarning($"SellPresenter: 알 수 없는 출처입니다. (ItemID: {itemID}, SourceType: {sourceType})");
                return false;
            }

            if (!sourceModel.SetQuantityToTarget(itemID, quantity, out string failReason))
            {
                view.ShowMessage(failReason);
                return false;
            }

            NotifySellListChanged();
            return true;
        }

        /// <summary>
        /// 판매 목록의 총 수량 설정 (0개면 제거).
        /// 수량을 줄여 인벤토리로 되돌릴 때는 플레이어 인벤토리분부터 되돌린다
        /// </summary>
        public bool SetSellTotalQuantity(int itemID, int totalQuantity)
        {
            int playerOwned = playerInventoryModel.GetOriginalMaxQuantity(itemID);
            int shipOwned = shipInventoryModel.GetOriginalMaxQuantity(itemID);
            int owned = playerOwned + shipOwned;

            if (owned == 0)
            {
                view.ShowMessage("[Error] 해당 아이템은 갖고 있지 않습니다");
                return false;
            }

            if (totalQuantity < 0 || totalQuantity > owned)
            {
                view.ShowMessage($"[Error] 0~{owned}개까지만 담을 수 있습니다");
                return false;
            }

            // 배 인벤토리분을 우선 유지해, 줄어드는 만큼은 플레이어 인벤토리로 먼저 돌아가게 한다
            int toShip = Math.Min(totalQuantity, shipOwned);
            int toPlayer = totalQuantity - toShip;

            ApplyTargetQuantity(shipInventoryModel, itemID, toShip);
            ApplyTargetQuantity(playerInventoryModel, itemID, toPlayer);

            NotifySellListChanged();
            return true;
        }

        // 해당 인벤토리에 없는 아이템은 건드리지 않는다
        private static void ApplyTargetQuantity(ShopModel model, int itemID, int quantity)
        {
            if (model.GetOriginalMaxQuantity(itemID) == 0)
                return;

            if (!model.SetQuantityToTarget(itemID, quantity, out string failReason))
                Debug.LogWarning($"SellPresenter: 판매 목록 반영 실패. (ItemID: {itemID}, Quantity: {quantity}, Reason: {failReason})");
        }

        private void NotifySellListChanged()
        {
            UpdatePriceDisplay();
            RefreshSellListView();
        }

        private ShopModel GetModelBySlotType(SlotType slotType)
        {
            return slotType switch
            {
                SlotType.PlayerInventory => playerInventoryModel,
                SlotType.ShipInventory => shipInventoryModel,
                _ => null
            };
        }

        /// <summary>
        /// 지정한 출처 인벤토리의 보유 수량 반환 (판매 목록 미반영)
        /// </summary>
        public int GetOriginalMaxQuantity(int itemID, SlotType sourceType)
        {
            return GetModelBySlotType(sourceType)?.GetOriginalMaxQuantity(itemID) ?? 0;
        }

        /// <summary>
        /// 두 인벤토리를 합친 보유 수량 반환 (판매 목록 미반영)
        /// </summary>
        public int GetOwnedQuantity(int itemID)
        {
            return playerInventoryModel.GetOriginalMaxQuantity(itemID)
                 + shipInventoryModel.GetOriginalMaxQuantity(itemID);
        }

        /// <summary>
        /// 지정한 출처 인벤토리에서 판매 목록에 담긴 수량 반환
        /// </summary>
        public int GetQuantityInSellList(int itemID, SlotType sourceType)
        {
            return GetModelBySlotType(sourceType)?.GetTargetQuantity(itemID) ?? 0;
        }

        /// <summary>
        /// 두 인벤토리에 나뉘어 있는 같은 아이템의 보유 수량 가중 평균 구매가 (기록이 없으면 원가)
        /// </summary>
        public int GetUnitPurchasePrice(int itemID)
        {
            int totalQuantity = 0;
            long totalCost = 0;

            AccumulatePurchaseCost(SlotType.PlayerInventory, itemID, ref totalQuantity, ref totalCost);
            AccumulatePurchaseCost(SlotType.ShipInventory, itemID, ref totalQuantity, ref totalCost);

            if (totalQuantity > 0)
                return (int)(totalCost / totalQuantity);

            ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(itemID);
            return itemData != null ? itemData.OriginPrice : 0;
        }

        private void AccumulatePurchaseCost(SlotType sourceType, int itemID, ref int totalQuantity, ref long totalCost)
        {
            InventoryData inventory = GetSourceInventory(sourceType);
            if (inventory == null || !inventory.Items.TryGetValue(itemID, out var invItem))
                return;

            totalQuantity += invItem.quantity;
            totalCost += (long)invItem.averagePurchasePrice * invItem.quantity;
        }

        /// <summary>
        /// 판매 목록에 담긴 아이템의 총 수량 반환 (두 인벤토리 합산)
        /// </summary>
        public int GetTotalQuantityInSellList(int itemID)
        {
            return playerInventoryModel.GetTargetQuantity(itemID) + shipInventoryModel.GetTargetQuantity(itemID);
        }
        #endregion

        #region Event Handlers
        private void HandlePlayerDataChanged()
        {
            view.RefreshPlayerInventorySlots(playerInventoryModel.SourceItems);
        }

        private void HandleShipDataChanged()
        {
            view.RefreshShipInventorySlots(shipInventoryModel.SourceItems);
        }

        private void HandleSellClicked()
        {
            ExecuteSell();
        }
        
        private void HandleSellListSlotClickedWithPosition(int itemID, Vector2 slotPosition)
        {
            int currentQuantity = GetTotalQuantityInSellList(itemID);
            if (currentQuantity == 0)
                return;

            UIManager.Instance.OpenPanel<MenuBarPanel>()?.InitializeSellListMenu(itemID, slotPosition, currentQuantity, GetOwnedQuantity(itemID), MarketTown);
        }
        #endregion

        #region View Update
        private void RefreshAllViews()
        {
            view.RefreshPlayerInventorySlots(playerInventoryModel.SourceItems);
            view.RefreshShipInventorySlots(shipInventoryModel.SourceItems);
            RefreshSellListView();
            UpdatePriceDisplay();
        }

        // 같은 아이템은 출처와 무관하게 하나의 슬롯으로 합산해 표시한다
        private void RefreshSellListView()
        {
            var sellListDict = new Dictionary<int, int>();
            AccumulateTargetItems(playerInventoryModel, sellListDict);
            AccumulateTargetItems(shipInventoryModel, sellListDict);

            view.RefreshSellListSlots(sellListDict);
        }

        private static void AccumulateTargetItems(ShopModel model, Dictionary<int, int> destination)
        {
            foreach (var kvp in model.TargetItems)
            {
                destination.TryGetValue(kvp.Key, out int existing);
                destination[kvp.Key] = existing + kvp.Value;
            }
        }

        private void UpdatePriceDisplay()
        {
            int totalPrice = CalculateTotalSellPrice();
            int purchasePrice = CalculateTotalPurchasePrice();
            int tradingFee = ShopUtility.CalculateTradingFee(totalPrice);
            int finalPrice = ShopUtility.CalculateFinalPrice(totalPrice, isPurchase: false);

            view.UpdatePriceDisplay(totalPrice, tradingFee, finalPrice, purchasePrice);
        }

        private int CalculateTotalSellPrice()
        {
            return playerInventoryModel.CalculateTotalPrice() + shipInventoryModel.CalculateTotalPrice();
        }

        // 병합된 판매 목록 기준으로 계산해야 정보 패널의 원가 표시와 일치한다
        private int CalculateTotalPurchasePrice()
        {
            var sellList = new Dictionary<int, int>();
            AccumulateTargetItems(playerInventoryModel, sellList);
            AccumulateTargetItems(shipInventoryModel, sellList);

            int total = 0;
            foreach (var kvp in sellList)
                total += GetUnitPurchasePrice(kvp.Key) * kvp.Value;

            return total;
        }

        private InventoryData GetSourceInventory(SlotType slotType)
        {
            return slotType switch
            {
                SlotType.PlayerInventory => InventoryManager.PlayerInventoryOrNull,
                SlotType.ShipInventory => InventoryManager.ShipInventoryOrNull,
                _ => null
            };
        }
        #endregion

        #region Transaction
        private void ExecuteSell()
        {
            if (!playerInventoryModel.HasTargetItems && !shipInventoryModel.HasTargetItems)
            {
                view.ShowMessage("판매 목록이 비어서 판매할 수 없다");
                return;
            }

            int totalPrice = CalculateTotalSellPrice();
            int netProfit = ShopUtility.CalculateFinalPrice(totalPrice, isPurchase: false);

            if (!HasEnoughItems(playerInventoryModel, SlotType.PlayerInventory)
                || !HasEnoughItems(shipInventoryModel, SlotType.ShipInventory))
            {
                view.ShowMessage("[Error] 판매할 아이템이 부족합니다");
                return;
            }

            var removedItems = new List<(InventoryData inventory, int itemId, int quantity, int purchasePrice)>();

            if (!TryRemoveTargetItems(playerInventoryModel, SlotType.PlayerInventory, removedItems)
                || !TryRemoveTargetItems(shipInventoryModel, SlotType.ShipInventory, removedItems))
            {
                RollbackRemovedItems(removedItems);
                Debug.LogWarning("[SellPresenter] 아이템 차감에 실패해 판매 롤백");
                view.ShowMessage("[Error] 판매에 실패했습니다");
                return;
            }

            long reward = Math.Max(0L, netProfit);
            if (!CurrencyManager.Instance.TryAddPlayer(CurrencyType.Gold, reward))
            {
                RollbackRemovedItems(removedItems);
                Debug.LogWarning("[SellPresenter] 골드 지급에 실패해 판매 롤백");
                view.ShowMessage("[Error] 판매에 실패했습니다");
                return;
            }

            Audio.AudioManager.TryPlaySfx(Audio.AudioManager.SfxCash);
            view.ShowMessage($"{netProfit}G를 획득했다");
            UIManager.Instance.ClosePanel<SellPanel>();
        }

        /// <summary>
        /// 판매 목록의 수량만큼 출처 인벤토리에 실제로 남아 있는지 확인
        /// </summary>
        private bool HasEnoughItems(ShopModel model, SlotType sourceType)
        {
            if (!model.HasTargetItems)
                return true;

            InventoryData inventory = GetSourceInventory(sourceType);
            if (inventory == null)
                return false;

            foreach (var kvp in model.TargetItems)
            {
                if (!inventory.HasItem(kvp.Key, kvp.Value))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 판매 목록의 아이템을 출처 인벤토리에서 차감하고 롤백 정보를 누적
        /// </summary>
        private bool TryRemoveTargetItems(ShopModel model, SlotType sourceType,
            List<(InventoryData inventory, int itemId, int quantity, int purchasePrice)> removedItems)
        {
            if (!model.HasTargetItems)
                return true;

            InventoryData inventory = GetSourceInventory(sourceType);
            if (inventory == null)
                return false;

            foreach (var kvp in model.TargetItems)
            {
                if (!inventory.Items.TryGetValue(kvp.Key, out var beforeItem))
                    return false;

                if (!inventory.RemoveItem(kvp.Key, kvp.Value))
                    return false;

                removedItems.Add((inventory, kvp.Key, kvp.Value, beforeItem.averagePurchasePrice));
            }

            return true;
        }

        private static void RollbackRemovedItems(List<(InventoryData inventory, int itemId, int quantity, int purchasePrice)> removedItems)
        {
            for (int i = removedItems.Count - 1; i >= 0; i--)
            {
                var entry = removedItems[i];
                entry.inventory?.AddItem(entry.itemId, entry.quantity, entry.purchasePrice);
            }
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            if (isDisposed) return;
            
            UnsubscribeFromEvents();
            Clear();
            isDisposed = true;
        }
        #endregion

    }
}
