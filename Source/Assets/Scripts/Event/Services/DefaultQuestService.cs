using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SeaVillage.Event.Services
{
    /// <summary>
    /// 퀘스트의 상태를 추적하고 관리하는 기본 퀘스트 서비스 구현체
    /// </summary>
    public class DefaultQuestService : IQuestService
    {
        private readonly HashSet<string> _completedQuests = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _activeQuestIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _activeQuestOrder = new();

        public IEnumerator StartQuest(string questId)
        {
            string normalized = NormalizeQuestId(questId);
            if (string.IsNullOrEmpty(normalized) || IsQuestCompleted(normalized))
                yield break;

            if (!_activeQuestIds.Add(normalized))
                yield break;

            _activeQuestOrder.Add(normalized);
            Debug.Log($"[QuestService] 퀘스트 시작 - ID: {normalized} (진행 중: {_activeQuestIds.Count})");
            yield return null;
        }

        public void MarkQuestCompleted(string questId)
        {
            string normalized = NormalizeQuestId(questId);
            if (string.IsNullOrEmpty(normalized)) return;

            // 이미 완료된 퀘스트면 완료 연출을 반복하지 않는다
            if (!_completedQuests.Add(normalized)) return;

            _activeQuestIds.Remove(normalized);
            RemoveActiveQuestOrder(normalized);

            Audio.AudioManager.TryPlaySfx(Audio.AudioManager.SfxClear);
            Debug.Log($"[QuestService] 퀘스트 완료 - ID: {normalized} (총 완료: {_completedQuests.Count})");
        }

        public bool IsQuestCompleted(string questId)
        {
            string normalized = NormalizeQuestId(questId);
            return !string.IsNullOrEmpty(normalized) && _completedQuests.Contains(normalized);
        }

        public bool IsQuestActive(string questId)
        {
            string normalized = NormalizeQuestId(questId);
            return !string.IsNullOrEmpty(normalized) && _activeQuestIds.Contains(normalized);
        }

        public IReadOnlyCollection<string> GetActiveQuestIds()
        {
            return new List<string>(_activeQuestOrder);
        }

        public void CopyActiveQuestIdsTo(List<string> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.Clear();
            target.AddRange(_activeQuestOrder);
        }

        public string GetActiveQuestId()
        {
            return _activeQuestOrder.Count > 0 ? _activeQuestOrder[0] : null;
        }

        #region Persistence
        public void CopyCompletedQuestIdsTo(List<string> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.Clear();
            foreach (string questId in _completedQuests)
                target.Add(questId);
        }

        public void LoadCompletedQuests(IEnumerable<string> data)
        {
            _completedQuests.Clear();
            if (data == null) return;
            foreach (string id in data)
            {
                string normalized = NormalizeQuestId(id);
                if (!string.IsNullOrEmpty(normalized))
                    _completedQuests.Add(normalized);
            }
        }

        public void LoadActiveQuest(string questId)
        {
            _activeQuestIds.Clear();
            _activeQuestOrder.Clear();

            string normalized = NormalizeQuestId(questId);
            if (!string.IsNullOrEmpty(normalized) && !_completedQuests.Contains(normalized))
            {
                _activeQuestIds.Add(normalized);
                _activeQuestOrder.Add(normalized);
            }
        }

        public void LoadActiveQuests(IEnumerable<string> questIds)
        {
            _activeQuestIds.Clear();
            _activeQuestOrder.Clear();
            if (questIds == null)
                return;

            foreach (string questId in questIds)
            {
                string normalized = NormalizeQuestId(questId);
                if (string.IsNullOrEmpty(normalized)
                    || _completedQuests.Contains(normalized)
                    || !_activeQuestIds.Add(normalized))
                {
                    continue;
                }

                _activeQuestOrder.Add(normalized);
            }
        }

        private static string NormalizeQuestId(string questId)
            => questId?.Trim() ?? string.Empty;

        private void RemoveActiveQuestOrder(string questId)
        {
            for (int i = _activeQuestOrder.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_activeQuestOrder[i], questId, StringComparison.OrdinalIgnoreCase))
                    _activeQuestOrder.RemoveAt(i);
            }
        }
        #endregion
    }
}
