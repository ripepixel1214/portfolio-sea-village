using System;
using System.Collections;

namespace SeaVillage.Event
{
    /// <summary>이벤트 NPC 생성, 상호작용 대기, 씬 단위 정리를 담당</summary>
    public interface IEventNpcRuntime
    {
        /// <summary>이벤트 NPC가 없으면 현재 마을의 지정 위치에 생성</summary>
        bool TrySpawn(string npcId);

        /// <summary>
        /// 지정 NPC와의 월드 상호작용 대기
        /// </summary>
        /// <returns>상호작용 완료까지 대기</returns>
        IEnumerator WaitForInteraction(string npcId, Action<bool> onCompleted);

        /// <summary>
        /// 지정 NPC 위에 대사를 표시하고 같은 NPC의 다음 상호작용까지 대기
        /// </summary>
        IEnumerator ShowDialogue(string npcId, string dialogueText, Action<bool> onCompleted);

        /// <summary>이벤트 구간 종료 후 연결된 상점 상호작용을 연다</summary>
        bool TryOpenShopAfterEvent(string npcId);

        /// <summary>씬 전환 중 이벤트 상호작용만 먼저 종료하고 씬 객체는 유지</summary>
        void BeginSceneRelease();

        /// <summary>현재 마을 씬을 위해 생성한 이벤트 NPC 참조와 상태 정리</summary>
        void ReleaseSceneInstances(bool destroySceneObjects = false);
    }
}
