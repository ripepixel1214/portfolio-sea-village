using System;
using System.Collections;
using System.Collections.Generic;
using SeaVillage.Data;

namespace SeaVillage.Event.Services
{
    /// <summary>
    /// 플레이어 선택지 처리를 담당하는 서비스 인터페이스
    /// 선택지 제시, 선택 수집, 결과 반영을 처리
    /// </summary>
    public interface IChoiceService
    {
        /// <summary>
        /// 플레이어에게 선택지 제시
        /// </summary>
        /// <param name="options">선택 가능한 옵션들(동일한 스텝의 시퀀스들)</param>
        /// <param name="onChosen">선택 완료 콜백 (선택된 옵션 전달)</param>
        /// <returns>선택 완료까지 대기</returns>
        IEnumerator Choose(List<EventSequenceData> options, Action<EventSequenceData> onChosen);
    }
}
