using System;
using UnityEngine;

namespace SeaVillage.Status
{
    /// <summary>
    /// 개별 스탯 정보를 담는 스탯 인스턴스 클래스
    /// </summary>
    [Serializable]
    public class StatusData
    {
        [SerializeField] private int _defaultValue;
        [SerializeField] private int _currentValue;
        [SerializeField] private float _multiplier;

        public int DefaultValue => _defaultValue;
        public int CurrentValue => _currentValue;
        public float Multiplier => _multiplier;
        public float EffectValue => _currentValue * _multiplier;

        public StatusData(int defaultValue, float multiplier = 1f)
        {
            _defaultValue = defaultValue;
            _currentValue = defaultValue;
            _multiplier = multiplier;
        }

        public void SetValue(int value)
        {
            _currentValue = value;
        }

        public void IncreaseValue(int amount)
        {
            _currentValue += amount;
        }

        public void DecreaseValue(int amount)
        {
            _currentValue = Mathf.Max(0, _currentValue - amount);
        }

        public void SetMultiplier(float multiplier)
        {
            _multiplier = multiplier;
        }

        public void ResetToBase()
        {
            _currentValue = _defaultValue;
        }
    }
}