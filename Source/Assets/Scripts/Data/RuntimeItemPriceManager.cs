using System;
using System.Collections.Generic;
using UnityEngine;
using SeaVillage.Utilities;
using SeaVillage.Core;

namespace SeaVillage.Data
{
    /// <summary>
    /// <b>런타임 아이템 가격 관리 매니저</b><br/>
    /// 가격 계산: {(OriginPrice × (Distance + Preference)) × BaseModifier} × NormalModifier<br/>
    /// - BaseModifier: 특수 효과 (2주 지속)<br/>
    /// - NormalModifier: 일반 효과 (카테고리별 누적 변동)
    /// </summary>
    public class RuntimeItemPriceManager : Singleton<RuntimeItemPriceManager>
    {
        // 하루에 뜨는 신문 소식 총 개수(특수+일반)
        private const int DailyNewsCount = 5;

        // 마을별 런타임 아이템 가격 데이터
        // Key: ItemPriceKey (ItemID, Town), Value: ItemPriceData (히스토리)
        private Dictionary<ItemPriceKey, ItemPriceData> runtimePriceDict = new Dictionary<ItemPriceKey, ItemPriceData>();

        private SpecialEffectManager specialEffectManager = new SpecialEffectManager();
        private NormalEffectManager normalEffectManager = new NormalEffectManager();

        private int preparedPriceChangeDay = -1;
        private SpecialEffectData preparedSpecialEffect;
        private List<NormalEffectInfo> preparedNormalEffects;
        private readonly HashSet<string> forcedExcludedCategories = new HashSet<string>(StringComparer.Ordinal);

        private bool isInitialized = false;

        #region Initialization
        private void OnEnable()
        {
            TimeManager.OnDayChanged += HandleDayChanged;
        }

        private void OnDisable()
        {
            TimeManager.OnDayChanged -= HandleDayChanged;
        }

        /// <summary>
        /// 기본 가격 데이터베이스로부터 런타임 데이터 초기화
        /// </summary>
        public void InitializeFromDatabase(ItemPriceDatabase defaultDatabase)
        {
            if (defaultDatabase == null)
            {
                Debug.LogError("RuntimeItemPriceManager: Default database is null!");
                return;
            }

            runtimePriceDict.Clear();
            specialEffectManager.ClearActiveSpecialEffects();
            normalEffectManager.Clear();
            ClearPreparedPriceChanges();

            foreach (var kvp in defaultDatabase.DefaultItemPriceDict)
            {
                var itemPriceData = kvp.Value;
                var runtimeData = new ItemPriceData
                {
                    ID = itemPriceData.ID,
                    Town = itemPriceData.Town,
                    Distance = itemPriceData.Distance,
                    Preference = itemPriceData.Preference
                };

                var itemData = DataManager.Instance.GetItemByItemPriceID(kvp.Key.id);
                if (itemData != null)
                {
                    int initialPrice = Mathf.RoundToInt(itemData.OriginPrice * (itemPriceData.Distance + itemPriceData.Preference));
                    runtimeData.AddPriceToHistory(initialPrice);
                }

                runtimePriceDict[kvp.Key] = runtimeData;
            }

            isInitialized = true;
        }

        /// <summary>
        /// 저장된 데이터로부터 런타임 데이터 복원 <br/>
        /// 게임 로드 시 호출
        /// </summary>
        public void LoadFromSaveData(
            List<ItemPriceData> savedPriceData,
            List<ActiveSpecialEffectSaveData> savedSpecialEffects,
            List<NormalEffectSaveData> savedNormalEffects)
        {
            if (savedPriceData == null || savedPriceData.Count == 0)
            {
                Debug.LogWarning("RuntimeItemPriceManager: No saved price data to load");
                return;
            }

            runtimePriceDict.Clear();

            foreach (var priceData in savedPriceData)
            {
                var key = new ItemPriceKey(priceData.ID, priceData.Town);
                var runtimeData = new ItemPriceData();
                SaveSnapshotList.CopyItemPriceData(priceData, runtimeData);
                runtimePriceDict[key] = runtimeData;
            }

            // 일반 효과는 가격 키가 채워진 뒤에만 아이템별 배율로 펼칠 수 있다
            specialEffectManager.ImportSaveData(savedSpecialEffects);
            normalEffectManager.ImportSaveData(savedNormalEffects, runtimePriceDict);
            ClearPreparedPriceChanges();

            isInitialized = true;
        }

        /// <summary>
        /// 현재 런타임 가격 데이터를 저장용 리스트로 반환
        /// </summary>
        public List<ItemPriceData> GetRuntimePriceData()
        {
            var result = new List<ItemPriceData>(runtimePriceDict.Count);
            CopyRuntimePriceDataTo(result);
            return result;
        }

        public void CopyRuntimePriceDataTo(List<ItemPriceData> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            int index = 0;
            foreach (ItemPriceData source in runtimePriceDict.Values)
            {
                ItemPriceData saved = SaveSnapshotList.GetOrCreate(target, index++);
                SaveSnapshotList.CopyItemPriceData(source, saved);
            }

            SaveSnapshotList.Trim(target, index);
        }

        /// <summary>
        /// 활성 특수 효과를 저장용 목록으로 반환
        /// </summary>
        public List<ActiveSpecialEffectSaveData> GetActiveSpecialEffectSaveData()
        {
            return specialEffectManager.ExportSaveData();
        }

        public void CopyActiveSpecialEffectSaveDataTo(List<ActiveSpecialEffectSaveData> target)
        {
            specialEffectManager.CopySaveDataTo(target);
        }

        /// <summary>
        /// 활성 일반 효과를 저장용 목록으로 반환
        /// </summary>
        public List<NormalEffectSaveData> GetActiveNormalEffectSaveData()
        {
            return normalEffectManager.ExportSaveData();
        }

        public void CopyActiveNormalEffectSaveDataTo(List<NormalEffectSaveData> target)
        {
            normalEffectManager.CopySaveDataTo(target);
        }
        #endregion

        #region Price Query
        public int GetCurrentPrice(int itemPriceId, string town)
        {
            var key = new ItemPriceKey(itemPriceId, town);
            return GetCurrentPrice(key);
        }

        public int GetCurrentPrice(ItemPriceKey key)
        {
            if (!runtimePriceDict.TryGetValue(key, out var priceData))
            {
                Debug.LogWarning($"RuntimeItemPriceManager: Price not found for {key}");
                return -1;
            }

            // 1. 기본 가격 계산
            var itemData = DataManager.Instance.GetItemByItemPriceID(key.id);
            if (itemData == null)
            {
                Debug.LogWarning($"RuntimeItemPriceManager: Item data not found for ID {key.id}, Town {key.town}");
                return -1;
            }

            float basePrice = itemData.OriginPrice * (priceData.Distance + priceData.Preference);

            // 2. 특수 효과 적용
            float priceAfterBase = specialEffectManager.ApplyBaseModifier(key, basePrice);

            // 3. 일반 효과 적용
            float finalPrice = normalEffectManager.ApplyNormalModifier(key, priceAfterBase);

            return Mathf.Max(1, Mathf.RoundToInt(finalPrice));
        }

        /// <summary>
        /// 특정 아이템의 가격 히스토리 조회
        /// </summary>
        public List<int> GetPriceHistory(int itemPriceId, string town)
        {
            var key = new ItemPriceKey(itemPriceId, town);
            if (runtimePriceDict.TryGetValue(key, out var priceData))
                return priceData.GetAllPriceHistory();

            return new List<int>();
        }
        #endregion

        #region Price Modification
        public void ActivateSpecialEffect(SpecialEffectData effectData)
        {
            specialEffectManager.ActivateSpecialEffect(effectData);
        }

        public Dictionary<string, ActiveSpecialEffect> GetActiveSpecialEffects()
        {
            return specialEffectManager.GetActiveSpecialEffects();
        }

        /// <summary>
        /// 특정 마을에 적용 중인 이벤트(특수 효과) 영향 아이템 ID 집합 반환.
        /// town-specific 효과와 "All" 효과를 합집합으로 반환. 활성 없으면 빈 집합.
        /// </summary>
        public HashSet<int> GetEventItemsForTown(string town)
        {
            var result = new HashSet<int>();
            var active = specialEffectManager.GetActiveSpecialEffects();
            if (active == null || active.Count == 0)
            {
                return result;
            }

            if (active.TryGetValue("All", out var allEffect) && allEffect?.AffectedItems != null)
            {
                result.UnionWith(allEffect.AffectedItems);
            }

            if (!string.IsNullOrEmpty(town)
                && !string.Equals(town, "All", StringComparison.OrdinalIgnoreCase)
                && active.TryGetValue(town, out var townEffect)
                && townEffect?.AffectedItems != null)
            {
                result.UnionWith(townEffect.AffectedItems);
            }

            return result;
        }

        public List<NormalEffectInfo> GetActiveNormalEffects()
        {
            return normalEffectManager.GetActiveNormalEffects();
        }

        public void PreparePriceChangesForDay(int targetDay, bool expiresBeforeApplication = false)
        {
            if (!isInitialized || targetDay <= 0 || preparedPriceChangeDay == targetDay)
                return;

            preparedSpecialEffect = specialEffectManager.SelectDailySpecialEffect(expiresBeforeApplication);
            int normalCount = DailyNewsCount - (preparedSpecialEffect != null ? 1 : 0);
            preparedNormalEffects = normalEffectManager.CreateDailyEffects(normalCount);
            preparedPriceChangeDay = targetDay;
        }

        public bool TryPrepareForcedPriceChangesForDay(
            int targetDay,
            string forcedCategory,
            float forcedMultiplier,
            int protectedItemId)
        {
            if (!isInitialized
                || targetDay <= 0
                || string.IsNullOrWhiteSpace(forcedCategory)
                || protectedItemId <= 0
                || !DataManager.HasInstance
                || DataManager.Instance.ItemDatabase == null)
            {
                return false;
            }

            string normalizedCategory = forcedCategory.Trim();
            ItemData protectedItem = DataManager.Instance.ItemDatabase.GetItem(protectedItemId);
            if (protectedItem?.Type == null || !protectedItem.Type.Contains(normalizedCategory))
                return false;

            forcedExcludedCategories.Clear();
            for (int i = 0; i < protectedItem.Type.Count; i++)
            {
                string category = protectedItem.Type[i]?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(category)
                    && !string.Equals(category, normalizedCategory, StringComparison.Ordinal))
                {
                    forcedExcludedCategories.Add(category);
                }
            }

            if (preparedPriceChangeDay == targetDay
                && IsForcedPriceDecreasePrepared(normalizedCategory, forcedMultiplier))
            {
                return true;
            }

            preparedSpecialEffect = null;
            preparedNormalEffects = normalEffectManager.CreateDailyEffects(
                DailyNewsCount,
                normalizedCategory,
                forcedMultiplier,
                forcedExcludedCategories);
            preparedPriceChangeDay = targetDay;

            return IsForcedPriceDecreasePrepared(normalizedCategory, forcedMultiplier);
        }

        public SpecialEffectData GetPreparedSpecialEffect()
        {
            return preparedSpecialEffect;
        }

        public List<NormalEffectInfo> GetPreparedNormalEffects()
        {
            return preparedNormalEffects != null
                ? new List<NormalEffectInfo>(preparedNormalEffects)
                : new List<NormalEffectInfo>();
        }

        public void ClearPreparedPriceChangesForDay(int targetDay)
        {
            if (targetDay > 0 && preparedPriceChangeDay == targetDay)
                ClearPreparedPriceChanges();
        }
        #endregion

        #region Day Change Handling
        /// <summary>
        /// 날짜 변경 시 호출되는 핸들러
        /// </summary>
        private void HandleDayChanged(int newDay)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("RuntimeItemPriceManager: Not initialized yet, skipping day change handling");
                return;
            }

            // 1. 만료된 수정자 정리
            CleanupExpiredModifiers();

            // 2. 일일 가격 변동 적용
            ApplyDailyPriceFluctuation(newDay);

            // 3. 현재 가격을 히스토리에 기록
            RecordCurrentPricesToHistory();
        }

        /// <summary>
        /// 만료된 특수 효과 정리
        /// </summary>
        private void CleanupExpiredModifiers()
        {
            specialEffectManager.CleanupExpiredModifiers();
        }

        /// <summary>
        /// 일일 가격 변동 적용 <br/>
        /// 하루 소식은 항상 5개: 특수 발동 시 특수 1 + 일반 4, 아니면 일반 5
        /// </summary>
        private void ApplyDailyPriceFluctuation(int day)
        {
            PreparePriceChangesForDay(day);

            if (preparedSpecialEffect != null)
                specialEffectManager.ActivateSpecialEffect(preparedSpecialEffect);

            normalEffectManager.ApplyNormalEffects(runtimePriceDict, preparedNormalEffects);
            ClearPreparedPriceChanges();
        }

        private void ClearPreparedPriceChanges()
        {
            preparedPriceChangeDay = -1;
            preparedSpecialEffect = null;
            preparedNormalEffects = null;
        }

        private bool HasPreparedNormalEffect(string category, float multiplier)
        {
            return preparedNormalEffects != null
                && preparedNormalEffects.Exists(effect =>
                    effect != null
                    && effect.Category == category
                    && Mathf.Approximately(effect.Multiplier, multiplier));
        }

        private bool IsForcedPriceDecreasePrepared(string category, float multiplier)
        {
            if (preparedSpecialEffect != null || !HasPreparedNormalEffect(category, multiplier))
                return false;

            for (int i = 0; i < preparedNormalEffects.Count; i++)
            {
                NormalEffectInfo effect = preparedNormalEffects[i];
                if (effect != null && forcedExcludedCategories.Contains(effect.Category))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 현재 모든 가격을 히스토리에 기록
        /// </summary>
        private void RecordCurrentPricesToHistory()
        {
            foreach (var kvp in runtimePriceDict)
            {
                var priceData = kvp.Value;
                // 수정자가 적용된 최종 가격 기록
                int finalPrice = GetCurrentPrice(kvp.Key);
                if (finalPrice > 0)
                    priceData.AddPriceToHistory(finalPrice);
            }
        }

        /// <summary>
        /// 수동으로 가격 히스토리 기록 (테스트/디버그용)
        /// </summary>
        [ContextMenu("Record Current Prices")]
        public void ForceRecordPrices()
        {
            RecordCurrentPricesToHistory();
            Debug.Log("RuntimeItemPriceManager: Manually recorded current prices to history");
        }
        #endregion
    }
}
