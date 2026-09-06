using System;
using System.Collections.Generic;
using UnityEngine;
using SeaVillage.Core;

namespace SeaVillage.Data
{
    /// <summary>한 마을 내 가게 오피스의 건설/업그레이드 정의</summary>
    [Serializable]
    public class PlayerShopUpgradeDefinition
    {
        [SerializeField] private TownKey _townKey = TownKey.Unknown;

        [Header("소비 아이템")]
        [SerializeField] private int _buildItemId;
        [SerializeField] private int _upgradeItemId;

        [Header("해금 결과 - Lv1(건설)")]
        [SerializeField] private int _level1SlotCapacity;
        [SerializeField] private int _level1QueueCapacity;

        [Header("해금 결과 - Lv2(업그레이드)")]
        [SerializeField] private int _level2SlotCapacity;
        [SerializeField] private int _level2QueueCapacity;

        public TownKey TownKey => _townKey;
        public int BuildItemId => _buildItemId;
        public int UpgradeItemId => _upgradeItemId;
        public int Level1SlotCapacity => _level1SlotCapacity;
        public int Level1QueueCapacity => _level1QueueCapacity;
        public int Level2SlotCapacity => _level2SlotCapacity;
        public int Level2QueueCapacity => _level2QueueCapacity;
    }

    /// <summary>마을별 내 가게 건설/업그레이드 정의(수기 편집 SO)</summary>
    [CreateAssetMenu(fileName = nameof(PlayerShopUpgradeCatalog), menuName = "SeaVillage/PlayerShop Upgrade Catalog")]
    public class PlayerShopUpgradeCatalog : ScriptableObject
    {
        [SerializeField] private List<PlayerShopUpgradeDefinition> _definitions = new List<PlayerShopUpgradeDefinition>();

        public IReadOnlyList<PlayerShopUpgradeDefinition> All => _definitions;

        /// <summary>마을 키로 오피스 정의 조회, 없으면 false</summary>
        public bool TryGetByTown(TownKey townKey, out PlayerShopUpgradeDefinition definition)
        {
            definition = null;
            if (townKey == TownKey.Unknown || _definitions == null)
                return false;

            for (int i = 0; i < _definitions.Count; i++)
            {
                if (_definitions[i] != null && _definitions[i].TownKey == townKey)
                {
                    definition = _definitions[i];
                    return true;
                }
            }

            return false;
        }

    }
}
