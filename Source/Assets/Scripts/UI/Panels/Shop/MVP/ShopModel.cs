using System;
using System.Collections.Generic;
using UnityEngine;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.UI.Shop
{
    /// <summary>
    /// 상점 데이터 모델 - 구매/판매 공통 로직
    /// </summary>
    public class ShopModel
    {
        // 소스 아이템 (상점 재고 / 플레이어 인벤토리)
        private readonly Dictionary<int, int> sourceItemsMax = new Dictionary<int, int>();
        private readonly Dictionary<int, int> sourceItems = new Dictionary<int, int>();

        // 타겟 아이템 (장바구니 / 판매 목록)
        private readonly Dictionary<int, int> targetItems = new Dictionary<int, int>();

        // Events
        public event Action OnDataChanged;

        private string marketTown;

        // 타겟에 담은 만큼 플레이어가 짊어지게 되는가 (구매: true, 판매: false)
        private readonly bool enforcesPlayerWeightLimit;

        // Properties
        public IReadOnlyDictionary<int, int> SourceItems => sourceItems;
        public IReadOnlyDictionary<int, int> TargetItems => targetItems;
        public bool HasTargetItems => targetItems.Count > 0;
        public string MarketTown => marketTown;

        public ShopModel(string marketTown, bool enforcesPlayerWeightLimit)
        {
            this.enforcesPlayerWeightLimit = enforcesPlayerWeightLimit;
            SetMarketTown(marketTown);
        }

        public void SetMarketTown(string town)
        {
            marketTown = TownKeyUtility.NormalizeStorageKey(town);
        }

        #region Initialization
        /// <summary>
        /// 소스 아이템 초기화 (상점 재고 또는 인벤토리)
        /// </summary>
        public void InitializeSourceItems(Dictionary<int, int> items)
        {
            sourceItemsMax.Clear();
            sourceItems.Clear();
            targetItems.Clear();

            foreach (var kvp in items)
            {
                sourceItemsMax[kvp.Key] = kvp.Value;
                sourceItems[kvp.Key] = kvp.Value;
            }

            OnDataChanged?.Invoke();
        }

        /// <summary>
        /// 모든 데이터 초기화
        /// </summary>
        public void Clear()
        {
            sourceItemsMax.Clear();
            sourceItems.Clear();
            targetItems.Clear();
            OnDataChanged?.Invoke();
        }
        #endregion

        #region Item Transfer
        /// <summary>
        /// 타겟(장바구니/판매목록)에 아이템 수량 설정
        /// </summary>
        /// <returns>성공 여부. 실패 시 failReason에 사용자에게 보여줄 사유를 담는다</returns>
        public bool SetQuantityToTarget(int itemID, int quantity, out string failReason)
        {
            if (!sourceItemsMax.TryGetValue(itemID, out int maxQuantity))
            {
                failReason = "[Error] 해당 아이템을 찾을 수 없습니다";
                return false;
            }

            if (quantity < 0 || quantity > maxQuantity)
            {
                failReason = $"[Error] 0~{maxQuantity}개까지만 담을 수 있습니다";
                return false;
            }

            if (enforcesPlayerWeightLimit && !CanPlayerCarry(itemID, quantity))
            {
                failReason = "가방이 너무 무겁다";
                return false;
            }

            failReason = string.Empty;

            // 타겟에 수량 설정
            if (quantity > 0)
                targetItems[itemID] = quantity;
            else
                targetItems.Remove(itemID);

            // 소스에서 남은 수량 업데이트
            int remainingQuantity = maxQuantity - quantity;
            if (remainingQuantity > 0)
                sourceItems[itemID] = remainingQuantity;
            else
                sourceItems.Remove(itemID);

            OnDataChanged?.Invoke();
            return true;
        }

        public int GetTargetQuantity(int itemID)
        {
            return targetItems.TryGetValue(itemID, out int qty) ? qty : 0;
        }

        public bool AllTargetItemsMatch(Predicate<int> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            foreach (int itemId in targetItems.Keys)
            {
                if (!predicate(itemId))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 원본 최대 수량 (장바구니 미반영)
        /// </summary>
        public int GetOriginalMaxQuantity(int itemID)
        {
            return sourceItemsMax.TryGetValue(itemID, out int max) ? max : 0;
        }

        /// <summary>
        /// 대상 아이템의 수량을 quantity로 설정했을 때 플레이어 무게 한계를 넘지 않는지 확인
        /// </summary>
        private bool CanPlayerCarry(int itemID, int quantity)
        {
            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;
            if (playerInventory == null) return false;

            ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(itemID);
            if (itemData == null) return false;

            // 대상 아이템의 기존 수량은 quantity로 대체되므로 장바구니 무게에서 제외
            float otherItemsWeight = CalculateTotalWeight() - Mathf.Round(itemData.Weight * GetTargetQuantity(itemID) * 10f) / 10f;
            float totalWeight = Mathf.Round((playerInventory.CurrentWeight + otherItemsWeight + itemData.Weight * quantity) * 10f) / 10f;
            return totalWeight <= playerInventory.MaxWeight;
        }
        #endregion

        #region Price Calculation
        /// <summary>
        /// 타겟 아이템들의 총 가격 계산
        /// </summary>
        public int CalculateTotalPrice()
        {
            int total = 0;
            foreach (var kvp in targetItems)
            {
                ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(kvp.Key);
                if (itemData != null)
                {
                    string town = ResolveMarketTown(itemData.Town);
                    int unitPrice = DataManager.Instance.GetItemPrice(itemData.PriceListID, town);
                    total += unitPrice * kvp.Value;
                }
            }
            return total;
        }

        private string ResolveMarketTown(string fallbackTown)
        {
            return ShopUtility.ResolveMarketTown(marketTown, fallbackTown);
        }

        #endregion

        #region Weight Calculation
        /// <summary>
        /// 타겟 아이템들의 총 무게 계산
        /// </summary>
        /// <returns></returns>
        public float CalculateTotalWeight()
        {
            float totalWeight = 0f;
            foreach (var kvp in targetItems)
            {
                ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(kvp.Key);

                if (itemData != null)
                    totalWeight += itemData.Weight * kvp.Value;
            }
            return Mathf.Round(totalWeight * 10f) / 10f;
        }
        #endregion
    }
}
