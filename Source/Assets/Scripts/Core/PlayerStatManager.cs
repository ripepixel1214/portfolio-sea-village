using System;
using System.Collections.Generic;
using SeaVillage.Data;
using SeaVillage.Utilities;

namespace SeaVillage.Core
{
    public enum PlayerStatType
    {
        Strength = 0,
        Agility = 1,
    }

    /// <summary>씬과 무관한 플레이어 스탯 원본 상태를 관리</summary>
    public sealed class PlayerStatManager : Singleton<PlayerStatManager>
    {
        public const int DefaultStrength = 10;
        public const int DefaultAgility = 0;

        private readonly HashSet<int> _usedStatItemIds = new HashSet<int>();
        private int _strength;
        private int _agility;

        public event Action<PlayerStatType, int> OnStatChanged;

        public bool IsInitialized { get; private set; }

        public int Strength => _strength;
        public int Agility => _agility;
        
        public float CarryCapacity => _strength;
        public float MovementSpeedMultiplier => 1f + (_agility * 0.1f);

        protected override void Awake()
        {
            base.Awake();
            ResetProgression();
            IsInitialized = true;
        }

        public bool HasUsedStatItem(int itemId)
        {
            return itemId > 0 && _usedStatItemIds.Contains(itemId);
        }

        internal bool CanApplyStatItem(PlayerStatType statType, int amount, int itemId, out string failReason)
        {
            failReason = string.Empty;

            if (!IsValidStatType(statType) || amount <= 0 || itemId <= 0)
            {
                failReason = "[Error] 스탯 아이템 정보가 유효하지 않습니다";
                return false;
            }

            if (_usedStatItemIds.Contains(itemId))
            {
                failReason = "대상에게 이미 사용한 아이템입니다.";
                return false;
            }

            int current = statType == PlayerStatType.Strength ? _strength : _agility;
            if (current > int.MaxValue - amount)
            {
                failReason = "[Error] 스탯이 유효 범위를 초과합니다";
                return false;
            }

            return true;
        }

        internal bool TryApplyStatItem(PlayerStatType statType, int amount, int itemId, out string failReason)
        {
            if (!CanApplyStatItem(statType, amount, itemId, out failReason))
                return false;

            if (statType == PlayerStatType.Strength)
                _strength += amount;
            else
                _agility += amount;

            _usedStatItemIds.Add(itemId);
            OnStatChanged?.Invoke(statType, statType == PlayerStatType.Strength ? _strength : _agility);
            return true;
        }

        public bool ChangeStat(PlayerStatType statType, int delta)
        {
            if (!IsValidStatType(statType) || delta == 0)
                return false;

            int current = statType == PlayerStatType.Strength ? _strength : _agility;
            long changed = (long)current + delta;
            int next = changed <= 0L ? 0 : changed >= int.MaxValue ? int.MaxValue : (int)changed;
            if (next == current)
                return false;

            if (statType == PlayerStatType.Strength)
                _strength = next;
            else
                _agility = next;

            OnStatChanged?.Invoke(statType, next);
            return true;
        }

        public PlayerStatSaveData ExportSaveData()
        {
            var result = new PlayerStatSaveData();
            CopySaveDataTo(result);
            return result;
        }

        public void CopySaveDataTo(PlayerStatSaveData target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.strength = _strength;
            target.agility = _agility;
            target.usedStatItemIds ??= new List<int>();
            target.usedStatItemIds.Clear();
            foreach (int itemId in _usedStatItemIds)
                target.usedStatItemIds.Add(itemId);
        }

        public void ImportSaveData(PlayerStatSaveData data)
        {
            _strength = data == null ? DefaultStrength : Math.Max(0, data.strength);
            _agility = data == null ? DefaultAgility : Math.Max(0, data.agility);
            _usedStatItemIds.Clear();

            List<int> usedItemIds = data?.usedStatItemIds;
            if (usedItemIds != null)
            {
                for (int i = 0; i < usedItemIds.Count; i++)
                {
                    int itemId = usedItemIds[i];
                    if (itemId > 0)
                        _usedStatItemIds.Add(itemId);
                }
            }

            OnStatChanged?.Invoke(PlayerStatType.Strength, _strength);
            OnStatChanged?.Invoke(PlayerStatType.Agility, _agility);
        }

        public void ResetProgression()
        {
            _strength = DefaultStrength;
            _agility = DefaultAgility;
            _usedStatItemIds.Clear();
        }

        private static bool IsValidStatType(PlayerStatType statType)
        {
            return statType == PlayerStatType.Strength || statType == PlayerStatType.Agility;
        }
    }
}
