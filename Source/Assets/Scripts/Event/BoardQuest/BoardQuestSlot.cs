using SeaVillage.Data;

namespace SeaVillage.Event
{
    /// <summary>
    /// 게시판에 표시되는 개별 퀘스트 슬롯
    /// </summary>
    public class BoardQuestSlot
    {
        public BoardData Board { get; set; }
        public string QuestKey { get; set; }
        public BoardQuestSlotState State { get; set; }

        public string TitleText { get; set; }
        public string DetailText { get; set; }
        public string RewardText { get; set; }

        public bool IsEmpty => State == BoardQuestSlotState.Empty || Board == null;

        public BoardQuestSlot()
        {
            State = BoardQuestSlotState.Empty;
        }
    }
}
