using System;
using System.Collections.Generic;
using SeaVillage.Core;
using UnityEngine;

namespace SeaVillage.Event
{
    public enum EventNpcInteractionMode
    {
        EventNpc = 0,
        RegularShop = 1,
        SpecialShop = 2
    }

    public enum EventNpcVisualMode
    {
        Dynamic = 0,
        SceneOnly = 1,
        None = 2
    }

    [Serializable]
    public sealed class EventNpcDefinition
    {
        [SerializeField] private string _npcId = string.Empty;
        [SerializeField] private TownKey _town = TownKey.Unknown;
        [SerializeField] private GameObject _prefab;
        [SerializeField] private string _spawnPointId = string.Empty;
        [SerializeField] private EventNpcInteractionMode _interactionMode = EventNpcInteractionMode.EventNpc;
        [SerializeField] private EventNpcVisualMode _visualMode = EventNpcVisualMode.Dynamic;
        [SerializeField] private int _shopId;
        [SerializeField] private SpecialShopType _specialShopType = SpecialShopType.Restaurant;

        public string NpcId => _npcId;
        public TownKey Town => _town;
        public GameObject Prefab => _prefab;
        public string SpawnPointId => _spawnPointId;
        public EventNpcInteractionMode InteractionMode => _interactionMode;
        public EventNpcVisualMode VisualMode => _visualMode;
        public int ShopId => _shopId;
        public SpecialShopType SpecialShopType => _specialShopType;
        public bool UsesShopInteraction => _interactionMode != EventNpcInteractionMode.EventNpc;
        public bool UsesShopVisual => UsesShopInteraction
            && _visualMode == EventNpcVisualMode.SceneOnly;
        public bool SpawnsDynamicVisual => !UsesShopInteraction
            && _visualMode == EventNpcVisualMode.Dynamic;

        public bool HasValidShopBinding
        {
            get
            {
                return _interactionMode == EventNpcInteractionMode.RegularShop
                    ? _shopId > 0
                    : _interactionMode == EventNpcInteractionMode.SpecialShop;
            }
        }
    }

    /// <summary>이벤트 NPC ID와 Unity 프리팹·마을 배치 계약을 연결</summary>
    [CreateAssetMenu(fileName = nameof(EventNpcCatalog), menuName = "SeaVillage/Event NPC Catalog")]
    public sealed class EventNpcCatalog : ScriptableObject
    {
        [SerializeField] private List<EventNpcDefinition> _definitions = new();

        private Dictionary<string, EventNpcDefinition> _definitionsById;

        public IReadOnlyList<EventNpcDefinition> Definitions => _definitions;

        public bool TryGet(string npcId, out EventNpcDefinition definition)
        {
            definition = null;
            EnsureIndex();
            return !string.IsNullOrWhiteSpace(npcId)
                && _definitionsById.TryGetValue(npcId, out definition);
        }

        private void OnEnable()
        {
            _definitionsById = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _definitionsById = null;
        }
#endif

        private void EnsureIndex()
        {
            if (_definitionsById != null)
                return;

            _definitionsById = new Dictionary<string, EventNpcDefinition>(StringComparer.OrdinalIgnoreCase);
            if (_definitions == null)
                return;

            for (int i = 0; i < _definitions.Count; i++)
            {
                EventNpcDefinition definition = _definitions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.NpcId))
                {
                    Debug.LogWarning($"[{nameof(EventNpcCatalog)}] 비어 있는 NPC ID가 있습니다: index {i}", this);
                    continue;
                }

                if (definition.Town == TownKey.Unknown
                    || (!definition.UsesShopInteraction
                        && string.IsNullOrWhiteSpace(definition.SpawnPointId)))
                {
                    Debug.LogWarning($"[{nameof(EventNpcCatalog)}] 정의가 완전하지 않습니다: {definition.NpcId}", this);
                    continue;
                }

                if (definition.UsesShopInteraction && !definition.HasValidShopBinding)
                {
                    Debug.LogWarning($"[{nameof(EventNpcCatalog)}] 상점 상호작용 매핑이 완전하지 않습니다: {definition.NpcId}", this);
                    continue;
                }

                bool requiresPrefab = !definition.UsesShopInteraction || definition.UsesShopVisual;
                if (requiresPrefab && definition.Prefab == null)
                {
                    Debug.LogWarning($"[{nameof(EventNpcCatalog)}] 이벤트 시각화 프리팹이 없습니다: {definition.NpcId}", this);
                    continue;
                }

                if (!_definitionsById.TryAdd(definition.NpcId, definition))
                    Debug.LogError($"[{nameof(EventNpcCatalog)}] NPC ID가 중복됩니다: {definition.NpcId}", this);
            }
        }
    }
}
