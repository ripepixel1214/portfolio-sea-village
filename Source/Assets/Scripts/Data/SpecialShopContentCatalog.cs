using System;
using System.Collections.Generic;
using SeaVillage.Core;
using UnityEngine;

namespace SeaVillage.Data
{
    public enum DollUnlockCondition
    {
        TotalPlayerShopRevenue,
        TotalPlayerShopSales,
        OwnItem,
        TotalTownLove,
        TotalDollCount,
    }

    [Serializable]
    public class ExchangeCostOptionDefinition
    {
        [SerializeField] private int _itemId;
        [SerializeField] private int _count = 1;

        public int ItemId => _itemId;
        public int Count => Mathf.Max(1, _count);
    }

    [Serializable]
    public class ExchangeRewardDefinition
    {
        [SerializeField] private int _rewardItemId;
        [SerializeField] private int _rewardCount = 1;
        [SerializeField] private List<ExchangeCostOptionDefinition> _costOptions = new();

        public int RewardItemId => _rewardItemId;
        public int RewardCount => Mathf.Max(1, _rewardCount);
        public IReadOnlyList<ExchangeCostOptionDefinition> CostOptions => _costOptions;
    }

    [Serializable]
    public class ShipUpgradeDefinition
    {
        [SerializeField] private int _targetLevel;
        [SerializeField] private int _goldCost;
        [SerializeField] private int _woodItemId;
        [SerializeField] private int _woodCount;
        [SerializeField] private int _ingotItemId;
        [SerializeField] private int _ingotCount;
        [SerializeField] private string _effectDescription = string.Empty;

        public int TargetLevel => Mathf.Max(1, _targetLevel);
        public int GoldCost => Mathf.Max(0, _goldCost);
        public int WoodItemId => _woodItemId;
        public int WoodCount => Mathf.Max(1, _woodCount);
        public int IngotItemId => _ingotItemId;
        public int IngotCount => Mathf.Max(1, _ingotCount);
        public string EffectDescription => _effectDescription ?? string.Empty;
    }

    [Serializable]
    public class EnhancementLevelDefinition
    {
        [SerializeField] private int _targetLevel;
        [SerializeField] private int _crystalCount;
        [SerializeField] private int _successRatePercent;

        public int TargetLevel => _targetLevel;
        public int CrystalCount => Mathf.Max(1, _crystalCount);
        public int SuccessRatePercent => Mathf.Clamp(_successRatePercent, 0, 100);
    }

    [Serializable]
    public class EnhancementItemDefinition
    {
        [SerializeField] private List<int> _levelItemIds = new();

        public IReadOnlyList<int> LevelItemIds => _levelItemIds;

        public bool TryGetLevel(int itemId, out int level)
        {
            level = -1;
            if (_levelItemIds == null)
                return false;

            for (int i = 0; i < _levelItemIds.Count; i++)
            {
                if (_levelItemIds[i] != itemId)
                    continue;

                level = i;
                return true;
            }

            return false;
        }
    }

    [Serializable]
    public class DollRewardDefinition
    {
        [SerializeField] private int _dollItemId;
        [SerializeField] private int _staffId;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField] private string _effectDescription = string.Empty;
        [SerializeField] private DollUnlockCondition _condition;
        [SerializeField] private int _conditionValue;
        [SerializeField] private int _conditionItemId;
        [SerializeField] private string _conditionLabel = string.Empty;
        [SerializeField] private Sprite _playerInventorySprite;

        public int DollItemId => _dollItemId;
        /// <summary>연결된 직원 ID</summary>
        public int StaffId => _staffId;
        public string DisplayName => _displayName ?? string.Empty;
        public string EffectDescription => _effectDescription ?? string.Empty;
        public DollUnlockCondition Condition => _condition;
        public int ConditionValue => Mathf.Max(0, _conditionValue);
        public int ConditionItemId => _conditionItemId;
        public string ConditionLabel => _conditionLabel ?? string.Empty;
        public Sprite PlayerInventorySprite => _playerInventorySprite;
    }

    [CreateAssetMenu(fileName = nameof(SpecialShopContentCatalog), menuName = "SeaVillage/Special Shop Content Catalog")]
    public class SpecialShopContentCatalog : ScriptableObject
    {
        [Header("교환소")]
        [SerializeField] private List<ExchangeRewardDefinition> _exchangeRewards = new();

        [Header("제련소")]
        [SerializeField] private ShipUpgradeDefinition _shipUpgrade = new();

        [Header("강화")]
        [SerializeField] private int _crystalItemId;
        [SerializeField] private List<EnhancementLevelDefinition> _enhancementLevels = new();
        [SerializeField] private List<EnhancementItemDefinition> _enhancementItems = new();

        [Header("인형")]
        [SerializeField] private List<DollRewardDefinition> _dolls = new();
        [SerializeField] private List<TownKey> _loveTownKeys = new();

        public IReadOnlyList<ExchangeRewardDefinition> ExchangeRewards => _exchangeRewards;
        public ShipUpgradeDefinition ShipUpgrade => _shipUpgrade;
        public int CrystalItemId => _crystalItemId;
        public IReadOnlyList<EnhancementItemDefinition> EnhancementItems => _enhancementItems;
        public IReadOnlyList<DollRewardDefinition> Dolls => _dolls;
        public IReadOnlyList<TownKey> LoveTownKeys => _loveTownKeys;

        public bool TryGetEnhancementLevel(int targetLevel, out EnhancementLevelDefinition definition)
        {
            definition = null;
            if (_enhancementLevels == null)
                return false;

            for (int i = 0; i < _enhancementLevels.Count; i++)
            {
                EnhancementLevelDefinition candidate = _enhancementLevels[i];
                if (candidate == null || candidate.TargetLevel != targetLevel)
                    continue;

                definition = candidate;
                return true;
            }

            return false;
        }

        public bool TryResolveEnhancement(
            int currentItemId,
            out int nextItemId,
            out EnhancementLevelDefinition levelDefinition)
        {
            nextItemId = 0;
            levelDefinition = null;
            if (currentItemId <= 0 || _enhancementItems == null)
                return false;

            for (int i = 0; i < _enhancementItems.Count; i++)
            {
                EnhancementItemDefinition itemDefinition = _enhancementItems[i];
                if (itemDefinition?.LevelItemIds == null
                    || !itemDefinition.TryGetLevel(currentItemId, out int currentLevel)
                    || currentLevel < 0
                    || currentLevel >= itemDefinition.LevelItemIds.Count - 1)
                {
                    continue;
                }

                int candidateNextItemId = itemDefinition.LevelItemIds[currentLevel + 1];
                if (candidateNextItemId <= 0
                    || candidateNextItemId == currentItemId
                    || !TryGetEnhancementLevel(currentLevel + 1, out EnhancementLevelDefinition candidateLevel))
                {
                    continue;
                }

                nextItemId = candidateNextItemId;
                levelDefinition = candidateLevel;
                return levelDefinition != null;
            }

            return false;
        }
    }
}
