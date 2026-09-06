using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.Event;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    /// <summary>
    /// 퀘스트 패널.
    /// 현재 수락 중인 게시판 이벤트 정보를 표시한다.
    /// </summary>
    public class QuestPanel : UIPanel, IContextualPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button abandonButton;

        [Header("Quest Info")]
        [SerializeField] private TextMeshProUGUI questTitle;
        [SerializeField] private TextMeshProUGUI questDescription;
        [SerializeField] private TextMeshProUGUI questReward;
        [SerializeField] private TextMeshProUGUI questVillage;

        [Header("Status")]
        [SerializeField] private GameObject activeQuestGroup;
        [SerializeField] private GameObject noQuestGroup;

        private BulletinBoardService _boardService;

        #region Lifecycle
        public override void OnOpen()
        {
            base.OnOpen();
            _boardService = EventManager.Instance?.BulletinBoardService;
            RefreshDisplay();
        }
        #endregion

        #region Event Listeners
        protected override void AddListeners()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            if (abandonButton != null)
                abandonButton.onClick.AddListener(OnAbandonClicked);
        }

        protected override void RemoveListeners()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();
            if (abandonButton != null)
                abandonButton.onClick.RemoveAllListeners();
        }
        #endregion

        #region Display
        private void RefreshDisplay()
        {
            string currentVillage = GameManager.HasInstance
                ? TownKeyUtility.ToStorageKey(GameManager.Instance.CurrentTownKey)
                : string.Empty;
            var displayedQuests = _boardService != null ? _boardService.GetDisplayedQuests(currentVillage) : null;
            var representativeQuest = displayedQuests?.FirstOrDefault(slot => slot != null && !slot.IsEmpty)?.Board;
            bool hasQuest = representativeQuest != null;

            if (activeQuestGroup != null)
                activeQuestGroup.SetActive(hasQuest);
            if (noQuestGroup != null)
                noQuestGroup.SetActive(!hasQuest);
            if (abandonButton != null)
                abandonButton.gameObject.SetActive(false);

            if (!hasQuest)
            {
                ClearQuestInfo();
                return;
            }

            if (questTitle != null) questTitle.text = ResolveQuestTitle(representativeQuest);
            if (questDescription != null) questDescription.text = representativeQuest.Description ?? "";
            if (questReward != null) questReward.text = representativeQuest.Reward > 0 ? $"골드 +{representativeQuest.Reward}" : "";
            if (questVillage != null) questVillage.text = currentVillage ?? "";
        }

        private static string ResolveQuestTitle(BoardData activeQuest)
        {
            if (activeQuest == null)
                return "";

            var item = DataManager.Instance?.GetItem(activeQuest.ItemID);
            if (item != null)
            {
                string itemName = DataManager.Instance.GetScriptTextKR(item.Name);
                if (!string.IsNullOrWhiteSpace(itemName))
                    return itemName;
            }

            return $"Item {activeQuest.ItemID}";
        }

        private void ClearQuestInfo()
        {
            if (questTitle != null) questTitle.text = "";
            if (questDescription != null) questDescription.text = "";
            if (questReward != null) questReward.text = "";
            if (questVillage != null) questVillage.text = "";
        }
        #endregion

        #region Interaction
        private void OnAbandonClicked()
        {
            Debug.LogWarning("[QuestPanel] 이벤트는 진행 중 취소를 지원하지 않습니다.");
        }
        #endregion
    }
}
