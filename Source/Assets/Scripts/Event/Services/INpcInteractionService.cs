using System.Collections;

namespace SeaVillage.Event.Services
{
    /// <summary>
    /// NPC 상호작용 기능을 담당하는 서비스 인터페이스
    /// </summary>
    public interface INpcInteractionService
    {
        /// <summary>
        /// NPC 상호작용 시작
        /// </summary>
        /// <returns>상호작용 완료까지 대기</returns>
        IEnumerator StartInteraction(string npcId);

        /// <summary>
        /// NPC 상호작용 종료
        /// </summary>
        /// <returns>처리 완료까지 대기</returns>
        IEnumerator EndInteraction(string npcId);
    }
}
