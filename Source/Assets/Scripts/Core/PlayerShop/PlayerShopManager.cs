using System;
using System.Collections.Generic;
using UnityEngine;
using SeaVillage.Data;
using SeaVillage.NPC;
using SeaVillage.Utilities;

namespace SeaVillage.Core
{
    /// <summary>PlayerShop 운영 상태를 마을 단위로 관리하는 클래스</summary>
    public class PlayerShopManager : Singleton<PlayerShopManager>
    {
        [Header("PlayerShop Rules")]
        [SerializeField] private int _level1SlotCapacity = 5;
        [SerializeField] private int _level2SlotCapacity = 7;
        [SerializeField] private int _level1QueueCapacity = 3;
        [SerializeField] private int _level2QueueCapacity = 5;


        private readonly Dictionary<TownKey, PlayerShopStateByTown> _shopStatesByTown = new();
        private readonly Dictionary<int, StaffProfile> _hiredStaffById = new();
        private readonly Dictionary<TownKey, IReadOnlyList<PlayerShopSaleOffer>> _saleOfferCacheByTown = new();
        private bool _readyNotified;

        public bool IsReady { get; private set; }
        public event Action OnReady;

        /// <summary>
        /// 단일 마을 상태 변경 시 TownKey와 함께 발생
        /// </summary>
        public event Action<TownKey> OnShopStateChanged;

        protected override void Awake()
        {
            base.Awake();
            MarkReady();
        }

        private void OnEnable()
        {
            TimeManager.OnDayChanged += HandleDayChanged;
        }

        private void OnDisable()
        {
            TimeManager.OnDayChanged -= HandleDayChanged;
        }

        public bool TryBuildShop(out string failReason)
        {
            return TryBuildShop(GetCurrentTownKey(), out failReason);
        }

        public bool TryBuildShop(TownKey townKey, out string failReason)
        {
            failReason = string.Empty;

            if (townKey == TownKey.Unknown)
            {
                failReason = "[Error] PlayerShop 마을 식별자가 유효하지 않습니다";
                return false;
            }

            if (!TownProgressionManager.HasInstance)
            {
                failReason = "[Error] 마을 진행 관리자를 찾을 수 없습니다";
                return false;
            }

            int affinity = TownProgressionManager.Instance.GetAffinity(townKey);
            if (affinity < TownAffinityRules.PlayerShopRequiredAffinity)
            {
                failReason = $"호감도 {TownAffinityRules.PlayerShopRequiredAffinity} 이상 필요";
                return false;
            }

            // 건설 아이템은 카탈로그가 권위
            PlayerShopUpgradeDefinition upgradeDef = GetUpgradeDefinition(townKey);
            int buildItemId = upgradeDef?.BuildItemId ?? 0;

            if (buildItemId <= 0)
            {
                failReason = "[Error] 건설 아이템 ID가 유효하지 않습니다";
                return false;
            }

            PlayerShopStateByTown state = GetOrCreateState(townKey);
            if (state.IsBuilt)
            {
                failReason = "이곳에는 이미 내 가게가 있다";
                return false;
            }

            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;
            if (playerInventory == null || !playerInventory.HasItem(buildItemId, 1))
            {
                failReason = "가게를 짓는 데 필요한 아이템이 부족하다";
                return false;
            }

            if (!playerInventory.RemoveItem(buildItemId, 1))
            {
                failReason = "[Error] 건설 아이템 소모 처리에 실패했습니다";
                return false;
            }

            InitializeBuiltState(state, TimeManager.HasInstance ? TimeManager.Instance.CurrentDay : 1, townKey);
            OnShopStateChanged?.Invoke(townKey);
            return true;
        }

        public bool TryUpgradeShop(TownKey townKey, out string failReason)
        {
            failReason = string.Empty;

            if (townKey == TownKey.Unknown)
            {
                failReason = "[Error] PlayerShop 마을 식별자가 유효하지 않습니다";
                return false;
            }

            // 업그레이드 아이템은 카탈로그가 권위
            PlayerShopUpgradeDefinition upgradeDef = GetUpgradeDefinition(townKey);
            int upgradeItemId = upgradeDef?.UpgradeItemId ?? 0;

            if (upgradeItemId <= 0)
            {
                failReason = "[Error] 업그레이드 아이템 ID가 유효하지 않습니다";
                return false;
            }

            PlayerShopStateByTown state = GetMutableState(townKey);

            if (state == null || !state.IsBuilt)
            {
                failReason = "내 가게가 있어야 업그레이드할 수 있다";
                return false;
            }

            if (state.buildStage >= PlayerShopBuildStage.Upgraded)
            {
                failReason = "내 가게가 이미 최대 레벨이다";
                return false;
            }

            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;
            if (playerInventory == null || !playerInventory.HasItem(upgradeItemId, 1))
            {
                failReason = "업그레이드에 필요한 마을 전용 아이템이 부족하다";
                return false;
            }

            if (!playerInventory.RemoveItem(upgradeItemId, 1))
            {
                failReason = "[Error] 업그레이드 아이템 소모 처리에 실패했습니다";
                return false;
            }

            state.buildStage = PlayerShopBuildStage.Upgraded;
            state.slotCapacity = upgradeDef != null && upgradeDef.Level2SlotCapacity > 0 ? upgradeDef.Level2SlotCapacity : _level2SlotCapacity;
            state.queueCapacity = upgradeDef != null && upgradeDef.Level2QueueCapacity > 0 ? upgradeDef.Level2QueueCapacity : _level2QueueCapacity;
            OnShopStateChanged?.Invoke(townKey);
            return true;
        }

        public bool IsStaffHired(int staffId)
        {
            if (staffId <= 0)
                return false;

            return _hiredStaffById.ContainsKey(staffId);
        }

        /// <summary>
        /// 고용된 직원 목록
        /// </summary>
        public IReadOnlyCollection<StaffInfo> HiredStaff
        {
            get
            {
                var readOnlyProfiles = new List<StaffInfo>(_hiredStaffById.Count);
                foreach (StaffProfile profile in _hiredStaffById.Values)
                    if (profile != null)
                        readOnlyProfiles.Add(new StaffInfo(profile));

                return readOnlyProfiles;
            }
        }

        /// <summary>
        /// 고용된 직원 프로필, 없으면 null
        /// </summary>
        public StaffInfo? GetHiredStaff(int staffId)
            => staffId > 0 && _hiredStaffById.TryGetValue(staffId, out StaffProfile profile) && profile != null
                ? new StaffInfo(profile)
                : null;

        /// <summary>
        /// 플레이어 가게의 역할 배치 정보, 없으면 null
        /// </summary>
        public StaffInfo? GetAssignment(TownKey townKey, StaffRole role)
        {
            PlayerShopStateByTown state = GetMutableState(townKey);
            if (state == null)
                return null;

            int staffId = role == StaffRole.Cashier ? state.cashierStaffId : state.salesStaffId;
            StaffProfile profile = GetAssignedStaffProfile(staffId);
            return profile != null ? new StaffInfo(profile) : null;
        }

        internal bool CanApplyStaffStatItem(
            int staffId,
            StaffStatType statType,
            int amount,
            int itemId,
            out string failReason)
        {
            failReason = string.Empty;

            if (staffId <= 0 || amount <= 0 || itemId <= 0
                || statType != StaffStatType.Intelligence && statType != StaffStatType.Charm)
            {
                failReason = "[Error] 직원 스탯 아이템 정보가 유효하지 않습니다";
                return false;
            }

            if (!_hiredStaffById.TryGetValue(staffId, out StaffProfile profile) || profile == null)
            {
                failReason = "고용하지 않은 직원이다";
                return false;
            }

            if (profile.HasUsedStatItem(itemId))
            {
                failReason = "대상에게 이미 사용한 아이템입니다.";
                return false;
            }

            int current = statType == StaffStatType.Intelligence ? profile.intelligence : profile.charm;
            if (current > int.MaxValue - amount)
            {
                failReason = "[Error] 직원 능력치가 유효 범위를 초과합니다";
                return false;
            }

            return true;
        }

        internal bool TryApplyStaffStatItem(
            int staffId,
            StaffStatType statType,
            int amount,
            int itemId,
            out string failReason)
        {
            if (!CanApplyStaffStatItem(staffId, statType, amount, itemId, out failReason))
                return false;

            StaffProfile profile = _hiredStaffById[staffId];
            if (statType == StaffStatType.Intelligence)
                profile.intelligence += amount;
            else
                profile.charm += amount;

            profile.usedStatItemIds ??= new HashSet<int>();
            profile.usedStatItemIds.Add(itemId);
            NotifyStaffProfileChanged(staffId);
            return true;
        }

        /// <summary>
        /// 플레이어 가게 직원 효과 조회
        /// </summary>
        public StaffEffectReadOnly GetStaffEffects(TownKey townKey)
        {
            PlayerShopStateByTown state = GetMutableState(townKey);
            if (state == null || !state.IsBuilt)
                return StaffEffectReadOnly.Empty;

            StaffProfile cashierProfile = GetAssignedStaffProfile(state.cashierStaffId);
            StaffProfile salesProfile = GetAssignedStaffProfile(state.salesStaffId);

            return new StaffEffectReadOnly(
                cashierProfile != null,
                cashierProfile?.intelligence ?? 0,
                salesProfile != null,
                salesProfile?.charm ?? 0);
        }

        /// <summary>
        /// 오프라인 판매 시 가정할 방문 손님 수 조회
        /// </summary>
        public int GetOfflineCustomerCount(TownKey townKey)
        {
            PlayerShopStateByTown state = GetMutableState(townKey);
            return state == null || !state.IsBuilt
                ? 0
                : PlayerShopOperatingRules.GetOfflineCustomerCount(state.buildStage);
        }

        /// <summary>
        /// NPC 구매 판단용 판매 상품 조회
        /// </summary>
        public IReadOnlyList<PlayerShopSaleOffer> GetSaleOffers(TownKey townKey)
        {
            PlayerShopStateByTown state = GetMutableState(townKey);
            if (state == null || !state.IsBuilt || !DataManager.HasInstance)
                return Array.Empty<PlayerShopSaleOffer>();

            if (_saleOfferCacheByTown.TryGetValue(townKey, out IReadOnlyList<PlayerShopSaleOffer> cachedOffers))
                return cachedOffers;

            if (state.listedItems == null || state.listedItems.Count == 0)
            {
                IReadOnlyList<PlayerShopSaleOffer> emptyOffers = Array.Empty<PlayerShopSaleOffer>();
                _saleOfferCacheByTown[townKey] = emptyOffers;
                return emptyOffers;
            }

            var offers = new List<PlayerShopSaleOffer>(state.listedItems.Count);
            for (int i = 0; i < state.listedItems.Count; i++)
            {
                PlayerShopItem listed = state.listedItems[i];
                if (listed == null || listed.itemId <= 0 || listed.quantity <= 0 || listed.unitPrice <= 0)
                    continue;

                ItemData item = DataManager.Instance.GetItem(listed.itemId);
                if (item == null)
                    continue;

                offers.Add(new PlayerShopSaleOffer(
                    listed.itemId,
                    item,
                    listed.unitPrice,
                    listed.quantity,
                    Mathf.Max(0, listed.averagePurchasePrice)));
            }

            IReadOnlyList<PlayerShopSaleOffer> readOnlyOffers = offers.AsReadOnly();
            _saleOfferCacheByTown[townKey] = readOnlyOffers;
            return readOnlyOffers;
        }

        /// <summary>
        /// 한 손님의 구매 결과를 플레이어 가게 매출로 확정
        /// </summary>
        public bool TryRecordCustomerPurchase(
            TownKey townKey,
            int itemId,
            out PlayerShopSaleResult receipt,
            out string failReason)
        {
            return TryRecordCustomerPurchase(
                townKey,
                new[] { new PlayerShopSaleRequest(itemId, 1) },
                -1L,
                out receipt,
                out failReason);
        }

        /// <summary>
        /// 손님 한 명의 장바구니와 방문을 함께 반영
        /// </summary>
        public bool TryRecordCustomerPurchase(
            TownKey townKey,
            IReadOnlyList<PlayerShopSaleRequest> requests,
            out PlayerShopSaleResult receipt,
            out string failReason)
        {
            return TryRecordCustomerPurchase(
                townKey,
                requests,
                -1L,
                out receipt,
                out failReason);
        }

        public bool TryRecordCustomerPurchase(
            TownKey townKey,
            IReadOnlyList<PlayerShopSaleRequest> requests,
            long customerPayment,
            out PlayerShopSaleResult receipt,
            out string failReason)
        {
            receipt = null;
            failReason = string.Empty;

            if (!TryPrepareCustomerVisit(townKey, out PlayerShopStateByTown state, out failReason))
                return false;

            state.pendingCustomerCount++;
            if (TryRecordSaleBatch(townKey, requests, customerPayment, out receipt, out failReason))
                return true;

            state.pendingCustomerCount--;
            return false;
        }


        /// <summary>
        /// 손님의 가게 방문을 정산 손님 수에 반영
        /// </summary>
        public bool TryRecordCustomerVisit(TownKey townKey, out string failReason)
        {
            if (!TryPrepareCustomerVisit(townKey, out PlayerShopStateByTown state, out failReason))
                return false;

            state.pendingCustomerCount++;
            OnShopStateChanged?.Invoke(townKey);
            return true;
        }


        public bool TryHireStaff(TownKey townKey, StaffProfile profile, long contractCost, out string failReason)
        {
            failReason = string.Empty;

            if (townKey == TownKey.Unknown)
            {
                failReason = "[Error] PlayerShop 마을 식별자가 유효하지 않습니다";
                return false;
            }

            if (profile == null || profile.staffId <= 0)
            {
                failReason = "[Error] 직원 정보가 유효하지 않습니다";
                return false;
            }

            StaffCatalog staffCatalog = DataManager.HasInstance ? DataManager.Instance.StaffCatalog : null;
            if (staffCatalog == null || !staffCatalog.TryGetByStaffId(profile.staffId, out StaffDefinition definition))
            {
                failReason = "[Error] 직원 카탈로그 정보를 찾을 수 없습니다";
                return false;
            }

            int affinity = TownProgressionManager.HasInstance
                ? TownProgressionManager.Instance.GetAffinity(townKey)
                : 0;
            if (affinity < definition.RequiredLoveLevel)
            {
                failReason = $"호감도 {definition.RequiredLoveLevel} 이상 필요";
                return false;
            }

            PlayerShopStateByTown state = GetMutableState(townKey);
            if (state == null || !state.IsBuilt)
            {
                failReason = "[Error] 내 가게를 찾을 수 없습니다";
                return false;
            }

            if (IsStaffHired(profile.staffId))
            {
                failReason = "[Error] 이미 고용된 직원입니다.";
                return false;
            }

            if (!CurrencyManager.Instance.TrySpendPlayer(CurrencyType.Gold, Math.Max(0L, contractCost)))
            {
                failReason = "계약금이 부족하다.";
                return false;
            }

            _hiredStaffById[profile.staffId] = new StaffProfile
            {
                staffId = profile.staffId,
                intelligence = profile.intelligence,
                charm = profile.charm,
                isTownHired = profile.isTownHired,
                usedStatItemIds = profile.usedStatItemIds != null
                    ? new HashSet<int>(profile.usedStatItemIds)
                    : new HashSet<int>(),
            };
            OnShopStateChanged?.Invoke(townKey);
            return true;
        }

        /// <summary>
        /// 인벤토리의 직원(인형)을 등록
        /// </summary>
        public bool TryRegisterStaffFromItem(TownKey townKey, int staffId, out string failReason)
        {
            failReason = string.Empty;

            if (townKey == TownKey.Unknown)
            {
                failReason = "[Error] PlayerShop 마을 식별자가 유효하지 않습니다";
                return false;
            }

            PlayerShopStateByTown state = GetMutableState(townKey);
            if (state == null || !state.IsBuilt)
            {
                failReason = "내 가게가 있어야 직원을 등록할 수 있다";
                return false;
            }

            StaffCatalog catalog = DataManager.HasInstance ? DataManager.Instance.StaffCatalog : null;
            if (catalog == null || !catalog.TryGetByStaffId(staffId, out StaffDefinition definition) || !definition.IsItemStaff)
            {
                failReason = "[Error] 등록할 직원 정보가 유효하지 않습니다";
                return false;
            }

            if (IsStaffHired(staffId))
            {
                failReason = "이미 등록한 직원이다";
                return false;
            }

            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;
            if (playerInventory == null)
            {
                failReason = "[Error] 인벤토리를 찾을 수 없습니다";
                return false;
            }

            if (!playerInventory.HasItem(definition.RequiredItemId, 1))
            {
                failReason = "등록할 인형이 없다";
                return false;
            }

            if (!playerInventory.RemoveItem(definition.RequiredItemId, 1))
            {
                failReason = "[Error] 등록 아이템 소모 처리에 실패했습니다";
                return false;
            }

            _hiredStaffById[staffId] = new StaffProfile
            {
                staffId = definition.StaffId,
                intelligence = Mathf.Max(0, definition.Intelligence),
                charm = Mathf.Max(0, definition.Charm),
                isTownHired = false,
            };

            OnShopStateChanged?.Invoke(townKey);
            return true;
        }

        /// <summary>
        /// 직원을 역할에 배치하고 다른 역할에 있으면 두 역할을 교환
        /// </summary>
        public bool TryAssignStaff(TownKey townKey, StaffRole role, int staffId, out string failReason)
        {
            failReason = string.Empty;

            if (townKey == TownKey.Unknown)
            {
                failReason = "[Error] PlayerShop 마을 식별자가 유효하지 않습니다";
                return false;
            }

            PlayerShopStateByTown state = GetMutableState(townKey);

            if (state == null || !state.IsBuilt)
            {
                failReason = "내 가게가 있어야 직원을 배치할 수 있다";
                return false;
            }

            if (!_hiredStaffById.TryGetValue(staffId, out StaffProfile profile))
            {
                failReason = "고용하지 않은 직원이다";
                return false;
            }

            if (role == StaffRole.Sales && state.ShopLevel < 2)
            {
                failReason = "영업 담당 자리는 Lv2부터 쓸 수 있다";
                return false;
            }

            if (role == StaffRole.Cashier)
            {
                int previousCashierStaffId = state.cashierStaffId;
                if (state.salesStaffId == staffId)
                    state.salesStaffId = previousCashierStaffId;

                state.cashierStaffId = profile.staffId;
            }
            else
            {
                int previousSalesStaffId = state.salesStaffId;
                if (state.cashierStaffId == staffId)
                    state.cashierStaffId = previousSalesStaffId;

                state.salesStaffId = profile.staffId;
            }

            OnShopStateChanged?.Invoke(townKey);
            return true;
        }

        /// <summary>
        /// 특정 슬롯의 등록 아이템, 비어 있으면 null
        /// </summary>
        public PlayerShopItemReadOnly GetSlotItem(TownKey townKey, int slotIndex)
            => GetState(townKey)?.GetSlotItem(slotIndex);

        /// <summary>
        /// 슬롯을 지정 아이템으로 제자리 설정(선택하기 확정), 기존 아이템은 가방 회수·새 아이템은 가방 차감
        /// </summary>
        public bool TrySetSlotItem(TownKey townKey, int slotIndex, int itemId, int quantity, int unitPrice, out string failReason)
        {
            failReason = string.Empty;

            PlayerShopStateByTown state = GetMutableState(townKey);
            if (state == null || !state.IsBuilt)
            {
                failReason = "내 가게가 있어야 재고를 등록할 수 있다";
                return false;
            }

            if (slotIndex < 0 || slotIndex >= state.slotCapacity)
            {
                failReason = "사용할 수 없는 칸이다";
                return false;
            }

            if (itemId <= 0 || quantity <= 0 || unitPrice <= 0)
            {
                failReason = "[Error] 등록 정보가 유효하지 않습니다";
                return false;
            }

            // 같은 아이템이 다른 칸에 이미 있으면 차단(아이템당 한 칸)
            PlayerShopItem duplicate = state.GetListedItem(itemId);
            if (duplicate != null && duplicate.slotIndex != slotIndex)
            {
                failReason = "이미 다른 칸에 등록된 아이템이다";
                return false;
            }

            InventoryData inventory = InventoryManager.PlayerInventoryOrNull;
            if (inventory == null || !inventory.Items.TryGetValue(itemId, out InventoryItem invItem) || invItem.quantity < quantity)
            {
                failReason = "등록할 아이템이 부족하다";
                return false;
            }

            PlayerShopItem existing = state.GetSlotItem(slotIndex);

            // 다른 아이템으로 교체 시 기존 칸 아이템을 가방으로 회수
            if (existing != null && existing.itemId != itemId && existing.quantity > 0
                && !inventory.AddItem(existing.itemId, existing.quantity, existing.averagePurchasePrice))
            {
                failReason = "가방이 가득 차서 교체할 수 없다";
                return false;
            }

            if (!inventory.RemoveItem(itemId, quantity))
            {
                failReason = "[Error] 재고 아이템 이동에 실패했습니다";
                return false;
            }

            if (existing != null)
                state.listedItems.Remove(existing);

            state.listedItems.Add(new PlayerShopItem
            {
                slotIndex = slotIndex,
                itemId = itemId,
                quantity = quantity,
                unitPrice = unitPrice,
                averagePurchasePrice = Math.Max(0, invItem.averagePurchasePrice),
            });

            InvalidateSaleOfferCache(townKey);
            OnShopStateChanged?.Invoke(townKey);
            return true;
        }

        /// <summary>
        /// 옮기기: 슬롯의 같은 아이템을 가방에서 지정 수량만큼 추가하고 판매가 갱신
        /// </summary>
        public bool TryAddToSlotItem(TownKey townKey, int slotIndex, int quantity, int unitPrice, out string failReason)
        {
            failReason = string.Empty;

            PlayerShopStateByTown state = GetMutableState(townKey);
            if (state == null || !state.IsBuilt)
            {
                failReason = "내 가게가 있어야 재고를 등록할 수 있다";
                return false;
            }

            PlayerShopItem existing = state.GetSlotItem(slotIndex);
            if (existing == null)
            {
                failReason = "옮길 대상 칸이 비어 있다";
                return false;
            }

            if (quantity <= 0 || unitPrice <= 0)
            {
                failReason = "[Error] 수량/가격이 유효하지 않습니다";
                return false;
            }

            InventoryData inventory = InventoryManager.PlayerInventoryOrNull;
            if (inventory == null || !inventory.Items.TryGetValue(existing.itemId, out InventoryItem invItem) || invItem.quantity < quantity)
            {
                failReason = "옮길 아이템이 부족하다";
                return false;
            }

            if (quantity > int.MaxValue - existing.quantity)
            {
                failReason = "[Error] 가게 재고 수량이 유효 범위를 초과했습니다";
                return false;
            }

            int previousQuantity = existing.quantity;
            int addedAveragePurchasePrice = Math.Max(0, invItem.averagePurchasePrice);
            int newQuantity = previousQuantity + quantity;
            long weightedCost = (long)Math.Max(0, existing.averagePurchasePrice) * previousQuantity
                + (long)addedAveragePurchasePrice * quantity;

            if (!inventory.RemoveItem(existing.itemId, quantity))
            {
                failReason = "[Error] 재고 아이템 이동에 실패했습니다";
                return false;
            }

            existing.quantity = newQuantity;
            existing.averagePurchasePrice = (int)(weightedCost / newQuantity);
            existing.unitPrice = unitPrice;
            InvalidateSaleOfferCache(townKey);
            OnShopStateChanged?.Invoke(townKey);
            return true;
        }

        /// <summary>
        /// 정산할 매출 존재 여부 확인
        /// </summary>
        public bool HasPendingSettlement(TownKey townKey)
        {
            if (townKey == TownKey.Unknown)
                return false;

            PlayerShopStateByTown state = GetMutableState(townKey);
            return state != null && state.IsBuilt && state.pendingRevenue > 0;
        }

        public bool TryGetSettlementReadOnly(TownKey townKey, out PlayerShopSalesRecord settlement, out string failReason)
        {
            settlement = null;
            failReason = string.Empty;

            if (townKey == TownKey.Unknown)
            {
                failReason = "[Error] PlayerShop 마을 식별자가 유효하지 않습니다";
                return false;
            }

            PlayerShopStateByTown state = GetMutableState(townKey);
            if (state == null || !state.IsBuilt)
            {
                failReason = "내 가게가 없다";
                return false;
            }

            if (state.settlementItems == null)
                state.settlementItems = new List<PlayerShopSettlementItem>();

            int currentDay = TimeManager.HasInstance ? TimeManager.Instance.CurrentDay : state.lastSettlementDay;
            int elapsedDays = Mathf.Max(0, currentDay - state.lastVisitDay);
            var soldItems = new List<PlayerShopItemSalesRecord>(state.settlementItems.Count);
            for (int i = 0; i < state.settlementItems.Count; i++)
            {
                PlayerShopSettlementItem settlementItem = state.settlementItems[i];
                if (settlementItem == null || settlementItem.itemId <= 0 || settlementItem.soldQuantity <= 0)
                    continue;

                soldItems.Add(new PlayerShopItemSalesRecord(settlementItem.itemId, settlementItem.soldQuantity));
            }

            settlement = new PlayerShopSalesRecord(
                elapsedDays,
                Mathf.Max(0, state.pendingCustomerCount),
                Mathf.Max(0, state.pendingRevenue),
                Mathf.Max(0, state.pendingTip),
                Mathf.Max(0, state.pendingPurchaseCost),
                soldItems);
            return true;
        }

        public bool TryCollectSettlement(TownKey townKey, out int collectedGold, out string failReason)
        {
            collectedGold = 0;
            failReason = string.Empty;

            if (!TryGetSettlementReadOnly(townKey, out PlayerShopSalesRecord settlement, out failReason))
                return false;

            int reward = settlement.Reward;
            if (!CurrencyManager.Instance.TryAddPlayer(CurrencyType.Gold, reward))
            {
                failReason = "[Error] 매출 수령 처리에 실패했습니다";
                return false;
            }

            PlayerShopStateByTown state = GetMutableState(townKey);
            state.pendingRevenue = 0;
            state.pendingTip = 0;
            state.pendingPurchaseCost = 0;
            state.pendingCustomerCount = 0;
            state.settlementItems?.Clear();
            state.lastVisitDay = TimeManager.HasInstance ? TimeManager.Instance.CurrentDay : state.lastSettlementDay;
            collectedGold = reward;
            OnShopStateChanged?.Invoke(townKey);
            return true;
        }

        /// <summary>
        /// 등록 아이템의 마을별 현재 시세
        /// </summary>
        public int GetListedMarketPrice(TownKey townKey, int itemId)
        {
            PlayerShopStateByTown state = GetMutableState(townKey);
            ItemData itemData = itemId > 0 ? DataManager.Instance.GetItem(itemId) : null;
            if (state == null || itemData == null)
                return 0;

            string marketTown = ResolveMarketTown(state.townKey, itemData.Town);
            return DataManager.Instance.GetItemPrice(itemData.PriceListID, marketTown);
        }

        /// <summary>
        /// 마을의 플레이어 가게 읽기 전용 상태 반환
        /// </summary>
        public PlayerShopStateReadOnly GetState(TownKey townKey)
        {
            PlayerShopStateByTown state = GetMutableState(townKey);
            return state == null
                ? null
                : new PlayerShopStateReadOnly(
                    state,
                    GetAssignedStaffProfile(state.cashierStaffId),
                    GetAssignedStaffProfile(state.salesStaffId));
        }


        public bool IsShopBuilt(TownKey townKey)
        {
            PlayerShopStateByTown state = GetMutableState(townKey);
            return state != null && state.IsBuilt;
        }


        public int GetShopLevel(TownKey townKey)
        {
            PlayerShopStateByTown state = GetMutableState(townKey);
            return state?.ShopLevel ?? 0;
        }

        /// <summary>
        /// 마을의 플레이어 가게 대기열 수용량 반환
        /// </summary>
        public int GetQueueCapacity(TownKey townKey)
        {
            PlayerShopStateByTown state = GetMutableState(townKey);
            return state?.queueCapacity ?? 0;
        }

        public List<PlayerShopData> ExportSaveData()
        {
            var result = new List<PlayerShopData>(_shopStatesByTown.Count);
            CopySaveDataTo(result);
            return result;
        }

        public void CopySaveDataTo(List<PlayerShopData> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            int shopIndex = 0;
            foreach (PlayerShopStateByTown state in _shopStatesByTown.Values)
            {
                PlayerShopData shopData = SaveSnapshotList.GetOrCreate(target, shopIndex++);
                shopData.townKey = TownKeyUtility.ToStorageKey(state.townKey);
                shopData.buildStage = (int)state.buildStage;
                shopData.pendingRevenue = state.pendingRevenue;
                shopData.pendingTip = state.pendingTip;
                shopData.pendingPurchaseCost = state.pendingPurchaseCost;
                shopData.pendingCustomerCount = state.pendingCustomerCount;
                shopData.totalRevenue = state.totalRevenue;
                shopData.lastSettlementDay = state.lastSettlementDay;
                shopData.lastVisitDay = state.lastVisitDay;
                shopData.dailySoldCount = state.dailySoldCount;
                shopData.totalSoldCount = state.totalSoldCount;
                shopData.slotCapacity = state.slotCapacity;
                shopData.queueCapacity = state.queueCapacity;
                shopData.cashierStaffId = state.cashierStaffId;
                shopData.salesStaffId = state.salesStaffId;
                shopData.listedItems ??= new List<PlayerShopItemData>();
                shopData.settlementItems ??= new List<PlayerShopSettlementItemData>();

                for (int i = 0; i < state.listedItems.Count; i++)
                {
                    PlayerShopItem listed = state.listedItems[i];
                    PlayerShopItemData savedItem = SaveSnapshotList.GetOrCreate(shopData.listedItems, i);
                    savedItem.slotIndex = listed.slotIndex;
                    savedItem.itemId = listed.itemId;
                    savedItem.quantity = listed.quantity;
                    savedItem.unitPrice = listed.unitPrice;
                    savedItem.averagePurchasePrice = listed.averagePurchasePrice;
                }
                SaveSnapshotList.Trim(shopData.listedItems, state.listedItems.Count);

                int settlementIndex = 0;
                for (int i = 0; i < state.settlementItems.Count; i++)
                {
                    PlayerShopSettlementItem settlementItem = state.settlementItems[i];
                    if (settlementItem == null || settlementItem.itemId <= 0 || settlementItem.soldQuantity <= 0)
                        continue;

                    PlayerShopSettlementItemData savedSettlement =
                        SaveSnapshotList.GetOrCreate(shopData.settlementItems, settlementIndex++);
                    savedSettlement.itemId = settlementItem.itemId;
                    savedSettlement.soldQuantity = settlementItem.soldQuantity;
                }
                SaveSnapshotList.Trim(shopData.settlementItems, settlementIndex);
            }

            SaveSnapshotList.Trim(target, shopIndex);
        }

        public List<StaffData> ExportHiredStaffSaveData()
        {
            var result = new List<StaffData>(_hiredStaffById.Count);
            CopyHiredStaffSaveDataTo(result);
            return result;
        }

        public void CopyHiredStaffSaveDataTo(List<StaffData> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            int index = 0;
            foreach (StaffProfile profile in _hiredStaffById.Values)
            {
                if (profile == null || profile.staffId <= 0)
                    continue;

                StaffData saved = SaveSnapshotList.GetOrCreate(target, index++);
                saved.staffId = profile.staffId;
                saved.intelligence = profile.intelligence;
                saved.charm = profile.charm;
                saved.isTownHired = profile.isTownHired;
                saved.usedStatItemIds ??= new List<int>();
                saved.usedStatItemIds.Clear();
                if (profile.usedStatItemIds != null)
                {
                    foreach (int itemId in profile.usedStatItemIds)
                        saved.usedStatItemIds.Add(itemId);
                }
            }

            SaveSnapshotList.Trim(target, index);
        }

        public void ImportSaveData(List<PlayerShopData> savedShopData, List<StaffData> hiredStaff)
        {
            _shopStatesByTown.Clear();
            _hiredStaffById.Clear();
            _saleOfferCacheByTown.Clear();
            int currentDay = TimeManager.HasInstance ? TimeManager.Instance.CurrentDay : 1;

            if (savedShopData != null)
            {
                for (int i = 0; i < savedShopData.Count; i++)
                {
                    PlayerShopData shopData = savedShopData[i];
                    if (shopData == null)
                        continue;

                    if (!TownKeyUtility.TryParse(shopData.townKey, out TownKey resolvedTownKey))
                        continue;

                    PlayerShopBuildStage buildStage = (PlayerShopBuildStage)Mathf.Clamp(shopData.buildStage, (int)PlayerShopBuildStage.None, (int)PlayerShopBuildStage.Upgraded);
                    if (buildStage == PlayerShopBuildStage.None || _shopStatesByTown.ContainsKey(resolvedTownKey))
                        continue;

                    var state = new PlayerShopStateByTown
                    {
                        townKey = resolvedTownKey,
                        buildStage = buildStage,
                        pendingRevenue = shopData.pendingRevenue,
                        pendingTip = shopData.pendingTip,
                        pendingPurchaseCost = shopData.pendingPurchaseCost,
                        pendingCustomerCount = shopData.pendingCustomerCount,
                        totalRevenue = shopData.totalRevenue,
                        lastSettlementDay = shopData.lastSettlementDay,
                        lastVisitDay = shopData.lastVisitDay > 0
                            ? shopData.lastVisitDay
                            : (shopData.lastSettlementDay > 0 ? shopData.lastSettlementDay : currentDay),
                        dailySoldCount = shopData.dailySoldCount,
                        totalSoldCount = shopData.totalSoldCount,
                        slotCapacity = shopData.slotCapacity > 0 ? shopData.slotCapacity : _level1SlotCapacity,
                        queueCapacity = shopData.queueCapacity > 0 ? shopData.queueCapacity : _level1QueueCapacity,
                        settlementItems = new List<PlayerShopSettlementItem>(),
                    };

                    // 배치값은 상태에만 복원, 풀은 hiredStaff로만 재구성(기본 직원 유령 주입 방지)
                    state.cashierStaffId = shopData.cashierStaffId;
                    state.salesStaffId = shopData.salesStaffId;

                    if (shopData.listedItems != null)
                    {
                        for (int itemIndex = 0; itemIndex < shopData.listedItems.Count; itemIndex++)
                        {
                            PlayerShopItemData listed = shopData.listedItems[itemIndex];
                            if (listed == null || listed.quantity <= 0)
                                continue;

                            state.listedItems.Add(new PlayerShopItem
                            {
                                slotIndex = listed.slotIndex,
                                itemId = listed.itemId,
                                quantity = listed.quantity,
                                unitPrice = listed.unitPrice,
                                averagePurchasePrice = listed.averagePurchasePrice,
                            });
                        }
                    }

                    if (shopData.settlementItems != null)
                    {
                        for (int itemIndex = 0; itemIndex < shopData.settlementItems.Count; itemIndex++)
                        {
                            PlayerShopSettlementItemData settlementItem = shopData.settlementItems[itemIndex];
                            if (settlementItem == null || settlementItem.itemId <= 0 || settlementItem.soldQuantity <= 0)
                                continue;

                            state.settlementItems.Add(new PlayerShopSettlementItem
                            {
                                itemId = settlementItem.itemId,
                                soldQuantity = settlementItem.soldQuantity,
                            });
                        }
                    }

                    _shopStatesByTown.Add(resolvedTownKey, state);
                }
            }

            if (hiredStaff != null)
            {
                for (int i = 0; i < hiredStaff.Count; i++)
                {
                    StaffData savedStaff = hiredStaff[i];
                    if (savedStaff == null || savedStaff.staffId <= 0)
                        continue;

                    RegisterHiredStaff(
                        savedStaff.staffId,
                        savedStaff.intelligence,
                        savedStaff.charm,
                        savedStaff.isTownHired,
                        savedStaff.usedStatItemIds);
                }
            }

            NormalizeLoadedStates(currentDay);
        }

        private void RegisterHiredStaff(
            int staffId,
            int intelligence,
            int charm,
            bool isTownHired,
            List<int> usedStatItemIds)
        {
            if (staffId <= 0 || _hiredStaffById.ContainsKey(staffId))
                return;

            _hiredStaffById[staffId] = new StaffProfile
            {
                staffId = staffId,
                intelligence = StaffRules.NormalizeIntelligence(intelligence),
                charm = StaffRules.NormalizeCharm(charm),
                isTownHired = isTownHired,
                usedStatItemIds = CreateUsedStatItemSet(usedStatItemIds),
            };
        }

        private bool TryPrepareCustomerVisit(
            TownKey townKey,
            out PlayerShopStateByTown state,
            out string failReason)
        {
            state = null;
            failReason = string.Empty;

            if (townKey == TownKey.Unknown)
            {
                failReason = "[Error] 플레이어 가게 마을 식별자가 유효하지 않습니다";
                return false;
            }

            state = GetMutableState(townKey);
            if (state == null || !state.IsBuilt)
            {
                failReason = "플레이어 가게가 없다";
                return false;
            }

            if (!CanAddAmount(state.pendingCustomerCount, 1))
            {
                failReason = "[Error] 방문 손님 수가 유효 범위를 초과했습니다";
                return false;
            }

            return true;
        }

        private bool TryRecordSaleBatch(
            TownKey townKey,
            IReadOnlyList<PlayerShopSaleRequest> requests,
            long customerPayment,
            out PlayerShopSaleResult receipt,
            out string failReason)
        {
            receipt = null;
            failReason = string.Empty;

            if (townKey == TownKey.Unknown)
            {
                failReason = "[Error] PlayerShop 마을 식별자가 유효하지 않습니다";
                return false;
            }

            PlayerShopStateByTown state = GetMutableState(townKey);
            if (state == null || !state.IsBuilt)
            {
                failReason = "내 가게가 없다";
                return false;
            }

            if (requests == null || requests.Count == 0)
            {
                failReason = "[Error] 판매 확정 요청이 비어 있습니다";
                return false;
            }

            var quantitiesByItem = new Dictionary<int, int>();
            int requestedQuantity = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                PlayerShopSaleRequest request = requests[i];
                if (request.ItemId <= 0 || request.Quantity <= 0)
                {
                    failReason = "[Error] 판매 확정 요청의 아이템 정보가 유효하지 않습니다";
                    return false;
                }

                if (request.Quantity > PlayerShopOperatingRules.MaxItemsPerCustomerPurchase
                    || requestedQuantity > PlayerShopOperatingRules.MaxItemsPerCustomerPurchase - request.Quantity)
                {
                    failReason = $"[Error] 손님 한 명은 상품을 최대 {PlayerShopOperatingRules.MaxItemsPerCustomerPurchase}개까지 구매할 수 있습니다";
                    return false;
                }

                requestedQuantity += request.Quantity;

                if (quantitiesByItem.TryGetValue(request.ItemId, out int currentQuantity))
                {
                    if (request.Quantity > int.MaxValue - currentQuantity)
                    {
                        failReason = "[Error] 판매 수량이 유효 범위를 초과했습니다";
                        return false;
                    }

                    quantitiesByItem[request.ItemId] = currentQuantity + request.Quantity;
                }
                else
                {
                    quantitiesByItem.Add(request.ItemId, request.Quantity);
                }
            }

            long maxPendingSoldQuantity = (long)Mathf.Max(0, state.pendingCustomerCount)
                * PlayerShopOperatingRules.MaxItemsPerCustomerPurchase;
            if (GetPendingSoldQuantity(state) > maxPendingSoldQuantity - requestedQuantity)
            {
                failReason = "[Error] 방문한 손님보다 많은 상품을 판매할 수 없습니다";
                return false;
            }

            long revenue = 0;
            long purchaseCost = 0;
            int soldQuantity = 0;

            foreach (KeyValuePair<int, int> pair in quantitiesByItem)
            {
                PlayerShopItem listed = state.GetListedItem(pair.Key);
                if (listed == null || listed.quantity < pair.Value || listed.unitPrice <= 0)
                {
                    failReason = "판매하려는 아이템의 재고가 부족하다";
                    return false;
                }

                if (pair.Value > int.MaxValue - soldQuantity)
                {
                    failReason = "[Error] 판매 수량이 유효 범위를 초과했습니다";
                    return false;
                }

                soldQuantity += pair.Value;
                revenue += (long)listed.unitPrice * pair.Value;
                purchaseCost += (long)Mathf.Max(0, listed.averagePurchasePrice) * pair.Value;
            }

            int revenueValue = ToIntAmount(revenue);
            int purchaseCostValue = ToIntAmount(purchaseCost);
            if (revenueValue < 0 || purchaseCostValue < 0)
            {
                failReason = "[Error] 판매 금액이 유효 범위를 초과했습니다";
                return false;
            }

            long intelligenceBonus = customerPayment >= 0L
                ? customerPayment - revenue
                : StaffRules.CalculateIntelligenceBonus(
                    revenue,
                    GetAssignedStaffProfile(state.cashierStaffId)?.intelligence ?? 0);
            int tipValue = ToIntAmount(intelligenceBonus);
            int grossRevenueValue = tipValue >= 0 && CanAddAmount(revenueValue, tipValue)
                ? revenueValue + tipValue
                : -1;

            if (grossRevenueValue < 0
                || !CanAddAmount(state.pendingRevenue, revenueValue)
                || !CanAddAmount(state.pendingTip, tipValue)
                || !CanAddAmount(state.pendingPurchaseCost, purchaseCostValue)
                || !CanAddAmount(state.dailySoldCount, soldQuantity)
                || !CanAddAmount(state.totalSoldCount, soldQuantity)
                || !CanAddAmount(state.totalRevenue, grossRevenueValue))
            {
                failReason = "[Error] 플레이어 가게 누적 금액이 유효 범위를 초과했습니다";
                return false;
            }

            foreach (KeyValuePair<int, int> pair in quantitiesByItem)
            {
                PlayerShopItem listed = state.GetListedItem(pair.Key);
                listed.quantity -= pair.Value;
                AddSettlementItem(state, pair.Key, pair.Value);

                if (listed.quantity <= 0)
                    state.listedItems.Remove(listed);
            }

            int previousTotalSoldCount = state.totalSoldCount;
            state.pendingRevenue += revenueValue;
            state.pendingTip += tipValue;
            state.pendingPurchaseCost += purchaseCostValue;
            state.dailySoldCount += soldQuantity;
            state.totalSoldCount += soldQuantity;
            state.totalRevenue += grossRevenueValue;
            InvalidateSaleOfferCache(townKey);

            if (TownProgressionManager.HasInstance)
                TownProgressionManager.Instance.RecordSoldItems(townKey, previousTotalSoldCount, state.totalSoldCount);

            receipt = new PlayerShopSaleResult(
                soldQuantity,
                revenueValue,
                tipValue,
                purchaseCostValue);

            OnShopStateChanged?.Invoke(townKey);
            return true;
        }
        private void NormalizeLoadedStates(int currentDay)
        {
            int normalizedCurrentDay = Math.Max(1, currentDay);
            foreach (PlayerShopStateByTown state in _shopStatesByTown.Values)
                NormalizeLoadedState(state, normalizedCurrentDay);
        }

        /// <summary>
        /// 특정 날짜에 대한 플레이어 가게 정보를 정규화한다
        /// </summary>
        private void NormalizeLoadedState(PlayerShopStateByTown state, int currentDay)
        {
            if (state == null)
                return;

            state.listedItems ??= new List<PlayerShopItem>();
            state.settlementItems ??= new List<PlayerShopSettlementItem>();

            state.buildStage = (PlayerShopBuildStage)Mathf.Clamp(
                (int)state.buildStage,
                (int)PlayerShopBuildStage.Built,
                (int)PlayerShopBuildStage.Upgraded);

            PlayerShopUpgradeDefinition upgradeDef = GetUpgradeDefinition(state.townKey);
            bool isUpgraded = state.buildStage == PlayerShopBuildStage.Upgraded;
            int fallbackSlotCapacity = isUpgraded ? _level2SlotCapacity : _level1SlotCapacity;
            int fallbackQueueCapacity = isUpgraded ? _level2QueueCapacity : _level1QueueCapacity;
            int catalogSlotCapacity = isUpgraded ? upgradeDef?.Level2SlotCapacity ?? 0 : upgradeDef?.Level1SlotCapacity ?? 0;
            int catalogQueueCapacity = isUpgraded ? upgradeDef?.Level2QueueCapacity ?? 0 : upgradeDef?.Level1QueueCapacity ?? 0;

            state.slotCapacity = Math.Max(1, catalogSlotCapacity > 0 ? catalogSlotCapacity : fallbackSlotCapacity);
            state.queueCapacity = Math.Max(1, catalogQueueCapacity > 0 ? catalogQueueCapacity : fallbackQueueCapacity);

            state.pendingRevenue = Math.Max(0, state.pendingRevenue);
            state.pendingTip = Math.Max(0, state.pendingTip);
            state.pendingPurchaseCost = Math.Max(0, state.pendingPurchaseCost);
            state.pendingCustomerCount = Math.Max(0, state.pendingCustomerCount);
            int pendingGrossRevenue = CanAddAmount(state.pendingRevenue, state.pendingTip)
                ? state.pendingRevenue + state.pendingTip
                : int.MaxValue;
            state.totalRevenue = Math.Max(pendingGrossRevenue, Math.Max(0, state.totalRevenue));
            state.dailySoldCount = Math.Max(0, state.dailySoldCount);
            state.totalSoldCount = Math.Max(state.dailySoldCount, Math.Max(0, state.totalSoldCount));

            state.lastSettlementDay = NormalizeDay(state.lastSettlementDay, state.lastVisitDay, currentDay);
            state.lastVisitDay = NormalizeDay(state.lastVisitDay, state.lastSettlementDay, currentDay);
            if (state.lastSettlementDay < state.lastVisitDay)
                state.lastSettlementDay = state.lastVisitDay;

            NormalizeListedItems(state);
            NormalizeSettlementItems(state);

            int pendingSoldQuantity = GetPendingSoldQuantity(state);
            long minimumCustomerCountLong = ((long)pendingSoldQuantity + PlayerShopOperatingRules.MaxItemsPerCustomerPurchase - 1)
                / PlayerShopOperatingRules.MaxItemsPerCustomerPurchase;
            int minimumCustomerCount = (int)Math.Min(int.MaxValue, minimumCustomerCountLong);
            state.pendingCustomerCount = Math.Max(state.pendingCustomerCount, minimumCustomerCount);
            state.totalSoldCount = Math.Max(state.totalSoldCount, pendingSoldQuantity);

            state.cashierStaffId = NormalizeStaffId(state.cashierStaffId);
            state.salesStaffId = NormalizeStaffId(state.salesStaffId);
            if (state.cashierStaffId > 0 && state.cashierStaffId == state.salesStaffId)
                state.salesStaffId = 0;
        }

        private static int NormalizeDay(int savedDay, int fallbackDay, int currentDay)
        {
            int normalizedDay = savedDay > 0 ? savedDay : fallbackDay;
            return Mathf.Clamp(normalizedDay > 0 ? normalizedDay : currentDay, 1, currentDay);
        }

        private int NormalizeStaffId(int staffId)
        {
            return staffId > 0
                && _hiredStaffById.TryGetValue(staffId, out StaffProfile profile)
                && profile != null
                    ? staffId
                    : 0;
        }

        private static void NormalizeListedItems(PlayerShopStateByTown state)
        {
            var normalizedItems = new List<PlayerShopItem>(state.listedItems.Count);
            var usedSlots = new HashSet<int>();
            var usedItemIds = new HashSet<int>();

            for (int i = 0; i < state.listedItems.Count; i++)
            {
                PlayerShopItem item = state.listedItems[i];
                if (item == null
                    || item.slotIndex < 0
                    || item.slotIndex >= state.slotCapacity
                    || item.itemId <= 0
                    || item.quantity <= 0
                    || item.unitPrice <= 0
                    || !usedSlots.Add(item.slotIndex)
                    || !usedItemIds.Add(item.itemId))
                {
                    continue;
                }

                normalizedItems.Add(new PlayerShopItem
                {
                    slotIndex = item.slotIndex,
                    itemId = item.itemId,
                    quantity = item.quantity,
                    unitPrice = item.unitPrice,
                    averagePurchasePrice = Math.Max(0, item.averagePurchasePrice),
                });
            }

            state.listedItems = normalizedItems;
        }

        private static void NormalizeSettlementItems(PlayerShopStateByTown state)
        {
            var normalizedItems = new List<PlayerShopSettlementItem>(state.settlementItems.Count);
            var itemIndexes = new Dictionary<int, int>();

            for (int i = 0; i < state.settlementItems.Count; i++)
            {
                PlayerShopSettlementItem item = state.settlementItems[i];
                if (item == null || item.itemId <= 0 || item.soldQuantity <= 0)
                    continue;

                if (itemIndexes.TryGetValue(item.itemId, out int normalizedIndex))
                {
                    PlayerShopSettlementItem normalizedItem = normalizedItems[normalizedIndex];
                    normalizedItem.soldQuantity = SaturatingAdd(normalizedItem.soldQuantity, item.soldQuantity);
                    continue;
                }

                itemIndexes.Add(item.itemId, normalizedItems.Count);
                normalizedItems.Add(new PlayerShopSettlementItem
                {
                    itemId = item.itemId,
                    soldQuantity = item.soldQuantity,
                });
            }

            state.settlementItems = normalizedItems;
        }

        private static int SaturatingAdd(int left, int right)
        {
            if (left <= 0)
                return Math.Max(0, right);
            if (right <= 0)
                return left;
            return left > int.MaxValue - right ? int.MaxValue : left + right;
        }

        /// <summary>
        /// 플레이어가 자리를 비운 하루를 손님 단위로 재현한다.
        /// 손님 선택은 스폰 확률, 구매 판단은 마을 씬 NPC와 같은 로직을 쓴다.
        /// </summary>
        private void SimulateOneDay(PlayerShopStateByTown state)
        {
            if (state == null)
                return;

            state.dailySoldCount = 0;

            CustomerDatabase customerDatabase = DataManager.HasInstance
                ? DataManager.Instance.CustomerDatabase
                : null;
            if (customerDatabase == null)
            {
                Debug.LogWarning($"[PlayerShopManager] 오프라인 판매를 위한 고객 데이터베이스가 없습니다: town={state.townKey}");
                return;
            }

            string townName = TownKeyUtility.ToStorageKey(state.townKey);
            if (string.IsNullOrWhiteSpace(townName))
            {
                Debug.LogWarning($"[PlayerShopManager] 오프라인 판매 마을 식별자가 유효하지 않습니다: town={state.townKey}");
                return;
            }

            int loveLevel = TownProgressionManager.HasInstance
                ? TownProgressionManager.Instance.GetAffinity(state.townKey)
                : 0;
            StaffEffectReadOnly staffEffects = GetStaffEffects(state.townKey);
            int dailyCustomerCount = PlayerShopOperatingRules.GetOfflineCustomerCount(state.buildStage);

            for (int i = 0; i < dailyCustomerCount; i++)
            {
                if (!CustomerProfileResolver.TryResolve(customerDatabase, townName, loveLevel, out CustomerData profile))
                {
                    Debug.LogWarning($"[PlayerShopManager] 오프라인 손님 선택 실패: town={townName}, loveLv={loveLevel}");
                    break;
                }

                // 판매 목록은 손님마다 다시 읽어야 앞선 손님이 사간 재고가 반영된다.
                PurchaseResult purchase = CustomerPurchaseLogic.Evaluate(
                    profile,
                    GetSaleOffers(state.townKey),
                    staffEffects,
                    townName,
                    profile.Money);

                if (!purchase.DidBuyAnything)
                {
                    if (!TryRecordCustomerVisit(state.townKey, out string visitFailReason))
                        Debug.LogWarning($"[PlayerShopManager] 오프라인 손님 방문 기록 실패: town={townName}, reason={visitFailReason}");

                    continue;
                }

                List<PlayerShopSaleRequest> requests = BuildSaleRequests(purchase);
                if (!TryRecordCustomerPurchase(
                    state.townKey,
                    requests,
                    purchase.TotalCost,
                    out _,
                    out string saleFailReason))
                {
                    Debug.LogWarning($"[PlayerShopManager] 오프라인 판매 기록 실패: town={townName}, reason={saleFailReason}");
                    if (!TryRecordCustomerVisit(state.townKey, out string fallbackVisitFailReason))
                    {
                        Debug.LogWarning($"[PlayerShopManager] 오프라인 판매 실패 후 방문 기록도 실패했습니다: town={townName}, reason={fallbackVisitFailReason}");
                    }
                }
            }
        }

        private static List<PlayerShopSaleRequest> BuildSaleRequests(PurchaseResult purchase)
        {
            var quantitiesByItem = new Dictionary<int, int>();
            if (purchase.Cart == null)
                return new List<PlayerShopSaleRequest>();

            for (int i = 0; i < purchase.Cart.Count; i++)
            {
                CartItem cartItem = purchase.Cart[i];
                if (cartItem.Item == null || cartItem.Item.ID <= 0)
                    continue;

                int itemId = cartItem.Item.ID;
                quantitiesByItem.TryGetValue(itemId, out int quantity);
                quantitiesByItem[itemId] = quantity + 1;
            }

            var requests = new List<PlayerShopSaleRequest>(quantitiesByItem.Count);
            foreach (KeyValuePair<int, int> pair in quantitiesByItem)
                requests.Add(new PlayerShopSaleRequest(pair.Key, pair.Value));

            return requests;
        }

        private static bool IsPlayerPresentInTown(TownKey townKey)
        {
            return GameManager.HasInstance
                && GameManager.Instance.CurrentGameState == GameState.Town
                && GameManager.Instance.CurrentTownKey == townKey;
        }

        private static int GetPendingSoldQuantity(PlayerShopStateByTown state)
        {
            if (state?.settlementItems == null)
                return 0;

            long total = 0;
            for (int i = 0; i < state.settlementItems.Count; i++)
            {
                PlayerShopSettlementItem settlementItem = state.settlementItems[i];
                if (settlementItem == null || settlementItem.soldQuantity <= 0)
                    continue;

                total += settlementItem.soldQuantity;
                if (total >= int.MaxValue)
                    return int.MaxValue;
            }

            return (int)total;
        }

        private static void AddSettlementItem(PlayerShopStateByTown state, int itemId, int soldQuantity)
        {
            if (state == null || itemId <= 0 || soldQuantity <= 0)
                return;

            state.settlementItems ??= new List<PlayerShopSettlementItem>();

            for (int i = 0; i < state.settlementItems.Count; i++)
            {
                PlayerShopSettlementItem settlementItem = state.settlementItems[i];
                if (settlementItem != null && settlementItem.itemId == itemId)
                {
                    settlementItem.soldQuantity += soldQuantity;
                    return;
                }
            }

            state.settlementItems.Add(new PlayerShopSettlementItem
            {
                itemId = itemId,
                soldQuantity = soldQuantity,
            });
        }

        private static int ToIntAmount(long amount)
        {
            return amount < 0 || amount > int.MaxValue ? -1 : (int)amount;
        }

        private static bool CanAddAmount(int current, int amount)
        {
            return current >= 0 && amount >= 0 && current <= int.MaxValue - amount;
        }

        private StaffProfile GetAssignedStaffProfile(int staffId)
        {
            if (staffId <= 0)
                return null;

            return _hiredStaffById.TryGetValue(staffId, out StaffProfile profile)
                ? profile
                : null;
        }

        private void NotifyStaffProfileChanged(int staffId)
        {
            foreach (KeyValuePair<TownKey, PlayerShopStateByTown> pair in _shopStatesByTown)
            {
                PlayerShopStateByTown state = pair.Value;
                if (state == null)
                    continue;

                bool isAssigned = state.cashierStaffId == staffId || state.salesStaffId == staffId;
                if (isAssigned)
                    OnShopStateChanged?.Invoke(pair.Key);
            }
        }

        private static HashSet<int> CreateUsedStatItemSet(List<int> itemIds)
        {
            var result = new HashSet<int>();
            if (itemIds == null)
                return result;

            for (int i = 0; i < itemIds.Count; i++)
            {
                int itemId = itemIds[i];
                if (itemId > 0)
                    result.Add(itemId);
            }

            return result;
        }

        private PlayerShopStateByTown GetOrCreateState(TownKey townKey)
        {
            if (townKey == TownKey.Unknown)
                return null;

            if (_shopStatesByTown.TryGetValue(townKey, out PlayerShopStateByTown state))
                return state;

            state = new PlayerShopStateByTown
            {
                townKey = townKey,
                buildStage = PlayerShopBuildStage.None,
                pendingRevenue = 0,
                lastVisitDay = TimeManager.HasInstance ? TimeManager.Instance.CurrentDay : 1,
                lastSettlementDay = TimeManager.HasInstance ? TimeManager.Instance.CurrentDay : 1,
                slotCapacity = _level1SlotCapacity,
                queueCapacity = _level1QueueCapacity,
            };

            _shopStatesByTown.Add(townKey, state);
            return state;
        }

        /// <summary>
        /// 마을에 해당하는 업그레이드 카탈로그 정의
        /// </summary>
        private static PlayerShopUpgradeDefinition GetUpgradeDefinition(TownKey townKey)
        {
            if (townKey == TownKey.Unknown || !DataManager.HasInstance)
                return null;

            PlayerShopUpgradeCatalog catalog = DataManager.Instance.PlayerShopUpgradeCatalog;
            return catalog != null && catalog.TryGetByTown(townKey, out PlayerShopUpgradeDefinition definition) ? definition : null;
        }

        /// <summary>
        /// PlayerShop의 상태 초기화
        /// </summary>
        private void InitializeBuiltState(PlayerShopStateByTown state, int currentDay, TownKey townKey)
        {
            state.townKey = townKey != TownKey.Unknown ? townKey : state.townKey;
            state.buildStage = PlayerShopBuildStage.Built;
            state.pendingRevenue = 0;
            state.pendingTip = 0;
            state.pendingPurchaseCost = 0;
            state.pendingCustomerCount = 0;
            state.totalRevenue = 0;
            state.lastSettlementDay = Mathf.Max(1, currentDay);
            state.lastVisitDay = Mathf.Max(1, currentDay);

            PlayerShopUpgradeDefinition upgradeDef = GetUpgradeDefinition(state.townKey);
            state.slotCapacity = upgradeDef != null && upgradeDef.Level1SlotCapacity > 0 ? upgradeDef.Level1SlotCapacity : _level1SlotCapacity;
            state.queueCapacity = upgradeDef != null && upgradeDef.Level1QueueCapacity > 0 ? upgradeDef.Level1QueueCapacity : _level1QueueCapacity;

            state.dailySoldCount = 0;
            state.totalSoldCount = 0;

            state.cashierStaffId = 0;
            state.salesStaffId = 0;

            state.listedItems.Clear();
            state.settlementItems.Clear();
        }

        private static string ResolveMarketTown(TownKey preferredTown, string fallbackTown)
        {
            string preferred = TownKeyUtility.ToStorageKey(preferredTown);
            return string.IsNullOrEmpty(preferred) ? TownKeyUtility.NormalizeStorageKey(fallbackTown) : preferred;
        }

        private static TownKey GetCurrentTownKey()
        {
            return GameManager.HasInstance ? GameManager.Instance.CurrentTownKey : TownKey.Unknown;
        }

        private void MarkReady()
        {
            if (_readyNotified)
                return;

            IsReady = true;
            _readyNotified = true;
            OnReady?.Invoke();
        }

        private PlayerShopStateByTown GetMutableState(TownKey townKey)
        {
            if (townKey == TownKey.Unknown)
                return null;

            _shopStatesByTown.TryGetValue(townKey, out PlayerShopStateByTown state);
            return state;
        }

        private void InvalidateSaleOfferCache(TownKey townKey)
        {
            if (townKey != TownKey.Unknown)
                _saleOfferCacheByTown.Remove(townKey);
        }

        private void HandleDayChanged(int currentDay)
        {
            foreach (PlayerShopStateByTown state in _shopStatesByTown.Values)
            {
                if (!state.IsBuilt)
                    continue;

                if (IsPlayerPresentInTown(state.townKey))
                {
                    if (state.lastSettlementDay < currentDay)
                    {
                        state.lastSettlementDay = currentDay;
                        OnShopStateChanged?.Invoke(state.townKey);
                    }

                    continue;
                }

                if (state.lastSettlementDay >= currentDay)
                    continue;

                while (state.lastSettlementDay < currentDay)
                {
                    SimulateOneDay(state);
                    state.lastSettlementDay++;
                }

                OnShopStateChanged?.Invoke(state.townKey);
            }
        }

    }

    internal static class StaffRegistrationPolicy
    {
        public static List<StaffDefinition> GetRegisterableDollStaff(int excludedStaffId)
        {
            var result = new List<StaffDefinition>();
            if (!DataManager.HasInstance || !PlayerShopManager.HasInstance)
                return result;

            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;
            StaffCatalog staffCatalog = DataManager.Instance.StaffCatalog;
            SpecialShopContentCatalog dollCatalog = DataManager.Instance.SpecialShopContentCatalog;
            if (playerInventory == null || staffCatalog == null || dollCatalog?.Dolls == null)
                return result;

            IReadOnlyList<DollRewardDefinition> dolls = dollCatalog.Dolls;
            for (int i = 0; i < dolls.Count; i++)
            {
                DollRewardDefinition doll = dolls[i];
                if (doll == null || doll.StaffId <= 0 || doll.StaffId == excludedStaffId)
                    continue;

                if (!staffCatalog.TryGetByStaffId(doll.StaffId, out StaffDefinition staffDefinition)
                    || !staffDefinition.IsItemStaff
                    || PlayerShopManager.Instance.IsStaffHired(staffDefinition.StaffId)
                    || !playerInventory.HasItem(staffDefinition.RequiredItemId, 1))
                    continue;

                result.Add(staffDefinition);
            }

            return result;
        }
    }
}
