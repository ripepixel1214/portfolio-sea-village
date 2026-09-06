using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
namespace SeaVillage.Status
{
    /// <summary>
    /// Status 관리 클래스. 캐릭터들이 공통으로 상속받음
    /// </summary>
    [Serializable]
    public abstract class Status : SerializedMonoBehaviour
    {
        [SerializeField] protected Dictionary<StatusType, StatusData> _stats = new Dictionary<StatusType, StatusData>();

        // Events
        public event Action<StatusType, int> OnStatChanged;
        public event Action<StatusType, int> OnStatSet;

        /// <summary>
        /// Status 초기화 - 자식 클래스에서 구현
        /// </summary>
        protected abstract void InitializeStatus();

        [Button("Add Default Stats")]
        protected abstract void AddDefaultStats();

        /// <summary>
        /// 특정 스탯 가져오기
        /// </summary>
        public StatusData GetStat(StatusType statType)
        {
            if (_stats.TryGetValue(statType, out StatusData stat))
                return stat;

            Debug.LogWarning($"Stat type {statType} not found in {GetType().Name}");
            return null;
        }

        /// <summary>
        /// 모든 스탯 가져오기
        /// </summary>
        public StatusData[] GetAllStats()
        {
            // GetStat 반복 대신 _stats.Values를 사용하여 바로 배열로 반환
            StatusData[] statsArray = new StatusData[_stats.Count];
            _stats.Values.CopyTo(statsArray, 0);
            return statsArray;
        }

        /// <summary>
        /// 스탯 값 가져오기
        /// </summary>
        public int GetStatValue(StatusType statType)
        {
            return GetStat(statType)?.CurrentValue ?? 0;
        }

        /// <summary>
        /// 스탯 효과값 가져오기 (배율 적용)
        /// </summary>
        public float GetStatEffectValue(StatusType statType)
        {
            return GetStat(statType)?.EffectValue ?? 0f;
        }

        /// <summary>
        /// 스탯 증가
        /// </summary>
        public virtual void IncreaseStat(StatusType statType, int amount)
        {
            if (_stats.TryGetValue(statType, out StatusData stat))
            {
                stat.IncreaseValue(amount);
                OnStatChanged?.Invoke(statType, amount);
            }
            else
                Debug.LogWarning($"Cannot increase stat {statType} - not found in {GetType().Name}");
        }

        /// <summary>
        /// 스탯 감소
        /// </summary>
        public virtual void DecreaseStat(StatusType statType, int amount)
        {
            if (_stats.TryGetValue(statType, out StatusData stat))
            {
                stat.DecreaseValue(amount);
                OnStatChanged?.Invoke(statType, -amount);
            }
            else
                Debug.LogWarning($"Cannot decrease stat {statType} - not found in {GetType().Name}");
        }

        /// <summary>
        /// 스탯을 특정 값으로 직접 설정
        /// </summary>
        public virtual void SetStat(StatusType statType, int value)
        {
            if (_stats.TryGetValue(statType, out StatusData stat))
            {
                stat.SetValue(value);
                OnStatSet?.Invoke(statType, value);
            }
            else
                Debug.LogWarning($"Cannot set stat {statType} - not found in {GetType().Name}");
        }

        /// <summary>
        /// 스탯 배율 설정
        /// </summary>
        public void SetStatMultiplier(StatusType statType, float multiplier)
        {
            if (_stats.TryGetValue(statType, out StatusData stat))
                stat.SetMultiplier(multiplier);
        }

        /// <summary>
        /// 모든 스탯을 기본값으로 리셋
        /// </summary>
        public void ResetAllStats()
        {
            foreach (var stat in _stats.Values)
                stat.ResetToBase();
        }

        /// <summary>
        /// 스탯이 존재하는지 확인
        /// </summary>
        public bool HasStat(StatusType statType)
        {
            return _stats.ContainsKey(statType);
        }

        /// <summary>
        /// 모든 스탯 타입 가져오기
        /// </summary>
        public IEnumerable<StatusType> GetAllStatTypes()
        {
            return _stats.Keys;
        }

        /// <summary>
        /// 새로운 스탯 추가
        /// </summary>
        protected void AddStat(StatusType statType, int baseValue, float multiplier = 1f)
        {
            if (!_stats.ContainsKey(statType))
                _stats[statType] = new StatusData(baseValue, multiplier);
            else
                Debug.LogWarning($"Stat {statType} already exists in {GetType().Name}");
        }

        /// <summary>
        /// 스탯 제거
        /// </summary>
        protected void RemoveStat(StatusType statType)
        {
            if (_stats.ContainsKey(statType))
                _stats.Remove(statType);
        }
    }
}