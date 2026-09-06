using System;
using System.Collections.Generic;
using UnityEngine;
using SeaVillage.Data;
using SeaVillage.Utilities;

namespace SeaVillage.Core
{
    /// <summary>
    /// NPC 상점의 당일 판매량을 관리한다.
    /// 남은 재고 = ShopDatabase의 저작 수량 - 당일 판매량이며, 날짜가 바뀌면 초기화된다.
    /// </summary>
    public class ShopStockManager : Singleton<ShopStockManager>
    {
        // Key: 상점 ID + 아이템 ID, Value: 당일 판매 수량
        private readonly Dictionary<ShopKey, int> _soldToday = new Dictionary<ShopKey, int>();

        private void OnEnable()
        {
            TimeManager.OnDayChanged += HandleDayChanged;
        }

        private void OnDisable()
        {
            TimeManager.OnDayChanged -= HandleDayChanged;
        }

        /// <summary>
        /// 해당 상점 아이템의 남은 재고
        /// </summary>
        public int GetRemainingStock(int shopId, int itemId, TownKey townKey)
        {
            int authoredCount = GetAuthoredCount(shopId, itemId);
            if (authoredCount <= 0)
                return 0;

            if (HasDoubleStockBonus(townKey))
                authoredCount = authoredCount > int.MaxValue / 2 ? int.MaxValue : authoredCount * 2;

            int sold = _soldToday.TryGetValue(new ShopKey(shopId, itemId), out int value) ? value : 0;
            return Mathf.Max(0, authoredCount - sold);
        }

        /// <summary>
        /// 판매를 기록해 남은 재고를 차감
        /// </summary>
        public void RecordSale(int shopId, int itemId, int quantity)
        {
            if (shopId <= 0 || itemId <= 0 || quantity <= 0)
                return;

            var key = new ShopKey(shopId, itemId);
            _soldToday.TryGetValue(key, out int sold);
            _soldToday[key] = sold + quantity;
        }

        /// <summary>
        /// 당일 판매량을 비워 재고를 원복
        /// </summary>
        public void ResetDailyStock()
        {
            _soldToday.Clear();
        }

        /// <summary>
        /// 당일 판매량을 저장용 목록으로 반환
        /// </summary>
        public List<ShopStockSaveData> ExportSaveData()
        {
            var result = new List<ShopStockSaveData>(_soldToday.Count);
            CopySaveDataTo(result);
            return result;
        }

        public void CopySaveDataTo(List<ShopStockSaveData> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            int index = 0;
            foreach (var kvp in _soldToday)
            {
                if (kvp.Value <= 0)
                    continue;

                ShopStockSaveData saved = SaveSnapshotList.GetOrCreate(target, index++);
                saved.shopId = kvp.Key.id;
                saved.itemId = kvp.Key.itemId;
                saved.soldCount = kvp.Value;
            }

            SaveSnapshotList.Trim(target, index);
        }

        /// <summary>
        /// 저장된 당일 판매량을 복원
        /// </summary>
        public void ImportSaveData(List<ShopStockSaveData> savedStock)
        {
            _soldToday.Clear();

            if (savedStock == null)
                return;

            foreach (var saved in savedStock)
            {
                if (saved == null || saved.shopId <= 0 || saved.itemId <= 0 || saved.soldCount <= 0)
                    continue;

                _soldToday[new ShopKey(saved.shopId, saved.itemId)] = saved.soldCount;
            }
        }

        private void HandleDayChanged(int newDay)
        {
            ResetDailyStock();
        }

        private static int GetAuthoredCount(int shopId, int itemId)
        {
            ShopData shopData = DataManager.Instance.ShopDatabase?.GetShop(shopId, itemId);
            return shopData != null ? shopData.Count : 0;
        }

        private static bool HasDoubleStockBonus(TownKey townKey)
        {
            if (townKey == TownKey.Unknown || !TownProgressionManager.HasInstance)
                return false;

            return TownProgressionManager.Instance.GetAffinity(townKey)
                >= TownAffinityRules.DoubleStockRequiredAffinity;
        }
    }
}
