using System.Collections;

namespace SeaVillage.Event.Services
{
    /// <summary>
    /// UI 표시 기능을 담당하는 서비스 인터페이스
    /// 대화창, 패널 등의 UI 표시를 처리
    /// </summary>
    public interface IUIService
    {
        /// <summary>
        /// 대화창 표시
        /// </summary>
        /// <param name="npcId">NPC ID</param>
        /// <param name="dialogueText">표시할 대사</param>
        /// <returns>대화가 종료될 때까지 대기</returns>
        IEnumerator ShowDialogue(string npcId, string dialogueText);

        /// <summary>
        /// UI 패널을 표시
        /// </summary>
        /// <param name="panelName">패널 이름(일러스트/이미지 키 등)</param>
        /// <param name="content">패널에 표시할 내용</param>
        /// <param name="isChoicePrompt">true 면 바로 다음 스텝이 선택지(Choice)인 프롬프트 — Event Panel 본문으로 표시. false 면 결과/내러티브 — Toast 로 표시.</param>
        /// <returns>표시 처리가 끝날 때까지 대기</returns>
        IEnumerator ShowPanel(string panelName, string content, bool isChoicePrompt);
    }
}
