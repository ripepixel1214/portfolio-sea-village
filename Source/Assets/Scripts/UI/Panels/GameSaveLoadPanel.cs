using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Data;
using SeaVillage.Utilities;

namespace SeaVillage.UI
{
    /// <summary>
    /// 인게임 세이브/로드 패널
    /// </summary>
    public class GameSaveLoadPanel : UIPanel
    {
        [Header("Slot UI")]
        [SerializeField] private SaveSlotUI[] slots = new SaveSlotUI[2];

        [Header("Panel UI")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button closeButton;

        private const string SaveTitle = "저장하기";
        private const string LoadTitle = "불러오기";
        private const string UnknownDay = "Day ??";
        private const string UnknownGold = "??";

        private bool isSaveMode = true;
        private int selectedSlot = -1;
        private CancellationTokenSource _refreshCts;
        private bool _isBusy;

        #region UIPanel Overrides
        public override void OnOpen()
        {
            ApplyMode();
            base.OnOpen();
            RefreshSlots();
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
            // SaveSlotUI 이벤트 설정
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].OnSlotClicked += OnSlotClicked;
                    slots[i].OnSlotDeleted += OnSlotDeleted;
                }
            }

            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        protected override void RemoveListeners()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].OnSlotClicked -= OnSlotClicked;
                    slots[i].OnSlotDeleted -= OnSlotDeleted;
                }
            }

            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }
        #endregion

        // 저장/로드 모드에 따른 표시 상태 반영
        private void ApplyMode()
        {
            selectedSlot = -1;
            SetBusy(false);

            if (titleText != null)
                titleText.text = isSaveMode ? SaveTitle : LoadTitle;
        }

        // 플레이스홀더를 먼저 칠하고 실제 슬롯 정보를 비동기로 채운다
        private void RefreshSlots()
        {
            InitializeSlots();
            StartRefreshSlotInfo();
        }

        private void InitializeSlots()
        {
            for (int i = 0; i < slots.Length; i++)
                InitializeSlot(i);
        }

        /// <summary>
        /// 슬롯을 로딩 플레이스홀더 상태로 초기화. 실제 슬롯 정보는 RefreshSlotInfoAsync가 채운다
        /// </summary>
        public void InitializeSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
                return;

            bool hasSaveData = SaveLoadManager.Instance.HasSaveFile(slotIndex);
            slots[slotIndex].Initialize(slotIndex, hasSaveData, UnknownDay, UnknownGold);
        }

        /// <summary>
        /// 저장 또는 불러오기 모드로 패널 초기화
        /// </summary>
        public void Initialize(bool saveMode)
        {
            isSaveMode = saveMode;
            ApplyMode();
            RefreshSlots();
        }

        public override void Close()
        {
            selectedSlot = -1;
            base.Close();
        }

        private void OnSlotClicked(int slotIndex, bool hasSaveData)
        {
            if (_isBusy)
                return;

            selectedSlot = slotIndex;

            if (isSaveMode)
            {
                // 저장 모드
                if (hasSaveData)
                    UIManager.Instance.ShowConfirmMessage(
                        $"슬롯 {slotIndex + 1}에 이미 저장된 데이터가 있습니다.\n덮어쓸까요?",
                        () => _ = ExecuteSaveAsync(), title: SaveTitle);
                else
                    _ = ExecuteSaveAsync();
            }
            else
            {
                // 로드 모드
                if (hasSaveData)
                    UIManager.Instance.ShowConfirmMessage(
                        $"슬롯 {slotIndex + 1}의 데이터를 불러올까요?",
                        () => _ = ExecuteLoadAsync(), title: LoadTitle);
                else
                    Debug.LogWarning($"슬롯 {slotIndex + 1}에 저장된 데이터가 없습니다.");
            }
        }

        private void OnSlotDeleted(int slotIndex)
        {
            Debug.Log($"슬롯 {slotIndex + 1}의 데이터가 삭제되었습니다.");
        }

        private void StartRefreshSlotInfo()
        {
            CancelRefresh();
            _refreshCts = new CancellationTokenSource();
            _ = RefreshSlotInfoAsync(_refreshCts.Token);
        }

        private void CancelRefresh()
        {
            if (_refreshCts == null)
                return;

            _refreshCts.Cancel();
            _refreshCts.Dispose();
            _refreshCts = null;
        }

        private async Task RefreshSlotInfoAsync(CancellationToken token)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (token.IsCancellationRequested)
                    return;

                if (slots[i] == null)
                    continue;

                if (!SaveLoadManager.Instance.HasSaveFile(i))
                {
                    slots[i].Initialize(i, false, UnknownDay, UnknownGold);
                    continue;
                }

                SaveData previewData = await SaveLoadManager.Instance.GetSaveDataPreviewAsync(i);
                if (token.IsCancellationRequested)
                    return;

                bool hasSaveData = previewData != null;
                string day = hasSaveData ? $"Day {previewData.gameDate}" : UnknownDay;
                string gold = hasSaveData ? previewData.gold + "골드" : UnknownGold;
                slots[i].Initialize(i, hasSaveData, day, gold);
            }
        }

        #region Save/Load Execution
        private async Task ExecuteSaveAsync()
        {
            if (selectedSlot < 0) return;

            SetBusy(true);
            bool success = await SaveLoadManager.Instance.SaveGameAsync(selectedSlot);
            if (!isActiveAndEnabled)
                return;

            if (!success)
            {
                SetBusy(false);
                Debug.LogError($"슬롯 {selectedSlot + 1}에 게임을 저장하지 못했습니다.");
                return;
            }

            StartRefreshSlotInfo();
            Close();
            SetBusy(false);

            Debug.Log($"게임이 슬롯 {selectedSlot + 1}에 저장되었습니다.");
        }

        private async Task ExecuteLoadAsync()
        {
            if (selectedSlot < 0) return;

            SetBusy(true);
            bool success = await SaveLoadManager.Instance.LoadGameAsync(selectedSlot);
            if (!isActiveAndEnabled)
                return;

            if (success)
            {
                Close();
                SetBusy(false);
                Debug.Log($"슬롯 {selectedSlot + 1}에서 게임을 로드했습니다.");

                // 저장 시점의 마을로 전환. 같은 씬이어도 재로드해 씬 상태를 저장 데이터와 일치시킨다
                SceneChanger.Instance.ChangeScene(SaveLoadManager.Instance.GetResumeSceneName());
            }
            else
            {
                SetBusy(false);
                Debug.LogError($"슬롯 {selectedSlot + 1}에서 게임 로드에 실패했습니다.");
            }
        }
        #endregion

        /// <summary>
        /// 패널이 바쁜 상태인지 설정. 바쁜 상태에서는 모든 버튼과 슬롯이 비활성화되어 사용자 입력을 차단
        /// </summary>
        private void SetBusy(bool isBusy)
        {
            _isBusy = isBusy;

            if (closeButton != null)
                closeButton.interactable = !isBusy;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                    continue;

                Button slotButton = slots[i].GetSlotButton();
                if (slotButton != null)
                    slotButton.interactable = !isBusy;

                Button deleteButton = slots[i].GetDeleteButton();
                if (deleteButton != null)
                    deleteButton.interactable = !isBusy;
            }
        }
    }
}
