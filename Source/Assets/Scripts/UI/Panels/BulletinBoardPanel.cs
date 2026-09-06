using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;
using SeaVillage.Data;
using SeaVillage.Event;
using SeaVillage.UI;
using SeaVillage.Utilities;

namespace SeaVillage.UI
{
    /// <summary>
    /// 게시판 패널.
    /// 현재 마을에서 시작 가능한 이벤트를 최대 3개까지 표시한다.
    /// </summary>
    public class BulletinBoardPanel : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button submitButton_1;
        [SerializeField] private Button submitButton_2;
        [SerializeField] private Button submitButton_3;

        [Header("Quest Slot 1")]
        [SerializeField] private TextMeshProUGUI title_1;
        [SerializeField] private Image icon_1;
        [SerializeField] private TextMeshProUGUI detail_1;
        [SerializeField] private TextMeshProUGUI reward_1;
        [SerializeField] private GameObject slotRoot_1;

        [Header("Quest Slot 2")]
        [SerializeField] private TextMeshProUGUI title_2;
        [SerializeField] private Image icon_2;
        [SerializeField] private TextMeshProUGUI detail_2;
        [SerializeField] private TextMeshProUGUI reward_2;
        [SerializeField] private GameObject slotRoot_2;

        [Header("Quest Slot 3")]
        [SerializeField] private TextMeshProUGUI title_3;
        [SerializeField] private Image icon_3;
        [SerializeField] private TextMeshProUGUI detail_3;
        [SerializeField] private TextMeshProUGUI reward_3;
        [SerializeField] private GameObject slotRoot_3;

        private BulletinBoardService _boardService;
        private string _currentVillage;
        private BoardQuestSlot[] _displayedSlots;

        private NavigationArea currentArea = NavigationArea.QuestAcceptButton;
        private int currentAcceptIndex = 1;

        #region UIPanel Overrides
        public override void OnOpen()
        {
            base.OnOpen();

            _boardService = EventManager.Instance?.BulletinBoardService;
            _currentVillage = GameManager.HasInstance
                ? TownKeyUtility.ToStorageKey(GameManager.Instance.CurrentTownKey)
                : string.Empty;

            RefreshDisplay();
        }

        public override void OnFocusRestored()
        {
            base.OnFocusRestored();
            RefreshDisplay();
        }
        #endregion

        #region Event Listeners
        protected override void AddListeners()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            if (submitButton_1 != null)
                submitButton_1.onClick.AddListener(() => OnAcceptClicked(0));
            if (submitButton_2 != null)
                submitButton_2.onClick.AddListener(() => OnAcceptClicked(1));
            if (submitButton_3 != null)
                submitButton_3.onClick.AddListener(() => OnAcceptClicked(2));
        }

        protected override void RemoveListeners()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();
            if (submitButton_1 != null)
                submitButton_1.onClick.RemoveAllListeners();
            if (submitButton_2 != null)
                submitButton_2.onClick.RemoveAllListeners();
            if (submitButton_3 != null)
                submitButton_3.onClick.RemoveAllListeners();
        }
        #endregion

        #region Display
        private void RefreshDisplay()
        {
            if (_boardService == null)
            {
                ClearAllSlots();
                return;
            }

            _displayedSlots = _boardService.GetDisplayedQuests(_currentVillage);

            BindSlot(0, title_1, icon_1, detail_1, reward_1, submitButton_1, slotRoot_1);
            BindSlot(1, title_2, icon_2, detail_2, reward_2, submitButton_2, slotRoot_2);
            BindSlot(2, title_3, icon_3, detail_3, reward_3, submitButton_3, slotRoot_3);
        }

        private void BindSlot(int index, TextMeshProUGUI title, Image icon, TextMeshProUGUI detail,
            TextMeshProUGUI reward, Button acceptBtn, GameObject slotRoot)
        {
            if (_displayedSlots == null || index >= _displayedSlots.Length)
            {
                SetSlotVisible(false, title, icon, detail, reward, acceptBtn, slotRoot);
                return;
            }

            var slot = _displayedSlots[index];
            if (slot == null || slot.IsEmpty)
            {
                SetSlotVisible(false, title, icon, detail, reward, acceptBtn, slotRoot);
                return;
            }

            SetSlotVisible(true, title, icon, detail, reward, acceptBtn, slotRoot);

            if (title != null) title.text = "현상수배";
            ApplyItemIcon(icon, slot);
            if (detail != null) detail.text = slot.DetailText ?? "";
            if (reward != null) reward.text = slot.RewardText ?? "";
            if (acceptBtn != null)
            {
                bool canSubmit = _boardService != null && _boardService.CanSubmitQuest(_currentVillage, index);
                acceptBtn.interactable = canSubmit;

                var btnText = acceptBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                    btnText.text = "제출하기";
            }
        }

        private void SetSlotVisible(bool visible, TextMeshProUGUI title, Image icon, TextMeshProUGUI detail,
            TextMeshProUGUI reward, Button acceptBtn, GameObject slotRoot)
        {
            if (slotRoot != null)
            {
                slotRoot.SetActive(visible);
                return;
            }

            if (title != null) title.gameObject.SetActive(visible);
            if (icon != null) icon.gameObject.SetActive(visible);
            if (detail != null) detail.gameObject.SetActive(visible);
            if (reward != null) reward.gameObject.SetActive(visible);
            if (acceptBtn != null) acceptBtn.gameObject.SetActive(visible);
        }

        private void ApplyItemIcon(Image icon, BoardQuestSlot slot)
        {
            if (icon == null)
                return;

            Sprite itemIcon = null;
            ItemData itemData = null;

            if (slot?.Board != null)
                itemData = DataManager.Instance?.GetItem(slot.Board.ItemID);

            if (itemData != null)
            {
                itemIcon = itemData.Icon;

                if (itemIcon == null && !string.IsNullOrEmpty(itemData.Image))
                    itemIcon = UIManager.Instance?.LoadItemIcon(itemData.Image);
            }

            icon.sprite = itemIcon;
            icon.enabled = itemIcon != null;
        }

        private void ClearAllSlots()
        {
            SetSlotVisible(false, title_1, icon_1, detail_1, reward_1, submitButton_1, slotRoot_1);
            SetSlotVisible(false, title_2, icon_2, detail_2, reward_2, submitButton_2, slotRoot_2);
            SetSlotVisible(false, title_3, icon_3, detail_3, reward_3, submitButton_3, slotRoot_3);
        }
        #endregion

        #region Interaction
        private void OnAcceptClicked(int slotIndex)
        {
            if (_boardService == null || string.IsNullOrEmpty(_currentVillage))
                return;

            var slot = _boardService.GetDisplayedQuest(_currentVillage, slotIndex);
            if (slot == null || slot.IsEmpty)
                return;

            var submissionPanel = UIManager.Instance?.OpenPanel<SubmissionPanel>();
            if (submissionPanel != null)
                submissionPanel.Configure(slot);
        }
        #endregion

        #region Navigation
        public override void NavigateToUpperButton()
        {
            if (currentArea == NavigationArea.QuestAcceptButton)
                currentArea = NavigationArea.CloseButton;

            SelectCurrentButton();
        }

        public override void NavigateToLowerButton()
        {
            if (currentArea == NavigationArea.CloseButton)
            {
                currentArea = NavigationArea.QuestAcceptButton;
                currentAcceptIndex = 1;
            }

            SelectCurrentButton();
        }

        public override void NavigateToLeftButton()
        {
            if (currentArea == NavigationArea.QuestAcceptButton && currentAcceptIndex > 0)
                currentAcceptIndex--;

            SelectCurrentButton();
        }

        public override void NavigateToRightButton()
        {
            if (currentArea == NavigationArea.QuestAcceptButton && currentAcceptIndex < 2)
                currentAcceptIndex++;

            SelectCurrentButton();
        }

        public override void ClickSelectedButton()
        {
            Button btn = GetCurrentButton();
            if (btn != null && btn.interactable)
                btn.onClick.Invoke();
        }

        private void SelectCurrentButton()
        {
            Button btn = GetCurrentButton();
            if (btn != null)
                btn.Select();
        }

        private Button GetCurrentButton()
        {
            if (currentArea == NavigationArea.CloseButton)
                return closeButton;

            return currentAcceptIndex switch
            {
                0 => submitButton_1,
                1 => submitButton_2,
                2 => submitButton_3,
                _ => submitButton_2
            };
        }
        #endregion
    }
}
