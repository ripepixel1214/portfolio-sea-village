using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        public const string EVENT_CONDITION_CLEAR = "Clear";

        public const string CONTEXT_KEY_GOLD = "Player_Gold";
        public const string CONTEXT_KEY_BOAT_FOOD = "Boat_Food";
        public const string CONTEXT_KEY_BOAT_LEVEL = "Boat_Level";

        // Trigger Types(Event table)
        public const string EVENT_TRIGGER_ONENTERMAP = "OnEnterMap";
        public const string EVENT_TRIGGER_DAILYRANDOM = "DailyRandom";
        public const string EVENT_TRIGGER_ONTALKNPC = "OnTalkNPC";
        public const string EVENT_TRIGGER_ONITEMOBTAINED = "OnItemObtained";

        private const string OceanSceneName = "Ocean";
        private const int ActiveQuestResumeMaxFrames = 120;

        // Dependency Services
        private IStatService _statService;
        private IQuestService _questService;
        private IEventNpcRuntime _eventNpcRuntime;

        private EventRuntimeStore _runtimeStore;
        private SceneChanger _sceneChanger;
        
        private EventCommandFactory _commandFactory;

        private EventCommandServices _services;
        private EventItemReservationLedger _itemReservationLedger;
        private EventUiCoordinator _eventUiCoordinator;

        private BulletinBoardService _bulletinBoardService;

        private readonly HashSet<string> _consumedEvents = new HashSet<string>();

        // 이벤트별 마지막 발동일 기록
        // key: EventID, value: 마지막 발동 시점의 GameDate
        private readonly Dictionary<string, int> _lastTriggerDays = new Dictionary<string, int>();

        // 이벤트 진행에 필요한 런타임 변수 저장소 (조건 평가 시 참조)
        private Dictionary<string, int> _contextVariables;

        private readonly Dictionary<string, EventExecutionContext> _activeContexts = new(StringComparer.OrdinalIgnoreCase);
        private int _nextContextInstanceId;
        private readonly Dictionary<string, Coroutine> _activeQuestResumeRoutines = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EventQuestProgressSaveData> _activeQuestProgress =
            new(StringComparer.OrdinalIgnoreCase);

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
            _eventNpcRuntime?.ReleaseSceneInstances(true);
            UnsubscribeGameEvents();
            base.OnDestroy();
        }

        private void Initialize()
        {
            EnsureRuntimeStore();
            _statService ??= new DefaultStatService();
            _questService ??= new DefaultQuestService();
            var uiService = new DefaultUIService();
            _eventNpcRuntime = new EventNpcRuntime(DataManager.Instance.EventNpcCatalog);
            var choiceService = new DefaultChoiceService();
            var evaluator = new ExpressionEvaluator();
            _itemReservationLedger = new EventItemReservationLedger();
            _eventUiCoordinator = new EventUiCoordinator();
            _services = new EventCommandServices(
                _statService,
                uiService,
                _questService,
                _eventNpcRuntime,
                choiceService,
                evaluator,
                _itemReservationLedger,
                _eventUiCoordinator);
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
            _sceneChanger = SceneChanger.Instance;
            _sceneChanger.OnSceneLoadStarted += HandleSceneLoadStarted;
            _sceneChanger.OnSceneLoadCompleted += HandleSceneLoadCompleted;
        }

        private void UnsubscribeGameEvents()
        {
            // static 이벤트라 인스턴스 불필요 (.Instance 접근 시 teardown 중 유령 싱글턴 생성됨)
            Core.TimeManager.OnDayChanged -= OnGameDateChangedHandler;
            if (_sceneChanger != null)
            {
                _sceneChanger.OnSceneLoadStarted -= HandleSceneLoadStarted;
                _sceneChanger.OnSceneLoadCompleted -= HandleSceneLoadCompleted;
            }
            _sceneChanger = null;
        }

        private void HandleSceneLoadStarted(string sceneName)
        {
            // 씬이 바뀌면 진행·보상 메시지 코루틴과 사라지는 UI의 선택/확인 대기를 함께 취소한다
            // 취소는 완료가 아니므로 귀항 콜백·완료 통지를 호출하지 않고 적용된 보상과 소비 기록은 보존한다.
            StopAllCoroutines();
            _activeQuestResumeRoutines.Clear();
            _eventUiCoordinator?.Reset();
            foreach (EventExecutionContext context in _activeContexts.Values)
            {
                CaptureActiveQuestProgress(context);
                _itemReservationLedger?.Release(context.RuntimeId);
            }
            _activeContexts.Clear();
            _completionCallbacks.Clear();
            _eventNpcRuntime?.BeginSceneRelease();
        }

        private void HandleSceneLoadCompleted(string sceneName)
        {
            _eventNpcRuntime?.ReleaseSceneInstances(false);
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
            string resolvedEventId = eventData.EventID;
            _completionCallbacks[resolvedEventId] = onCompleted;
            if (!TryStartEvent(eventData))
            {
                _completionCallbacks.Remove(resolvedEventId);
                onCompleted();
                return;
            }
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
            string resolvedEventId = eventData.EventID;
            if (onCompleted != null)
                _completionCallbacks[resolvedEventId] = onCompleted;

            if (!TryStartEvent(eventData, ignoreConditions: true))
            {
                _completionCallbacks.Remove(resolvedEventId);
                onCompleted?.Invoke();
                return;
            }
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

        public void LoadBoardAssignedSlotsFromSaveData(Dictionary<string, List<string>> data)
            => _bulletinBoardService?.LoadAssignedSlotsFromSaveData(data);
        #endregion

        #region Event Runtime
        /// <summary>
        /// 주어진 트리거 타입을 기반으로 조건을 만족하는 이벤트를 검색 후 가능한 이벤트를 시작<br/>
        /// 조건을 만족하는 이벤트는 서로 다른 ID라면 동시에 시작할 수 있다
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

            if (ShouldSuppressEventsForOceanTutorial(sceneName)) return;

            if (TownKeyUtility.TryParse(townName, out TownKey currentTown))
            {
                TryResumeActiveQuestEvents(
                    currentTown);
            }

            TryTriggerOnEnterMapEvents(townName, sceneName);
        }

        private bool TryResumeActiveQuestEvents(TownKey currentTown)
        {
            bool resumedAny = false;

            IReadOnlyCollection<string> activeQuestIds = _questService?.GetActiveQuestIds();
            if (activeQuestIds == null || activeQuestIds.Count == 0)
                return false;

            foreach (string questId in activeQuestIds)
            {
                if (!TryGetActiveQuestWithSequences(questId, out EventData eventData)
                    || !string.Equals(eventData.TriggerType, EVENT_TRIGGER_ONENTERMAP, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (TryStartEvent(eventData, ignoreConditions: true, activeEntryTown: currentTown))
                {
                    resumedAny = true;
                    continue;
                }

                bool hasEntry = TryResolveActiveEntry(eventData, currentTown, out _);
                if (!IsEventActive(eventData.EventID) && hasEntry)
                    ScheduleActiveQuestResume(eventData, currentTown);
                if (hasEntry)
                    resumedAny = true;
            }

            return resumedAny;
        }

        private bool TryGetActiveQuestWithSequences(string questId, out EventData eventData)
        {
            // 활성 퀘스트 ID를 기준으로 재개하고 수락 단계의 시퀀스 형태는 제한하지 않는다
            if (string.IsNullOrWhiteSpace(questId)
                || _questService?.IsQuestActive(questId) != true
                || !_runtimeStore.TryGetEvent(questId, out eventData)
                || !_runtimeStore.TryGetSequences(eventData.EventID, out List<EventSequenceData> sequences)
                || sequences == null
                || sequences.Count == 0)
            {
                eventData = null;
                return false;
            }

            return true;
        }

        private bool TryResolveActiveEntry(
            EventData eventData,
            TownKey currentTown,
            out int entryStep)
        {
            entryStep = -1;
            if (eventData == null
                || currentTown == TownKey.Unknown
                || !_runtimeStore.TryGetSequences(eventData.EventID, out List<EventSequenceData> sequences)
                || sequences == null
                || sequences.Count == 0)
            {
                return false;
            }

            return EventSequenceEntryResolver.TryResolveActiveEntry(
                sequences,
                DataManager.Instance.EventNpcCatalog,
                currentTown,
                out entryStep);
        }

        private bool TryGetSavedQuestStep(
            string eventId,
            IReadOnlyList<EventSequenceData> sequences,
            TownKey currentTown,
            out int step)
        {
            step = -1;
            if (string.IsNullOrWhiteSpace(eventId)
                || sequences == null
                || !_activeQuestProgress.TryGetValue(eventId, out EventQuestProgressSaveData progress)
                || progress == null
                || progress.step < 0)
            {
                return false;
            }

            if (currentTown != TownKey.Unknown
                && progress.townKey != (int)currentTown)
            {
                return false;
            }

            if (currentTown == TownKey.Unknown && progress.townKey <= 0)
                return false;

            for (int i = 0; i < sequences.Count; i++)
            {
                if (sequences[i].Step != progress.step)
                    continue;

                step = progress.step;
                return true;
            }

            return false;
        }

        private void SetActiveQuestProgress(string eventId, int step, TownKey town)
        {
            if (string.IsNullOrWhiteSpace(eventId) || step < 0)
                return;

            _activeQuestProgress[eventId] = new EventQuestProgressSaveData
            {
                step = step,
                townKey = (int)town
            };
        }

        private void CaptureActiveQuestProgress(EventExecutionContext context)
        {
            if (context?.EventData == null
                || _questService?.IsQuestActive(context.EventData.EventID) != true)
            {
                return;
            }

            int step = context.GetPersistenceStep();
            if (step < 0)
                return;

            TownKey town = context.CurrentTown;
            if (town == TownKey.Unknown && GameManager.HasInstance)
                town = GameManager.Instance.CurrentTownKey;

            SetActiveQuestProgress(context.EventData.EventID, step, town);
        }

        private void ScheduleActiveQuestResume(EventData eventData, TownKey currentTown)
        {
            if (eventData == null || string.IsNullOrWhiteSpace(eventData.EventID)
                || _activeQuestResumeRoutines.ContainsKey(eventData.EventID))
                return;

            Debug.LogWarning($"[EventManager] 활성 이벤트 퀘스트 재개를 대기합니다: {eventData.EventID}, town={currentTown}");
            _activeQuestResumeRoutines[eventData.EventID] =
                StartCoroutine(ResumeActiveQuestWhenReady(eventData, currentTown));
        }

        private IEnumerator ResumeActiveQuestWhenReady(EventData eventData, TownKey currentTown)
        {
            bool resumed = false;
            try
            {
                for (int frame = 0; frame < ActiveQuestResumeMaxFrames; frame++)
                {
                    if (!TryGetActiveQuestWithSequences(eventData.EventID, out EventData activeQuest))
                        yield break;

                    TownKey resumeTown = currentTown;
                    if (resumeTown == TownKey.Unknown && GameManager.HasInstance)
                        resumeTown = GameManager.Instance.CurrentTownKey;

                    if (resumeTown == TownKey.Unknown)
                    {
                        yield return null;
                        continue;
                    }

                    if (GameManager.HasInstance
                        && GameManager.Instance.CurrentTownKey != TownKey.Unknown
                        && GameManager.Instance.CurrentTownKey != resumeTown)
                    {
                        yield break;
                    }

                    if (IsEventActive(activeQuest.EventID))
                    {
                        resumed = true;
                        yield break;
                    }

                    if (TryStartEvent(activeQuest, ignoreConditions: true, activeEntryTown: resumeTown))
                    {
                        resumed = true;
                        yield break;
                    }

                    yield return null;
                }
            }
            finally
            {
                _activeQuestResumeRoutines.Remove(eventData.EventID);
            }

            if (!resumed)
                Debug.LogError($"[EventManager] 활성 이벤트 퀘스트 재개에 실패했습니다: {eventData.EventID}, town={currentTown}");
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
                if (_questService?.IsQuestActive(evt.EventID) == true) continue;
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
                string token = tokens[i];
                if (OnEnterMapScenes.Contains(token))
                    return token;

                // 실제 마을 씬 이름은 StartTown·ForestTown처럼 Town 접미사를 사용
                // 이벤트 데이터는 Town 접미사 없는 키를 사용하므로 씬 진입 전에 동일한 키로 정규화
                const string townSuffix = "Town";
                if (token.EndsWith(townSuffix, StringComparison.OrdinalIgnoreCase)
                    && token.Length > townSuffix.Length)
                {
                    string townName = token[..^townSuffix.Length];
                    if (OnEnterMapScenes.Contains(townName))
                        return townName;
                }

                if (token.Equals("Town", StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Length)
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

        private bool TryStartEvent(
            EventData eventData,
            bool ignoreConditions = false,
            TownKey activeEntryTown = TownKey.Unknown)
        {
            if (eventData == null) return false;
            if (AreEventsSuppressed) return false;
            if (IsEventActive(eventData.EventID)) return false;

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

            if (_questService?.IsQuestCompleted(eventData.EventID) == true
                && EventSequenceEntryResolver.IsQuestFlow(sequences))
            {
                return false;
            }

            bool isActiveQuest = _questService?.IsQuestActive(eventData.EventID) == true;
            TownKey currentTown = activeEntryTown;
            if (currentTown == TownKey.Unknown && GameManager.HasInstance)
                currentTown = GameManager.Instance.CurrentTownKey;

            int firstStep;
            if (isActiveQuest)
            {
                if (!TryGetSavedQuestStep(eventData.EventID, sequences, currentTown, out firstStep)
                    && !TryResolveActiveEntry(eventData, currentTown, out firstStep))
                {
                    return false;
                }
            }
            else
            {
                firstStep = sequences.Min(sequence => sequence.Step);
            }

            var context = new EventExecutionContext(
                eventData,
                sequences,
                wasQuestActiveAtStart: isActiveQuest);
            context.RuntimeId = CreateRuntimeContextId(eventData.EventID);
            context.State = EventState.Running;
            context.CurrentTown = currentTown;
            context.SetNextStep(firstStep);

            if (isActiveQuest)
                SetActiveQuestProgress(eventData.EventID, firstStep, currentTown);

            _activeContexts[context.RuntimeId] = context;
            StartCoroutine(RunEvent(context));

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
                    if (condition.Value <= 0 || !DataManager.HasInstance)
                        return false;

                    CustomerData customer = DataManager.Instance.CustomerDatabase?.GetCustomer(condition.Value);
                    return customer != null && !string.IsNullOrWhiteSpace(customer.Like);
                // 특정 이벤트 완료 조건
                case EVENT_CONDITION_QUEST:
                    string questIdStr = condition.Value.ToString();
                    return _questService?.IsQuestCompleted(questIdStr) ?? false;
                case EVENT_CONDITION_CLEAR:
                    if (string.IsNullOrWhiteSpace(condition.Variable))
                    {
                        Debug.LogError($"Clear 조건의 선행 이벤트 ID가 비어 있습니다: {condition.EventID}");
                        return false;
                    }
                    return _questService?.IsQuestCompleted(condition.Variable) == true
                        || _consumedEvents.Contains(condition.Variable);
                // 단순 확률 조건 (0 ~ 100%)
                case EVENT_CONDITION_PERCENT:
                    return UnityEngine.Random.Range(0, 100) < condition.Value;
                default:
                    Debug.LogWarning($"Unknown condition type: {condition.ConditionType}");
                    return false;
            }
        }

        private IEnumerator RunEvent(EventExecutionContext context)
        {
            while (context != null && context.NextStep != -1)
            {
                int step = context.NextStep;
                context.CurrentStep = step;
                CaptureActiveQuestProgress(context);

                // 동일한 스텝 번호를 가진 시퀀스들을 가져온다
                var steps = context.GetSequencesByStep(step);
                if (steps == null || steps.Count == 0)
                {
                    Debug.LogWarning($"Step {step} not found for event {context.EventData.EventID}");
                    context.State = EventState.Failed;
                    break;
                }

                var command = _commandFactory.Get(steps[0].Command);
                if (command == null)
                {
                    Debug.LogWarning($"Unsupported command: {steps[0].Command}");
                    context.State = EventState.Failed;
                    break;
                }

                yield return command.Execute(context, steps, _services);
                CaptureActiveQuestProgress(context);
            }

            if (context.State != EventState.Failed)
                context.State = EventState.Completed;

            EndEvent(context);
        }

        private void EndEvent(EventExecutionContext context)
        {
            if (context == null) return;

            string eventId = context.EventData.EventID;
            bool completed = context.State == EventState.Completed;
            Debug.Log(completed ? $"이벤트 종료: {eventId}" : $"이벤트 중단: {eventId}");
            if (!string.IsNullOrWhiteSpace(context.RuntimeId))
                _activeContexts.Remove(context.RuntimeId);
            _eventUiCoordinator?.Release(context.RuntimeId);

            if (!completed)
            {
                // 이미 수락한 퀘스트의 대화가 끝나면 상점 기능으로 복귀한다
                TryOpenShopAfterEvent(context);
                _activeQuestProgress.Remove(eventId);
                _itemReservationLedger?.Release(context.RuntimeId);
                return;
            }

            if (context.ShouldCompleteQuest)
            {
                if (!context.TryCommitPendingItemRemovals(
                        InventoryManager.PlayerInventoryOrNull,
                        _itemReservationLedger))
                {
                    Debug.LogError($"이벤트 제출 아이템 차감에 실패했습니다: {eventId}");
                    _activeQuestProgress.Remove(eventId);
                    _itemReservationLedger?.Release(context.RuntimeId);
                    context.State = EventState.Failed;
                    return;
                }

                _questService?.MarkQuestCompleted(eventId);
                _activeQuestProgress.Remove(eventId);
            }
            else if (_questService?.IsQuestActive(eventId) == true)
            {
                _itemReservationLedger?.Release(context.RuntimeId);
                // 현재 컨텍스트가 끝났으므로 이전 대화·실패 스텝이 아닌 마을 진입 스텝부터 대기
                _activeQuestProgress.Remove(eventId);
                Debug.Log($"이벤트 퀘스트 대기: {eventId}");
                TryOpenShopAfterEvent(context);
                TownKey currentTown = GameManager.HasInstance
                    ? GameManager.Instance.CurrentTownKey
                    : TownKey.Unknown;
                if (!TryStartEvent(context.EventData, ignoreConditions: true, activeEntryTown: currentTown))
                    ScheduleActiveQuestResume(context.EventData, currentTown);
                return;
            }

            _itemReservationLedger?.Release(context.RuntimeId);

            // AfterDay 조건 평가를 위해 마지막 발동일 기록
            _lastTriggerDays[eventId] = Core.GameManager.Instance?.GameDate ?? 1;

            if (!context.EventData.Repeatable)
                _consumedEvents.Add(eventId);

            TryOpenShopAfterEvent(context);

            OnEventCompleted?.Invoke(eventId);

            if (_completionCallbacks.TryGetValue(eventId, out var callback))
            {
                _completionCallbacks.Remove(eventId);
                callback?.Invoke();
            }
        }

        private void TryOpenShopAfterEvent(EventExecutionContext context)
        {
            if (context == null
                || !context.WasQuestActiveAtStart
                || context.ShouldCompleteQuest)
                return;

            _eventNpcRuntime?.TryOpenShopAfterEvent(context.LastInteractionNpcId);
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

        /// <summary>기존 퀘스트 완료 패널에 후속 Calculate 보상 내용을 합친다</summary>
        public string BuildQuestCompletionPanelContent(
            EventExecutionContext context,
            EventSequenceData panelStep,
            EventSequenceData confirmation,
            EventCommandServices services)
        {
            if (context == null || panelStep == null || confirmation == null)
                return panelStep?.Value ?? string.Empty;

            List<EventRewardChange> changes = CollectPendingRewardChanges(context, confirmation.NextStep, services);
            if (changes.Count == 0)
                return panelStep.Value;

            var builder = new StringBuilder("퀘스트 완료! 보상을 획득했습니다.");
            bool hasPositiveReward = false;
            for (int i = 0; i < changes.Count; i++)
            {
                EventRewardChange change = changes[i];
                if (change.Delta <= 0)
                    continue;

                string line = change.IsItem
                    ? FormatItemReward(change.ItemId, change.Delta)
                    : FormatStatReward(context, change.TargetKey, change.Delta);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    builder.Append('\n').Append(line);
                    hasPositiveReward = true;
                }
            }

            return hasPositiveReward ? builder.ToString() : panelStep.Value;
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

        private int GetStat(string key) => _statService?.GetStat(key) ?? 0;

        /// <summary>
        /// 내부 DefaultQuestService 접근자
        /// </summary>
        public DefaultQuestService QuestServiceAsDefault => _questService as DefaultQuestService;
        #endregion

        #region Persistence Helpers
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

        public void CopyActiveQuestProgressTo(Dictionary<string, EventQuestProgressSaveData> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            foreach (EventExecutionContext context in _activeContexts.Values)
                CaptureActiveQuestProgress(context);

            target.Clear();
            if (_questService == null)
                return;

            IReadOnlyCollection<string> activeQuestIds = _questService.GetActiveQuestIds();
            if (activeQuestIds == null)
                return;

            foreach (string eventId in activeQuestIds)
            {
                if (!_activeQuestProgress.TryGetValue(eventId, out EventQuestProgressSaveData progress)
                    || progress == null
                    || progress.step < 0)
                {
                    continue;
                }

                target[eventId] = new EventQuestProgressSaveData
                {
                    step = progress.step,
                    townKey = progress.townKey
                };
            }
        }

        public void LoadActiveQuestProgress(Dictionary<string, EventQuestProgressSaveData> data)
        {
            _activeQuestProgress.Clear();
            if (data == null)
                return;

            foreach (KeyValuePair<string, EventQuestProgressSaveData> entry in data)
            {
                string eventId = entry.Key?.Trim();
                EventQuestProgressSaveData progress = entry.Value;
                if (string.IsNullOrEmpty(eventId)
                    || progress == null
                    || progress.step < 0)
                {
                    continue;
                }

                _activeQuestProgress[eventId] = new EventQuestProgressSaveData
                {
                    step = progress.step,
                    townKey = progress.townKey
                };
            }
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

        #endregion

        #region Quest Reward Helpers
        private static List<EventRewardChange> CollectPendingRewardChanges(
            EventExecutionContext context,
            int firstStep,
            EventCommandServices services)
        {
            var changes = new List<EventRewardChange>();
            if (context == null || firstStep < 0)
                return changes;

            var visitedSteps = new HashSet<int>();
            int step = firstStep;
            while (step >= 0 && visitedSteps.Add(step))
            {
                List<EventSequenceData> sequences = context.GetSequencesByStep(step);
                if (sequences == null || sequences.Count != 1)
                    break;

                EventSequenceData sequence = sequences[0];
                if (string.Equals(sequence.Command, EventCommandFactory.EVENT_COMMAND_CALCULATE, StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(sequence.Target?.Trim(), out int itemId) && itemId > 0)
                    {
                        if (int.TryParse(sequence.Value?.Trim(), out int itemDelta) && itemDelta != 0)
                            AddRewardChange(changes, EventRewardChange.ForItem(itemId, itemDelta));
                    }
                    else if (!string.Equals(sequence.Target, "SubmitItem", StringComparison.OrdinalIgnoreCase)
                        && TryResolveRewardDelta(sequence.Value, services, out int statDelta))
                    {
                        AddRewardChange(changes, EventRewardChange.ForStat(sequence.Target, statDelta));
                    }
                }

                step = sequence.NextStep;
            }

            return changes;
        }

        private static void AddRewardChange(List<EventRewardChange> changes, EventRewardChange newChange)
        {
            if (changes == null || newChange.Delta == 0)
                return;

            for (int i = 0; i < changes.Count; i++)
            {
                EventRewardChange current = changes[i];
                bool sameItem = newChange.IsItem
                    && current.IsItem
                    && current.ItemId == newChange.ItemId;
                bool sameStat = !newChange.IsItem
                    && !current.IsItem
                    && string.Equals(current.TargetKey, newChange.TargetKey, StringComparison.OrdinalIgnoreCase);
                if (!sameItem && !sameStat)
                    continue;

                changes[i] = current.WithDelta(current.Delta + newChange.Delta);
                return;
            }

            changes.Add(newChange);
        }

        private static bool TryResolveRewardDelta(
            string value,
            EventCommandServices services,
            out int delta)
        {
            if (int.TryParse(value, out delta))
                return true;

            if (services?.Evaluator != null
                && services.StatService != null
                && services.Evaluator.TryEvaluateNumber(
                    value,
                    key => services.StatService.GetStatLong(key),
                    out float evaluated))
            {
                delta = Mathf.RoundToInt(evaluated);
                return true;
            }

            delta = 0;
            return false;
        }

        private static string FormatItemReward(int itemId, int delta)
        {
            string itemName = "아이템";
            if (DataManager.HasInstance)
            {
                ItemData item = DataManager.Instance.GetItem(itemId);
                if (item != null)
                {
                    string localizedName = DataManager.Instance.GetScriptTextKR(item.Name);
                    if (!string.IsNullOrWhiteSpace(localizedName))
                        itemName = localizedName.Trim();
                }
            }

            return $"{FormatSigned(delta)} {itemName}";
        }

        private static string FormatStatReward(EventExecutionContext context, string targetKey, int delta)
        {
            string normalizedKey = TownKeyUtility.NormalizeLoveLevelKey(targetKey)?.Trim();
            if (string.Equals(normalizedKey, CONTEXT_KEY_GOLD, StringComparison.OrdinalIgnoreCase))
                return $"{FormatSigned(delta)}G";

            if (string.Equals(normalizedKey, CONTEXT_KEY_BOAT_FOOD, StringComparison.OrdinalIgnoreCase))
                return $"식량 {FormatSigned(delta)}";

            if (string.Equals(normalizedKey, CONTEXT_KEY_BOAT_LEVEL, StringComparison.OrdinalIgnoreCase))
                return $"배 등급 {FormatSigned(delta)}";

            if (string.Equals(normalizedKey, "LoveLv", StringComparison.OrdinalIgnoreCase))
            {
                string townName = context != null && context.CurrentTown != TownKey.Unknown
                    ? TownDisplayNames.GetTownDisplayName(context.CurrentTown)
                    : "현재 마을";
                return $"{townName} 호감도 {FormatSigned(delta)}";
            }

            const string affinityPrefix = "LoveLv_";
            if (normalizedKey.StartsWith(affinityPrefix, StringComparison.OrdinalIgnoreCase)
                && TownKeyUtility.TryParse(normalizedKey.Substring(affinityPrefix.Length), out TownKey townKey))
            {
                return $"{TownDisplayNames.GetTownDisplayName(townKey)} 호감도 {FormatSigned(delta)}";
            }

            if (normalizedKey.StartsWith("Player_", StringComparison.OrdinalIgnoreCase))
            {
                string playerStatName = normalizedKey.Substring("Player_".Length);
                string displayName = playerStatName.Equals("Strength", StringComparison.OrdinalIgnoreCase)
                    || playerStatName.Equals("Str", StringComparison.OrdinalIgnoreCase)
                    ? "힘"
                    : playerStatName.Equals("Agility", StringComparison.OrdinalIgnoreCase)
                        || playerStatName.Equals("Dex", StringComparison.OrdinalIgnoreCase)
                        ? "민첩"
                        : "플레이어 능력치";
                return $"{displayName} {FormatSigned(delta)}";
            }

            return $"기타 보상 {FormatSigned(delta)}";
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? $"+{value}" : value.ToString();
        }
        #endregion

    }
}
