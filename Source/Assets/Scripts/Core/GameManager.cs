using UnityEngine;
using System;
using System.Collections;
using SeaVillage.Utilities;
using SeaVillage.Data;
using SeaVillage.UI;
using SeaVillage.Audio;
using SeaVillage.Event;

namespace SeaVillage.Core
{
    /// <summary>
    /// 게임의 전체적인 상태와 진행을 관리하는 메인 매니저 클래스
    /// </summary>
    public class GameManager : Singleton<GameManager>
    {
        private const float ManagerInitializationTimeoutSeconds = 10f;

        [Header("Game State")]
        [SerializeField] private bool isPaused = false;
        [SerializeField] private GameState currentGameState = GameState.Loading;

        [Header("Scene Info")]
        [SerializeField] private TownKey currentTownKey = TownKey.Unknown;

        [Header("Time Settings")]
        [SerializeField] private float gameTimeScale = 1.0f;

        // Events
        public static event Action<GameState> OnGameStateChanged; // 게임 상태 변경 이벤트
        public static event Action<bool> OnPauseStateChanged; // 게임 일시정지 상태 변경 이벤트

        // References
        private SceneChanger sceneChanger;
        private Player.Player player;
        private string initializationFailureReason = string.Empty;

        // Properties
        public bool IsAllManagerInitialized { get; private set; } = false;
        public bool HasInitializationFailed => !string.IsNullOrEmpty(initializationFailureReason);
        public string InitializationFailureReason => initializationFailureReason;
        public bool HasPlayer => player != null;
        public bool IsPaused => isPaused;
        public float GameTimeScale => gameTimeScale;
        public int GameDate => TimeManager.Instance.CurrentDay;
        public float DayProgress => TimeManager.Instance.DayProgress;
        public GameState CurrentGameState => currentGameState;
        public Player.Player Player => player;

        // 현재 씬의 총괄 매니저(ISceneRoot). 씬 루트가 Awake에서 RegisterSceneRoot로 등록한다.
        public ISceneRoot CurrentSceneRoot { get; private set; }

        // 씬 이름에서 추출한 현재 마을 식별자
        /// <summary>현재 마을 식별자</summary>
        public TownKey CurrentTownKey => currentTownKey;

        // 현재 마을(town키) 변경 통지. 씬 진입과 하위 마을 전환을 단일 경로로 알림
        /// <summary>현재 마을 변경 알림</summary>
        public event Action<TownKey> OnCurrentTownChanged;

        #region MonoBehaviour
        protected override void Awake()
        {
            base.Awake();

            InitializeGame();
        }
        #endregion

        private void InitializeGame()
        {
            // 시간 스케일 설정
            Time.timeScale = gameTimeScale;

            // Scene 전환 관련 기능 초기화 및 이벤트 구독
            InitializeSceneTransition();

            // 매니저들 초기화 시작
            StartCoroutine(InitializeManagers());
        }



        /// <summary>
        /// Scene 전환 관련 매니저들 초기화 및 이벤트 구독
        /// </summary>
        private void InitializeSceneTransition()
        {
            // SceneChanger 초기화
            sceneChanger = SceneChanger.Instance;

            if (sceneChanger != null)
            {
                string sceneName = sceneChanger.CurrentSceneName;
                ApplyCurrentTown(ResolveSceneTownKey(sceneName));
                SetGameState(ResolveGameState(sceneName));
            }

            // SceneChanger 이벤트 구독
            SubscirbeSceneChangeEvents();

            Debug.Log("Scene transition managers initialized and events subscribed.");
        }

        /// <summary>
        /// 매니저들의 초기화 순서를 보장하는 코루틴
        /// </summary>
        private IEnumerator InitializeManagers()
        {
            IsAllManagerInitialized = false;
            initializationFailureReason = string.Empty;

            GameBootstrapper gb = FindFirstObjectByType<GameBootstrapper>();
            if (gb != null)
            {
                yield return new WaitUntil(() => gb == null || gb.IsGameManagerEnsured);
            }
            else
            {
                Debug.LogWarning("GameBootstrapper 없이 씬을 직접 실행해 매니저 초기화를 계속합니다");
            }

            ManagerInitializationStep[] steps = CreateManagerInitializationSteps();
            for (int i = 0; i < steps.Length; i++)
            {
                yield return StartCoroutine(WaitForManagerInitialization(steps[i]));
                if (HasInitializationFailed)
                    yield break;
            }

            IsAllManagerInitialized = true;
            Debug.Log("All managers initialized successfully!");
        }

        /// <summary>
        /// 특정 매니저가 초기화될 때까지 대기하는 코루틴
        /// </summary>
        private IEnumerator WaitForManagerInitialization(ManagerInitializationStep step)
        {
            float startTime = Time.realtimeSinceStartup;
            bool creationAttempted = false;

            while (Time.realtimeSinceStartup - startTime < ManagerInitializationTimeoutSeconds)
            {
                MonoBehaviour managerInstance = step.FindInstance();

                if (managerInstance != null)
                {
                    bool isReady;
                    try
                    {
                        isReady = step.IsReady(managerInstance);
                    }
                    catch (Exception exception)
                    {
                        FailManagerInitialization(
                            $"{step.ManagerName} 준비 상태 확인 중 오류 발생: {exception.Message}");
                        yield break;
                    }

                    if (isReady)
                        yield break;
                }
                else if (!creationAttempted && step.CreateInstance != null)
                {
                    creationAttempted = true;
                    try
                    {
                        step.CreateInstance();
                    }
                    catch (Exception exception)
                    {
                        FailManagerInitialization(
                            $"{step.ManagerName} 생성 중 오류 발생: {exception.Message}");
                        yield break;
                    }
                }

                yield return null;
            }

            FailManagerInitialization(
                $"{step.ManagerName} 초기화가 {ManagerInitializationTimeoutSeconds:F0}초 안에 완료되지 않았습니다");
        }

        private static ManagerInitializationStep[] CreateManagerInitializationSteps()
        {
            return new[]
            {
                new ManagerInitializationStep(
                    nameof(DataManager),
                    () => FindAnyObjectByType<DataManager>(),
                    () => DataManager.Instance,
                    manager => ((DataManager)manager).IsDataLoaded()),
                new ManagerInitializationStep(
                    nameof(SaveLoadManager),
                    () => FindAnyObjectByType<SaveLoadManager>(),
                    () => SaveLoadManager.Instance),
                new ManagerInitializationStep(
                    nameof(RuntimeItemPriceManager),
                    () => FindAnyObjectByType<RuntimeItemPriceManager>(),
                    () => RuntimeItemPriceManager.Instance),
                new ManagerInitializationStep(
                    nameof(CurrencyManager),
                    () => FindAnyObjectByType<CurrencyManager>(),
                    () => CurrencyManager.Instance),
                new ManagerInitializationStep(
                    nameof(TimeManager),
                    () => FindAnyObjectByType<TimeManager>(),
                    () => TimeManager.Instance,
                    manager => ((TimeManager)manager).IsInitialized),
                new ManagerInitializationStep(
                    nameof(PlayerStatManager),
                    () => FindAnyObjectByType<PlayerStatManager>(),
                    () => PlayerStatManager.Instance,
                    manager => ((PlayerStatManager)manager).IsInitialized),
                new ManagerInitializationStep(
                    nameof(TownProgressionManager),
                    () => FindAnyObjectByType<TownProgressionManager>(),
                    () => TownProgressionManager.Instance,
                    manager => ((TownProgressionManager)manager).IsInitialized),
                new ManagerInitializationStep(
                    nameof(PlayerShopManager),
                    () => FindAnyObjectByType<PlayerShopManager>(),
                    () => PlayerShopManager.Instance,
                    manager => ((PlayerShopManager)manager).IsReady),
                new ManagerInitializationStep(
                    nameof(TutorialManager),
                    () => FindAnyObjectByType<TutorialManager>(),
                    () => TutorialManager.Instance,
                    manager => ((TutorialManager)manager).IsInitialized),
                new ManagerInitializationStep(
                    nameof(UIManager),
                    () => FindAnyObjectByType<UIManager>(),
                    () => UIManager.Instance),
                new ManagerInitializationStep(
                    nameof(AudioManager),
                    () => FindAnyObjectByType<AudioManager>(),
                    () => AudioManager.Instance),
            };
        }

        private void FailManagerInitialization(string reason)
        {
            IsAllManagerInitialized = false;
            initializationFailureReason = reason ?? "알 수 없는 초기화 오류";
            Debug.LogError($"[GameManager] 전체 매니저 초기화 중단: {initializationFailureReason}");
        }

        private sealed class ManagerInitializationStep
        {
            private static readonly Func<MonoBehaviour, bool> Exists = manager => manager != null;

            public ManagerInitializationStep(
                string managerName,
                Func<MonoBehaviour> findInstance,
                Func<MonoBehaviour> createInstance,
                Func<MonoBehaviour, bool> isReady = null)
            {
                ManagerName = managerName;
                FindInstance = findInstance ?? throw new ArgumentNullException(nameof(findInstance));
                CreateInstance = createInstance;
                IsReady = isReady ?? Exists;
            }

            public string ManagerName { get; }
            public Func<MonoBehaviour> FindInstance { get; }
            public Func<MonoBehaviour> CreateInstance { get; }
            public Func<MonoBehaviour, bool> IsReady { get; }
        }

        /// <summary>
        /// 게임 상태 변경
        /// </summary>
        public void SetGameState(GameState newState)
        {
            if (currentGameState != newState)
            {
                GameState previousState = currentGameState;
                currentGameState = newState;

                Debug.Log($"게임 상태 변경: {previousState} -> {newState}");
                OnGameStateChanged?.Invoke(newState);
            }
        }

        /// <summary>
        /// 게임을 일시정지/재개하는 함수
        /// </summary>
        public void SetPauseState(bool pause)
        {
            if (isPaused != pause)
            {
                isPaused = pause;
                Time.timeScale = isPaused ? 0f : gameTimeScale;

                // TimeManager 시간 진행도 일시정지/재개
                if (isPaused)
                    TimeManager.Instance.PauseTimeProgress();
                else
                    TimeManager.Instance.ResumeTimeProgress();

                OnPauseStateChanged?.Invoke(isPaused);
                Debug.Log($"Game {(isPaused ? "Paused" : "Resumed")}");
            }
        }

        #region Date Management
        public void AdvanceDate(int days = 1)
        {
            TimeManager.Instance.AdvanceDay(days);
        }

        public void SetDate(int newGameDate)
        {
            TimeManager.Instance.SetDay(newGameDate);
        }

        public void SetDateSilent(int newGameDate, float dayProgress)
        {
            TimeManager.Instance.SetDaySilent(newGameDate, dayProgress);
        }
        #endregion

        #region Scene Transition
        /// <summary>
        /// 현재 씬의 총괄 매니저(ISceneRoot)를 등록한다. 씬 루트가 자신의 Awake에서 호출하며,
        /// 씬 로딩 시작 시에는 null로 초기화하기 위해 GameManager 내부에서도 호출한다.
        /// SceneChanger가 씬 전환 애니메이션 종료 후 등록된 매니저의 Initialize()를 실행한다.
        /// </summary>
        public void RegisterSceneRoot(ISceneRoot sceneRoot)
        {
            if (sceneRoot != null && CurrentSceneRoot != null)
            {
                Debug.LogWarning($"[GameManager] 같은 씬에 ISceneRoot가 둘 이상입니다. 마지막 등록만 유지됩니다: {sceneRoot}");
            }
            CurrentSceneRoot = sceneRoot;
        }

        /// <summary>
        /// Scene 로딩 시작 시 호출
        /// </summary>
        /// <param name="sceneName">로딩 시작할 Scene 이름</param>
        private void OnSceneLoadStarted(string sceneName)
        {
            UIManager.Instance.OnSceneUnLoaded();
            SetGameState(GameState.Loading);
            RegisterSceneRoot(null);
            Debug.Log($"Scene loading started: {sceneName}");
        }

        /// <summary>
        /// Scene 로딩 진행률 업데이트 시 호출
        /// </summary>
        /// <param name="sceneName">로딩 중인 Scene 이름</param>
        /// <param name="progress">로딩 진행률 (0~1)</param>
        private void OnSceneLoadProgress(string sceneName, float progress)
        {
            // 로딩 진행률에 따른 추가 로직이 필요하면 여기에 추가
            // Debug.Log($"Loading progress: {sceneName} - {progress * 100:F0}%");
        }

        /// <summary>
        /// Scene 로딩 완료 시 호출 <br/>
        /// </summary>
        /// <param name="sceneName">로딩 완료된 Scene 이름</param>
        private void OnSceneLoadCompleted(string sceneName)
        {
            // 씬 활성화 직후 현재 마을 갱신 — 페이드 완료를 기다리지 않고 즉시 반영
            ApplyCurrentTown(ResolveSceneTownKey(sceneName));

            // 씬 진입 시 이벤트 트리거
            EventManager.Instance?.OnSceneEnter(sceneName);
        }

        /// <summary>
        /// Scene 전환 완료 시 호출(페이드 인까지 완료) <br/>
        /// 필요한 씬 초기화 작업은 이곳에서 호출
        /// </summary>
        private void OnSceneTransitionCompleted(string sceneName)
        {
            // 마을 상세는 OnSceneLoadCompleted에서 이미 갱신됨(하위 마을 refinement 보존을 위해 재파싱하지 않음)
            SetGameState(ResolveGameState(sceneName));

            if (sceneChanger.ActiveSceneIndex == 0) { }
            else if (sceneChanger.ActiveSceneIndex > 0)
                FindPlayer();

            UIManager.Instance.OnSceneLoaded();
        }

        /// <summary>
        /// SceneChanger 이벤트 구독 함수
        /// </summary>
        private void SubscirbeSceneChangeEvents()
        {
            if (sceneChanger != null)
            {
                sceneChanger.OnSceneLoadStarted += OnSceneLoadStarted;
                sceneChanger.OnSceneLoadProgress += OnSceneLoadProgress;
                sceneChanger.OnSceneLoadCompleted += OnSceneLoadCompleted;
                sceneChanger.OnSceneTransitionCompleted += OnSceneTransitionCompleted;
            }
        }

        /// <summary>
        /// SceneChanger 이벤트 구독 해제 함수
        /// </summary>
        private void UnsubscribeSceneChangeEvents()
        {
            if (sceneChanger != null)
            {
                sceneChanger.OnSceneLoadStarted -= OnSceneLoadStarted;
                sceneChanger.OnSceneLoadProgress -= OnSceneLoadProgress;
                sceneChanger.OnSceneLoadCompleted -= OnSceneLoadCompleted;
                sceneChanger.OnSceneTransitionCompleted -= OnSceneTransitionCompleted;
            }
        }

        /// <summary>
        /// 씬 이름에서 마을 상세 키를 파생. 마을 씬이 아니면 빈 문자열<br/>
        /// e.g. UI_Town_Start -> Start
        /// </summary>
        private TownKey ResolveSceneTownKey(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return TownKey.Unknown;

            if (sceneName.Contains("Town", StringComparison.OrdinalIgnoreCase))
                return TownKeyUtility.TryParse(ExtractSceneDetail(sceneName, "Town"), out TownKey townKey)
                    ? townKey
                    : TownKey.Unknown;

            return TownKey.Unknown;
        }

        /// <summary>
        /// 씬 이름으로 GameState 판정
        /// </summary>
        private GameState ResolveGameState(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return GameState.MainMenu;

            if (sceneName.Contains("Town", StringComparison.OrdinalIgnoreCase))
                return GameState.Town;

            if (sceneName.Contains("Voyage", StringComparison.OrdinalIgnoreCase) || sceneName.Contains("Ocean", StringComparison.OrdinalIgnoreCase))
                return GameState.Sailing;

            return GameState.MainMenu;
        }

        // 현재 마을(town키) 쓰기 단일 경로. 실제로 바뀔 때만 변경 이벤트 발생
        private void ApplyCurrentTown(TownKey townKey)
        {
            if (currentTownKey == townKey)
                return;

            currentTownKey = townKey;
            OnCurrentTownChanged?.Invoke(currentTownKey);
        }

        /// <summary>
        /// 기준 키워드 뒤 토큰을 상세 정보로 추출
        /// </summary>
        private string ExtractSceneDetail(string sceneName, string keyword)
        {
            if (string.IsNullOrEmpty(sceneName))
                return string.Empty;

            var tokens = sceneName.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (token.Equals(keyword, StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Length)
                    return tokens[i + 1];

                if (token.EndsWith(keyword, StringComparison.OrdinalIgnoreCase) && token.Length > keyword.Length)
                    return token.Substring(0, token.Length - keyword.Length);
            }

            return string.Empty;
        }

        // 같은 씬에 여러 하위 마을이 공존할 때(광산/동굴) 플레이어 위치 기반으로 현재 마을 town키 갱신
        public void SetCurrentTown(TownKey townKey)
        {
            if (townKey == TownKey.Unknown)
                return;

            if (currentTownKey == townKey)
                return;

            Debug.Log($"[GameManager] 현재 마을 변경: {TownKeyUtility.ToStorageKey(currentTownKey)} → {TownKeyUtility.ToStorageKey(townKey)}");
            ApplyCurrentTown(townKey);
        }
        #endregion

        public void QuitGame()
        {
            Debug.Log("Quitting Game...");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            SetPauseState(pauseStatus);
        }

        protected override void OnDestroy()
        {
            UnsubscribeSceneChangeEvents();

            base.OnDestroy();
        }

        #region Utilities
        private void FindPlayer()
        {
            Debug.Log("GameManager: Player 탐색을 시작합니다.");

            player = FindFirstObjectByType<Player.Player>();
            if (player == null)
                Debug.LogWarning("GameManager: Player 오브젝트를 찾을 수 없습니다.");
        }
        #endregion
    }

}

