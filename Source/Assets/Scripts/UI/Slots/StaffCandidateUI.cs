using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    /// <summary>직원 변경 패널의 고용 직원 후보 카드, 능력치·아이템·배치 버튼</summary>
    public class StaffCandidateUI : MonoBehaviour
    {
        [SerializeField] private Image _staffImage;
        [SerializeField] private TMP_Text _statText;
        [SerializeField] private Button _itemButton;
        [SerializeField] private Button _assignButton;
        [SerializeField] private TMP_Text _assignButtonText;

        private Action _onAssign;
        private Sprite _defaultStaffSprite;

        private void Awake()
        {
            if (_staffImage != null)
                _defaultStaffSprite = _staffImage.sprite;

            if (_assignButton != null)
                _assignButton.onClick.AddListener(RaiseAssign);
        }

        /// <summary>후보 카드를 능력치·아이템 활성·배치 라벨/활성으로 구성</summary>
        public void Configure(Sprite staffSprite, int intelligence, int charm, bool itemEnabled, string assignLabel, bool assignInteractable, Action onAssign)
        {
            _onAssign = onAssign;
            SetStaffSprite(staffSprite);

            if (_statText != null)
                _statText.text = $"지능 : {intelligence}\n매력 : {charm}";

            if (_itemButton != null)
                _itemButton.interactable = itemEnabled;

            if (_assignButtonText != null)
                _assignButtonText.text = assignLabel;

            if (_assignButton != null)
                _assignButton.interactable = assignInteractable;
        }

        private void RaiseAssign() => _onAssign?.Invoke();

        private void SetStaffSprite(Sprite staffSprite)
        {
            if (_staffImage == null)
                return;

            if (_defaultStaffSprite == null)
                _defaultStaffSprite = _staffImage.sprite;

            _staffImage.sprite = staffSprite ?? _defaultStaffSprite;
        }
    }
}
