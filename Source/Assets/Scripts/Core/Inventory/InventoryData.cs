using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using SeaVillage.Data;

namespace SeaVillage.Core
{
    /// <summary>
    /// 인벤토리 데이터 클래스
    /// </summary>
    [Serializable]
    public class InventoryData
    {
        [SerializeField] private float currentWeight = 0f;

        [SerializeField] private Dictionary<int, InventoryItem> items = new Dictionary<int, InventoryItem>();

        // 최대 무게를 결정하는 함수 (외부에서 람다로 주입)
        private Func<float> maxWeightProvider;
        
        // Properties
        public float MaxWeight => Mathf.Round((maxWeightProvider?.Invoke() ?? 15f) * 10f) / 10f;
        public bool IsFull => currentWeight >= MaxWeight;
        public int ItemCount => items.Count;
        public float CurrentWeight => currentWeight;
        public IReadOnlyDictionary<int, InventoryItem> Items => items;

        // Events
        public event Action<int, int> OnItemAdded;
        public event Action<int, int> OnItemRemoved;
        public event Action OnInventoryChanged;
        public event Action OnOverweight;

        public InventoryData(Func<float> maxWeightProvider = null)
        {
            this.maxWeightProvider = maxWeightProvider;
            items = new Dictionary<int, InventoryItem>();
        }

        /// <summary>
        /// 최대 무게 제공자 설정
        /// </summary>
        public void SetMaxWeightProvider(Func<float> provider)
        {
            maxWeightProvider = provider;
        }

        /// <summary>
        /// 아이템 추가
        /// </summary>
        /// <param name="itemID">아이템 ID</param>
        /// <param name="quantity">추가 수량</param>
        /// <param name="purchasePrice">단위당 구매가 (0이면 원가 사용)</param>
        public bool AddItem(int itemID, int quantity = 1, int purchasePrice = 0)
        {
            ItemData itemData = DataManager.Instance.ItemDatabase.GetItem(itemID);
            if (itemData == null)
            {
                Debug.LogWarning($"아이템 ID {itemID}를 찾을 수 없습니다.");
                return false;
            }

            // 구매가가 지정되지 않았으면 원가 사용
            if (purchasePrice == 0)
                purchasePrice = itemData.OriginPrice;

            // 무게 체크
            float weightToAdd = Mathf.Round(itemData.Weight * quantity * 10f) / 10f;
            if (currentWeight + weightToAdd > MaxWeight)
            {
                OnOverweight?.Invoke();
                return false;
            }

            // 아이템 추가 및 구매평균가 계산
            if (items.TryGetValue(itemID, out InventoryItem invItem))
            {
                int newTotalPrice = invItem.totalPurchasePrice + (purchasePrice * quantity);
                int newQuantity = invItem.quantity + quantity;
                invItem.totalPurchasePrice = newTotalPrice;
                invItem.quantity = newQuantity;
                invItem.averagePurchasePrice = newTotalPrice / newQuantity; // 이동평균
                items[itemID] = invItem;
            }
            else
            {
                items[itemID] = new InventoryItem
                {
                    id = itemID,
                    quantity = quantity,
                    unitWeight = Mathf.Round(itemData.Weight * 10f) / 10f,
                    totalPurchasePrice = purchasePrice * quantity,
                    averagePurchasePrice = purchasePrice
                };
            }

            currentWeight = Mathf.Round((currentWeight + weightToAdd) * 10f) / 10f;

            OnItemAdded?.Invoke(itemID, quantity);
            OnInventoryChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// 아이템 제거
        /// </summary>
        public bool RemoveItem(int itemID, int quantity = 1)
        {
            if (!items.TryGetValue(itemID, out InventoryItem invItem))
                return false;
            if (invItem.quantity < quantity)
                return false;

            float weightToRemove = Mathf.Round(invItem.unitWeight * quantity * 10f) / 10f;
            invItem.quantity -= quantity;
            
            // 구매평균가 기준으로 총 구매가 감소
            invItem.totalPurchasePrice -= invItem.averagePurchasePrice * quantity;

            if (invItem.quantity <= 0)
                items.Remove(itemID);
            else
                items[itemID] = invItem;

            currentWeight = Mathf.Max(0f, Mathf.Round((currentWeight - weightToRemove) * 10f) / 10f);

            OnItemRemoved?.Invoke(itemID, quantity);
            OnInventoryChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// 아이템 사용
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UseItem(int itemID, int quantity = 1)
        {
            var itemData = DataManager.Instance.ItemDatabase.GetItem(itemID);
            Debug.Log($"{itemData?.Name}을(를) {quantity}개 사용했습니다.");
            return RemoveItem(itemID, quantity);
        }

        /// <summary>
        /// 아이템 보유량 확인
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetItemCount(int itemID)
        {
            return items.TryGetValue(itemID, out InventoryItem item) ? item.quantity : 0;
        }

        /// <summary>
        /// 모든 아이템 목록 반환
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public List<InventoryItem> GetAllItems()
        {
            var result = new List<InventoryItem>(items.Count);
            CopyAllItemsTo(result);
            return result;
        }

        public void CopyAllItemsTo(List<InventoryItem> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.Clear();
            foreach (InventoryItem item in items.Values)
                target.Add(item);
        }

        /// <summary>
        /// 특정 아이템 보유 여부 확인
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasItem(int itemID, int requiredQuantity = 1)
        {
            return GetItemCount(itemID) >= requiredQuantity;
        }

        /// <summary>
        /// 인벤토리 초기화
        /// </summary>
        public void Clear()
        {
            items.Clear();
            currentWeight = 0f;
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// 이벤트 구독 해제 (메모리 누수 방지)
        /// </summary>
        public void ClearAllEvents()
        {
            OnItemAdded = null;
            OnItemRemoved = null;
            OnInventoryChanged = null;
            OnOverweight = null;
        }
    }
}
