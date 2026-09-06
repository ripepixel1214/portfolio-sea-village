using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using SeaVillage.Core;
using SeaVillage.Data;
using SeaVillage.Utilities;
using SeaVillage.Event.Services;
using ExpressionEvaluator = SeaVillage.Utilities.ExpressionEvaluator;

namespace SeaVillage.Event
{
    /// <summary>
    /// 이벤트 시스템 중앙 관리자
    /// </summary>
    public class EventManager : Singleton<EventManager>
    {
        public const string EVENT_CONDITION_AFTERDAY = "AfterDay";
        public const string EVENT_CONDITION_PERCENT = "Percent";
        public const string EVENT_CONDITION_FAVORITE = "Favorite";
        public const string EVENT_CONDITION_LOVELV = "LoveLv";
        public const string EVENT_CONDITION_QUEST = "Quest";

        public const string CONTEXT_KEY_GOLD = "Player_Gold";
        public const string CONTEXT_KEY_BOAT_FOOD = "Boat_Food";
        public const string CONTEXT_KEY_BOAT_LEVEL = "Boat_Level";

        // Trigger Types(Event table)
        public const string EVENT_TRIGGER_ONENTERMAP = "OnEnterMap";
        public const string EVENT_TRIGGER_DAILYRANDOM = "DailyRandom";
        public const string EVENT_TRIGGER_ONTALKNPC = "OnTalkNPC";
        public const string EVENT_TRIGGER_ONITEMOBTAINED = "OnItemObtained";

        private const string OceanSceneName = "Ocean";

        // Dependency Services
        private IStatService _statService;
        private IQuestService _questService;

        private EventRuntimeStore _runtimeStore;
        
        private EventCommandFactory _commandFactory;

        private EventCommandServices _services;

        private BulletinBoardService _bulletinBoardService;

        private readonly HashSet<string> _consumedEvents = new HashSet<string>();

        // 이벤트별 마지막 발동일 기록
        // key: EventID, value: 마지막 발동 시점의 GameDate
        private readonly Dictionary<string, int> _lastTriggerDays = new Dictionary<string, int>();

        // 이벤트 진행에 필요한 런타임 변수 저장소 (조건 평가 시 참조)
        private Dictionary<string, int> _contextVariables;

        private readonly Dictionary<string, EventExecutionContext> _activeContexts = new(StringComparer.OrdinalIgnoreCase);
        private int _nextContextInstanceId;

        // 특정 이벤트 종료 시 호출할 1회성 콜백 (key: EventID)
        private readonly Dictionary<string, Action> _completionCallbacks = new(StringComparer.OrdinalIgnoreCase);

        // DailyRandom 트리거를 외부에서 한시적으로 막는 게이트. true 를 반환하면 그 시점의 DailyRandom 발동을 건너뛴다.
        // null 이면 항상 허용. (예: 항해 식량 고갈 발생일에 보상성 돌발 이벤트가 함께 뜨지 않도록 OceanManager 가 등록)
        private Func<bool> _dailyRandomSuppressor;

        private static readonly HashSet<string> OnEnterMapScenes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Start",
            "Forest",
            "Mine",
            "Sea",
            "Dessert",
            "Cave",
            "Ocean"
        };

        #region Properties
        // 게시판 UI에서 사용하는 이벤트 목록 서비스
        public BulletinBoardService BulletinBoardService => _bulletinBoardService;

        // 현재 이벤트 진행 여부
        public bool IsEventRunning => _activeContexts.Count > 0;
        public bool AreEventsSuppressed => ShouldSuppressEventsForOceanTutorial(GetCurrentSceneName());

        // 이벤트 1건 종료 시 EventID 와 함께 발생
        public event Action<string> OnEventCompleted;
        #endregion

        protected override void Awake()
        {
            base.Awake();
            Initialize();
            SubscribeGameEvents();
        }

        protected override void OnDestroy()
        {
            UnsubscribeGameEvents();
            base.OnDestroy();
        }

        private void Initialize()
        {
            EnsureRuntimeStore();
            _statService ??= new DefaultStatService();
            _questService ??= new DefaultQuestService();
            var uiService = new DefaultUIService();
            var npcInteractionService = new DefaultNpcInteractionService();
            var choiceService = new DefaultChoiceService();
            var evaluator = new ExpressionEvaluator();
            _services = new EventCommandServices(_statService, uiService, _questService, npcInteractionService, choiceService, evaluator);
            _commandFactory = new EventCommandFactory();
            InitializeBulletinBoard();
        }

        #region Board Quest
        /// <summary>
        /// 게시판 서비스 초기화
        /// </summary>
        private void InitializeBulletinBoard()
        {
            _bulletinBoardService = new BulletinBoardService();
        }

        private void SubscribeGameEvents()
        {
            // static 이벤트라 인스턴스 불필요 (.Instance 접근 시 teardown 중 유령 싱글턴 생성됨)
            Core.TimeManager.OnDayChanged += OnGameDateChangedHandler;
        }

        private void UnsubscribeGameEvents()
        {
            // static 이벤트라 인스턴스 불필요 (.Instance 접근 시 teardown 중 유령 싱글턴 생성됨)
            Core.TimeManager.OnDayChanged -= OnGameDateChangedHandler;
        }

        private bool EnsureRuntimeStore()
        {
            if (_runtimeStore != null) return true;

            _runtimeStore = EventRuntimeStore.Build(DataManager.Instance.EventDatabase);

            if (_runtimeStore == null)
            {
                Debug.LogWarning("EventRuntimeStore build failed (database missing)");
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// 이벤트 ID로 이벤트 시작
        /// </summary>
        public void StartEvent(string eventID)
        {
            if (!EnsureRuntimeStore()) return;

            if (!_runtimeStore.TryGetEvent(eventID, out var eventData))
            {
                Debug.LogWarning($"Event not found: {eventID}");
                return;
            }

            EnsureContextVariables();
            TryStartEvent(eventData);
        }

        /// <summary>
        /// 이벤트를 시작하고 종료 시 콜백을 호출한다. 이벤트가 없거나 시작에 실패하면 콜백을 즉시 호출한다.
        /// </summary>
        public void StartEvent(string eventID, Action onCompleted)
        {
            if (onCompleted == null)
            {
                StartEvent(eventID);
                return;
            }

            if (!EnsureRuntimeStore() || !_runtimeStore.TryGetEvent(eventID, out var eventData))
            {
                Debug.LogWarning($"Event not found: {eventID} (완료 콜백 즉시 호출)");
                onCompleted();
                return;
            }

            EnsureContextVariables();
            if (!TryStartEvent(eventData))
            {
                onCompleted();
                return;
            }

            _completionCallbacks[eventID] = onCompleted;
        }

        /// <summary>
        /// 이벤트를 시작하되 EventCondition(AfterDay·Percent 등) 평가를 건너뛸 수 있다(ignoreConditions=true).
        /// 물리 보물 상자처럼 플레이어가 직접 상호작용해 '무조건' 발동시켜야 하는 진입점에서 사용한다.
        /// 종료(또는 시작 실패) 시 onCompleted 를 호출한다.
        /// </summary>
        public void StartEvent(string eventID, Action onCompleted, bool ignoreConditions)
        {
            if (!ignoreConditions)
            {
                StartEvent(eventID, onCompleted);
                return;
            }

            if (!EnsureRuntimeStore() || !_runtimeStore.TryGetEvent(eventID, out var eventData))
            {
                Debug.LogWarning($"Event not found: {eventID} (완료 콜백 즉시 호출)");
                onCompleted?.Invoke();
                return;
            }

            EnsureContextVariables();
            if (!TryStartEvent(eventData, ignoreConditions: true))
            {
                onCompleted?.Invoke();
                return;
            }

            if (onCompleted != null)
                _completionCallbacks[eventID] = onCompleted;
        }

        public bool CanSubmitBoardQuest(BoardData boardData)
        {
            if (boardData == null)
                return false;

            string questKey = BulletinBoardService.GetQuestKey(boardData);
            return !IsQuestCompleted(questKey);
        }

        public bool CompleteBoardQuest(BoardData boardData)
        {
            if (_bulletinBoardService == null || !CanSubmitBoardQuest(boardData))
                return false;

            string questKey = BulletinBoardService.GetQuestKey(boardData);
            if (string.IsNullOrWhiteSpace(questKey))
                return false;

            if (!TownKeyUtility.TryParse(boardData.Town, out TownKey townKey)
                || !TownProgressionManager.HasInstance)
            {
                Debug.LogError($"[EventManager] 게시판 퀘스트의 마을 호감도 대상을 확인할 수 없습니다: {boardData.Town}");
                return false;
            }

            townKey = TownProgressionManager.NormalizeTownKey(townKey);

            if (boardData.Reward > 0 && !Core.CurrencyManager.Instance.TryGrantPlayerReward(boardData.Reward))
                return false;

            TownProgressionManager.Instance.ChangeAffinity(townKey, TownAffinityRules.BoardQuestReward);

            _questService?.MarkQuestCompleted(questKey);
            _bulletinBoardService.OnBoardQuestCompleted(boardData.Town, questKey);
            return true;
        }

        public Dictionary<string, string[]> GetBoardAssignedSlots()
            => _bulletinBoardService?.GetAssignedSlots();

        public void CopyBoardAssignedSlotsTo(Dictionary<string, List<string>> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (_bulletinBoardService == null)
            {
                target.Clear();
                return;
            }

            _bulletinBoardService.CopyAssignedSlotsTo(target);
        }

        public void LoadBoardAssignedSlots(Dictionary<string, string[]> data)
            => _bulletinBoardService?.LoadAssignedSlots(data);

        public void LoadBoardAssignedSlotsFromSaveData(Dictionary<string, List<string>> data)
            => _bulletinBoardService?.LoadAssignedSlotsFromSaveData(data);
        #endregion

        #region Event Runtime
        /// <summary>
        /// 주어진 트리거 타입을 기반으로 조건을 만족하는 이벤트를 검색 후 하나 시작<br/>
        /// 임시: 첫 번째로 조건을 만족하는 이벤트만 실행 (다중 이벤트 진행 금지)
        /// </summary>
        public bool TryTriggerEvents(string triggerType)
        {
            if (!EnsureRuntimeStore()) return false;

            bool startedAny = false;

            // 트리거별 캐시 사용
            foreach (var evt in _runtimeStore.GetEventsByTrigger(triggerType))
            {
                if (evt == null) continue;
                if (!evt.Repeatable && _consumedEvents.Contains(evt.EventID)) continue;
                if (!TryStartEvent(evt)) continue;
                startedAny = true;
            }

            return startedAny;
        }

        #region Trigger Methods
        /// <summary>
        /// 씬 진입 시 발동 가능한 이벤트를 체크한다.
        /// </summary>
        public void OnSceneEnter(string sceneName)
        {
            if (!EnsureRuntimeStore()) return;
            if (string.IsNullOrEmpty(sceneName)) return;

            EnsureContextVariables();

            var townName = NormalizeOnEnterMapSceneName(sceneName);
            if (OnEnterMapScenes.Contains(townName))
                _bulletinBoardService?.RefreshTownSlots(townName);

            if (ShouldSuppressEventsForOceanTutorial(sceneName)) return;
            TryTriggerOnEnterMapEvents(townName, sceneName);
        }

        private bool TryTriggerOnEnterMapEvents(string normalizedSceneName, string rawSceneName)
        {
            if (!EnsureRuntimeStore()) return false;

            bool startedAny = false;
            foreach (var evt in _runtimeStore.GetEventsByTrigger(EVENT_TRIGGER_ONENTERMAP))
            {
                if (evt == null) continue;
                if (!IsOnEnterMapSceneMatch(evt.SceneName, normalizedSceneName, rawSceneName)) continue;
                if (!evt.Repeatable && _consumedEvents.Contains(evt.EventID)) continue;
                if (!TryStartEvent(evt)) continue;
                startedAny = true;
            }

            return startedAny;
        }

        /// <summary>
        /// 씬 이름을 Event.SceneName 기준의 마을 키(Start 등)로 정규화한다.
        /// </summary>
        private static string NormalizeOnEnterMapSceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return string.Empty;

            if (OnEnterMapScenes.Contains(sceneName))
                return sceneName;

            var tokens = sceneName.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (OnEnterMapScenes.Contains(tokens[i]))
                    return tokens[i];

                if (tokens[i].Equals("Town", StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Length)
                    return tokens[i + 1];
            }

            return sceneName;
        }

        /// <summary>
        /// 날짜가 경과할 때 발동 가능한 이벤트를 체크한다.
        /// </summary>
        public void OnDayPassed(int currentDay)
        {
            EnsureContextVariables();

            TryTriggerDailyRandomEvents(GetCurrentSceneName());
        }

        private void OnGameDateChangedHandler(int newDay)
        {
            OnDayPassed(newDay);
        }

        /// <summary>
        /// DailyRandom 이벤트 트리거를 한시적으로 막는 게이트를 설정한다. predicate 가 true 면 그 시점의 DailyRandom 발동을 건너뛴다.
        /// 예: 항해 식량 고갈(난파) 발생일에 보상성 돌발 이벤트가 함께 뜨지 않도록 OceanManager 가 등록한다. 해제는 null 전달.
        /// </summary>
        public void SetDailyRandomSuppressor(Func<bool> suppressor) => _dailyRandomSuppressor = suppressor;

        private bool TryTriggerDailyRandomEvents(string currentSceneName)
        {
            if (!EnsureRuntimeStore()) return false;
            if (string.IsNullOrWhiteSpace(currentSceneName)) return false;
            if (_dailyRandomSuppressor != null && _dailyRandomSuppressor()) return false;

            bool startedAny = false;

            foreach (var evt in _runtimeStore.GetEventsByTrigger(EVENT_TRIGGER_DAILYRANDOM))
            {
                if (evt == null) continue;
                if (!IsDailyRandomSceneMatch(evt.SceneName, currentSceneName)) continue;
                if (!evt.Repeatable && _consumedEvents.Contains(evt.EventID)) continue;

                if (!TryStartEvent(evt)) continue;
                startedAny = true;
            }

            return startedAny;
        }

        private static bool IsDailyRandomSceneMatch(string eventSceneName, string currentSceneName)
        {
            if (string.IsNullOrWhiteSpace(eventSceneName) || string.IsNullOrWhiteSpace(currentSceneName))
                return false;

            return currentSceneName.Equals(eventSceneName, StringComparison.OrdinalIgnoreCase)
                || currentSceneName.Contains(eventSceneName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOnEnterMapSceneMatch(string eventSceneName, string normalizedSceneName, string rawSceneName)
        {
            if (string.IsNullOrWhiteSpace(eventSceneName))
                return false;

            if (!string.IsNullOrWhiteSpace(normalizedSceneName)
                && string.Equals(normalizedSceneName, eventSceneName, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrWhiteSpace(rawSceneName)
                && rawSceneName.Contains(eventSceneName, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetCurrentSceneName()
        {
            if (SceneChanger.HasInstance)
                return SceneChanger.Instance.CurrentSceneName;

            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        private static bool ShouldSuppressEventsForOceanTutorial(string sceneName)
        {
            return string.Equals(sceneName, OceanSceneName, StringComparison.OrdinalIgnoreCase)
                && Core.TutorialManager.HasInstance
                && Core.TutorialManager.Instance.IsInitialized
                && Core.TutorialManager.Instance.IsOceanTutorialInProgress;
        }

        /// <summary>
        /// NPC와 대화 시 발동 가능한 이벤트를 체크한다.
        /// </summary>
        public void OnTalkToNPC(int npcId)
        {
            EnsureContextVariables();

            _contextVariables["NpcId"] = npcId;

            TryTriggerEvents(EVENT_TRIGGER_ONTALKNPC);
        }

        /// <summary>
        /// 특정 아이템 획득 시 발동 가능한 이벤트를 체크한다.
        /// </summary>
        public void OnItemObtained(int itemId, int quantity)
        {
            _contextVariables = new Dictionary<string, int>
            {
                {CONTEXT_KEY_GOLD, GetStat(CONTEXT_KEY_GOLD)},
                {"ItemId", itemId},
                {"Quantity", quantity}
            };
            TryTriggerEvents(EVENT_TRIGGER_ONITEMOBTAINED);
        }
        #endregion

        private bool TryStartEvent(EventData eventData, bool ignoreConditions = false)
        {
            if (eventData == null) return false;
            if (AreEventsSuppressed) return false;

            // 일부 이벤트 차단 (직접 발동 목적)
            if (!ignoreConditions
                && _runtimeStore.TryGetConditions(eventData.EventID, out var conditions) && conditions != null && conditions.Count > 0)
            {
                // 모든 조건이 만족되어야 통과
                foreach (var condition in conditions)
                {
                    if (!EvaluateCondition(condition))
                    {
                        Debug.Log($"Event {eventData.EventID} 조건 미충족: {condition.ConditionType} = {condition.Value} (Variable: {condition.Variable})");
                        return false;
                    }
                }
            }

            // 2. 런타임 이벤트 저장소로부터 시퀀스 로드
            if (!_runtimeStore.TryGetSequences(eventData.EventID, out var sequences) || sequences == null || sequences.Count == 0)
            {
                Debug.LogWarning($"Event sequences of event {eventData.EventID} not found!");
                return false;
            }

            var context = new EventExecutionContext(eventData, sequences);
            context.RuntimeId = CreateRuntimeContextId(eventData.EventID);
            context.State = EventState.Running;
            var firstStep = sequences.Min(x => x.Step);
            context.SetNextStep(firstStep);

            _activeContexts[context.RuntimeId] = context;
            StartCoroutine(RunEvent(context));

            if (!eventData.Repeatable)
                _consumedEvents.Add(eventData.EventID);

            return true;
        }

        /// <summary>
        /// 개별 EventCondition 평가
        /// </summary>
        private bool EvaluateCondition(EventConditionData condition)
        {
            switch (condition.ConditionType)
            {
                // 마지막 발동일로부터 condition.Value일 이상 경과했는지 확인
                case EVENT_CONDITION_AFTERDAY:
                    int currentDay = Core.GameManager.Instance?.GameDate ?? 1;
                    if (!_lastTriggerDays.TryGetValue(condition.EventID, out var lastDay))
                        return true;
                    return (currentDay - lastDay) >= condition.Value;
                // Variable 필드로 특정 마을의 호감도 지정. 구형 접미사 표기(SeaLoveLv)는 저장소 입구에서 LoveLv_Sea 로 흡수된다
                case EVENT_CONDITION_LOVELV:
                    string loveLvKey = string.IsNullOrEmpty(condition.Variable) ? EVENT_CONDITION_LOVELV : condition.Variable;
                    int currentLoveLv = ResolveStatWithContext(loveLvKey);
                    return currentLoveLv >= condition.Value;
                // Customer 테이블의 Favorite 아이템 선호도 체크
                case EVENT_CONDITION_FAVORITE:
                    int currentFavorite = ResolveStatWithContext(EVENT_CONDITION_FAVORITE);
                    return currentFavorite >= condition.Value;
                // 특정 이벤트 완료 조건
                case EVENT_CONDITION_QUEST:
                    string questIdStr = condition.Value.ToString();
                    return _questService?.IsQuestCompleted(questIdStr) ?? false;
                // 단순 확률 조건 (0 ~ 100%)
                case EVENT_CONDITION_PERCENT:
                    return UnityEngine.Random.Range(0, 100) < condition.Value;
                default:
                    Debug.LogWarning($"Unknown condition type: {condition.ConditionType}");
                    return true; // 알 수 없는 조건은 통과 처리
            }
        }

        private IEnumerator RunEvent(EventExecutionContext context)
        {
            while (context != null && context.NextStep != -1)
            {
                int step = context.NextStep;
                context.CurrentStep = step;

                // 동일한 스텝 번호를 가진 시퀀스들을 가져온다
                var steps = context.GetSequencesByStep(step);
                if (steps == null || steps.Count == 0)
                {
                    Debug.LogWarning($"Step {step} not found for event {context.EventData.EventID}");
                    break;
                }

                var command = _commandFactory.Get(steps[0].Command);
                if (command == null)
                {
                    Debug.LogWarning($"Unsupported command: {steps[0].Command}");
                    break;
                }

                yield return command.Execute(context, steps, _services);
            }

            EndEvent(context);
        }

        private void EndEvent(EventExecutionContext context)
        {
            if (context == null) return;

            string eventId = context.EventData.EventID;
            Debug.Log($"이벤트 종료: {eventId}");

            // AfterDay 조건 평가를 위해 마지막 발동일 기록
            _lastTriggerDays[eventId] = Core.GameManager.Instance?.GameDate ?? 1;

            if (!string.IsNullOrWhiteSpace(context.RuntimeId))
                _activeContexts.Remove(context.RuntimeId);

            OnEventCompleted?.Invoke(eventId);

            if (_completionCallbacks.TryGetValue(eventId, out var callback))
            {
                _completionCallbacks.Remove(eventId);
                callback?.Invoke();
            }
        }

        public bool IsEventActive(string eventId)
        {
            return !string.IsNullOrWhiteSpace(eventId)
                && _activeContexts.Values.Any(context => string.Equals(context.EventData.EventID, eventId, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsQuestCompleted(string questId)
        {
            return _questService?.IsQuestCompleted(questId) ?? false;
        }

        private string CreateRuntimeContextId(string eventId)
        {
            _nextContextInstanceId++;
            return $"{eventId}#{_nextContextInstanceId}";
        }

        private void EnsureContextVariables()
        {
            _contextVariables ??= new Dictionary<string, int>();
            _contextVariables[CONTEXT_KEY_GOLD] = GetStat(CONTEXT_KEY_GOLD);
        }
        #endregion

        #region IStatService
        private int ResolveStatWithContext(string key)
        {
            if (_contextVariables != null && _contextVariables.TryGetValue(key, out var ctxVal))
                return ctxVal;
            return GetStat(key);
        }

        public int GetStat(string key) => _statService?.GetStat(key) ?? 0;
        public long GetStatLong(string key) => _statService?.GetStatLong(key) ?? 0L;
        public void ChangeStat(string key, int delta) => _statService?.ChangeStat(key, delta);

        /// <summary>
        /// 내부 DefaultQuestService 접근자
        /// </summary>
        public DefaultQuestService QuestServiceAsDefault => _questService as DefaultQuestService;
        #endregion

        #region Persistence Helpers
        /// <summary>
        /// 마지막 발동일 데이터 반환 (세이브용)
        /// </summary>
        public Dictionary<string, int> GetLastTriggerDays()
            => new Dictionary<string, int>(_lastTriggerDays);

        public void CopyLastTriggerDaysTo(Dictionary<string, int> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.Clear();
            foreach (var kvp in _lastTriggerDays)
                target[kvp.Key] = kvp.Value;
        }

        /// <summary>
        /// 마지막 발동일 데이터 일괄 로드 (로드용)
        /// </summary>
        public void LoadLastTriggerDays(Dictionary<string, int> data)
        {
            _lastTriggerDays.Clear();
            if (data == null) return;
            foreach (var kvp in data)
                _lastTriggerDays[kvp.Key] = kvp.Value;
        }

        /// <summary>
        /// 소비된(비반복) 이벤트 ID 목록 반환 (세이브용)
        /// </summary>
        public HashSet<string> GetConsumedEvents()
            => new HashSet<string>(_consumedEvents);

        public void CopyConsumedEventIdsTo(List<string> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.Clear();
            foreach (string eventId in _consumedEvents)
                target.Add(eventId);
        }

        /// <summary>
        /// 소비된(비반복) 이벤트 ID 목록 일괄 로드 (로드용)
        /// </summary>
        public void LoadConsumedEvents(HashSet<string> data)
        {
            _consumedEvents.Clear();
            if (data == null) return;
            foreach (var id in data)
                _consumedEvents.Add(id);
        }

        public void LoadConsumedEventIds(IEnumerable<string> data)
        {
            _consumedEvents.Clear();
            if (data == null)
                return;

            foreach (string eventId in data)
                _consumedEvents.Add(eventId);
        }
        #endregion

        #region Dialogue Helpers
        /// <summary>
        /// DialogueID로 대화 데이터를 조회한다
        /// </summary>
        public EventDialogueData GetDialogue(int dialogueID)
        {
            if (!EnsureRuntimeStore()) return null;
            
            _runtimeStore.TryGetDialogue(dialogueID, out var dialogue);
            return dialogue;
        }

        /// <summary>
        /// 대화의 Speaker가 비어있는지 확인 (시스템 메시지 여부)
        /// </summary>
        public bool IsSystemMessage(EventDialogueData dialogue)
        {
            return dialogue != null && dialogue.Speaker == 0;
        }
        #endregion

    }
}
