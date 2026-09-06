using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;

namespace SeaVillage.UI
{
    public enum GaugeType { PlayerInventory, ShipInventory, Food }

    /// <summary>
    /// Slider를 대체하는 기본 게이지 클래스(추후 인벤토리 외 게이지 필요시 현재 클래스의 코드를 Inventory 종속성 분리 후 사용 예정)
    /// </summary>
    public class Gauge : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] protected Image fillImage;

        [Header("Gauge Type Settings")]
        [SerializeField] protected GaugeType gaugeType = GaugeType.PlayerInventory;

        [Header("Food Gauge Settings")]
        [SerializeField] protected float maxFoodStorage = 100f;

        // Runtime reference
        protected InventoryData targetInventory;

        // Cached values
        protected float maxWidth;
        protected float maxValue;
        protected float currentValue;

        // Food Gauge State
        private bool isFoodGauge = false;
        private InventoryManager inventoryManager;

        // Properties
        public bool IsInitialized { get; protected set; } = false;
        public InventoryData TargetInventory => targetInventory;
        public float CurrentValue => currentValue;
        public float MaxValue => maxValue;
        public float MaxFoodStorage => InventoryManager.HasInstance
            ? InventoryManager.Instance.ShipFoodCapacity
            : maxFoodStorage;

        #region MonoBehaviour
        protected virtual void Awake()
        {
            CacheMaxWidth();
        }

        protected virtual void Start()
        {
            StartCoroutine(InitializeCoroutine());
        }

        protected virtual void OnDestroy()
        {
            RemoveListeners();
        }
        #endregion

        #region Initialization
        protected virtual IEnumerator InitializeCoroutine()
        {
            yield return WaitForInventory();

            AddListeners();
            RefreshGauge();

            IsInitialized = true;
        }

        protected IEnumerator WaitForInventory()
        {
            // 이미 설정되어 있으면 스킵
            if (targetInventory != null || isFoodGauge) yield break;

            switch (gaugeType)
            {
                case GaugeType.PlayerInventory:
                    yield return new WaitUntil(() => InventoryManager.HasInstance && InventoryManager.Instance.PlayerInventory != null);
                    targetInventory = InventoryManager.Instance.PlayerInventory;
                    break;

                case GaugeType.ShipInventory:
                    yield return new WaitUntil(() => InventoryManager.HasInstance && InventoryManager.Instance.ShipInventory != null);
                    targetInventory = InventoryManager.Instance.ShipInventory;
                    break;

                case GaugeType.Food:
                    yield return new WaitUntil(() => InventoryManager.HasInstance);
                    inventoryManager = InventoryManager.Instance;
                    isFoodGauge = true;
                    break;
            }
        }

        /// <summary>
        /// 외부에서 인벤토리를 설정할 때 사용
        /// </summary>
        public virtual void SetInventory(InventoryData inventory)
        {
            // 기존 리스너 제거
            RemoveListeners();

            targetInventory = inventory;

            // 새 리스너 등록 및 게이지 갱신
            if (targetInventory != null)
            {
                AddListeners();
                RefreshGauge();
            }
        }

        protected void CacheMaxWidth()
        {
            RectTransform rt = GetComponent<RectTransform>();
            if (rt != null)
                maxWidth = rt.rect.width;
        }
        #endregion

        protected virtual void AddListeners()
        {
            // Food Gauge: 식량 저장량 변경 이벤트 구독으로 자동 갱신 (패널의 수동 RefreshGauge 호출과 중복돼도 idempotent)
            if (isFoodGauge || gaugeType == GaugeType.Food)
            {
                if (InventoryManager.HasInstance)
                    InventoryManager.Instance.OnShipFoodStorageChanged += RefreshGauge;
                return;
            }

            if (gaugeType == GaugeType.ShipInventory)
            {
                // ShipInventory may raise events via InventoryData or via InventoryManager; subscribe both
                if (targetInventory != null)
                    targetInventory.OnInventoryChanged += RefreshGauge;

                if (InventoryManager.HasInstance)
                    InventoryManager.Instance.OnShipInventoryChanged += RefreshGauge;
            }
            else
            {
                if (targetInventory != null)
                    targetInventory.OnInventoryChanged += RefreshGauge;
            }
        }

        // OnDestroy에서도 호출되므로 HasInstance로 확인
        protected virtual void RemoveListeners()
        {
            if (isFoodGauge || gaugeType == GaugeType.Food)
            {
                if (InventoryManager.HasInstance)
                    InventoryManager.Instance.OnShipFoodStorageChanged -= RefreshGauge;
                return;
            }

            if (gaugeType == GaugeType.ShipInventory)
            {
                if (targetInventory != null)
                    targetInventory.OnInventoryChanged -= RefreshGauge;

                if (InventoryManager.HasInstance)
                    InventoryManager.Instance.OnShipInventoryChanged -= RefreshGauge;
            }
            else
            {
                if (targetInventory != null)
                    targetInventory.OnInventoryChanged -= RefreshGauge;
            }
        }

        #region Gauge Update
        /// <summary>
        /// 인벤토리 변경 시 호출되어 게이지를 갱신
        /// </summary>
        public virtual void RefreshGauge()
        {
            if (gaugeType == GaugeType.Food || isFoodGauge)
            {
                // 식량 저장값 게이지
                if (inventoryManager == null && InventoryManager.HasInstance)
                    inventoryManager = InventoryManager.Instance;

                if (inventoryManager != null)
                {
                    currentValue = inventoryManager.ShipFoodStorage;
                    maxValue = inventoryManager.ShipFoodCapacity;
                }
                else
                {
                    // No inventory manager available yet
                    currentValue = 0f;
                    maxValue = MaxFoodStorage;
                }
            }
            else
            {
                // 인벤토리 기반 게이지
                if (targetInventory == null)
                {
                    if (gaugeType == GaugeType.ShipInventory)
                        targetInventory = InventoryManager.ShipInventoryOrNull;
                    else if (gaugeType == GaugeType.PlayerInventory)
                        targetInventory = InventoryManager.PlayerInventoryOrNull;
                }

                if (targetInventory == null)
                {
                    currentValue = 0f;
                    maxValue = 1f;
                }
                else
                {
                    maxValue = targetInventory.MaxWeight;
                    currentValue = targetInventory.CurrentWeight;
                }
            }

            UpdateFillImage();
        }

        /// <summary>
        /// Fill 이미지의 크기를 현재 비율에 맞게 업데이트
        /// </summary>
        protected virtual void UpdateFillImage()
        {
            if (fillImage == null || maxValue <= 0) return;

            float ratio = Mathf.Clamp01(currentValue / maxValue);
            fillImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxWidth * ratio);
        }
        #endregion
    }
}
