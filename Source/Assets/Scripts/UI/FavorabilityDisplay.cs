using UnityEngine;
using TMPro;
using SeaVillage.Core;

namespace SeaVillage.UI
{
    /// <summary>마을 호감도 수치와 단계 호칭을 표시</summary>
    public class FavorabilityDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text _displayText;

        private void Awake()
        {
            if (_displayText == null)
                Debug.LogError("[FavorabilityDisplay] 호감도 텍스트 참조가 없습니다", this);
        }

        public void SetLevel(int level)
        {
            if (_displayText == null)
                return;

            int affinity = TownAffinityRules.Clamp(level);
            _displayText.text = $"♡ {affinity} | {TownAffinityRules.GetTitle(affinity)}";
        }
    }
}
