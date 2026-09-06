using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    /// <summary>직원 관리 패널의 역할 슬롯(계산/호객), 능력치 표시와 [변경] 진입</summary>
    public class StaffSlotUI : MonoBehaviour
    {
        [Header("State Roots")]
        [SerializeField] private GameObject _activeRoot;
        [SerializeField] private GameObject _lockedRoot;

        [Header("Content")]
        [SerializeField] private Image _staffImage;
        [SerializeField] private TMP_Text _roleLabelText;
        [SerializeField] private TMP_Text _statText;
        [SerializeField] private Button _changeButton;

        private Sprite _defaultStaffSprite;

        public event Action OnChangeRequested;

        private void Awake()
        {
            if (_staffImage != null)
                _defaultStaffSprite = _staffImage.sprite;

            if (_changeButton != null)
                _changeButton.onClick.AddListener(RaiseChangeRequested);
        }

        /// <summary>능력치를 표시하고 [변경]을 활성화</summary>
        public void ShowActive(string roleLabel, bool hasStaff, int intelligence, int charm)
        {
            ShowActive(roleLabel, hasStaff, null, intelligence, charm);
        }

        /// <summary>설정된 직원의 표시 Sprite와 능력치를 표시</summary>
        public void ShowActive(string roleLabel, bool hasStaff, Sprite staffSprite, int intelligence, int charm)
        {
            SetActive(_activeRoot, true);
            SetActive(_lockedRoot, false);
            SetRoleLabel(roleLabel);
            SetStaffSprite(hasStaff ? staffSprite : null);

            if (_changeButton != null)
                _changeButton.interactable = true;

            if (_statText != null)
                _statText.text = hasStaff
                    ? $"지능 : {intelligence}\n매력 : {charm}"
                    : "지능 : -\n매력 : -";
        }

        /// <summary>직원이 없는 빈 슬롯 표시</summary>
        public void ShowEmpty(string roleLabel)
        {
            ShowActive(roleLabel, false, 0, 0);

            if (_changeButton != null)
                _changeButton.interactable = false;
        }

        public void ShowLocked(string roleLabel)
        {
            SetActive(_activeRoot, false);
            SetActive(_lockedRoot, true);
            SetRoleLabel(roleLabel);
        }

        private void RaiseChangeRequested() => OnChangeRequested?.Invoke();

        private void SetStaffSprite(Sprite staffSprite)
        {
            if (_staffImage == null)
                return;

            if (_defaultStaffSprite == null)
                _defaultStaffSprite = _staffImage.sprite;

            _staffImage.sprite = staffSprite != null ? staffSprite : _defaultStaffSprite;
        }

        private static void SetActive(GameObject go, bool value)
        {
            if (go != null)
                go.SetActive(value);
        }

        private void SetRoleLabel(string label)
        {
            if (_roleLabelText != null)
                _roleLabelText.text = label;
        }
    }
}
