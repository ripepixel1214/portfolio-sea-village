using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.Event
{
    /// <summary>
    /// 마을 게시판 퀘스트 서비스 <br/>
    /// Board 테이블을 기반으로 마을별 최대 3개 슬롯을 관리한다.
    /// </summary>
    public class BulletinBoardService
    {
        public const int MAX_DISPLAY_SLOTS = 3;

        // 마을 이름 => 전체 마을 퀘스트 풀
        private readonly Dictionary<string, List<BoardData>> _questPool = new();
        private readonly List<string> _persistenceStaleKeys = new();

        // 마을별 슬롯에 배정된 퀘스트 키 (null = 빈 슬롯)
        // key: {TownName}
        // value: 길이 3 배열 (index = 슬롯 번호, value = 퀘스트 키)
        private readonly Dictionary<string, string[]> _activatedQuestKeys = new();

        // 마을별 시작 퀘스트 키 세트
        // Key: {TownName} (e.g. "Start", "Sea")
        // Value: 해당 마을의 시작 퀘스트 키 집합
        private readonly Dictionary<string, HashSet<string>> _initialQuests = new();

        public BulletinBoardService()
        {
            BuildQuestPool();
        }

        private void EnsureQuestPool()
        {
            if (_questPool.Count == 0)
                BuildQuestPool();
        }

        private void BuildQuestPool()
        {
            _questPool.Clear();
            _initialQuests.Clear();

            var boardDatabase = DataManager.Instance?.BoardDatabase;
            if (boardDatabase == null)
            {
                Debug.LogWarning("[BulletinBoardService] BoardDatabase가 null입니다.");
                return;
            }

            foreach (var board in boardDatabase.Boards)
            {
                if (board == null || string.IsNullOrEmpty(board.Town)) continue;

                if (!_questPool.TryGetValue(board.Town, out var pool))
                {
                    pool = new List<BoardData>();
                    _questPool[board.Town] = pool;
                }
                pool.Add(board);

                if (board.StartQuest)
                {
                    if (!_initialQuests.TryGetValue(board.Town, out var initialSet))
                    {
                        initialSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        _initialQuests[board.Town] = initialSet;
                    }

                    initialSet.Add(GetQuestKey(board));
                }
            }

            foreach (var kvp in _questPool)
            {
                kvp.Value.Sort((a, b) =>
                {
                    int startQuestCompare = b.StartQuest.CompareTo(a.StartQuest);
                    if (startQuestCompare != 0)
                        return startQuestCompare;

                    return a.ItemID.CompareTo(b.ItemID);
                });
            }

            int totalQuests = _questPool.Values.Sum(p => p.Count);
            Debug.Log($"[BulletinBoardService] 퀘스트 풀 구축: {_questPool.Count}개 마을, 총 {totalQuests}개 퀘스트");
        }

        /// <summary>
        /// 마을 게시판 슬롯을 현재 시작 가능한 이벤트 기준으로 새로 구성한다.
        /// </summary>
        public void RefreshTownSlots(string townName)
        {
            if (string.IsNullOrEmpty(townName)) return;

            EnsureQuestPool();

            var slotIds = BuildAssignedSlots(townName, excludedQuestKey: null);

            _activatedQuestKeys[townName] = slotIds;
        }

        /// <summary>
        /// 현재 슬롯 배정 상태를 기반으로 표시용 BoardQuestSlot 배열을 반환한다.
        /// </summary>
        public BoardQuestSlot[] GetDisplayedQuests(string townName)
        {
            if (string.IsNullOrEmpty(townName))
                return CreateEmptySlots();

            EnsureTownSlots(townName);

            var slots = CreateEmptySlots();
            var slotIds = _activatedQuestKeys[townName];

            for (int i = 0; i < MAX_DISPLAY_SLOTS; i++)
            {
                if (string.IsNullOrEmpty(slotIds[i])) continue;

                var board = FindBoardByQuestKey(slotIds[i]);
                if (board == null) continue;

                if (IsBoardVisible(board))
                    FillSlot(slots[i], board);
            }

            return slots;
        }

        public BoardQuestSlot GetDisplayedQuest(string townName, int slotIndex)
        {
            if (string.IsNullOrEmpty(townName) || slotIndex < 0 || slotIndex >= MAX_DISPLAY_SLOTS)
                return null;

            var slots = GetDisplayedQuests(townName);
            if (slots == null || slotIndex >= slots.Length)
                return null;

            var slot = slots[slotIndex];
            return slot != null && !slot.IsEmpty ? slot : null;
        }

        public int GetDisplayedQuestCount(string townName)
        {
            if (string.IsNullOrEmpty(townName))
                return 0;

            return GetDisplayedQuests(townName).Count(slot => slot != null && !slot.IsEmpty);
        }

        public bool CanSubmitQuest(string townName, int slotIndex)
        {
            var slot = GetDisplayedQuest(townName, slotIndex);
            return slot != null && slot.Board != null && (!EventManager.HasInstance || !EventManager.Instance.IsQuestCompleted(slot.QuestKey));
        }

        private void FillSlot(BoardQuestSlot slot, BoardData board)
        {
            slot.Board = board;
            slot.QuestKey = GetQuestKey(board);
            slot.State = BoardQuestSlotState.Accepted;
            slot.TitleText = ResolveBoardTitle(board);
            slot.DetailText = $"특징: {board.Description}";
            slot.RewardText = ResolveRewardText(board, slot.QuestKey);
        }

        /// <summary>
        /// 게시판 퀘스트 완료 시 호출된다.<br/>
        /// 완료 후 현재 조건 기준으로 마을 슬롯을 다시 구성한다.
        /// </summary>
        public void OnBoardQuestCompleted(string townName, string completedQuestKey)
        {
            if (string.IsNullOrEmpty(townName) || string.IsNullOrEmpty(completedQuestKey)) return;

            _activatedQuestKeys[townName] = BuildAssignedSlots(townName, completedQuestKey);
            Debug.Log($"[BulletinBoard] {townName} 완료 후 슬롯 재구성: completed={completedQuestKey}");
        }

        /// <summary>
        /// 해당 퀘스트가 마을의 초기 3개 퀘스트에 포함되는지 확인한다.<br/>
        /// 게시판 퀘스트 완료 후 완료 상태를 반영
        /// </summary>
        public bool IsInitialQuest(string townName, string questKey)
        {
            if (string.IsNullOrEmpty(townName) || string.IsNullOrEmpty(questKey)) return false;
            return _initialQuests.TryGetValue(townName, out var set) && set.Contains(questKey);
        }

        // 게시판 퀘스트가 현재 표시 조건을 충족하는지 확인한다. (퀘스트 완료 여부 등)
        private bool IsBoardVisible(BoardData board)
        {
            if (board == null)
                return false;

            string questKey = GetQuestKey(board);
            return !EventManager.HasInstance || !EventManager.Instance.IsQuestCompleted(questKey);
        }

        /// <summary>
        /// 현재 시작 가능한 퀘스트 중에서 아직 슬롯에 배정되지 않은 퀘스트 키를 반환한다. 시작 퀘스트는 항상 슬롯에 우선 배정된다.
        /// </summary>
        private string[] BuildAssignedSlots(string townName, string excludedQuestKey)
        {
            var selectedQuestKeys = new List<string>(MAX_DISPLAY_SLOTS);
            var excludedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(excludedQuestKey))
                excludedKeys.Add(excludedQuestKey);

            foreach (var requiredStartQuestKey in GetPendingStartQuestKeys(townName))
            {
                if (selectedQuestKeys.Count >= MAX_DISPLAY_SLOTS)
                    break;

                if (!excludedKeys.Contains(requiredStartQuestKey))
                    selectedQuestKeys.Add(requiredStartQuestKey);
            }

            if (_activatedQuestKeys.TryGetValue(townName, out var currentSlotIds) && currentSlotIds != null)
            {
                for (int i = 0; i < currentSlotIds.Length && selectedQuestKeys.Count < MAX_DISPLAY_SLOTS; i++)
                {
                    string questKey = currentSlotIds[i];
                    if (string.IsNullOrWhiteSpace(questKey))
                        continue;
                    if (excludedKeys.Contains(questKey) || selectedQuestKeys.Contains(questKey, StringComparer.OrdinalIgnoreCase))
                        continue;

                    var board = FindBoardByQuestKey(questKey);
                    if (board != null && string.Equals(board.Town, townName, StringComparison.OrdinalIgnoreCase) && IsBoardVisible(board))
                        selectedQuestKeys.Add(questKey);
                }
            }

            foreach (var randomQuestKey in GetRandomInactiveQuestKeys(townName, selectedQuestKeys, excludedKeys))
            {
                if (selectedQuestKeys.Count >= MAX_DISPLAY_SLOTS)
                    break;

                selectedQuestKeys.Add(randomQuestKey);
            }

            var slotIds = new string[MAX_DISPLAY_SLOTS];
            for (int i = 0; i < selectedQuestKeys.Count && i < MAX_DISPLAY_SLOTS; i++)
                slotIds[i] = selectedQuestKeys[i];

            return slotIds;
        }

        private IEnumerable<string> GetPendingStartQuestKeys(string townName)
        {
            if (!_questPool.TryGetValue(townName, out var pool) || pool == null)
                return Enumerable.Empty<string>();

            return pool
                .Where(board => board != null && board.StartQuest && IsBoardVisible(board))
                .OrderBy(board => board.ItemID)
                .Select(GetQuestKey);
        }

        private IEnumerable<string> GetRandomInactiveQuestKeys(string townName, IEnumerable<string> activeQuestKeys, HashSet<string> excludedKeys)
        {
            // 먼저 활성 퀘스트 키와 제외 키를 HashSet으로 만들어 빠른 조회가 가능하도록 한다. (대소문자 구분 X)
            if (!_questPool.TryGetValue(townName, out var pool) || pool == null)
                return Enumerable.Empty<string>();

            var activeKeySet = new HashSet<string>(activeQuestKeys ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            
            // 활성 퀘스트에 포함되지 않고, 제외 목록에도 없는 퀘스트 키를 후보로 선정한다. 시작 퀘스트는 이 단계에서 제외된다.
            var candidateKeys = pool
                .Where(IsBoardVisible)
                .Select(GetQuestKey)
                .Where(questKey => !string.IsNullOrWhiteSpace(questKey))
                .Where(questKey => !activeKeySet.Contains(questKey))
                .Where(questKey => excludedKeys == null || !excludedKeys.Contains(questKey))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Shuffle(candidateKeys);
            return candidateKeys;
        }

        private static void Shuffle(List<string> values)
        {
            if (values == null || values.Count <= 1)
                return;

            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }
        }

        private void EnsureTownSlots(string townName)
        {
            if (string.IsNullOrEmpty(townName))
                return;

            _activatedQuestKeys[townName] = BuildAssignedSlots(townName, excludedQuestKey: null);
        }

        private BoardData FindBoardByQuestKey(string questKey)
        {
            if (string.IsNullOrWhiteSpace(questKey))
                return null;

            foreach (var pool in _questPool.Values)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    var board = pool[i];
                    if (string.Equals(GetQuestKey(board), questKey, StringComparison.OrdinalIgnoreCase))
                        return board;
                }
            }

            return null;
        }

        private string ResolveRewardText(BoardData board, string questKey)
        {
            if (board == null || board.Reward <= 0)
                return "";

            string rewardText = $"+ {board.Reward} G";
            if (IsInitialQuest(board.Town, questKey))
                rewardText += ", <sprite name=heart>";

            return rewardText;
        }

        private static string ResolveBoardTitle(BoardData board)
        {
            if (board == null)
                return "";

            var item = DataManager.Instance?.GetItem(board.ItemID);
            if (item != null)
            {
                string itemName = DataManager.Instance.GetScriptTextKR(item.Name);
                if (!string.IsNullOrWhiteSpace(itemName))
                    return itemName;
            }

            return $"Item {board.ItemID}";
        }

        public static string GetQuestKey(BoardData board)
        {
            if (board == null)
                return string.Empty;

            return $"Quest_{board.Town}_{board.ItemID}";
        }

        private static BoardQuestSlot[] CreateEmptySlots()
        {
            var slots = new BoardQuestSlot[MAX_DISPLAY_SLOTS];
            for (int i = 0; i < MAX_DISPLAY_SLOTS; i++)
                slots[i] = new BoardQuestSlot();
            return slots;
        }

        #region Persistence
        /// <summary>
        /// 슬롯 배정 데이터 반환
        /// </summary>
        public Dictionary<string, string[]> GetAssignedSlots()
            => _activatedQuestKeys.ToDictionary(kvp => kvp.Key, kvp => (string[])kvp.Value.Clone());

        public void CopyAssignedSlotsTo(Dictionary<string, List<string>> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            _persistenceStaleKeys.Clear();
            foreach (string key in target.Keys)
            {
                if (!_activatedQuestKeys.ContainsKey(key))
                    _persistenceStaleKeys.Add(key);
            }

            for (int i = 0; i < _persistenceStaleKeys.Count; i++)
                target.Remove(_persistenceStaleKeys[i]);

            foreach (var kvp in _activatedQuestKeys)
            {
                if (!target.TryGetValue(kvp.Key, out List<string> savedSlots) || savedSlots == null)
                {
                    savedSlots = new List<string>(kvp.Value.Length);
                    target[kvp.Key] = savedSlots;
                }

                savedSlots.Clear();
                for (int i = 0; i < kvp.Value.Length; i++)
                    savedSlots.Add(kvp.Value[i]);
            }
        }

        /// <summary>
        /// 슬롯 배정 데이터 일괄 로드
        /// </summary>
        public void LoadAssignedSlots(Dictionary<string, string[]> data)
        {
            _activatedQuestKeys.Clear();
            if (data == null) return;
            foreach (var kvp in data)
                _activatedQuestKeys[kvp.Key] = (string[])kvp.Value.Clone();
        }

        public void LoadAssignedSlotsFromSaveData(Dictionary<string, List<string>> data)
        {
            _persistenceStaleKeys.Clear();
            foreach (string key in _activatedQuestKeys.Keys)
            {
                if (data == null || !data.ContainsKey(key))
                    _persistenceStaleKeys.Add(key);
            }

            for (int i = 0; i < _persistenceStaleKeys.Count; i++)
                _activatedQuestKeys.Remove(_persistenceStaleKeys[i]);

            if (data == null)
                return;

            foreach (var kvp in data)
            {
                int slotCount = kvp.Value?.Count ?? MAX_DISPLAY_SLOTS;
                if (!_activatedQuestKeys.TryGetValue(kvp.Key, out string[] slots)
                    || slots == null
                    || slots.Length != slotCount)
                {
                    slots = new string[slotCount];
                    _activatedQuestKeys[kvp.Key] = slots;
                }

                if (kvp.Value == null)
                {
                    Array.Clear(slots, 0, slots.Length);
                    continue;
                }

                for (int i = 0; i < slotCount; i++)
                    slots[i] = kvp.Value[i];
            }
        }

        #endregion
    }
}
