using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using System.Linq;

namespace SeaVillage.Data
{
    [CreateAssetMenu(fileName = "SpecialEffectDatabase", menuName = "SeaVillage/Data/Special Effect Database")]
    public class SpecialEffectDatabase : SerializedScriptableObject
    {
        [SerializeField] private Dictionary<int, SpecialEffectData> _specialEffectDict = new Dictionary<int, SpecialEffectData>();
        [SerializeField] private Dictionary<int, List<SpecialEffectItemChangeData>> _SEItemChangeDict = new Dictionary<int, List<SpecialEffectItemChangeData>>();

        public SpecialEffectData GetSpecialEffect(int sEID)
        {
            return _specialEffectDict.TryGetValue(sEID, out var data) ? data : null;
        }

        public List<SpecialEffectItemChangeData> GetSEItemChanges(int sEID)
        {
            return _SEItemChangeDict.TryGetValue(sEID, out var list) ? list : null;
        }

        /// <summary>
        /// 신문에 나올 수 있는(News=true) 특수 효과 목록 반환
        /// </summary>
        public List<SpecialEffectData> GetAvailableEffects()
        {
            return _specialEffectDict.Values.Where(sE => sE.News).ToList();
        }

        public void SetData(List<SpecialEffectData> specialEffects, List<SpecialEffectItemChangeData> itemChanges)
        {
            _specialEffectDict.Clear();
            _SEItemChangeDict.Clear();

            foreach (var effect in specialEffects)
                _specialEffectDict[effect.ID] = effect;

            foreach (var change in itemChanges)
            {
                if (!_SEItemChangeDict.ContainsKey(change.ID))
                    _SEItemChangeDict[change.ID] = new List<SpecialEffectItemChangeData>();
                _SEItemChangeDict[change.ID].Add(change);
            }
        }
    }
}