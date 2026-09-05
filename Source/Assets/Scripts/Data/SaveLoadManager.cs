using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using MemoryPack;
using SeaVillage.Utilities;

namespace SeaVillage.Data
{
    /// <summary>
    /// 게임 데이터 저장 및 로드를 관리하는 매니저
    /// </summary>
    public class SaveLoadManager : Singleton<SaveLoadManager>
    {
        // MemoryPack 바이너리 파일로 저장
        private const string SaveFileNameFormat = "SeaVillage_SaveData_{0}.mp";
        private const string TemporaryFileSuffix = ".tmp";
        private const string BackupFileSuffix = ".bak";
        private const int MaxSlots = 2;
        private const float DeferredApplyTimeoutSeconds = 5f;
        private const string DefaultResumeScene = "StartTown";
        private const int SerializationBufferInitialCapacity = 128 * 1024;
        private const int MaxRetainedSerializationBufferCapacity = 4 * 1024 * 1024;
        private const int FileStreamBufferSize = 4096;

        [Header("Save Settings")]
        [SerializeField]
        private bool _enableAutoSave = false;

        // 단위: s, 기본값: 5분
        [SerializeField]
        private float _autoSaveInterval = 300f;
        private float _autoSaveTimer;

        #region Runtime State
        private SaveData _currentSaveData;
        private ArrayBufferWriter<byte> _serializationBuffer =
            new ArrayBufferWriter<byte>(SerializationBufferInitialCapacity);
        private readonly List<PlayerShopData> _pendingPlayerShops = new List<PlayerShopData>();
        private readonly List<StaffData> _pendingHiredStaff = new List<StaffData>();
        private readonly List<Core.InventoryItem> _pendingPlayerInventoryItems = new List<Core.InventoryItem>();
        private Vector2? _pendingPlayerPosition;
        private readonly List<Core.InventoryItem> _pendingShipInventoryItems = new List<Core.InventoryItem>();
        private readonly List<string> _pendingCompletedTutorialIds = new List<string>();
        private int _pendingTutorialForcedFoodPriceTargetDay;
        private bool _pendingTutorialRewardGranted;
        private TutorialProgressSaveData _pendingTutorialProgress = new TutorialProgressSaveData();
        private FirstWreckRecoverySaveData _pendingFirstWreckRecovery = new FirstWreckRecoverySaveData();
        private readonly PlayerStatSaveData _pendingPlayerStatData = new PlayerStatSaveData();
        private readonly TownProgressionSaveData _pendingTownProgressionData = new TownProgressionSaveData();
        private float _pendingShipFoodStorage;
        private int _pendingShipLevel;
        private float _pendingShipBonusCapacity;
        private SceneChanger _sceneChanger;
        private readonly SemaphoreSlim _saveLoadMutex = new SemaphoreSlim(1, 1);
        private Coroutine _inventoryApplyRoutine;
        private Coroutine _playerShopApplyRoutine;
        private Coroutine _tutorialApplyRoutine;
        private Coroutine _progressionApplyRoutine;
        private bool _inventoryEventsBound;
        private bool _playerShopEventsBound;
        private bool _hasPendingPlayerShopData;
        private bool _hasPendingPlayerInventoryItems;
        private bool _hasPendingShipInventoryItems;
        private bool _hasPendingShipState;
        private bool _hasPendingTutorialData;
        private bool _hasPendingPlayerStatData;
        private bool _hasPendingTownProgressionData;
        #endregion

        // Events
        public static event Action OnSaveStarted;
        public static event Action OnSaveCompleted;
        public static event Action OnLoadStarted;
        public static event Action OnLoadCompleted;
        public static event Action<string> OnSaveError;
        public static event Action<string> OnLoadError;

        // Properties
        public int CurrentSlot { get; private set; } = 0;

        public bool HasSaveFile(int slot) => File.Exists(GetSaveFilePath(slot));
        public int GetMaxSlots() => MaxSlots;
        public SaveData CurrentGameData => _currentSaveData;

        #region MonoBehaviour
        protected override void Awake()
        {
            base.Awake();
            Initialize();
        }

        private void OnEnable()
        {
            BindSceneChangerEvents();
            SceneManager.sceneLoaded += OnSceneLoaded;
            BindManagerEvents();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindManagerEvents();
            UnbindSceneChangerEvents();
            StopDeferredApplyRoutines();
        }

        private void Update()
        {
            if (_enableAutoSave && _autoSaveInterval > 0)
            {
                _autoSaveTimer += Time.deltaTime;
                if (_autoSaveTimer >= _autoSaveInterval)
                {
                    _ = AutoSaveAsync();
                    _autoSaveTimer = 0f;
                }
            }
        }
        #endregion

        #region Scene Change Handling
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindSceneChangerEvents();
            BindManagerEvents();
            StartDeferredApplyRoutines();
        }

        private void BindSceneChangerEvents()
        {
            if (_sceneChanger != null)
                return;

            _sceneChanger = FindAnyObjectByType<SceneChanger>();
            if (_sceneChanger == null)
                return;

            _sceneChanger.OnSceneLoadCompleted += HandleSceneLoaded;
            _sceneChanger.OnSceneTransitionCompleted += HandleSceneLoaded;
        }

        private void UnbindSceneChangerEvents()
        {
            if (_sceneChanger == null)
                return;

            _sceneChanger.OnSceneLoadCompleted -= HandleSceneLoaded;
            _sceneChanger.OnSceneTransitionCompleted -= HandleSceneLoaded;
            _sceneChanger = null;
        }

        // InventoryManager와 PlayerShopManager의 준비 완료 이벤트를 구독하여, 로드 시점에 준비되지 않은 데이터를 지연 적용할 수 있도록 한다.
        private void BindManagerEvents()
        {
            if (!_inventoryEventsBound && Core.InventoryManager.HasInstance)
            {
                var inventoryManager = Core.InventoryManager.Instance;
                inventoryManager.OnPlayerInventoryReady += HandleInventoryReady;
                inventoryManager.OnShipInventoryReady += HandleInventoryReady;
                inventoryManager.OnInventoriesReady += HandleInventoryReady;
                _inventoryEventsBound = true;
            }

            if (!_playerShopEventsBound && Core.PlayerShopManager.HasInstance)
            {
                Core.PlayerShopManager.Instance.OnReady += HandlePlayerShopReady;
                _playerShopEventsBound = true;
            }
        }

        // OnDisable에서도 호출되므로 HasInstance로 확인
        private void UnbindManagerEvents()
        {
            if (_inventoryEventsBound && Core.InventoryManager.HasInstance)
            {
                var inventoryManager = Core.InventoryManager.Instance;
                inventoryManager.OnPlayerInventoryReady -= HandleInventoryReady;
                inventoryManager.OnShipInventoryReady -= HandleInventoryReady;
                inventoryManager.OnInventoriesReady -= HandleInventoryReady;
            }
            _inventoryEventsBound = false;

            if (_playerShopEventsBound && Core.PlayerShopManager.HasInstance)
                Core.PlayerShopManager.Instance.OnReady -= HandlePlayerShopReady;

            _playerShopEventsBound = false;
        }

        #endregion

        #region Exclusivity
        private void Initialize()
        {
            _currentSaveData = new SaveData();
        }

        /// <summary>
        /// Save/Load 작업이 동시에 실행되지 않도록 보장하는 헬퍼 메서드
        /// </summary>
        private async Task RunExclusiveAsync(string operationName, Func<Task> operation)
        {
            if (_saveLoadMutex.CurrentCount == 0)
                Debug.Log($"[Save Load Manager] {operationName} waiting for ongoing save/load.");

            await _saveLoadMutex.WaitAsync();
            try
            {
                await operation();
            }
            finally
            {
                ExitSaveLoad();
            }
        }

        private async Task<T> RunExclusiveAsync<T>(string operationName, Func<Task<T>> operation)
        {
            if (_saveLoadMutex.CurrentCount == 0)
                Debug.Log($"[Save Load Manager] {operationName} waiting for ongoing save/load.");

            await _saveLoadMutex.WaitAsync();
            try
            {
                return await operation();
            }
            finally
            {
                ExitSaveLoad();
            }
        }

        private void ExitSaveLoad()
        {
            _saveLoadMutex.Release();
        }

        #endregion

        #region Public Save/Load API
        /// <summary>
        /// 게임 데이터를 지정된 슬롯에 비동기 저장
        /// </summary>
        /// <param name="slot">저장할 슬롯 번호</param>
        public async Task<bool> SaveGameAsync(int slot)
        {
            if (!IsValidSlot(slot))
            {
                Debug.LogError($"[Save Load Manager] Invalid slot number: {slot}. Must be between 0 and {MaxSlots - 1}");
                return false;
            }

            return await RunExclusiveAsync("Save", () => SaveGameInternalAsync(slot));
        }

        private async Task<bool> SaveGameInternalAsync(int slot, bool collectRuntimeData = true)
        {
            try
            {
                OnSaveStarted?.Invoke();
                if (collectRuntimeData)
                {
                    await EnsureInventoryReadyForSaveAsync();
                    CollectGameData();
                }

                ReadOnlyMemory<byte> data = SerializeSaveData(_currentSaveData);
                string filePath = GetSaveFilePath(slot);

                // 임시 파일 검증 후 기존 세이브를 원자적으로 교체
                await WriteSaveFileAtomicallyAsync(filePath, data);

                CurrentSlot = slot;
                Debug.Log($"[Save Load Manager] Game saved successfully to: {filePath}");
                OnSaveCompleted?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                string errorMsg = $"[Save Load Manager] Failed to save game: {e.Message}";
                Debug.LogError(errorMsg);
                OnSaveError?.Invoke(errorMsg);
                return false;
            }
            finally
            {
                ResetSerializationBuffer();
            }
        }

        /// <summary>
        /// 동기식 저장
        /// </summary>
        /// <param name="slot">저장할 슬롯 번호 (0 또는 1)</param>
        public void SaveGame(int slot)
        {
            _ = SaveGameAsync(slot);
        }

        /// <summary>
        /// 지정된 슬롯에서 게임 데이터를 로드
        /// </summary>
        /// <param name="slot">로드할 슬롯 번호</param>
        public async Task<bool> LoadGameAsync(int slot)
        {
            if (!IsValidSlot(slot))
            {
                Debug.LogError($"[Save Load Manager] Invalid slot number: {slot}. Must be between 0 and {MaxSlots - 1}");
                return false;
            }

            return await RunExclusiveAsync("Load", () => LoadGameInternalAsync(slot));
        }

        private async Task<bool> LoadGameInternalAsync(int slot)
        {
            try
            {
                OnLoadStarted?.Invoke();

                string filePath = GetSaveFilePath(slot);
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning("[Save Load Manager] Save file not found.");
                    return false;
                }

                SaveData loadedSaveData = await DeserializeSaveFileAsync(filePath);

                if (loadedSaveData == null)
                {
                    Debug.LogError("[Save Load Manager] Failed to parse save data.");
                    return false;
                }

                SaveData previousSaveData = _currentSaveData;
                int previousSlot = CurrentSlot;
                _currentSaveData = loadedSaveData;

                try
                {
                    ApplyGameData();
                }
                catch
                {
                    _currentSaveData = previousSaveData;
                    CurrentSlot = previousSlot;
                    throw;
                }

                CurrentSlot = slot;
                Debug.Log($"[Save Load Manager] Game loaded successfully from: {filePath}");
                OnLoadCompleted?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                string errorMsg = $"[Save Load Manager] Failed to load game: {e.Message}";
                Debug.LogError(errorMsg);
                OnLoadError?.Invoke(errorMsg);
                return false;
            }
        }

        /// <summary>
        /// 새 게임 시작 시 기본 데이터 생성
        /// </summary>
        /// <param name="slot">저장할 슬롯 번호</param>
        public async Task<bool> CreateNewGameAsync(
            int slot,
            Core.PlayerGender playerGender = Core.PlayerGender.Male)
        {
            if (!IsValidSlot(slot))
            {
                Debug.LogError($"[Save Load Manager] Invalid slot number: {slot}. Must be between 0 and {MaxSlots - 1}");
                return false;
            }

            if (!Core.PlayerGenderPolicy.IsValid(playerGender))
            {
                Debug.LogError($"[Save Load Manager] Invalid player gender: {playerGender}");
                return false;
            }

            return await RunExclusiveAsync("CreateNewGame", async () =>
            {
                CreateDefaultGameData(playerGender);
                ApplyGameData();
                bool saved = await SaveGameInternalAsync(slot, collectRuntimeData: false);
                if (!saved)
                    return false;

                Debug.Log($"[Save Load Manager] New game created and saved to slot {slot}");
                return true;
            });
        }

        public void CreateNewGame(int slot)
        {
            _ = CreateNewGameAsync(slot);
        }

        /// <summary>
        /// 로드한 데이터가 지정하는 복귀 씬 이름. 저장된 씬을 쓸 수 없으면 기본 씬을 반환한다
        /// </summary>
        public string GetResumeSceneName()
        {
            string sceneName = _currentSaveData != null ? _currentSaveData.currentSceneName : string.Empty;
            if (string.IsNullOrWhiteSpace(sceneName))
                return DefaultResumeScene;

            sceneName = sceneName.Trim();
            if (SceneChanger.Instance.IsSceneInBuildSettings(sceneName))
                return sceneName;

            Debug.LogWarning($"[Save Load Manager] Saved scene '{sceneName}' is not in build settings. Falling back to '{DefaultResumeScene}'.");
            return DefaultResumeScene;
        }

        #endregion

        #region Save/Load Internals
        /// <summary>
        /// 현재 슬롯에 자동 저장
        /// </summary>
        private async Task AutoSaveAsync()
        {
            if (_currentSaveData != null)
            {
                bool saved = await SaveGameAsync(CurrentSlot);
                if (saved)
                    Debug.Log($"[Save Load Manager] Auto-save completed to slot {CurrentSlot}");
            }
        }

        /// <summary>
        /// 현재 게임 상태를 GameData에 수집
        /// </summary>
        private void CollectGameData()
        {
            if (_currentSaveData == null)
                _currentSaveData = new SaveData();

            // 기본 정보
            _currentSaveData.saveDate = DateTime.Now.ToString();
            _currentSaveData.gameVersion = Application.version;

            // GameManager에서 데이터 수집
            if (Core.GameManager.HasInstance)
            {
                _currentSaveData.gameDate = Core.GameManager.Instance.GameDate;
                _currentSaveData.dayProgress = Core.GameManager.Instance.DayProgress;
                _currentSaveData.gameState = Core.GameManager.Instance.CurrentGameState.ToString();
            }

            long currentGold = Core.CurrencyManager.Instance.GetPlayerBalance(CurrencyType.Gold);
            _currentSaveData.gold = Math.Max(0L, currentGold);

            CollectInventoryData();
            CollectItemPriceData();
            CollectShopStockData();
            CollectQuestData();
            CollectPlayerShopData();
            CollectOceanFogData();
            CollectVoyageData();
            CollectSceneData();
            CollectPlayerPositionData();
            CollectTutorialData();
            CollectProgressionData();
        }

        private void CollectProgressionData()
        {
            if (Core.PlayerStatManager.HasInstance && Core.PlayerStatManager.Instance.IsInitialized)
            {
                _currentSaveData.playerStats ??= new PlayerStatSaveData();
                Core.PlayerStatManager.Instance.CopySaveDataTo(_currentSaveData.playerStats);
            }
            else
                _currentSaveData.playerStats ??= new PlayerStatSaveData();

            if (Core.TownProgressionManager.HasInstance && Core.TownProgressionManager.Instance.IsInitialized)
            {
                _currentSaveData.townProgression ??= new TownProgressionSaveData();
                Core.TownProgressionManager.Instance.CopySaveDataTo(_currentSaveData.townProgression);
            }
            else
                _currentSaveData.townProgression ??= new TownProgressionSaveData();
        }

        private void CollectTutorialData()
        {
            if (Core.TutorialManager.HasInstance && Core.TutorialManager.Instance.IsInitialized)
            {
                _currentSaveData.completedTutorialIds ??= new List<string>();
                Core.TutorialManager.Instance.CopyCompletedTutorialIdsTo(_currentSaveData.completedTutorialIds);
                _currentSaveData.tutorialForcedFoodPriceTargetDay =
                    Core.TutorialManager.Instance.ForcedFoodPriceTargetDay;
                _currentSaveData.tutorialRewardGranted =
                    Core.TutorialManager.Instance.IsRewardGranted;
                _currentSaveData.tutorialProgress = Core.TutorialManager.Instance.ExportProgress();
                _currentSaveData.firstWreckRecovery =
                    Core.TutorialManager.Instance.ExportFirstWreckRecovery();
            }
            else
            {
                _currentSaveData.completedTutorialIds ??= new List<string>();
                _currentSaveData.tutorialProgress ??= new TutorialProgressSaveData();
                _currentSaveData.firstWreckRecovery ??= new FirstWreckRecoverySaveData();
            }
        }

        // 플레이어 위치는 Town 상태에서만 캡처. 항해 중엔 미갱신으로 직전 마을 위치 유지
        private void CollectPlayerPositionData()
        {
            if (!Core.GameManager.HasInstance)
                return;

            Core.GameManager gm = Core.GameManager.Instance;
            if (gm.CurrentGameState != Core.GameState.Town || !gm.HasPlayer)
                return;

            Vector3 pos = gm.Player.transform.position;
            _currentSaveData.playerPosX = pos.x;
            _currentSaveData.playerPosY = pos.y;
            _currentSaveData.hasPlayerPosition = true;
        }

        // 복귀할 마을을 기록. 항해 중에는 배 위치를 저장하지 않으므로 마지막 기항 마을로 대체한다
        private void CollectSceneData()
        {
            Core.GameState state = Core.GameManager.HasInstance
                ? Core.GameManager.Instance.CurrentGameState
                : Core.GameState.MainMenu;

            string sceneName = state switch
            {
                Core.GameState.Town => SceneManager.GetActiveScene().name,
                Core.GameState.Sailing => Ocean.VoyageSession.ResolveOrDefault(DefaultResumeScene),
                _ => DefaultResumeScene,
            };

            _currentSaveData.currentSceneName = string.IsNullOrWhiteSpace(sceneName)
                ? DefaultResumeScene
                : sceneName.Trim();
        }

        // Ocean 미니맵 안개 탐험 상태를 정적 보관소에서 수집
        private void CollectOceanFogData()
        {
            _currentSaveData.oceanFog ??= new OceanFogData();
            Ocean.MinimapFogState.CopyBitsTo(
                ref _currentSaveData.oceanFog.bits,
                out _currentSaveData.oceanFog.cols,
                out _currentSaveData.oceanFog.rows);
        }

        private void CollectVoyageData()
        {
            _currentSaveData.oceanLastVisitTown = Ocean.VoyageSession.LastVisitTown;
        }

        private void CollectShopStockData()
        {
            _currentSaveData.shopStock ??= new List<ShopStockSaveData>();
            Core.ShopStockManager.Instance.CopySaveDataTo(_currentSaveData.shopStock);
        }

        private void CollectInventoryData()
        {
            Core.InventoryManager invManager = Core.InventoryManager.Instance;

            var playerInventory = invManager.PlayerInventory;
            if (playerInventory != null)
            {
                _currentSaveData.playerInventoryItems ??= new List<Core.InventoryItem>();
                playerInventory.CopyAllItemsTo(_currentSaveData.playerInventoryItems);
            }
            else
                Debug.LogWarning("[Save Load Manager] Player Inventory is null.");

            var shipInventory = invManager.ShipInventory;
            if (shipInventory != null)
            {
                _currentSaveData.shipInventoryItems ??= new List<Core.InventoryItem>();
                shipInventory.CopyAllItemsTo(_currentSaveData.shipInventoryItems);
            }
            else
                Debug.LogWarning("[Save Load Manager] Ship Inventory is null.");

            _currentSaveData.shipFoodStorage = invManager.ShipFoodStorage;
            _currentSaveData.shipLevel = invManager.ShipLevel;
            _currentSaveData.shipBonusCapacity = invManager.ShipBonusCapacity;
        }

        /// <summary>
        /// 저장 시점에 인벤토리가 준비되지 않았을 경우, 일정 시간 대기하여 준비될 때까지 기다리는 헬퍼 메서드
        /// </summary>
        private async Task EnsureInventoryReadyForSaveAsync(float timeoutSeconds = 2f)
        {
            float startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
            {
                Core.InventoryManager invManager = Core.InventoryManager.Instance;
                if (invManager.IsPlayerInventoryReady && invManager.IsShipInventoryReady)
                    return;

                await Task.Yield();
            }

            Debug.LogWarning("[Save Load Manager] Inventory 초기화 타임아웃. 저장 시점에 인벤토리가 완전히 준비되지 않았을 수 있습니다.");
        }

        private void CollectItemPriceData()
        {
            // 히스토리 포함된 런타임 가격 데이터 수집
            if (RuntimeItemPriceManager.HasInstance)
            {
                _currentSaveData.itemPriceData ??= new List<ItemPriceData>();
                _currentSaveData.activeSpecialEffects ??= new List<ActiveSpecialEffectSaveData>();
                _currentSaveData.activeNormalEffects ??= new List<NormalEffectSaveData>();

                RuntimeItemPriceManager priceManager = RuntimeItemPriceManager.Instance;
                priceManager.CopyRuntimePriceDataTo(_currentSaveData.itemPriceData);
                priceManager.CopyActiveSpecialEffectSaveDataTo(_currentSaveData.activeSpecialEffects);
                priceManager.CopyActiveNormalEffectSaveDataTo(_currentSaveData.activeNormalEffects);
            }
            else
            {
                // Fallback: 기본 데이터베이스에서 수집
                ItemPriceDatabase itemPriceDatabase = DataManager.Instance.ItemPriceDatabase;
                if (itemPriceDatabase != null)
                {
                    _currentSaveData.itemPriceData ??= new List<ItemPriceData>();
                    itemPriceDatabase.CopyAllItemPricesTo(_currentSaveData.itemPriceData);
                }
                else
                    Debug.LogWarning("[Save Load Manager] ItemPriceDatabase reference is missing in Data Manager.");
            }
        }

        private void CollectQuestData()
        {
            var em = Event.EventManager.Instance;
            if (em == null) return;

            _currentSaveData.eventLastTriggerDays ??= new Dictionary<string, int>();
            _currentSaveData.consumedEventIds ??= new List<string>();
            _currentSaveData.boardAssignedSlots ??= new Dictionary<string, List<string>>();
            em.CopyLastTriggerDaysTo(_currentSaveData.eventLastTriggerDays);
            em.CopyConsumedEventIdsTo(_currentSaveData.consumedEventIds);
            em.CopyBoardAssignedSlotsTo(_currentSaveData.boardAssignedSlots);

            // 완료된 퀘스트
            var questService = GetDefaultQuestService(em);
            if (questService != null)
            {
                _currentSaveData.completedQuestIds ??= new List<string>();
                questService.CopyCompletedQuestIdsTo(_currentSaveData.completedQuestIds);
            }
        }

        private void CollectPlayerShopData()
        {
            _currentSaveData.playerShops ??= new List<PlayerShopData>();
            _currentSaveData.hiredStaff ??= new List<StaffData>();

            if (Core.PlayerShopManager.HasInstance)
            {
                Core.PlayerShopManager shopManager = Core.PlayerShopManager.Instance;
                shopManager.CopySaveDataTo(_currentSaveData.playerShops);
                shopManager.CopyHiredStaffSaveDataTo(_currentSaveData.hiredStaff);
            }
            else
            {
                _currentSaveData.playerShops.Clear();
                _currentSaveData.hiredStaff.Clear();
            }
        }

        /// <summary>
        /// EventManager의 내부 DefaultQuestService를 반환 (세이브/로드 용)
        /// </summary>
        private static Event.Services.DefaultQuestService GetDefaultQuestService(Event.EventManager em)
        {
            return em.QuestServiceAsDefault;
        }

        #endregion

        #region Apply Loaded Data
        /// <summary>
        /// 로드된 GameData를 게임에 적용
        /// </summary>
        private void ApplyGameData()
        {
            if (_currentSaveData == null) return;

            ApplyGameDate();
            ApplyAccountData();
            ApplyInventoryData();
            ApplyItemPriceData();
            ApplyShopStockData();
            ApplyEventData();
            ApplyPlayerShopData();
            ApplyOceanFogData();
            ApplyVoyageData();
            ApplyPlayerPositionData();
            ApplyTutorialData();
            ApplyProgressionData();
            StartDeferredApplyRoutines();
        }

        private void ApplyProgressionData()
        {
            SaveSnapshotList.CopyPlayerStatData(_currentSaveData.playerStats, _pendingPlayerStatData);
            SaveSnapshotList.CopyTownProgressionData(_currentSaveData.townProgression, _pendingTownProgressionData);
            _hasPendingPlayerStatData = true;
            _hasPendingTownProgressionData = true;
            TryApplyDeferredProgressionData();
        }

        private void ApplyTutorialData()
        {
            CopyList(_currentSaveData.completedTutorialIds, _pendingCompletedTutorialIds);
            _pendingTutorialForcedFoodPriceTargetDay =
                Mathf.Max(0, _currentSaveData.tutorialForcedFoodPriceTargetDay);
            _pendingTutorialRewardGranted = _currentSaveData.tutorialRewardGranted;
            _pendingTutorialProgress = _currentSaveData.tutorialProgress ?? new TutorialProgressSaveData();
            _pendingFirstWreckRecovery =
                _currentSaveData.firstWreckRecovery?.Copy() ?? new FirstWreckRecoverySaveData();
            _hasPendingTutorialData = true;
            TryApplyDeferredTutorialData();
        }

        // 로드된 위치를 지연 버퍼에 적재. 실제 적용은 씬 로드 후 HandleSceneLoaded에서 수행
        private void ApplyPlayerPositionData()
        {
            _pendingPlayerPosition = _currentSaveData.hasPlayerPosition
                ? new Vector2(_currentSaveData.playerPosX, _currentSaveData.playerPosY)
                : (Vector2?)null;
        }

        // 복원 위치를 플레이어에 1회 적용. 씬·플레이어 준비 후 호출
        private void TryApplyPendingPlayerPosition()
        {
            if (_pendingPlayerPosition == null)
                return;

            Player.PlayerController controller = FindFirstObjectByType<Player.PlayerController>();
            if (controller == null)
                return;

            controller.SetPosition(_pendingPlayerPosition.Value);
            _pendingPlayerPosition = null;
        }

        // 저장된 안개 탐험 상태를 정적 보관소에 주입(Ocean 재진입 시 미니맵이 복원)
        private void ApplyOceanFogData()
        {
            OceanFogData fog = _currentSaveData.oceanFog;
            if (fog != null) Ocean.MinimapFogState.ImportBits(fog.cols, fog.rows, fog.bits);
            else Ocean.MinimapFogState.ImportBits(0, 0, null);
        }

        private void ApplyVoyageData()
        {
            Ocean.VoyageSession.Load(_currentSaveData.oceanLastVisitTown);
        }

        private void ApplyShopStockData()
        {
            Core.ShopStockManager.Instance.ImportSaveData(_currentSaveData.shopStock);
        }

        private void ApplyAccountData()
        {
            Core.CurrencyManager.Instance.SetPlayerBalance(CurrencyType.Gold, _currentSaveData.gold);
        }

        private void ApplyGameDate()
        {
            // 로드 시에는 이벤트를 발생시키지 않고 날짜만 설정
            Core.GameManager.Instance.SetDateSilent(_currentSaveData.gameDate, _currentSaveData.dayProgress);
        }

        private void ApplyInventoryData()
        {
            CopyList(_currentSaveData.playerInventoryItems, _pendingPlayerInventoryItems);
            CopyList(_currentSaveData.shipInventoryItems, _pendingShipInventoryItems);
            _hasPendingPlayerInventoryItems = true;
            _hasPendingShipInventoryItems = true;
            _pendingShipFoodStorage = _currentSaveData.shipFoodStorage;
            _pendingShipLevel = _currentSaveData.shipLevel;
            _pendingShipBonusCapacity = _currentSaveData.shipBonusCapacity;
            _hasPendingShipState = true;

            TryApplyDeferredInventoryData();
        }

        private void ApplyEventData()
        {
            var em = Event.EventManager.Instance;
            if (em == null) return;

            // AfterDay 조건용 마지막 트리거 일수
            if (_currentSaveData.eventLastTriggerDays != null)
                em.LoadLastTriggerDays(_currentSaveData.eventLastTriggerDays);

            em.LoadConsumedEventIds(_currentSaveData.consumedEventIds);
            if (_currentSaveData.boardAssignedSlots != null)
                em.LoadBoardAssignedSlotsFromSaveData(_currentSaveData.boardAssignedSlots);

            // 완료된 퀘스트
            var questService = GetDefaultQuestService(em);
            if (questService != null && _currentSaveData.completedQuestIds != null)
                questService.LoadCompletedQuests(_currentSaveData.completedQuestIds);
        }

        private void ApplyPlayerShopData()
        {
            SaveSnapshotList.CopyPlayerShopDataList(_currentSaveData.playerShops, _pendingPlayerShops);
            SaveSnapshotList.CopyStaffDataList(_currentSaveData.hiredStaff, _pendingHiredStaff);
            _hasPendingPlayerShopData = true;

            TryApplyDeferredPlayerShopData();
        }

        #endregion

        #region Deferred Apply

        /// <summary>
        /// 로드 시점에 준비되지 않은 데이터를 고려, 지연 적용하는 방식으로 실행
        /// </summary>
        private void HandleSceneLoaded(string sceneName)
        {
            TryApplyDeferredInventoryData();
            TryApplyDeferredPlayerShopData();
            TryApplyPendingPlayerPosition();
            TryApplyDeferredTutorialData();
            TryApplyDeferredProgressionData();
            StartDeferredApplyRoutines();
        }

        private void StartDeferredApplyRoutines()
        {
            if ((_hasPendingPlayerInventoryItems || _hasPendingShipInventoryItems) && _inventoryApplyRoutine == null)
                _inventoryApplyRoutine = StartCoroutine(WaitForInventoryAndApply(DeferredApplyTimeoutSeconds));

            if (_hasPendingPlayerShopData && _playerShopApplyRoutine == null)
                _playerShopApplyRoutine = StartCoroutine(WaitForPlayerShopAndApply(DeferredApplyTimeoutSeconds));

            if (_hasPendingTutorialData && _tutorialApplyRoutine == null)
                _tutorialApplyRoutine = StartCoroutine(WaitForTutorialAndApply(DeferredApplyTimeoutSeconds));

            if ((_hasPendingPlayerStatData || _hasPendingTownProgressionData)
                && _progressionApplyRoutine == null)
            {
                _progressionApplyRoutine = StartCoroutine(WaitForProgressionAndApply(DeferredApplyTimeoutSeconds));
            }
        }

        private void StopDeferredApplyRoutines()
        {
            if (_inventoryApplyRoutine != null)
            {
                StopCoroutine(_inventoryApplyRoutine);
                _inventoryApplyRoutine = null;
            }

            if (_playerShopApplyRoutine != null)
            {
                StopCoroutine(_playerShopApplyRoutine);
                _playerShopApplyRoutine = null;
            }

            if (_tutorialApplyRoutine != null)
            {
                StopCoroutine(_tutorialApplyRoutine);
                _tutorialApplyRoutine = null;
            }

            if (_progressionApplyRoutine != null)
            {
                StopCoroutine(_progressionApplyRoutine);
                _progressionApplyRoutine = null;
            }
        }

        private IEnumerator WaitForInventoryAndApply(float timeoutSeconds)
        {
            float startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
            {
                if (!_hasPendingPlayerInventoryItems && !_hasPendingShipInventoryItems)
                    break;

                TryApplyDeferredInventoryData();

                if (!_hasPendingPlayerInventoryItems && !_hasPendingShipInventoryItems)
                    break;

                yield return null;
            }

            if (_hasPendingPlayerInventoryItems || _hasPendingShipInventoryItems)
                Debug.LogWarning("[Save Load Manager] Inventory apply timed out after load.");

            _inventoryApplyRoutine = null;
        }

        private IEnumerator WaitForPlayerShopAndApply(float timeoutSeconds)
        {
            float startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
            {
                if (!_hasPendingPlayerShopData)
                    break;

                TryApplyDeferredPlayerShopData();

                if (!_hasPendingPlayerShopData)
                    break;

                yield return null;
            }

            if (_hasPendingPlayerShopData)
                Debug.LogWarning("[Save Load Manager] Player shop apply timed out after load.");

            _playerShopApplyRoutine = null;
        }

        private IEnumerator WaitForTutorialAndApply(float timeoutSeconds)
        {
            float startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
            {
                if (!_hasPendingTutorialData)
                    break;

                TryApplyDeferredTutorialData();
                if (!_hasPendingTutorialData)
                    break;

                yield return null;
            }

            if (_hasPendingTutorialData)
                Debug.LogWarning("[Save Load Manager] Tutorial progress apply timed out after load.");

            _tutorialApplyRoutine = null;
        }

        private IEnumerator WaitForProgressionAndApply(float timeoutSeconds)
        {
            float startTime = Time.realtimeSinceStartup;

            while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
            {
                if (!_hasPendingPlayerStatData && !_hasPendingTownProgressionData)
                    break;

                TryApplyDeferredProgressionData();
                if (!_hasPendingPlayerStatData && !_hasPendingTownProgressionData)
                    break;

                yield return null;
            }

            if (_hasPendingPlayerStatData || _hasPendingTownProgressionData)
                Debug.LogWarning("[Save Load Manager] Progression data apply timed out after load.");

            _progressionApplyRoutine = null;
        }

        private void TryApplyDeferredProgressionData()
        {
            if (_hasPendingPlayerStatData
                && Core.PlayerStatManager.HasInstance
                && Core.PlayerStatManager.Instance.IsInitialized)
            {
                Core.PlayerStatManager.Instance.ImportSaveData(_pendingPlayerStatData);
                _hasPendingPlayerStatData = false;
            }

            if (_hasPendingTownProgressionData
                && Core.TownProgressionManager.HasInstance
                && Core.TownProgressionManager.Instance.IsInitialized)
            {
                Core.TownProgressionManager.Instance.ImportSaveData(_pendingTownProgressionData);
                _hasPendingTownProgressionData = false;
            }
        }

        private void TryApplyDeferredTutorialData()
        {
            if (!_hasPendingTutorialData
                || !Core.TutorialManager.HasInstance
                || !Core.TutorialManager.Instance.IsInitialized)
            {
                return;
            }

            Core.TutorialManager.Instance.ImportProgress(
                _pendingTutorialProgress,
                _pendingCompletedTutorialIds,
                _pendingTutorialForcedFoodPriceTargetDay,
                _pendingTutorialRewardGranted);
            Core.TutorialManager.Instance.ImportFirstWreckRecovery(_pendingFirstWreckRecovery);
            _hasPendingTutorialData = false;
            _pendingTutorialForcedFoodPriceTargetDay = 0;
            _pendingTutorialRewardGranted = false;
            _pendingTutorialProgress = new TutorialProgressSaveData();
            _pendingFirstWreckRecovery = new FirstWreckRecoverySaveData();
        }

        private void TryApplyDeferredInventoryData()
        {
            if (!_hasPendingPlayerInventoryItems && !_hasPendingShipInventoryItems)
                return;

            // InventoryManager가 아직 준비되지 않은 경우, 다음 폴링을 기다린다
            if (!Core.InventoryManager.HasInstance)
                return;

            Core.InventoryManager invManager = Core.InventoryManager.Instance;

            if (_hasPendingPlayerInventoryItems && !invManager.IsPlayerInventoryReady)
                return;

            if (_hasPendingShipInventoryItems && !invManager.IsShipInventoryReady)
                return;

            if (_hasPendingPlayerInventoryItems)
            {
                var playerInventory = invManager.PlayerInventory;
                if (playerInventory != null)
                {
                    RestoreInventory(
                        playerInventory,
                        _pendingPlayerInventoryItems,
                        "Player");
                    _hasPendingPlayerInventoryItems = false;
                }
            }

            if (_hasPendingShipInventoryItems)
            {
                var shipInventory = invManager.ShipInventory;
                if (shipInventory != null)
                {
                    RestoreInventory(shipInventory, _pendingShipInventoryItems, "Ship");
                    _hasPendingShipInventoryItems = false;
                }
            }

            // 배 식량/등급 저장값 로드
            if (_hasPendingShipState)
            {
                invManager.SetShipLevel(_pendingShipLevel);
                invManager.SetShipBonusCapacity(_pendingShipBonusCapacity);
                invManager.SetShipFoodStorageFromSave(_pendingShipFoodStorage);
                _hasPendingShipState = false;
            }
        }

        private static void CopyList<T>(List<T> source, List<T> target)
        {
            target.Clear();
            if (source != null)
                target.AddRange(source);
        }

        private static void RestoreInventory(
            Core.InventoryData targetInventory,
            List<Core.InventoryItem> sourceItems,
            string inventoryName,
            int priorityItemId = 0)
        {
            targetInventory.Clear();

            if (sourceItems == null || sourceItems.Count == 0)
                return;

            int failedCount = 0;
            if (priorityItemId > 0)
            {
                for (int i = 0; i < sourceItems.Count; i++)
                {
                    Core.InventoryItem priorityItem = sourceItems[i];
                    if (priorityItem.id != priorityItemId)
                        continue;

                    if (!targetInventory.AddItem(
                            priorityItem.id,
                            priorityItem.quantity,
                            priorityItem.averagePurchasePrice))
                    {
                        failedCount++;
                    }
                    break;
                }
            }

            foreach (var item in sourceItems)
            {
                if (item.id == priorityItemId)
                    continue;

                bool isAdded = targetInventory.AddItem(item.id, item.quantity, item.averagePurchasePrice);
                if (!isAdded)
                    failedCount++;
            }

            if (failedCount > 0)
                Debug.LogWarning($"[Save Load Manager] Failed to restore {failedCount} items to {inventoryName} inventory.");
        }

        private void TryApplyDeferredPlayerShopData()
        {
            if (!_hasPendingPlayerShopData)
                return;

            // PlayerShopManager가 아직 준비되지 않은 경우, 다음 폴링을 기다린다
            if (!Core.PlayerShopManager.HasInstance)
                return;

            Core.PlayerShopManager shopManager = Core.PlayerShopManager.Instance;

            if (!shopManager.IsReady)
                return;

            shopManager.ImportSaveData(_pendingPlayerShops, _pendingHiredStaff);
            _hasPendingPlayerShopData = false;
        }

        private void HandleInventoryReady()
        {
            TryApplyDeferredInventoryData();
        }

        private void HandlePlayerShopReady()
        {
            TryApplyDeferredPlayerShopData();
        }

        #endregion

        #region Save Data Utilities

        /// <summary>
        /// 저장된 아이템 가격 데이터를 RuntimeItemPriceManager에 적용
        /// </summary>
        private void ApplyItemPriceData()
        {
            if (_currentSaveData.itemPriceData != null && _currentSaveData.itemPriceData.Count > 0)
                // 저장된 데이터로 복원
                RuntimeItemPriceManager.Instance.LoadFromSaveData(
                    _currentSaveData.itemPriceData,
                    _currentSaveData.activeSpecialEffects,
                    _currentSaveData.activeNormalEffects);
            else
                // 저장된 데이터가 없으면 기본 데이터베이스로 초기화
                InitializeRuntimePriceManager();
        }

        /// <summary>
        /// 기본 게임 데이터 생성
        /// </summary>
        private void CreateDefaultGameData(Core.PlayerGender playerGender = Core.PlayerGender.Male)
        {
            _pendingPlayerPosition = null;
            _currentSaveData = new SaveData
            {
                saveDate = DateTime.Now.ToString(),
                gameVersion = Application.version,
                gameDate = 1,
                gameState = Core.GameState.MainMenu.ToString(),
                gold = 10000L,
                playerGender = Core.PlayerGenderPolicy.Normalize(playerGender),
                shipFoodStorage = Core.InventoryManager.FoodStatPerDay,
                playerShops = new List<PlayerShopData>(),
                hiredStaff = new List<StaffData>(),
                oceanFog = new OceanFogData(),
                oceanLastVisitTown = "StartTown",
            };

            _pendingPlayerShops.Clear();
            _pendingHiredStaff.Clear();
            _pendingPlayerInventoryItems.Clear();
            _pendingShipInventoryItems.Clear();
            _pendingCompletedTutorialIds.Clear();
            _hasPendingPlayerShopData = false;
            _hasPendingPlayerInventoryItems = false;
            _hasPendingShipInventoryItems = false;
            _hasPendingShipState = false;
            _hasPendingTutorialData = false;
            _hasPendingPlayerStatData = false;
            _hasPendingTownProgressionData = false;
            _pendingTutorialForcedFoodPriceTargetDay = 0;
            _pendingTutorialRewardGranted = false;
            _pendingTutorialProgress = new TutorialProgressSaveData();
            _pendingFirstWreckRecovery = new FirstWreckRecoverySaveData();

            if (Core.TutorialManager.HasInstance && Core.TutorialManager.Instance.IsInitialized)
                Core.TutorialManager.Instance.ResetProgress();

            if (Core.PlayerStatManager.HasInstance && Core.PlayerStatManager.Instance.IsInitialized)
                Core.PlayerStatManager.Instance.ResetProgression();

            if (Core.TownProgressionManager.HasInstance && Core.TownProgressionManager.Instance.IsInitialized)
                Core.TownProgressionManager.Instance.ResetProgression();

            // 새 게임/폴백 시 미니맵 안개·항해 세션(출항일/마지막 기항지)도 초기화(이전 세션 잔존 방지)
            Ocean.MinimapFogState.Reset();
            Ocean.VoyageSession.Load(_currentSaveData.oceanLastVisitTown);

            InitializeRuntimePriceManager();
        }

        /// <summary>
        /// RuntimeItemPriceManager를 기본 데이터베이스로 초기화
        /// </summary>
        private void InitializeRuntimePriceManager()
        {
            var defaultDatabase = DataManager.Instance?.ItemPriceDatabase;
            if (defaultDatabase != null)
                RuntimeItemPriceManager.Instance.InitializeFromDatabase(defaultDatabase);
        }

        /// <summary>
        /// 저장 파일의 전체 경로 반환 (파일 이름 포함)
        /// </summary>
        private string GetSaveFilePath(int slot)
        {
            string fileName = string.Format(SaveFileNameFormat, slot);
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        private bool IsValidSlot(int slot)
        {
            return slot >= 0 && slot < MaxSlots;
        }

        private ReadOnlyMemory<byte> SerializeSaveData(SaveData saveData)
        {
            _serializationBuffer.Clear();
            MemoryPackSerializer.Serialize(_serializationBuffer, saveData);
            return _serializationBuffer.WrittenMemory;
        }

        private static async Task<SaveData> DeserializeSaveFileAsync(string filePath)
        {
            using (var stream = new FileStream(
                       filePath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       FileStreamBufferSize,
                       FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                if (stream.Length == 0)
                    throw new InvalidDataException("Save file is empty.");

                return await MemoryPackSerializer.DeserializeAsync<SaveData>(stream);
            }
        }

        private void ResetSerializationBuffer()
        {
            if (_serializationBuffer.Capacity > MaxRetainedSerializationBufferCapacity)
            {
                _serializationBuffer = new ArrayBufferWriter<byte>(SerializationBufferInitialCapacity);
                return;
            }

            _serializationBuffer.Clear();
        }

        private static async Task WriteSaveFileAtomicallyAsync(string filePath, ReadOnlyMemory<byte> data)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Save file path is required.", nameof(filePath));

            if (data.IsEmpty)
                throw new InvalidDataException("Serialized save data is empty.");

            string directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException($"Save directory could not be resolved: {filePath}");

            Directory.CreateDirectory(directory);

            string temporaryPath = filePath + TemporaryFileSuffix;
            string backupPath = filePath + BackupFileSuffix;
            TryDeleteSidecarFile(temporaryPath);

            try
            {
                await WriteTemporarySaveFileAsync(temporaryPath, data);
                await ValidateTemporarySaveFileAsync(temporaryPath, data.Length);
                ReplaceSaveFile(filePath, temporaryPath, backupPath);
            }
            catch
            {
                TryDeleteSidecarFile(temporaryPath);
                RestoreBackupIfRequired(filePath, backupPath);
                throw;
            }
        }

        private static async Task WriteTemporarySaveFileAsync(string temporaryPath, ReadOnlyMemory<byte> data)
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       FileStreamBufferSize,
                       FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(data, CancellationToken.None);
                await stream.FlushAsync();
                stream.Flush(true);
            }
        }

        private static async Task ValidateTemporarySaveFileAsync(string temporaryPath, int expectedLength)
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.None,
                       FileStreamBufferSize,
                       FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                if (stream.Length != expectedLength)
                {
                    throw new InvalidDataException(
                        $"Temporary save file length mismatch. Expected {expectedLength}, actual {stream.Length}.");
                }

                SaveData validationData = await MemoryPackSerializer.DeserializeAsync<SaveData>(stream);
                if (validationData == null)
                    throw new InvalidDataException("Temporary save file validation failed.");
            }
        }

        private static void ReplaceSaveFile(string filePath, string temporaryPath, string backupPath)
        {
            if (File.Exists(filePath))
            {
                TryDeleteSidecarFile(backupPath);
                File.Replace(temporaryPath, filePath, backupPath, true);
                TryDeleteSidecarFile(backupPath);
                return;
            }

            File.Move(temporaryPath, filePath);
        }

        private static void RestoreBackupIfRequired(string filePath, string backupPath)
        {
            if (File.Exists(filePath) || !File.Exists(backupPath))
                return;

            try
            {
                File.Move(backupPath, filePath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Save Load Manager] Failed to restore save backup: {exception.Message}");
            }
        }

        private static void TryDeleteSidecarFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Save Load Manager] Failed to clean up '{filePath}': {exception.Message}");
            }
        }

        public async Task<bool> DeleteSaveFileAsync(int slot)
        {
            if (!IsValidSlot(slot))
            {
                Debug.LogError($"Invalid slot number: {slot}. Must be between 0 and {MaxSlots - 1}");
                return false;
            }

            return await RunExclusiveAsync("Delete", () => Task.FromResult(DeleteSaveFileInternal(slot)));
        }

        public void DeleteSaveFile(int slot)
        {
            _ = DeleteSaveFileAsync(slot);
        }

        private bool DeleteSaveFileInternal(int slot)
        {
            try
            {
                string filePath = GetSaveFilePath(slot);
                TryDeleteSidecarFile(filePath + TemporaryFileSuffix);
                TryDeleteSidecarFile(filePath + BackupFileSuffix);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Debug.Log($"Save file deleted from slot {slot}");
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Save Load Manager] Failed to delete slot {slot}: {exception.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteAllSaveFilesAsync()
        {
            return await RunExclusiveAsync("DeleteAll", () =>
            {
                bool allDeleted = true;
                for (int i = 0; i < MaxSlots; i++)
                    allDeleted &= DeleteSaveFileInternal(i);

                if (allDeleted)
                    Debug.Log("All save files deleted");

                return Task.FromResult(allDeleted);
            });
        }

        public void DeleteAllSaveFiles()
        {
            _ = DeleteAllSaveFilesAsync();
        }

        /// <summary>
        /// 특정 슬롯의 세이브 데이터 미리보기 (로드 및 적용하지 않고 정보만 확인)
        /// </summary>
        public async Task<SaveData> GetSaveDataPreviewAsync(int slot)
        {
            if (!IsValidSlot(slot))
            {
                Debug.LogError($"Invalid slot number: {slot}. Must be between 0 and {MaxSlots - 1}");
                return null;
            }

            string filePath = GetSaveFilePath(slot);
            if (!File.Exists(filePath))
                return null;

            try
            {
                return await DeserializeSaveFileAsync(filePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to preview save data from slot {slot}: {e.Message}");
                return null;
            }
        }

        #endregion
    }
}
