using System;
using UnityEngine;

namespace SeaVillage.Event
{
    /// <summary>상점 프리팹 내부에서 이벤트 NPC가 등장할 위치를 제공</summary>
    [DisallowMultipleComponent]
    public sealed class EventNpcShopSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string _npcId = string.Empty;
        [Tooltip("이벤트 시 상점 밖으로 이동시킬 상점 내부 NPC 루트")]
        [SerializeField] private Transform _visual;

        public string NpcId => _npcId;
        public Transform Visual => _visual;

        public bool Matches(string npcId)
        {
            return !string.IsNullOrWhiteSpace(npcId)
                && string.Equals(_npcId, npcId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
