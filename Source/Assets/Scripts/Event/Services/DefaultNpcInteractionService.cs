using System.Collections;
using UnityEngine;

namespace SeaVillage.Event.Services
{
    public class DefaultNpcInteractionService : INpcInteractionService
    {
        public IEnumerator StartInteraction(string npcId)
        {
            Debug.Log($"[NpcInteractionService] StartInteraction - NPC: {npcId}");
            // TODO: NPCManager와 연동하여 NPC 상호작용 시작
            yield return null;
        }

        public IEnumerator EndInteraction(string npcId)
        {
            Debug.Log($"[NpcInteractionService] EndInteraction - NPC: {npcId}");
            // TODO: NPCManager와 연동하여 NPC 상호작용 종료
            yield return null;
        }
    }
}
