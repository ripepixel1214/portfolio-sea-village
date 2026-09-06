using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    /// <summary>
    /// 세이브 슬롯 UI 컴포넌트
    /// </summary>
    public class SaveSlotUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image playerAvatarImage;
        [SerializeField] private TextMeshProUGUI dayText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private GameObject emptyIndicator;
        [SerializeField] private Button slotButton;
        [SerializeField] private Button deleteButton;

        private int slotIndex;
        private bool hasSaveData;

        /// <summary>
        /// 슬롯 클릭 이벤트 (슬롯 인덱스, 저장 데이터 유무)
        /// </summary>
        public event Action<int, bool> OnSlotClicked;
        public event Action<int> OnSlotDeleted;

        public Button GetSlotButton() => slotButton;
        public Button GetDeleteButton() => deleteButton;
        public void ToggleEmptyIndicator(bool toggle) => emptyIndicator.SetActive(toggle);

        private void Awake()
        {
            if (slotButton == null)
                slotButton = GetComponent<Button>();

            AddListeners();
        }

        private void OnDestroy()
        {
            RemoveListeners();
        }

        private void AddListeners()
        {
            if (slotButton != null)
                slotButton.onClick.AddListener(OnSlotButtonClicked);

            if (deleteButton != null)
                deleteButton.onClick.AddListener(OnDeleteButtonClicked);
        }

        private void RemoveListeners()
        {
            if (slotButton != null)
                slotButton.onClick.RemoveListener(OnSlotButtonClicked);

            if (deleteButton != null)
                deleteButton.onClick.RemoveListener(OnDeleteButtonClicked);
        }

        /// <summary>
        /// 슬롯 초기화
        /// </summary>
        /// <param name="index">슬롯 인덱스</param>
        /// <param name="hasSave">저장 데이터 유무</param>
        /// <param name="info">슬롯 정보 텍스트</param>
        public void Initialize(int index, bool hasSave, string day, string gold)
        {
            slotIndex = index;
            hasSaveData = hasSave;

            // 저장 데이터 유무와 무관하게 슬롯 본 버튼은 항상 선택/클릭 가능해야 한다.
            if (slotButton != null)
            {
                slotButton.gameObject.SetActive(true);
                slotButton.interactable = true;
            }

            ClearGameDataContainers();

            if (hasSave)
            {
                if (playerAvatarImage != null)
                    playerAvatarImage.gameObject.SetActive(true);
                if (dayText != null)
                    dayText.text = day;
                if (goldText != null)
                    goldText.text = gold;
                if (deleteButton != null)
                    deleteButton.gameObject.SetActive(true);
            }

            emptyIndicator.SetActive(!hasSave);
        }

        private void ClearGameDataContainers()
        {
            if (playerAvatarImage != null)
                playerAvatarImage.gameObject.SetActive(false);
            if (dayText != null)
                dayText.text = string.Empty;
            if (goldText != null)
                goldText.text = string.Empty;
            if (deleteButton != null)
                deleteButton.gameObject.SetActive(false);
        }

        private void OnSlotButtonClicked()
        {
            OnSlotClicked?.Invoke(slotIndex, hasSaveData);
        }

        private async void OnDeleteButtonClicked()
        {
            bool deleted = await SaveLoadManager.Instance.DeleteSaveFileAsync(slotIndex);
            if (!deleted || this == null)
                return;

            ClearGameDataContainers();
            
            if (emptyIndicator != null)
                emptyIndicator.SetActive(true);
            
            hasSaveData = false;
            OnSlotDeleted?.Invoke(slotIndex);
        }
    }
}
