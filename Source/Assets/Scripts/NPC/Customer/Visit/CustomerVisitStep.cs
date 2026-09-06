using System;
using System.Collections;
using System.Collections.Generic;
using SeaVillage.Core;
using SeaVillage.Town;
using UnityEngine;

namespace SeaVillage.NPC.Process
{
    /// <summary>
    /// 선택한 목적지의 대기열, 이동, 예약, 방문 연출을 하나의 방문 단계에서 조율한다.
    /// 중단되거나 실패하면 보유한 예약과 실행 중인 동작을 함께 정리한다.
    /// </summary>
    public sealed class CustomerVisitStep : ICustomerProcessStep
    {
        private readonly CustomerProcessState _state;
        private readonly NPCDestinationSelector _selector;
        private readonly ICustomerVisitScheduler _scheduler;
        private readonly ICustomerMovement _movement;
        private readonly ICustomerVisitPresentation _presentation;
        private readonly Func<IShop, PurchaseResult> _evaluatePurchase;
        private readonly Action<PurchaseResult> _publishResult;
        private readonly Func<bool> _shouldExit;

        private Action<CustomerStepSignal> _onComplete;
        private int _generation;
        private int _movementGeneration;
        private bool _schedulerActive;
        private bool _movementActive;
        private bool _presentationActive;
        private IShop _activeShop;
        private NPCDestination _activeDestination;
        private PurchaseResult _capturedPurchase;
        private bool _hasCapturedPurchase;

        public CustomerVisitStep(
            CustomerProcessState state,
            NPCDestinationSelector selector,
            ICustomerVisitScheduler scheduler,
            ICustomerMovement movement,
            ICustomerVisitPresentation presentation,
            Func<IShop, PurchaseResult> evaluatePurchase,
            Action<PurchaseResult> publishResult,
            Func<bool> shouldExit)
        {
            _state = state;
            _selector = selector;
            _scheduler = scheduler;
            _movement = movement;
            _presentation = presentation;
            _evaluatePurchase = evaluatePurchase;
            _publishResult = publishResult;
            _shouldExit = shouldExit;
        }

        public void Start(Action<CustomerStepSignal> onComplete)
        {
            Stop();

            int generation = ++_generation;
            _onComplete = onComplete;

            NPCDestination? selectedDestination = _state.SelectedDestination;
            if (!selectedDestination.HasValue)
            {
                Complete(generation, CustomerStepSignal.BeginExit);
                return;
            }

            NPCDestination destination = selectedDestination.Value;
            if (destination.Type == DestinationType.Shop)
            {
                StartShopVisit(generation, destination);
                return;
            }

            Schedule(generation, destination, null);
        }

        public void Stop()
        {
            if (_onComplete == null
                && !_schedulerActive
                && !_movementActive
                && !_presentationActive)
            {
                return;
            }

            Stop(_generation);
        }

        private void StartShopVisit(int generation, NPCDestination destination)
        {
            IShop shop = destination.Shop;
            // 대기열 진입과 이탈에서 같은 델리게이트 인스턴스를 사용해야 예약이 정확히 해제된다.
            Action<int> queuePositionChanged = position =>
                OnQueuePositionChanged(generation, shop, position);
            int queuePosition = shop.EnterQueue(queuePositionChanged);

            if (queuePosition < 0)
            {
                Complete(generation, CustomerStepSignal.SelectAgain);
                return;
            }

            _state.SetSelection(
                destination,
                new VisitLease(() => shop.ExitQueue(queuePositionChanged)));

            _activeShop = shop;
            _activeDestination = destination;
            MoveToQueuePosition(generation, shop, queuePosition);
        }

        private void Schedule(
            int generation,
            NPCDestination destination,
            IShop shop)
        {
            if (!IsCurrent(generation))
            {
                return;
            }

            _schedulerActive = true;
            _presentationActive = true;
            _hasCapturedPurchase = false;
            _capturedPurchase = PurchaseResult.Empty;
            _scheduler.Run(
                VisitRoutine(generation, destination, shop),
                exception => OnSchedulerFault(generation, exception));
        }

        private IEnumerator VisitRoutine(
            int generation,
            NPCDestination destination,
            IShop shop)
        {
            IEnumerator presentationRoutine;
            if (shop == null)
            {
                presentationRoutine = _presentation.VisitBoard();
            }
            else
            {
                presentationRoutine = _presentation.VisitShop(
                    shop,
                    () => EvaluatePurchase(generation, shop),
                    result => CapturePurchase(generation, result));
            }

            if (!IsCurrent(generation))
            {
                yield break;
            }

            yield return presentationRoutine;

            if (!IsCurrent(generation))
            {
                yield break;
            }

            _presentationActive = false;
            bool didPurchase = false;
            if (shop != null)
            {
                PurchaseResult result = _hasCapturedPurchase
                    ? _capturedPurchase
                    : PurchaseResult.Empty;
                RecordPlayerShopOutcome(shop, result);
                _publishResult(result);
                if (!IsCurrent(generation))
                {
                    yield break;
                }

                didPurchase = shop.ShopType == ShopType.Restaurant || result.DidBuyAnything;
            }

            _selector.OnDestinationCompleted(destination, didPurchase);
            if (!IsCurrent(generation))
            {
                yield break;
            }

            _state.Reset();
            if (!IsCurrent(generation))
            {
                yield break;
            }

            bool shouldExit = _shouldExit();
            if (!IsCurrent(generation))
            {
                yield break;
            }

            Complete(
                generation,
                shouldExit
                    ? CustomerStepSignal.BeginExit
                    : CustomerStepSignal.SelectAgain);
        }

        private void Complete(int generation, CustomerStepSignal signal)
        {
            if (!IsCurrent(generation))
            {
                return;
            }

            _schedulerActive = false;
            _movementActive = false;
            _presentationActive = false;
            _movementGeneration++;
            _activeShop = null;
            Action<CustomerStepSignal> onComplete = _onComplete;
            _onComplete = null;
            onComplete(signal);
        }

        private void Stop(int generation)
        {
            if (!IsCurrent(generation))
            {
                return;
            }

            bool stopScheduler = _schedulerActive;
            bool stopMovement = _movementActive;
            bool stopPresentation = _presentationActive;
            _schedulerActive = false;
            _movementActive = false;
            _presentationActive = false;
            _movementGeneration++;
            _activeShop = null;
            _onComplete = null;
            _generation++;

            if (stopScheduler)
            {
                _scheduler.Stop();
            }

            if (stopPresentation)
            {
                _presentation.Stop();
            }

            if (stopMovement)
            {
                _movement.Stop();
            }

            _state.Reset();
        }

        private bool IsCurrent(int generation)
        {
            return _onComplete != null && generation == _generation;
        }

        private void OnQueuePositionChanged(
            int generation,
            IShop shop,
            int position)
        {
            if (!IsCurrent(generation)
                || !ReferenceEquals(shop, _activeShop)
                || _schedulerActive)
            {
                return;
            }

            MoveToQueuePosition(generation, shop, position);
        }

        private void MoveToQueuePosition(
            int generation,
            IShop shop,
            int position)
        {
            var target = shop.GetQueuePosition(position);
            if (!IsCurrent(generation) || !ReferenceEquals(shop, _activeShop))
            {
                return;
            }

            _movementActive = true;
            int movementGeneration = ++_movementGeneration;
            _movement.MoveTo(
                target,
                () => OnQueuePositionArrived(
                    generation,
                    movementGeneration,
                    shop,
                    position));
        }

        private void OnQueuePositionArrived(
            int generation,
            int movementGeneration,
            IShop shop,
            int position)
        {
            if (!IsCurrent(generation)
                || movementGeneration != _movementGeneration
                || !ReferenceEquals(shop, _activeShop)
                || !_movementActive)
            {
                return;
            }

            _movementActive = false;
            if (position != 0
                && shop.ShopType != ShopType.Restaurant
                && shop.ShopType != ShopType.TownHall)
            {
                return;
            }

            Schedule(generation, _activeDestination, shop);
        }

        private void OnSchedulerFault(int generation, Exception exception)
        {
            if (!IsCurrent(generation))
            {
                return;
            }

            _schedulerActive = false;
            _movementActive = false;
            bool stopPresentation = _presentationActive;
            _presentationActive = false;
            _movementGeneration++;
            _activeShop = null;

            Action<CustomerStepSignal> onComplete = _onComplete;
            _onComplete = null;
            _generation++;

            if (stopPresentation)
            {
                _presentation.Stop();
            }

            _state.Reset();
            onComplete(CustomerStepSignal.Cancelled);
        }

        private static void RecordPlayerShopOutcome(IShop shop, PurchaseResult result)
        {
            if (shop is not IPlayerShopSalesTarget target)
            {
                return;
            }

            if (!result.DidBuyAnything)
            {
                if (!target.TryRecordCustomerVisit(out string visitFailReason))
                {
                    Debug.LogWarning($"[CustomerVisitStep] 플레이어 가게 방문 기록 실패: {visitFailReason}");
                }

                return;
            }

            var quantitiesByItem = new Dictionary<int, int>();
            foreach (CartItem cartItem in result.Cart)
            {
                quantitiesByItem.TryGetValue(cartItem.Item.ID, out int quantity);
                quantitiesByItem[cartItem.Item.ID] = quantity + 1;
            }

            var requests = new List<PlayerShopSaleRequest>(quantitiesByItem.Count);
            foreach (KeyValuePair<int, int> pair in quantitiesByItem)
            {
                requests.Add(new PlayerShopSaleRequest(pair.Key, pair.Value));
            }

            if (target.TryRecordCustomerPurchase(
                    requests,
                    result.TotalCost,
                    out _,
                    out string purchaseFailReason))
            {
                return;
            }

            Debug.LogWarning($"[CustomerVisitStep] 플레이어 가게 구매 기록 실패: {purchaseFailReason}");
            if (!target.TryRecordCustomerVisit(out string fallbackFailReason))
            {
                Debug.LogWarning($"[CustomerVisitStep] 플레이어 가게 방문 기록 실패: {fallbackFailReason}");
            }
        }

        private void CapturePurchase(int generation, PurchaseResult result)
        {
            if (!IsCurrent(generation) || !_presentationActive)
            {
                return;
            }

            _capturedPurchase = result;
            _hasCapturedPurchase = true;
        }

        private PurchaseResult EvaluatePurchase(int generation, IShop shop)
        {
            if (!IsCurrent(generation) || !_presentationActive)
            {
                return PurchaseResult.Empty;
            }

            PurchaseResult result = _evaluatePurchase(shop);
            return IsCurrent(generation) && _presentationActive
                ? result
                : PurchaseResult.Empty;
        }
    }
}
