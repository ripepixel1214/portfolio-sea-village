using System;
using System.Collections.Generic;
using System.Linq;
using SeaVillage.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SeaVillage.Data
{
    /// <summary>
    /// 특수 효과(BaseModifier) 관리 클래스 <br/>
    /// 2주 동안 지속되는 특수 효과를 관리
    /// </summary>
    public class SpecialEffectManager
    {
        // 특수 효과 (BaseModifier): 마을별 특수 효과
        // Key: Town, Value: 활성된 특수 효과
        private Dictionary<string, ActiveSpecialEffect> activeSpecialEffects = new Dictionary<string, ActiveSpecialEffect>();

        // 특수 소식 발동 확률(매일 10%)
        private const float SPECIAL_EFFECT_TRIGGER_CHANCE = 0.1f;
        private const int SPECIAL_EFFECT_DURATION_DAYS = 14; // 2주

        public void ClearActiveSpecialEffects()
        {
            activeSpecialEffects.Clear();
        }

        /// <summary>
        /// 특수 효과 (BaseModifier) 적용
        /// </summary>
        public float ApplyBaseModifier(ItemPriceKey key, float basePrice)
        {
            if (!activeSpecialEffects.TryGetValue(key.town, out var specialEffect))
                return basePrice;

            // 특수 효과가 해당 아이템에 영향을 주는지 확인하고 배율 가져오기
            if (specialEffect.ItemPriceMultipliers != null && 
                specialEffect.ItemPriceMultipliers.TryGetValue(key.id, out var multiplier))
                return basePrice * multiplier;

            return basePrice;
        }

        /// <summary>
        /// 특수 소식 발동 시도. 실제로 발동했으면 true
        /// </summary>
        public bool TryTriggerSpecialEffect()
        {
            SpecialEffectData selectedEffect = SelectDailySpecialEffect();
            return selectedEffect != null && ActivateSpecialEffect(selectedEffect);
        }

        public SpecialEffectData SelectDailySpecialEffect(bool expiresBeforeApplication = false)
        {
            if (Random.value > SPECIAL_EFFECT_TRIGGER_CHANCE)
                return null;

            var specialEffectDb = DataManager.Instance.SpecialEffectDatabase;
            if (specialEffectDb == null)
            {
                Debug.LogWarning("SpecialEffectDatabase not found");
                return null;
            }

            // News = true인 특수 효과 목록 가져오기
            var availableEffects = specialEffectDb.GetAvailableEffects();
            if (availableEffects == null || availableEffects.Count == 0)
                return null;

            // 현재 발동 중인 효과와 마을 필터링
            var eligibleEffects = availableEffects
                .Where(effect => !IsEffectActive(effect.ID, expiresBeforeApplication) && !HasTownEffect(effect.Town, expiresBeforeApplication))
                .ToList();

            if (eligibleEffects.Count == 0)
                return null;

            // 마을 필터링
            Core.TownKey currentTown = Core.GameManager.Instance.CurrentTownKey;
            if (currentTown == Core.TownKey.Unknown)
                return null;

            string currentTownKey = Core.TownKeyUtility.ToStorageKey(currentTown);

            eligibleEffects = eligibleEffects
                .Where(effect => effect.Town == "All" || string.Equals(effect.Town, currentTownKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (eligibleEffects.Count == 0)
                return null;

            // 랜덤으로 하나 선택하여 발동. 활성화 실패 시 그날 특수 슬롯 미소비
            var selectedEffect = eligibleEffects[Random.Range(0, eligibleEffects.Count)];
            return selectedEffect;
        }

        /// <summary>
        /// 특수 효과 활성화. 실제로 활성화됐으면 true
        /// </summary>
        public bool ActivateSpecialEffect(SpecialEffectData effectData)
        {
            if (activeSpecialEffects.ContainsKey(effectData.Town))
            {
                Debug.LogWarning($"Town {effectData.Town} already has an active special effect");
                return false;
            }

            var activeEffect = BuildActiveEffect(effectData, SPECIAL_EFFECT_DURATION_DAYS);
            if (activeEffect == null)
                return false;

            activeSpecialEffects[effectData.Town] = activeEffect;
            return true;
        }

        /// <summary>
        /// 활성 특수 효과를 저장용 목록으로 반환
        /// </summary>
        public List<ActiveSpecialEffectSaveData> ExportSaveData()
        {
            var result = new List<ActiveSpecialEffectSaveData>(activeSpecialEffects.Count);
            CopySaveDataTo(result);
            return result;
        }

        public void CopySaveDataTo(List<ActiveSpecialEffectSaveData> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            int index = 0;
            foreach (var kvp in activeSpecialEffects)
            {
                if (kvp.Value?.EffectData == null)
                    continue;

                ActiveSpecialEffectSaveData saved = SaveSnapshotList.GetOrCreate(target, index++);
                saved.effectId = kvp.Value.EffectData.ID;
                saved.remainingDays = kvp.Value.RemainingDays;
            }

            SaveSnapshotList.Trim(target, index);
        }

        /// <summary>
        /// 저장된 특수 효과를 복원. 발동 이벤트는 발생시키지 않는다
        /// </summary>
        public void ImportSaveData(List<ActiveSpecialEffectSaveData> savedEffects)
        {
            activeSpecialEffects.Clear();

            if (savedEffects == null || savedEffects.Count == 0)
                return;

            var specialEffectDb = DataManager.Instance.SpecialEffectDatabase;
            if (specialEffectDb == null)
            {
                Debug.LogWarning("SpecialEffectDatabase not found");
                return;
            }

            foreach (var saved in savedEffects)
            {
                if (saved == null || saved.remainingDays <= 0)
                    continue;

                SpecialEffectData effectData = specialEffectDb.GetSpecialEffect(saved.effectId);
                if (effectData == null)
                {
                    Debug.LogWarning($"Special effect {saved.effectId} not found in database. Skipping restore.");
                    continue;
                }

                var activeEffect = BuildActiveEffect(effectData, saved.remainingDays);
                if (activeEffect != null)
                    activeSpecialEffects[effectData.Town] = activeEffect;
            }
        }

        // 영향 아이템과 가격 배율을 데이터베이스에서 계산해 활성 효과를 구성
        private ActiveSpecialEffect BuildActiveEffect(SpecialEffectData effectData, int remainingDays)
        {
            var specialEffectDb = DataManager.Instance.SpecialEffectDatabase;
            if (specialEffectDb == null)
            {
                Debug.LogWarning("SpecialEffectDatabase not found");
                return null;
            }

            // SpecialEffectItemChangeData에서 아이템별 가격 변동 정보 조회
            var itemChanges = specialEffectDb.GetSEItemChanges(effectData.ItemChange);
            if (itemChanges == null || itemChanges.Count == 0)
            {
                Debug.LogWarning($"No item changes found for special effect {effectData.ID}");
                return null;
            }

            var affectedItems = new HashSet<int>();
            var itemPriceMultipliers = new Dictionary<int, float>();

            foreach (var change in itemChanges)
            {
                affectedItems.Add(change.ItemID);
                float changePercent = change.ItemChange / 100f;
                itemPriceMultipliers[change.ItemID] = 1f + changePercent;
            }

            return new ActiveSpecialEffect
            {
                EffectData = effectData,
                RemainingDays = remainingDays,
                AffectedItems = affectedItems,
                ItemPriceMultipliers = itemPriceMultipliers
            };
        }

        /// <summary>
        /// 만료된 특수 효과 정리
        /// </summary>
        public void CleanupExpiredModifiers()
        {
            var expiredTowns = new List<string>();

            foreach (var kvp in activeSpecialEffects)
            {
                kvp.Value.RemainingDays--;
                if (kvp.Value.RemainingDays <= 0)
                {
                    expiredTowns.Add(kvp.Key);
                    Debug.Log($"Special effect '{kvp.Value.EffectData.Name}' expired in {kvp.Key}");
                }
            }

            foreach (var town in expiredTowns)
                activeSpecialEffects.Remove(town);
        }

        /// <summary>
        /// 활성화된 특수 효과 목록 조회
        /// </summary>
        public Dictionary<string, ActiveSpecialEffect> GetActiveSpecialEffects()
        {
            return activeSpecialEffects;
        }

        /// <summary>
        /// 특정 ID의 특수 효과가 현재 활성화되어 있는지 확인
        /// </summary>
        private bool IsEffectActive(int effectId, bool expiresBeforeApplication)
        {
            return activeSpecialEffects.Values.Any(effect =>
                effect.EffectData.ID == effectId && (!expiresBeforeApplication || effect.RemainingDays > 1));
        }

        private bool HasTownEffect(string town, bool expiresBeforeApplication)
        {
            return activeSpecialEffects.TryGetValue(town, out ActiveSpecialEffect effect) &&
                   (!expiresBeforeApplication || effect.RemainingDays > 1);
        }
    }

    #region Active Special Effect
    [Serializable]
    public class ActiveSpecialEffect
    {
        public int RemainingDays;
        
        public SpecialEffectData EffectData;

        // 영향받는 아이템 ID 목록
        public HashSet<int> AffectedItems;
        // 아이템별 가격 배율 (Key: ItemID, Value: Multiplier)
        public Dictionary<int, float> ItemPriceMultipliers;
    }
    #endregion
}
