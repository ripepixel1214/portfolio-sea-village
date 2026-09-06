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
        private readonly HashSet<string> _completedQuests = new();
        private string _activeQuestId;

        public IEnumerator StartQuest(string questId)
        {
            _activeQuestId = questId;
            Debug.Log($"[QuestService] 퀘스트 시작 - ID: {questId}");
            yield return null;
        }

        public IEnumerator UpdateQuest(string questId, string data)
        {
            Debug.Log($"[QuestService] 퀘스트 업데이트 - ID: {questId}, Data: {data}");
            yield return null;
        }

        public IEnumerator CompleteQuest(string questId)
        {
            MarkQuestCompleted(questId);
            yield return null;
        }

        public void MarkQuestCompleted(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return;

            // 이미 완료된 퀘스트면 완료 연출을 반복하지 않는다
            if (!_completedQuests.Add(questId)) return;

            if (_activeQuestId == questId)
                _activeQuestId = null;

            Audio.AudioManager.TryPlaySfx(Audio.AudioManager.SfxClear);
            Debug.Log($"[QuestService] 퀘스트 완료 - ID: {questId} (총 완료: {_completedQuests.Count})");
        }

        public bool IsQuestCompleted(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return false;
            return _completedQuests.Contains(questId);
        }

        public string GetActiveQuestId()
        {
            return _activeQuestId;
        }

        #region Persistence
        public HashSet<string> GetCompletedQuests()
            => new HashSet<string>(_completedQuests);

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
            foreach (var id in data)
                _completedQuests.Add(id);
        }
        #endregion
    }
}
