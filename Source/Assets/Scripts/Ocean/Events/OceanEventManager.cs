using System;
using UnityEngine;
using SeaVillage.Core;

namespace SeaVillage.Ocean
{
    /// <summary>
    /// 항해 이벤트 도메인 루트. 이벤트 설정(OceanEventConfig)을 보유하고
    /// 보물 상자·식량 소비·난파 귀항 흐름을 직접 소유·조율한다. 이벤트 실행은 전역 EventManager 에 위임한다.
    /// </summary>
    public class OceanEventManager : MonoBehaviour
    {
        private const string TreasurePrefabPath = "Prefabs/Ocean/TreasureChest";
        private const string FoodConsumptionBlockedStepId = "ocean.move";

        [Header("항해 이벤트 설정 값")]
        [SerializeField] private OceanEventConfig _config;

        [Header("보물 상자")]
        [Tooltip("보물 상자 프리팹. 비우면 Resources 에서 로드한다")]
        [SerializeField] private TreasureChest _treasurePrefab;

        [Tooltip("보물 상자 인스턴스의 부모(선택). 비우면 루트에 생성")]
        [SerializeField] private Transform _treasureParent;

        private TreasureChestManager _treasureManager;
        private Event.EventManager _globalEventManager;

        // 식량 소비 (매일 차감, 고갈 시 귀항 절차 1회). 스탯 단위(= config 일치/일 × FoodStatPerDay)
        private float _foodPerDay;
        private bool _foodDepleted;
        private bool _foodSubscribed;

        private string _defaultIslandName;
        private Action<string> _goToScene;
        private bool _initialized;

        /// <summary>
        /// 항해 종료 절차(식량 고갈 귀항 등)가 시작될 때 발생. OceanManager 가 씬 구동 중지에 사용한다.
        /// </summary>
        public event Action VoyageEnded;

        /// <summary>
        /// 필수 참조 검증 + 보물 프리팹 폴백 로드. 실제 시동은 Initialize()에서 한다.
        /// </summary>
        private void Awake()
        {
            if (_config == null)
            {
                throw new InvalidOperationException($"[OceanEventManager] {name}: OceanEventConfig 미할당");
            }

            if (_treasurePrefab == null) _treasurePrefab = Resources.Load<TreasureChest>(TreasurePrefabPath);
            if (_treasurePrefab == null) Debug.LogWarning($"[OceanEventManager] {nameof(_treasurePrefab)} 미연결·'{TreasurePrefabPath}' 로드 실패 — 보물 상자 비활성");
        }

        /// <summary>
        /// OceanManager 가 씬 시동 시 호출하는 진입점. 바다 영역·귀항 통로를 주입받아
        /// 보물 상자를 스폰하고 식량 소비·이벤트 억제 게이트를 가동한다(idempotent).
        /// </summary>
        public void Initialize(Vector2 seaCenter, Vector2 seaSize, string defaultIslandName, Action<string> goToScene)
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;

            _defaultIslandName = defaultIslandName;
            _goToScene = goToScene;

            _treasureManager = new TreasureChestManager(
                _config.TreasureEventId, _treasurePrefab, _treasureParent, _config.TreasureCount,
                _config.TreasureInteractDistance, seaCenter, seaSize, _config.TreasureSpawnInsetMargin);
            _treasureManager.SpawnAll();

            // config는 일치/일 단위 → 스탯/일로 환산(3 스탯 = 1일치)
            _foodPerDay = Mathf.Max(0f, _config.FoodConsumptionPerDay) * InventoryManager.FoodStatPerDay;
            _foodDepleted = false;
            TimeManager.OnDayChanged += HandleDayChanged;
            _foodSubscribed = true;

            // 고갈일에 보상성 돌발 이벤트가 겹치지 않도록 억제 게이트 등록 (전역 EventManager)
            _globalEventManager = Event.EventManager.Instance;
            _globalEventManager?.SetDailyRandomSuppressor(IsFoodDepletedOrAboutToDeplete);
        }

        /// <summary>
        /// 매 프레임 보물 상자 근접 강조 갱신 (OceanManager LateUpdate 에서 호출).
        /// </summary>
        public void Tick(Vector2 shipPosition)
        {
            _treasureManager?.Tick(shipPosition);
        }

        /// <summary>
        /// 인접 보물 상자와 상호작용 시도. 발동했으면 true.
        /// </summary>
        public bool TryInteractTreasure()
        {
            if (_globalEventManager != null && _globalEventManager.AreEventsSuppressed)
                return false;

            return _treasureManager != null && _treasureManager.TryInteract();
        }

        // 하루 경과 시 배 식량 차감, 0 이하면 고갈 처리 (1회)
        private void HandleDayChanged(int currentDay)
        {
            if (IsFoodConsumptionBlockedByTutorial()
                || _foodDepleted
                || _foodPerDay <= 0f)
            {
                return;
            }

            InventoryManager inventory = InventoryManager.Instance;
            if (inventory == null)
            {
                return;
            }

            inventory.ShipFoodStorage -= _foodPerDay;

            if (inventory.ShipFoodStorage <= 0f)
            {
                _foodDepleted = true;
                HandleFoodDepleted();
            }
        }

        private static bool IsFoodConsumptionBlockedByTutorial()
        {
            return TutorialManager.HasInstance
                && TutorialManager.Instance.IsInitialized
                && TutorialManager.Instance.IsActive
                && TutorialManager.Instance.ActiveStepId == FoodConsumptionBlockedStepId;
        }

        // 고갈됐거나 다음 차감으로 고갈될지 판정 (전역 DailyRandom 억제 게이트용)
        private bool IsFoodDepletedOrAboutToDeplete()
        {
            if (_foodDepleted)
            {
                return true;
            }
            if (_foodPerDay <= 0f)
            {
                return false;
            }

            InventoryManager inventory = InventoryManager.Instance;
            return inventory != null && inventory.ShipFoodStorage <= _foodPerDay;
        }

        // 식량 고갈 시 난파 이벤트 재생 후 마지막 항구로 귀환
        private void HandleFoodDepleted()
        {
            VoyageEnded?.Invoke();

            string returnScene = VoyageSession.ResolveOrDefault(_defaultIslandName);

            Event.EventManager eventManager = Event.EventManager.Instance;
            if (eventManager != null && !string.IsNullOrWhiteSpace(_config.ShipwreckEventId))
                eventManager.StartEvent(_config.ShipwreckEventId, () => _goToScene?.Invoke(returnScene));
            else
                FallbackReturnToPort(returnScene);
        }

        // 난파 이벤트가 없는 경우 알림 후 즉시 귀항하는 폴백
        private void FallbackReturnToPort(string returnScene)
        {
            UI.UIManager uiManager = UI.UIManager.Instance;
            if (uiManager != null)
                uiManager.ShowAlertMessage("식량이 모두 떨어져 가장 가까운 항구로 돌아간다", () => _goToScene?.Invoke(returnScene));
            else
                _goToScene?.Invoke(returnScene);
        }

        /// <summary>
        /// 구독·게이트·풀 정리 (영속 싱글턴 누수 방지).
        /// </summary>
        private void OnDestroy()
        {
            if (_globalEventManager != null) _globalEventManager.SetDailyRandomSuppressor(null);

            if (_foodSubscribed)
            {
                TimeManager.OnDayChanged -= HandleDayChanged;
                _foodSubscribed = false;
            }

            _treasureManager?.Clear();
        }
    }
}
