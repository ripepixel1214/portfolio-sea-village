using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SeaVillage.Data
{
    #region Normal Effect Info
    /// <summary>
    /// 일반 효과 정보(카테고리, 적용 배율)를 담는 구조체
    /// </summary>
    [System.Serializable]
    public class NormalEffectInfo
    {
        public string Category { get; private set; }
        public float Multiplier { get; private set; }

        public NormalEffectInfo(string category, float multiplier)
        {
            Category = category;
            Multiplier = multiplier;
        }

        /// <summary>변동률을 정규화된 퍼센트 형식으로 반환 (+10%, -20% 등)</summary>
        public string GetFluctuationRate()
        {
            float percentage = (Multiplier - 1f) * 100f;
            string sign = percentage >= 0 ? "+" : ""; // 음수면 자동으로 '-' 포함
            return $"{sign}{percentage:F0}%";
        }
    }
    #endregion

    /// <summary>
    /// 일반 효과(NormalModifier) 관리 클래스<br/>
    /// 매일 카테고리 5개를 선정해 변동폭을 퍼센트포인트로 ±50% 한도 내에서 누적 합산하고 기본가에 반영
    /// </summary>
    public class NormalEffectManager
    {
        private const int DAILY_CATEGORY_COUNT = 5;

        // 시세 변동 누적 상·하한(±50%)과 경계 판정 오차 허용치
        private const float MAX_ACCUMULATED_OFFSET = 0.5f;
        private const float MIN_ACCUMULATED_OFFSET = -0.5f;
        private const float OFFSET_EPSILON = 0.0001f;

        // 발동 가능한 변동폭 후보(모두 동일 확률)
        private static readonly float[] FluctuationDeltas = { 0.1f, -0.1f, 0.2f, -0.2f, 0.3f, -0.3f };

        // 경계 통과 후보 임시 담기용 재사용 리스트(할당 회피)
        private readonly List<float> eligibleDeltas = new List<float>(FluctuationDeltas.Length);

        // 카테고리별 누적 변동폭(퍼센트포인트 비율, 0.4 = +40%) — 가격 계산의 단일 진리원
        private Dictionary<string, float> categoryOffsets = new Dictionary<string, float>();

        // 아이템별 최종 배율 캐시 — categoryOffsets에서 파생
        private Dictionary<ItemPriceKey, float> normalModifiers = new Dictionary<ItemPriceKey, float>();

        // 오늘 변동된 카테고리와 당일 변동폭 — 뉴스 표시용
        private List<NormalEffectInfo> todayEffects = new List<NormalEffectInfo>();
        private readonly Dictionary<string, float> _saveTodayByCategory = new Dictionary<string, float>();
        private readonly HashSet<string> _saveCategories = new HashSet<string>();

        public void Clear()
        {
            categoryOffsets.Clear();
            normalModifiers.Clear();
            todayEffects.Clear();
        }

        public float ApplyNormalModifier(ItemPriceKey key, float priceAfterBase)
        {
            if (!normalModifiers.TryGetValue(key, out var modifier))
                return priceAfterBase;

            return priceAfterBase * modifier;
        }

        /// <summary>날짜 변경 시 당일 카테고리 변동을 누적 적용. categoryCount만큼 카테고리 선정</summary>
        public void ApplyNormalEffects(Dictionary<ItemPriceKey, ItemPriceData> runtimePriceDict, int categoryCount = DAILY_CATEGORY_COUNT)
        {
            ApplyNormalEffects(runtimePriceDict, CreateDailyEffects(categoryCount));
        }

        public void ApplyNormalEffects(Dictionary<ItemPriceKey, ItemPriceData> runtimePriceDict, IReadOnlyList<NormalEffectInfo> effects)
        {
            todayEffects.Clear();

            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    if (effect == null || string.IsNullOrWhiteSpace(effect.Category))
                        continue;

                    float previousOffset = categoryOffsets.TryGetValue(effect.Category, out var offset) ? offset : 0f;
                    float nextOffset = previousOffset + effect.Multiplier - 1f;
                    if (nextOffset > MAX_ACCUMULATED_OFFSET + OFFSET_EPSILON ||
                        nextOffset < MIN_ACCUMULATED_OFFSET - OFFSET_EPSILON)
                        continue;

                    categoryOffsets[effect.Category] = Mathf.Clamp(nextOffset, MIN_ACCUMULATED_OFFSET, MAX_ACCUMULATED_OFFSET);
                    todayEffects.Add(new NormalEffectInfo(effect.Category, effect.Multiplier));
                }
            }

            RebuildNormalModifiers(runtimePriceDict);
        }

        /// <summary>누적 카테고리 변동을 저장용 목록으로 반환</summary>
        public List<NormalEffectSaveData> ExportSaveData()
        {
            var result = new List<NormalEffectSaveData>();
            CopySaveDataTo(result);
            return result;
        }

        public void CopySaveDataTo(List<NormalEffectSaveData> target)
        {
            if (target == null)
                throw new System.ArgumentNullException(nameof(target));

            _saveTodayByCategory.Clear();
            for (int i = 0; i < todayEffects.Count; i++)
            {
                NormalEffectInfo effect = todayEffects[i];
                _saveTodayByCategory[effect.Category] = effect.Multiplier - 1f;
            }

            _saveCategories.Clear();
            _saveCategories.UnionWith(categoryOffsets.Keys);
            _saveCategories.UnionWith(_saveTodayByCategory.Keys);

            int index = 0;
            foreach (string category in _saveCategories)
            {
                float accumulated = categoryOffsets.TryGetValue(category, out var o) ? o : 0f;
                float today = _saveTodayByCategory.TryGetValue(category, out var d) ? d : 0f;

                if (Mathf.Approximately(accumulated, 0f) && Mathf.Approximately(today, 0f))
                    continue;

                NormalEffectSaveData saved = SaveSnapshotList.GetOrCreate(target, index++);
                saved.category = category;
                saved.accumulatedOffset = accumulated;
                saved.todayDelta = today;
            }

            SaveSnapshotList.Trim(target, index);
        }

        /// <summary>저장된 누적 변동을 복원. 아이템별 배율은 재계산한다</summary>
        public void ImportSaveData(List<NormalEffectSaveData> savedEffects, Dictionary<ItemPriceKey, ItemPriceData> runtimePriceDict)
        {
            categoryOffsets.Clear();
            todayEffects.Clear();

            if (savedEffects != null)
            {
                foreach (var saved in savedEffects)
                {
                    if (saved == null || string.IsNullOrWhiteSpace(saved.category))
                        continue;

                    if (!Mathf.Approximately(saved.accumulatedOffset, 0f))
                        categoryOffsets[saved.category] = saved.accumulatedOffset;

                    if (!Mathf.Approximately(saved.todayDelta, 0f))
                        todayEffects.Add(new NormalEffectInfo(saved.category, 1f + saved.todayDelta));
                }
            }

            RebuildNormalModifiers(runtimePriceDict);
        }

        // 당일 변동 카테고리를 중복 없이 categoryCount개 선정하고 각 변동폭을 누적
        public List<NormalEffectInfo> CreateDailyEffects(int categoryCount)
        {
            return CreateDailyEffects(categoryCount, null);
        }

        private List<NormalEffectInfo> CreateDailyEffects(
            int categoryCount,
            ISet<string> excludedCategories)
        {
            var effects = new List<NormalEffectInfo>();

            var itemDatabase = DataManager.Instance.ItemDatabase;
            if (itemDatabase == null || itemDatabase.Items == null)
                return effects;

            var allCategories = itemDatabase.Items
                .Where(item => item.Type != null)
                .SelectMany(item => item.Type)
                .Where(category => excludedCategories == null || !excludedCategories.Contains(category))
                .Distinct()
                .ToList();

            if (allCategories.Count == 0)
            {
                Debug.LogWarning("No item categories found for normal effect application");
                return effects;
            }

            int selectCount = Mathf.Clamp(categoryCount, 0, allCategories.Count);
            var selectedCategories = new HashSet<string>();

            while (selectedCategories.Count < selectCount)
                selectedCategories.Add(allCategories[Random.Range(0, allCategories.Count)]);

            foreach (var category in selectedCategories)
            {
                float currentOffset = categoryOffsets.TryGetValue(category, out var offset) ? offset : 0f;
                float delta = PickFluctuationDelta(currentOffset);

                // 경계로 인해 발동 가능한 변동폭이 없으면 스킵
                if (Mathf.Approximately(delta, 0f))
                    continue;

                effects.Add(new NormalEffectInfo(category, 1f + delta));
            }

            return effects;
        }

        /// <summary>
        /// 지정 카테고리 변동을 첫 뉴스로 고정하고 나머지는 기존 일일 규칙으로 구성
        /// </summary>
        public List<NormalEffectInfo> CreateDailyEffects(
            int categoryCount,
            string forcedCategory,
            float forcedMultiplier,
            ISet<string> excludedCategories)
        {
            List<NormalEffectInfo> effects = CreateDailyEffects(categoryCount, excludedCategories);
            if (categoryCount <= 0 || string.IsNullOrWhiteSpace(forcedCategory))
                return effects;

            string normalizedCategory = forcedCategory.Trim();
            float forcedDelta = forcedMultiplier - 1f;
            float currentOffset = categoryOffsets.TryGetValue(normalizedCategory, out float offset) ? offset : 0f;
            float nextOffset = currentOffset + forcedDelta;
            if (nextOffset > MAX_ACCUMULATED_OFFSET + OFFSET_EPSILON
                || nextOffset < MIN_ACCUMULATED_OFFSET - OFFSET_EPSILON)
            {
                Debug.LogWarning($"NormalEffectManager: 강제 변동이 누적 한도를 벗어납니다: {normalizedCategory}, {forcedDelta:P0}");
                return effects;
            }

            bool categoryExists = DataManager.Instance.ItemDatabase.Items.Any(
                item => item.Type != null && item.Type.Contains(normalizedCategory));
            if (!categoryExists)
            {
                Debug.LogWarning($"NormalEffectManager: 강제 변동 카테고리를 찾을 수 없습니다: {normalizedCategory}");
                return effects;
            }

            effects.RemoveAll(effect => effect != null && effect.Category == normalizedCategory);
            effects.Insert(0, new NormalEffectInfo(normalizedCategory, forcedMultiplier));
            if (effects.Count > categoryCount)
                effects.RemoveRange(categoryCount, effects.Count - categoryCount);

            return effects;
        }

        // 누적 카테고리 변동을 아이템별 배율로 펼쳐 캐시 재구성
        private void RebuildNormalModifiers(Dictionary<ItemPriceKey, ItemPriceData> runtimePriceDict)
        {
            normalModifiers.Clear();

            if (categoryOffsets.Count == 0 || runtimePriceDict == null || runtimePriceDict.Count == 0)
                return;

            var itemDatabase = DataManager.Instance.ItemDatabase;
            if (itemDatabase == null || itemDatabase.Items == null)
                return;

            // 가격 키를 PriceListID로 묶어 아이템당 탐색 비용 절감
            var keysByPriceId = new Dictionary<int, List<ItemPriceKey>>();
            foreach (var key in runtimePriceDict.Keys)
            {
                if (!keysByPriceId.TryGetValue(key.id, out var list))
                {
                    list = new List<ItemPriceKey>();
                    keysByPriceId[key.id] = list;
                }
                list.Add(key);
            }

            foreach (var item in itemDatabase.Items)
            {
                if (item.Type == null)
                    continue;

                // 속한 모든 카테고리의 누적 오프셋 합산
                float offset = 0f;
                foreach (var category in item.Type)
                    if (categoryOffsets.TryGetValue(category, out var o))
                        offset += o;

                if (Mathf.Approximately(offset, 0f))
                    continue;

                if (!keysByPriceId.TryGetValue(item.PriceListID, out var keys))
                    continue;

                float multiplier = 1f + offset;
                foreach (var key in keys)
                    normalModifiers[key] = multiplier;
            }
        }

        /// <summary>오늘 변동된 일반 효과 정보 조회 (카테고리별)</summary>
        public List<NormalEffectInfo> GetActiveNormalEffects()
        {
            return new List<NormalEffectInfo>(todayEffects);
        }

        // 현재 누적 offset에서 ±50%를 넘기지 않는 변동폭만 후보로 두고 균등 확률로 선정
        // 발동 가능한 후보가 없으면 0 반환
        private float PickFluctuationDelta(float currentOffset)
        {
            eligibleDeltas.Clear();

            foreach (float delta in FluctuationDeltas)
            {
                float next = currentOffset + delta;
                if (next <= MAX_ACCUMULATED_OFFSET + OFFSET_EPSILON &&
                    next >= MIN_ACCUMULATED_OFFSET - OFFSET_EPSILON)
                    eligibleDeltas.Add(delta);
            }

            if (eligibleDeltas.Count == 0)
                return 0f;

            return eligibleDeltas[Random.Range(0, eligibleDeltas.Count)];
        }
    }
}
