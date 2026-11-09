using System;
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
        /// UI 패널을 표시
        /// </summary>
        /// <param name="panelName">패널 이름(일러스트/이미지 키 등)</param>
        /// <param name="content">패널에 표시할 내용</param>
        /// <param name="isChoicePrompt">true면 바로 다음 스텝이 복수 선택지인 프롬프트로 Event Panel 본문에 표시</param>
        /// <returns>표시 처리가 끝날 때까지 대기</returns>
        IEnumerator ShowPanel(string panelName, string content, bool isChoicePrompt);

        /// <summary>단일 확인형 이벤트 팝업을 표시하고 응답까지 대기</summary>
        IEnumerator ShowPopup(string title, string content, string confirmText, Action<bool> onCompleted);
    }
}
