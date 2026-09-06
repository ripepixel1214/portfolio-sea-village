using System.Collections;

namespace SeaVillage.Event.Services
{
    /// <summary>
    /// 퀘스트 관리 기능을 담당하는 서비스 인터페이스 <br/>
    /// 이벤트 시퀀스 기반 퀘스트의 시작, 업데이트, 완료 및 퀘스트 상태 조회 기능을 제공
    /// </summary>
    public interface IQuestService
    {
        IEnumerator StartQuest(string questId);

        IEnumerator UpdateQuest(string questId, string data);

        IEnumerator CompleteQuest(string questId);

        /// <summary>
        /// 퀘스트 완료 상태를 즉시 기록 (동기, 이벤트 종료 등 비코루틴 컨텍스트에서 호출)
        /// </summary>
        void MarkQuestCompleted(string questId);

        /// <summary>
        /// 특정 퀘스트가 완료되었는지 확인, EventManager의 Quest 조건 평가에서 사용
        /// </summary>
        bool IsQuestCompleted(string questId);

        string GetActiveQuestId();
    }
}
