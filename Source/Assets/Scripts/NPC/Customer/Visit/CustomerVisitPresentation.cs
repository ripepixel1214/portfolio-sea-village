using System;
using System.Collections;
using SeaVillage.Core;
using SeaVillage.NPC.Components;
using SeaVillage.Town;
using SeaVillage.Utilities;
using Spine.Unity;
using UnityEngine;

namespace SeaVillage.NPC.Process
{
    /// <summary>
    /// 방문 연출에서 필요한 대화 표시 기능에 대한 계약이다.
    /// </summary>
    public interface ICustomerVisitDialogue
    {
        void PlayBoardText(string town);
        void PlayThinking();
        void PlayProgress(float duration);
        void PlayCost(long amount);
        void Hide();
    }

    /// <summary>
    /// 목적지 종류에 맞는 애니메이션, 대화, 결제 연출을 코루틴으로 실행한다.
    /// </summary>
    public sealed class CustomerVisitPresentation : ICustomerVisitPresentation
    {
        private const float FadeDuration = 0.5f;
        private const float RestaurantUseDuration = 5f;
        private const float TownHallMinDuration = 3f;
        private const float TownHallMaxDuration = 7f;

        private readonly NPCMove _move;
        private readonly ICustomerVisitDialogue _dialogue;
        private readonly SkeletonAnimation _skeleton;
        private readonly Account _account;
        private readonly NPCBehaviourConfig _config;
        private readonly string _townName;
        private readonly Action<PurchaseResult> _playEmotion;
        private readonly Action _playTownHallFeedback;
        private readonly Func<float> _selectTownHallDuration;

        public CustomerVisitPresentation(
            NPCMove move,
            ICustomerVisitDialogue dialogue,
            SkeletonAnimation skeleton,
            Account account,
            NPCBehaviourConfig config,
            string townName,
            Action<PurchaseResult> playEmotion,
            Action playTownHallFeedback,
            Func<float> selectTownHallDuration = null)
        {
            _move = move;
            _dialogue = dialogue;
            _skeleton = skeleton;
            _account = account;
            _config = config;
            _townName = townName;
            _playEmotion = playEmotion;
            _playTownHallFeedback = playTownHallFeedback;
            _selectTownHallDuration = selectTownHallDuration ??
                (() => UnityEngine.Random.Range(TownHallMinDuration, TownHallMaxDuration));
        }

        public IEnumerator VisitShop(
            IShop shop,
            Func<PurchaseResult> evaluatePurchase,
            Action<PurchaseResult> captureResult)
        {
            return shop.ShopType switch
            {
                ShopType.Restaurant => VisitRestaurant(evaluatePurchase, captureResult),
                ShopType.TownHall => VisitTownHall(captureResult),
                _ => VisitGeneral(shop, evaluatePurchase, captureResult)
            };
        }

        public IEnumerator VisitBoard()
        {
            _dialogue.PlayThinking();
            yield return new WaitForSeconds(_config.StepDuration);

            _dialogue.Hide();
            _dialogue.PlayBoardText(_townName);
            yield return new WaitForSeconds(_config.ReactionDisplayDuration);

            if (_config.BoardPostTalkDelay > 0f)
            {
                yield return new WaitForSeconds(_config.BoardPostTalkDelay);
            }
        }

        public void Stop()
        {
            _move.Stop();
            _dialogue.Hide();
            _skeleton.Skeleton.A = 1f;
        }

        private IEnumerator VisitGeneral(
            IShop shop,
            Func<PurchaseResult> evaluatePurchase,
            Action<PurchaseResult> captureResult)
        {
            StaffEffectReadOnly effects = (shop as IPlayerShopSalesTarget)?.StaffEffects
                ?? StaffEffectReadOnly.Empty;
            float thinkingTime = effects.SalesThinkingTime > 0f
                ? effects.SalesThinkingTime
                : _config.StepDuration;
            float calculationTime = effects.CashierCalculationTime > 0f
                ? effects.CashierCalculationTime
                : _config.StepDuration;

            var stepWait = new WaitForSeconds(_config.StepDuration);
            var gapWait = new WaitForSeconds(_config.StepGap);

            _dialogue.PlayThinking();
            yield return new WaitForSeconds(thinkingTime);
            yield return gapWait;

            PurchaseResult result = evaluatePurchase();
            captureResult(result);
            _dialogue.Hide();

            if (result.DidBuyAnything)
            {
                bool arrived = !shop.HasGreeter;
                if (shop.HasGreeter)
                {
                    _move.Movement(shop.PayPoint, () => arrived = true);
                }

                yield return new WaitUntil(() => arrived);

                _dialogue.PlayProgress(calculationTime);
                yield return new WaitForSeconds(calculationTime);
                Pay(result);
                yield return gapWait;

                _dialogue.PlayCost(result.TotalCost);
                yield return stepWait;
            }

            yield return Feedback(result);
        }

        private IEnumerator VisitRestaurant(
            Func<PurchaseResult> evaluatePurchase,
            Action<PurchaseResult> captureResult)
        {
            var stepWait = new WaitForSeconds(_config.StepDuration);

            PurchaseResult result = evaluatePurchase();
            captureResult(result);
            _dialogue.Hide();

            yield return _skeleton.FadeAlphaRoutine(1f, 0f, FadeDuration);
            yield return new WaitForSeconds(RestaurantUseDuration);
            yield return _skeleton.FadeAlphaRoutine(0f, 1f, FadeDuration);

            Pay(result);
            _dialogue.PlayCost(result.TotalCost);
            yield return stepWait;
        }

        private IEnumerator VisitTownHall(Action<PurchaseResult> captureResult)
        {
            yield return _skeleton.FadeAlphaRoutine(1f, 0f, FadeDuration);
            yield return new WaitForSeconds(_selectTownHallDuration());
            yield return _skeleton.FadeAlphaRoutine(0f, 1f, FadeDuration);

            PurchaseResult result = PurchaseResult.Empty;
            captureResult(result);
            _playTownHallFeedback();
            yield return new WaitForSeconds(_config.ReactionDisplayDuration);
        }

        private IEnumerator Feedback(PurchaseResult result)
        {
            _playEmotion(result);
            yield return new WaitForSeconds(_config.ReactionDisplayDuration);
            yield return new WaitForSeconds(_config.StepGap);
        }

        private void Pay(PurchaseResult result)
        {
            if (result.TotalCost > 0)
            {
                _account.TrySpend(CurrencyType.Gold, result.TotalCost);
            }
        }
    }
}
