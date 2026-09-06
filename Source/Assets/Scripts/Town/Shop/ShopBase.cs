using System;
using System.Collections.Generic;
using SeaVillage.Data;
using UnityEngine;
using UnityEngine.Serialization;

namespace SeaVillage.Town
{
    /// <summary>
    /// 손님 시뮬레이션 축의 상점 공통 뼈대(IShop 구현). 포인트·단계·대기열·종업원을 소유하고
    /// 활성/비활성 시 상위 ShopManager 에 자기등록·해제한다.
    /// 플레이어 상호작용(패널 열기 등)은 같은 GameObject 의 Interactable 컴포넌트가 담당한다(병합 금지).
    /// </summary>
    public abstract class ShopBase : MonoBehaviour, IShop
    {
        private const float NormalShopMarkup = 1.1f;

        [Header("상점 설정")]
        [FormerlySerializedAs("shopType")]
        [SerializeField] private ShopType _shopType;

        [Header("단계 (연출 전용)")]
        [SerializeField] private ShopTier _tier = ShopTier.Tier1;

        [Header("영업 상태")]
        [SerializeField] private bool _isOpen = true;

        [Header("대기열")]
        [SerializeField] private int _maxQueueSize = 5;
        [SerializeField] private float _queueSpacing = 1.5f;

        [Header("포인트")]
        [FormerlySerializedAs("buyPointTrans")]
        [SerializeField] private Transform _sellerPoint;
        [SerializeField] private Transform _greeterPoint;

        private WaitingQueue _queue;
        private ShopManager _manager;

        /// <summary>상점 식별자. 기본은 직렬화 값이며, 타입이 고정된 파생은 override 로 선언한다.</summary>
        public virtual ShopType ShopType => _shopType;

        /// <summary>상점 단계. 기본은 직렬화 값이며, 파생이 외부 데이터에서 파생할 수 있다.</summary>
        public virtual ShopTier Tier => _tier;

        public abstract bool IsPlayerOwned { get; }

        /// <summary>영업 여부. 파생은 자기 도메인 조건을 추가로 걸 수 있다.</summary>
        public virtual bool IsOpen => _isOpen && gameObject.activeInHierarchy;
        public bool HasGreeter => _greeterPoint != null;

        public Vector2 BrowsePoint => HasGreeter
            ? (Vector2)_greeterPoint.position
            : (Vector2)_sellerPoint.position;

        public Vector2 PayPoint => (Vector2)_sellerPoint.position;

        public bool IsQueueFull => _queue.IsFull;

        /// <summary>대기열 정원. 기본은 직렬화 값이며, 파생이 정책으로 고정할 수 있다.</summary>
        protected virtual int QueueCapacity => _maxQueueSize;

        /// <summary>
        /// Unity 생명주기. 필수 참조인 SellerPoint 검증 후 대기열 생성.
        /// </summary>
        protected virtual void Awake()
        {
            if (_sellerPoint == null)
            {
                throw new InvalidOperationException(
                    $"[{GetType().Name}] {name}: SellerPoint 미할당");
            }

            _queue = new WaitingQueue(
                () => (Vector2)_sellerPoint.position,
                () => QueueCapacity,
                _queueSpacing);
        }

        /// <summary>
        /// 활성화 시 상위 ShopManager 에 자기 자신을 등록. 매니저 부재는 경고로 표면화한다.
        /// </summary>
        protected virtual void OnEnable()
        {
            _manager = GetComponentInParent<ShopManager>(true);
            if (_manager == null)
            {
                Debug.LogWarning($"[{GetType().Name}] {name}: 부모 계층에 ShopManager 없음 — 등록 생략");
                return;
            }

            _manager.Register(this);
        }

        /// <summary>
        /// 비활성화 시 ShopManager 에서 자기 자신을 해제.
        /// </summary>
        protected virtual void OnDisable()
        {
            _manager?.Unregister(this);
            _manager = null;
        }

        /// <summary>
        /// 영업 시작/종료. 등록(존재)과 별개의 명시적 상태다.
        /// </summary>
        public void SetOpen(bool isOpen)
        {
            if (_isOpen == isOpen) return;

            _isOpen = isOpen;
            _manager?.NotifyStateChanged(this);
        }

        public int EnterQueue(Action<int> onPositionChanged) => _queue.TryEnter(onPositionChanged);

        public void ExitQueue(Action<int> onPositionChanged) => _queue.Exit(onPositionChanged);

        public Vector2 GetQueuePosition(int position) => _queue.GetPosition(position);

        public virtual IReadOnlyList<ShopListing> GetListings(string townName)
        {
            if (!DataManager.HasInstance)
            {
                return Array.Empty<ShopListing>();
            }

            DataManager dataManager = DataManager.Instance;
            if (dataManager.ShopDatabase == null || dataManager.ItemDatabase == null)
            {
                return Array.Empty<ShopListing>();
            }

            List<ShopData> shopItems = dataManager.ShopDatabase.GetShopsById((int)ShopType);
            if (shopItems == null || shopItems.Count == 0)
            {
                return Array.Empty<ShopListing>();
            }

            var result = new List<ShopListing>(shopItems.Count);
            foreach (ShopData shopData in shopItems)
            {
                ItemData item = dataManager.ItemDatabase.GetItem(shopData.ItemID);
                if (item == null)
                {
                    continue;
                }

                int marketPrice = dataManager.GetItemPrice(item.PriceListID, townName);
                if (marketPrice <= 0)
                {
                    marketPrice = item.OriginPrice;
                }

                result.Add(new ShopListing(item.ID, Mathf.RoundToInt(marketPrice * NormalShopMarkup)));
            }

            return result;
        }
    }
}
