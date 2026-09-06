using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    /// <summary>고용 패널의 직원 오퍼 카드, 능력치·계약금·조건 문구와 고용 버튼</summary>
    public class StaffOfferUI : MonoBehaviour
    {
        [SerializeField] private GameObject _abilityRoot;
        [SerializeField] private Image _staffImage;
        [SerializeField] private TMP_Text _intelligenceText;
        [SerializeField] private TMP_Text _charmText;
        [SerializeField] private TMP_Text _contractCostText;
        [SerializeField] private TMP_Text _requirementText;
        [SerializeField] private Button _hireButton;
        [SerializeField] private TMP_Text _hireButtonText;

        private Action _onHire;

        private void Awake()
        {
            if (_hireButton != null)
                _hireButton.onClick.AddListener(RaiseHire);
        }

        /// <summary>오퍼 카드를 능력치·계약금·조건 문구·고용 버튼 상태로 구성</summary>
        public void Configure(Sprite staffSprite, int intelligence, int charm, int contractCost, bool warnGold,
            string requirement, Color requirementColor, string hireLabel, bool hireInteractable, Action onHire)
        {
            _onHire = onHire;

            if (_staffImage != null && staffSprite != null)
                _staffImage.sprite = staffSprite;

            if (_intelligenceText != null)
                _intelligenceText.text = $"지능 : {intelligence}";

            if (_charmText != null)
                _charmText.text = $"매력 : {charm}";

            if (_contractCostText != null)
            {
                _contractCostText.text = $"{Mathf.Max(0, contractCost):N0}G";
                _contractCostText.color = warnGold ? Color.red : Color.black;
            }

            // 조건 문구 표시 여부(호감도 부족·고용 완료), 표시 시 능력치 블록은 숨김
            bool showRequirement = !string.IsNullOrEmpty(requirement);

            if (_requirementText != null)
            {
                _requirementText.gameObject.SetActive(showRequirement);
                if (showRequirement)
                {
                    _requirementText.text = requirement;
                    _requirementText.color = requirementColor;
                }
            }

            if (_abilityRoot != null)
                _abilityRoot.SetActive(!showRequirement);

            if (_hireButtonText != null)
                _hireButtonText.text = hireLabel;

            if (_hireButton != null)
                _hireButton.interactable = hireInteractable;
        }

        private void RaiseHire() => _onHire?.Invoke();
    }
}
