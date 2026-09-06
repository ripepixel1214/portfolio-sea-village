using System;
using System.Collections;
using UnityEngine;
using SeaVillage.Utilities;

namespace SeaVillage.Core
{
    /// <summary>
    /// 인벤토리 매니저 - 모든 인벤토리를 중앙에서 관리하는 싱글톤
    /// Player, Ship 인벤토리를 통합 관리
    /// </summary>
    public class InventoryManager : Singleton<InventoryManager>
    {
        // 식량 스탯 3 = 1일치 (스탯-일치 변환의 단일 진리원)
        public const float FoodStatPerDay = 3f;

        /// <summary>현재 식량 잔여 일수(일치). ShipFoodStorage를 FoodStatPerDay로 나눈 몫</summary>
        public int ShipFoodDays => Mathf.FloorToInt(shipFoodStorage / FoodStatPerDay);

        [Header("Ship Inventory Settings")]
        [SerializeField] private float baseShipCargoCapacity = 15f;
        [SerializeField] private float capacityPerLevel = 5f;
        [Header("Ship Food Settings")]
        [SerializeField, Min(1f)] private float baseShipFoodCapacity = 100f;
        [SerializeField, Min(0f)] private float foodCapacityPerLevel = 20f;
        private float bonusShipCapacity = 0f;
        private float shipFoodStorage = 0f;
        private int shipLevel = 0;

        // 인벤토리 데이터
        private InventoryData playerInventory;
        private InventoryData shipInventory;

        private bool inventoriesReadyNotified;

        // Properties
        public InventoryData PlayerInventory => playerInventory;
        public InventoryData ShipInventory => shipInventory;

        /// <summary>
        /// 인스턴스를 생성하지 않고 플레이어 인벤토리 조회 (준비 전이면 null)
        /// </summary>
        public static InventoryData PlayerInventoryOrNull => HasInstance ? Instance.playerInventory : null;

        /// <summary>
        /// 인스턴스를 생성하지 않고 배 인벤토리 조회 (준비 전이면 null)
        /// </summary>
        public static InventoryData ShipInventoryOrNull => HasInstance ? Instance.shipInventory : null;
        public bool IsPlayerInventoryReady { get; private set; }
        public bool IsShipInventoryReady { get; private set; }
        public bool IsReady { get; private set; }
        public float ShipMaxCapacity => baseShipCargoCapacity + bonusShipCapacity + shipLevel * capacityPerLevel;
        public float ShipBonusCapacity => bonusShipCapacity;
        public float ShipFoodCapacity => Mathf.Max(1f, baseShipFoodCapacity + shipLevel * foodCapacityPerLevel);
        public int ShipLevel => shipLevel;
        public float ShipFoodStorage
        {
            get { return shipFoodStorage; }
            set
            {
                float requestedValue = Mathf.Max(0f, value);
                float clampedValue = shipFoodStorage > ShipFoodCapacity && requestedValue <= shipFoodStorage
                    ? requestedValue
                    : Mathf.Min(requestedValue, ShipFoodCapacity);
                if (Mathf.Approximately(shipFoodStorage, clampedValue))
                    return;

                shipFoodStorage = clampedValue;
                OnShipFoodStorageChanged?.Invoke();
            }
        }

        // Events (UI 등에서 구독용)
        public event Action OnPlayerInventoryChanged;
        public event Action OnShipInventoryChanged;
        public event Action OnShipFoodStorageChanged;
        public event Action OnPlayerInventoryReady;
        public event Action OnShipInventoryReady;
        public event Action OnInventoriesReady;

        protected override void Awake()
        {
            base.Awake();
            InitializeInventories();
        }

        private void InitializeInventories()
        {
            InitializeShipInventory();
            StartCoroutine(InitializePlayerInventory());
        }

        /// <summary>
        /// Ship 인벤토리 초기화(최대 무게 프로퍼티 제공)
        /// </summary>
        private void InitializeShipInventory()
        {
            shipInventory = new InventoryData(() => ShipMaxCapacity);
            shipInventory.OnInventoryChanged += () => OnShipInventoryChanged?.Invoke();
            shipInventory.OnOverweight += OnShipOverweight;
            IsShipInventoryReady = true;
            OnShipInventoryReady?.Invoke();
            NotifyInventoriesReadyIfPossible();
        }

        /// <summary>
        /// Player 인벤토리 초기화 코루틴(Player Status가 초기화될 때까지 대기)
        /// </summary>
        /// <returns></returns>
        private IEnumerator InitializePlayerInventory()
        {
            GameManager gm = GameManager.Instance;

            yield return new WaitUntil(() => gm.HasPlayer &&
                                             gm.Player.IsComponentInitialized);

            playerInventory = new InventoryData(GetPlayerCarryCapacity);
            playerInventory.OnInventoryChanged += () => OnPlayerInventoryChanged?.Invoke();
            playerInventory.OnOverweight += OnPlayerOverweight;
            IsPlayerInventoryReady = true;
            OnPlayerInventoryReady?.Invoke();
            NotifyInventoriesReadyIfPossible();
        }

        private float GetPlayerCarryCapacity()
        {
            float baseCapacity = PlayerStatManager.HasInstance
                ? PlayerStatManager.Instance.CarryCapacity
                : PlayerStatManager.DefaultStrength;
            return baseCapacity;
        }

        private void NotifyInventoriesReadyIfPossible()
        {
            if (inventoriesReadyNotified)
                return;

            if (!IsPlayerInventoryReady || !IsShipInventoryReady)
                return;

            IsReady = true;
            inventoriesReadyNotified = true;
            OnInventoriesReady?.Invoke();
        }

        #region Ship Inventory Methods

        /// <summary>
        /// 배 등급 업그레이드. 등급이 오르면 적재 용량(capacityPerLevel)과 접근 가능한 섬이 늘어난다.
        /// </summary>
        public void UpgradeShip(int newLevel)
        {
            if (newLevel <= shipLevel)
                return;

            shipLevel = newLevel;
            OnShipInventoryChanged?.Invoke();
            OnShipFoodStorageChanged?.Invoke();
            Debug.Log($"배 등급이 Lv.{shipLevel}로 상승했습니다. 식량 저장 상한: {ShipFoodCapacity}");
        }

        /// <summary>
        /// 배 등급 직접 설정 (세이브 로드용, 로그 없음).
        /// </summary>
        public void UpgradeShipCapacity(float additionalCapacity)
        {
            float clampedAdditionalCapacity = Mathf.Max(0f, additionalCapacity);
            if (Mathf.Approximately(clampedAdditionalCapacity, 0f))
                return;

            bonusShipCapacity += clampedAdditionalCapacity;
            OnShipInventoryChanged?.Invoke();
            Debug.Log($"Ship capacity increased by {clampedAdditionalCapacity}. Current max capacity: {ShipMaxCapacity}");
        }

        public void SetShipLevel(int level)
        {
            int clampedLevel = Mathf.Max(0, level);
            if (shipLevel == clampedLevel)
                return;

            shipLevel = clampedLevel;
            OnShipInventoryChanged?.Invoke();
            OnShipFoodStorageChanged?.Invoke();
        }

        public bool CanAddShipFood(float amount)
        {
            if (amount <= 0f)
                return false;

            return shipFoodStorage + amount <= ShipFoodCapacity;
        }

        public bool TryAddShipFood(float amount)
        {
            if (!CanAddShipFood(amount))
                return false;

            ShipFoodStorage = shipFoodStorage + amount;
            return true;
        }

        /// <summary>식량을 amount만큼 소모. 하한 0으로 클램프</summary>
        public void ConsumeShipFood(float amount)
        {
            if (amount <= 0f)
                return;

            ShipFoodStorage = shipFoodStorage - amount;
        }

        /// <summary>
        /// 이전 버전 저장 데이터가 새 상한보다 큰 경우에도 식량을 유실하지 않고 복원합니다.
        /// 이후 충전은 상한 이하에서만 허용됩니다.
        /// </summary>
        public void SetShipFoodStorageFromSave(float value)
        {
            float restoredValue = Mathf.Max(0f, value);
            if (Mathf.Approximately(shipFoodStorage, restoredValue))
                return;

            shipFoodStorage = restoredValue;
            OnShipFoodStorageChanged?.Invoke();
        }

        public void SetShipBonusCapacity(float capacity)
        {
            float clampedCapacity = Mathf.Max(0f, capacity);
            if (Mathf.Approximately(bonusShipCapacity, clampedCapacity))
                return;

            bonusShipCapacity = clampedCapacity;
            OnShipInventoryChanged?.Invoke();
        }

        private void OnShipOverweight()
        {
            Debug.LogWarning("배의 적재 용량이 초과되었습니다!");
            // TODO: UI 알림 표시
        }

        #endregion

        #region Player Inventory Methods

        private void OnPlayerOverweight()
        {
            Debug.LogWarning("플레이어 인벤토리가 너무 무겁습니다!");
            // TODO: UI 알림 표시
        }

        #endregion

        #region Transfer Methods

        public bool TransferToShip(int itemID, int quantity = 1)
        {
            if (playerInventory == null || shipInventory == null)
                return false;

            if (!playerInventory.Items.TryGetValue(itemID, out InventoryItem sourceItem) || sourceItem.quantity < quantity)
                return false;

            // 먼저 배에 추가 시도
            if (!shipInventory.AddItem(itemID, quantity, sourceItem.averagePurchasePrice))
                return false;

            // 성공하면 플레이어에서 제거
            if (playerInventory.RemoveItem(itemID, quantity))
                return true;

            shipInventory.RemoveItem(itemID, quantity);
            return false;
        }

        public bool TransferToPlayer(int itemID, int quantity = 1)
        {
            if (playerInventory == null || shipInventory == null)
                return false;

            if (!shipInventory.Items.TryGetValue(itemID, out InventoryItem sourceItem) || sourceItem.quantity < quantity)
                return false;

            // 먼저 플레이어에 추가 시도
            if (!playerInventory.AddItem(itemID, quantity, sourceItem.averagePurchasePrice))
                return false;

            // 성공하면 배에서 제거
            if (shipInventory.RemoveItem(itemID, quantity))
                return true;

            playerInventory.RemoveItem(itemID, quantity);
            return false;
        }
        #endregion
    }
}
