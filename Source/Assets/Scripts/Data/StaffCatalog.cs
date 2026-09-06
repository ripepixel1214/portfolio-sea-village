using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaVillage.Data
{
    /// <summary>고용 가능한 직원 한 명의 정의</summary>
    [Serializable]
    public class StaffDefinition
    {
        [SerializeField] private int _staffId;
        [SerializeField] private int _requiredLoveLevel;
        [SerializeField] private int _intelligence;
        [SerializeField] private int _charm;
        [SerializeField] private int _contractCost;
        [SerializeField] private Sprite _sprite;
        [SerializeField] private int _requiredItemId;

        public int StaffId => _staffId;
        public int RequiredLoveLevel => _requiredLoveLevel;
        public int Intelligence => _intelligence;
        public int Charm => _charm;
        public int ContractCost => _contractCost;
        public Sprite Sprite => _sprite;
        /// <summary>직원 등록에 소비할 아이템 ID</summary>
        public int RequiredItemId => _requiredItemId;

        /// <summary>아이템으로 등록하는 직원 여부</summary>
        public bool IsItemStaff => _requiredItemId > 0;
    }

    /// <summary>시청에서 고용 가능한 직원 목록(수기 편집 SO)</summary>
    [CreateAssetMenu(fileName = nameof(StaffCatalog), menuName = "SeaVillage/Staff Catalog")]
    public class StaffCatalog : ScriptableObject
    {
        [SerializeField] private List<StaffDefinition> _definitions = new List<StaffDefinition>();

        public IReadOnlyList<StaffDefinition> All => _definitions;

        /// <summary>인덱스 위치의 직원 정의, 범위를 벗어나면 false</summary>
        public bool TryGet(int index, out StaffDefinition definition)
        {
            if (_definitions == null || index < 0 || index >= _definitions.Count)
            {
                definition = null;
                return false;
            }

            definition = _definitions[index];
            return definition != null;
        }

        /// <summary>직원 ID로 직원 정의 조회</summary>
        public bool TryGetByStaffId(int staffId, out StaffDefinition definition)
        {
            if (_definitions != null && staffId > 0)
            {
                for (int i = 0; i < _definitions.Count; i++)
                {
                    StaffDefinition candidate = _definitions[i];
                    if (candidate != null && candidate.StaffId == staffId)
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

    }
}
