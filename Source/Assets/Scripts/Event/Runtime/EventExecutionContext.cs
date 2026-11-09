using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.Event
{
    public readonly struct EventRewardChange
    {
        public bool IsItem { get; }
        public int ItemId { get; }
        public string TargetKey { get; }
        public int Delta { get; }

        private EventRewardChange(bool isItem, int itemId, string targetKey, int delta)
        {
            IsItem = isItem;
            ItemId = itemId;
            TargetKey = targetKey;
            Delta = delta;
        }

        public static EventRewardChange ForItem(int itemId, int delta)
            => new EventRewardChange(true, itemId, null, delta);

        public static EventRewardChange ForStat(string targetKey, int delta)
            => new EventRewardChange(false, 0, targetKey ?? string.Empty, delta);

        public EventRewardChange WithDelta(int delta)
            => new EventRewardChange(IsItem, ItemId, TargetKey, delta);
    }

    /// <summary>
    /// 런타임에서 이벤트 실행 컨텍스트를 관리하기 위한 클래스
    /// </summary>
    public sealed class EventExecutionContext
    {
        private readonly Dictionary<int, List<EventSequenceData>> _steps;
        private readonly Dictionary<int, int> _pendingItemRemovals = new();

        public string RuntimeId { get; set; }

        public int CurrentStep { get; set; }
        public int NextStep { get; private set; }
        public TownKey CurrentTown { get; set; }
        public string LastInteractionNpcId { get; private set; }
        
        public EventData EventData { get; }
        public IReadOnlyList<EventSequenceData> Sequences { get; }
        public EventState State { get; set; }
        public bool WasQuestActiveAtStart { get; }
        public bool ShouldCompleteQuest { get; private set; }
        public int SubmittedItemId { get; private set; }

        public EventExecutionContext(
            EventData data,
            List<EventSequenceData> sequences,
            bool wasQuestActiveAtStart = false)
        {
            EventData = data;
            Sequences = sequences;
            WasQuestActiveAtStart = wasQuestActiveAtStart;
            _steps = sequences
                .GroupBy(x => x.Step)
                .ToDictionary(g => g.Key, g => g.ToList());
            State = EventState.Ready;
            NextStep = -1;
            CurrentTown = TownKey.Unknown;
        }

        public int GetPersistenceStep()
        {
            if (NextStep >= 0 && _steps.ContainsKey(NextStep))
                return NextStep;

            return _steps.ContainsKey(CurrentStep) ? CurrentStep : -1;
        }

        public List<EventSequenceData> GetSequencesByStep(int step)
        {
            return _steps.TryGetValue(step, out var list) ? list : null;
        }

        public bool TryGetNextStepAfter(int currentStep, out int nextStep)
        {
            nextStep = int.MaxValue;
            foreach (int step in _steps.Keys)
            {
                if (step > currentStep && step < nextStep)
                    nextStep = step;
            }

            if (nextStep != int.MaxValue)
                return true;

            nextStep = -1;
            return false;
        }

        public void SetNextStep(int step)
        {
            NextStep = step;
        }

        public void SetLastInteractionNpc(string npcId)
        {
            LastInteractionNpcId = string.IsNullOrWhiteSpace(npcId) ? null : npcId;
        }

        public void MarkQuestCompletionPending()
        {
            ShouldCompleteQuest = true;
        }

        public void SetSubmittedItem(int itemId)
        {
            SubmittedItemId = itemId > 0 ? itemId : 0;
        }

        public int GetPendingItemRemoval(int itemId)
        {
            return _pendingItemRemovals.TryGetValue(itemId, out int quantity) ? quantity : 0;
        }

        public void ReserveItemRemoval(int itemId, int quantity)
        {
            if (itemId <= 0 || quantity <= 0)
                return;

            _pendingItemRemovals[itemId] = GetPendingItemRemoval(itemId) + quantity;
        }

        public bool HasPendingItemRemovals => _pendingItemRemovals.Count > 0;

        public bool TryCommitPendingItemRemovals(InventoryData inventory)
        {
            if (_pendingItemRemovals.Count == 0)
                return true;
            if (inventory == null)
                return false;

            foreach (KeyValuePair<int, int> removal in _pendingItemRemovals)
            {
                if (!inventory.HasItem(removal.Key, removal.Value))
                    return false;
            }

            foreach (KeyValuePair<int, int> removal in _pendingItemRemovals)
            {
                if (!inventory.RemoveItem(removal.Key, removal.Value))
                    return false;
            }

            _pendingItemRemovals.Clear();
            return true;
        }

        public bool TryCommitPendingItemRemovals(
            InventoryData inventory,
            EventItemReservationLedger reservationLedger)
        {
            if (_pendingItemRemovals.Count == 0)
                return true;

            if (reservationLedger == null)
                return TryCommitPendingItemRemovals(inventory);

            if (!reservationLedger.TryCommit(RuntimeId, inventory))
                return false;

            _pendingItemRemovals.Clear();
            return true;
        }
    }

    /// <summary>
    /// 동시에 진행 중인 이벤트가 같은 인벤토리 아이템을 중복 제출하지 않도록 예약을 추적
    /// </summary>
    public sealed class EventItemReservationLedger
    {
        private readonly Dictionary<string, Dictionary<int, int>> _reservationsByOwner =
            new(System.StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, int> _reservedTotals = new();

        public bool TryReserve(string ownerId, int itemId, int quantity, InventoryData inventory)
        {
            if (string.IsNullOrWhiteSpace(ownerId)
                || itemId <= 0
                || quantity <= 0
                || inventory == null)
            {
                return false;
            }

            int ownerReserved = GetOwnerQuantity(ownerId, itemId);
            int totalReserved = GetTotalQuantity(itemId);
            int reservedByOtherOwners = totalReserved - ownerReserved;
            if (!inventory.HasItem(itemId, reservedByOtherOwners + quantity))
                return false;

            if (!_reservationsByOwner.TryGetValue(ownerId, out Dictionary<int, int> ownerReservations))
            {
                ownerReservations = new Dictionary<int, int>();
                _reservationsByOwner[ownerId] = ownerReservations;
            }

            ownerReservations[itemId] = ownerReserved + quantity;
            _reservedTotals[itemId] = totalReserved + quantity;
            return true;
        }

        public bool TryCommit(string ownerId, InventoryData inventory)
        {
            if (string.IsNullOrWhiteSpace(ownerId)
                || !_reservationsByOwner.TryGetValue(ownerId, out Dictionary<int, int> ownerReservations))
            {
                return true;
            }

            if (inventory == null)
                return false;

            foreach (KeyValuePair<int, int> reservation in ownerReservations)
            {
                if (!inventory.HasItem(reservation.Key, reservation.Value))
                    return false;
            }

            foreach (KeyValuePair<int, int> reservation in ownerReservations)
            {
                if (!inventory.RemoveItem(reservation.Key, reservation.Value))
                    return false;
            }

            Release(ownerId);
            return true;
        }

        public void Release(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId)
                || !_reservationsByOwner.TryGetValue(ownerId, out Dictionary<int, int> ownerReservations))
            {
                return;
            }

            foreach (KeyValuePair<int, int> reservation in ownerReservations)
            {
                int remaining = GetTotalQuantity(reservation.Key) - reservation.Value;
                if (remaining > 0)
                    _reservedTotals[reservation.Key] = remaining;
                else
                    _reservedTotals.Remove(reservation.Key);
            }

            _reservationsByOwner.Remove(ownerId);
        }

        private int GetOwnerQuantity(string ownerId, int itemId)
        {
            return _reservationsByOwner.TryGetValue(ownerId, out Dictionary<int, int> ownerReservations)
                && ownerReservations.TryGetValue(itemId, out int quantity)
                ? quantity
                : 0;
        }

        private int GetTotalQuantity(int itemId)
        {
            return _reservedTotals.TryGetValue(itemId, out int quantity) ? quantity : 0;
        }
    }

    /// <summary>
    /// 이벤트 컨텍스트가 공유 모달 UI를 동시에 점유하지 않도록 순서를 조정
    /// </summary>
    public sealed class EventUiCoordinator
    {
        private string _ownerId;

        public IEnumerator Acquire(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                yield break;

            while (!TryAcquire(ownerId))
                yield return null;
        }

        public bool TryAcquire(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
                return false;

            if (string.IsNullOrEmpty(_ownerId)
                || string.Equals(_ownerId, ownerId, System.StringComparison.OrdinalIgnoreCase))
            {
                _ownerId = ownerId;
                return true;
            }

            return false;
        }

        public void Release(string ownerId)
        {
            if (string.Equals(_ownerId, ownerId, System.StringComparison.OrdinalIgnoreCase))
                _ownerId = null;
        }

        public void Reset()
        {
            _ownerId = null;
        }
    }
}
