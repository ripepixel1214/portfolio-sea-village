using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaVillage.Event
{
    /// <summary>씬에서 이벤트 NPC의 고정 등장 위치를 제공</summary>
    [DisallowMultipleComponent]
    public sealed class EventNpcSpawnPoint : MonoBehaviour
    {
        private static readonly Dictionary<string, EventNpcSpawnPoint> ActiveById =
            new(StringComparer.OrdinalIgnoreCase);

        [SerializeField] private string _spawnPointId = string.Empty;
        [SerializeField] private GameObject _replacementTarget;
        [Tooltip("씬에 수동 배치한 시각 NPC의 말풍선 기준점")]
        [SerializeField] private Transform _visualAnchor;

        private bool _replacementApplied;
        private bool _replacementOriginalActive;

        public string SpawnPointId => _spawnPointId;
        public GameObject ReplacementTarget => _replacementTarget;
        public Transform VisualAnchor => _visualAnchor;

        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(_spawnPointId))
            {
                Debug.LogError($"[{nameof(EventNpcSpawnPoint)}] {name}: Spawn Point ID가 비어 있습니다", this);
                return;
            }

            if (ActiveById.TryGetValue(_spawnPointId, out EventNpcSpawnPoint existing)
                && existing != null
                && existing != this)
            {
                Debug.LogError($"[{nameof(EventNpcSpawnPoint)}] Spawn Point ID가 중복됩니다: {_spawnPointId}", this);
                return;
            }

            ActiveById[_spawnPointId] = this;
        }

        private void OnDisable()
        {
            RestoreReplacementTarget();

            if (!string.IsNullOrWhiteSpace(_spawnPointId)
                && ActiveById.TryGetValue(_spawnPointId, out EventNpcSpawnPoint existing)
                && existing == this)
            {
                ActiveById.Remove(_spawnPointId);
            }
        }

        /// <summary>이벤트 NPC가 같은 시각 위치를 대체할 때 기존 상점 오브젝트를 숨긴다</summary>
        public void ApplyReplacementTarget()
        {
            if (_replacementApplied || _replacementTarget == null)
                return;

            _replacementOriginalActive = _replacementTarget.activeSelf;
            _replacementTarget.SetActive(false);
            _replacementApplied = true;
        }

        /// <summary>이벤트 NPC 제거 후 대체한 상점 오브젝트의 원래 상태를 복원한다</summary>
        public void RestoreReplacementTarget()
        {
            if (!_replacementApplied)
                return;

            if (_replacementTarget != null)
                _replacementTarget.SetActive(_replacementOriginalActive);

            _replacementApplied = false;
            _replacementOriginalActive = false;
        }

        public static bool TryGetActive(string spawnPointId, out EventNpcSpawnPoint spawnPoint)
        {
            spawnPoint = null;
            if (string.IsNullOrWhiteSpace(spawnPointId)
                || !ActiveById.TryGetValue(spawnPointId, out EventNpcSpawnPoint candidate)
                || candidate == null
                || !candidate.isActiveAndEnabled
                || !candidate.gameObject.scene.IsValid())
            {
                return false;
            }

            spawnPoint = candidate;
            return true;
        }
    }
}
