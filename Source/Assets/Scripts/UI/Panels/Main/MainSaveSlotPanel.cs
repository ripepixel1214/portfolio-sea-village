using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    /// <summary>
    /// 세이브 슬롯 선택 패널
    /// </summary>
    public class MainSaveSlotPanel : UIPanel
    {
        [Header("Save Slots")]
        [SerializeField] private SaveSlotUI[] slots = new SaveSlotUI[0];

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        // Reference
        private MainMenuUIController mainMenuController;
        private CancellationTokenSource _refreshCts;
        private bool _isBusy;

        #region Panel Methods
        public override void OnOpen()
        {
            if (mainMenuController == null)
                mainMenuController = FindFirstObjectByType<MainMenuUIController>();

            base.OnOpen();
            SetBusy(false);
            StartInitializeSlots();
        }

        public override void OnClose()
        {
            CancelRefresh();
            SetBusy(false);
            base.OnClose();
        }
        #endregion

        #region Event Listeners
        protected override void AddListeners()
        {
            foreach (var slot in slots)
                if (slot != null)
                    slot.OnSlotClicked += OnSlotClicked;

            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        protected override void RemoveListeners()
        {
            foreach (var slot in slots)
                if (slot != null)
                    slot.OnSlotClicked -= OnSlotClicked;

            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }
        #endregion

        private void StartInitializeSlots()
        {
            CancelRefresh();
            _refreshCts = new CancellationTokenSource();
            _ = InitializeSlotsAsync(_refreshCts.Token);
        }

        private void CancelRefresh()
        {
            if (_refreshCts == null)
                return;

            _refreshCts.Cancel();
            _refreshCts.Dispose();
            _refreshCts = null;
        }

        private async Task InitializeSlotsAsync(CancellationToken token)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (token.IsCancellationRequested)
                    return;

                await InitializeSlotAsync(i, token);
            }
        }

        /// <summary>
        /// 각 슬롯을 비동기로 초기화. 저장 데이터가 있으면 날짜와 골드 정보를 표시, 없으면 "??"로 표시
        /// </summary>
        private async Task InitializeSlotAsync(int slotIndex, CancellationToken token)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
                return;

            if (!SaveLoadManager.HasInstance)
            {
                slots[slotIndex].Initialize(slotIndex, false, "Day ??", "??");
                return;
            }

            SaveData previewData = await SaveLoadManager.Instance.GetSaveDataPreviewAsync(slotIndex);
            if (token.IsCancellationRequested)
                return;
            bool hasSaveData = previewData != null;

            string day = hasSaveData ? $"Day {previewData.gameDate}" : "Day ??";
            string gold = hasSaveData ? previewData.gold + "골드" : "??";
            slots[slotIndex].Initialize(slotIndex, hasSaveData, day, gold);
        }

        private void OnSlotClicked(int slotIndex, bool hasSaveData)
        {
            if (_isBusy)
                return;

            _ = HandleSlotClickedAsync(slotIndex, hasSaveData);
        }

        private async Task HandleSlotClickedAsync(int slotIndex, bool hasSaveData)
        {
            Debug.Log($"Slot {slotIndex} clicked. Has save data: {hasSaveData}");

            if (mainMenuController == null)
            {
                Debug.LogError("MainMenuUIController not found!");
                return;
            }

            SetBusy(true);
            if (hasSaveData)
            {
                await mainMenuController.ContinueGameAsync(slotIndex);
            }
            else
            {
                if (!mainMenuController.OpenGenderSelection(slotIndex))
                {
                    Debug.LogError("성별 선택 패널을 열 수 없습니다");
                    SetBusy(false);
                    return;
                }
            }

            if (isActiveAndEnabled)
            {
                SetBusy(false);
                UIManager.Instance.ClosePanel<MainSaveSlotPanel>();
            }
        }

        private void OnCloseButtonClicked()
        {
            UIManager.Instance.ClosePanel<MainSaveSlotPanel>();
        }

        private void SetBusy(bool isBusy)
        {
            _isBusy = isBusy;

            if (closeButton != null)
                closeButton.interactable = !isBusy;

            foreach (var slot in slots)
            {
                if (slot == null)
                    continue;

                Button slotButton = slot.GetSlotButton();
                if (slotButton != null)
                    slotButton.interactable = !isBusy;
            }
        }
    }
}
