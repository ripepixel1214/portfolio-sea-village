using System;
using System.Collections;
using System.Collections.Generic;
using SeaVillage.Data;
using SeaVillage.Player;
using SeaVillage.UI;
using SeaVillage.Utilities;
using Spine.Unity;
using UnityEngine;

namespace SeaVillage.Core
{
    public sealed class TutorialEffectExecutor : MonoBehaviour
    {
        private const string SettingsResourcePath = "Data/ScriptableObjects/TutorialSequenceSettings";
        private const string FoodCategory = "FoodStuff";
        private const string PriceEffectId = "tutorial.price_change.food";
        private const string RewardEffectId = "tutorial.reward.final";
        private const long RewardGold = 100L;
        private const float RewardFood = 29f;
        private const long FirstWreckRewardGold = 300L;
        private const float FirstWreckRewardFood = PlayerStateChecker.FirstWreckRecoveryFoodAmount;
        private const float ForcedFoodMultiplier = 0.7f;
        private const float GuideOffset = 1.5f;
        private const float ShipGuideMinimumX = -26f;
        private const float ArrivalThreshold = 2.75f;
        private const float GuideFadeDuration = 1f;
        private const float DependencyTimeoutSeconds = 5f;

        private readonly HashSet<string> _appliedEffectIds =
            new HashSet<string>(StringComparer.Ordinal);

        private TutorialController _controller;
        private TutorialSequenceSettings _settings;
        private TutorialControlScope _controlScope;
        private Coroutine _monitorRoutine;
        private Coroutine _rewardRetryRoutine;
        private GameObject _guide;
        private GuideMove _guideMove;
        private SkeletonAnimation _guideSkeleton;
        private PlayerController _player;
        private ShopInteractable _foodShop;
        private BaseInteractable _ship;
        private Action<bool> _rewardCallback;
        private Action<bool> _firstWreckRewardCallback;
        private bool _rewardPending;
        private bool _firstWreckRewardPending;
        private bool _rewardGranted;
        private int _forcedFoodPriceTargetDay;
        private bool _executionFailed;

        #region Events

        public event Action<string> OnFailed;

        #endregion

        #region Properties

        public int ForcedFoodPriceTargetDay => _forcedFoodPriceTargetDay;
        public bool IsRewardGranted => _rewardGranted;

        #endregion

        #region MonoBehaviour

        private void OnDisable()
        {
            StopOwnedRoutines();
            ReleaseControlScope();
        }

        #endregion

        #region Public API

        public bool Initialize(TutorialController controller)
        {
            if (controller == null)
                return false;

            _controller = controller;
            _settings = Resources.Load<TutorialSequenceSettings>(SettingsResourcePath);
            if (TryValidateConfiguration(out string failReason))
                return true;

            Debug.LogError($"[TutorialEffectExecutor] 설정 검증 실패: {failReason}");
            return false;
        }

        public IEnumerator ExecuteActions(
            IReadOnlyList<TutorialActionType> actions,
            TutorialEntryCause cause)
        {
            _executionFailed = false;
            if (actions == null)
                yield break;

            for (int i = 0; i < actions.Count; i++)
            {
                TutorialActionType type = actions[i];
                switch (type)
                {
                    case TutorialActionType.ResetControls:
                        ResetControlPolicies();
                        break;
                    case TutorialActionType.BlockMovement:
                        SetMovementConstraint(TutorialMovementConstraint.Blocked);
                        break;
                    case TutorialActionType.AllowRightMovement:
                        SetMovementConstraint(TutorialMovementConstraint.RightOnly);
                        break;
                    case TutorialActionType.AllowLeftMovement:
                        SetMovementConstraint(TutorialMovementConstraint.LeftOnly);
                        break;
                    case TutorialActionType.BlockCommandInput:
                        EnsureControlScope().SetCommandInputBlocked(true);
                        break;
                    case TutorialActionType.BlockAllInteractions:
                        EnsureControlScope().BlockAllInteractions();
                        RefreshPlayerInteractionTarget();
                        break;
                    case TutorialActionType.RestrictToShop:
                        PrepareWorldInteraction(InteractionType.Shop);
                        break;
                    case TutorialActionType.RestrictToShip:
                        PrepareWorldInteraction(InteractionType.Ship);
                        break;
                    case TutorialActionType.EnsureGuide:
                        ResolveSceneReferences();
                        EnsureGuide();
                        break;
                    case TutorialActionType.FacePlayerAndGuide:
                        FacePlayerAndGuide();
                        break;
                    case TutorialActionType.MoveGuideToFoodShop:
                        ResolveSceneReferences();
                        MoveGuide(ResolveFoodShopTarget());
                        break;
                    case TutorialActionType.MoveGuideToShip:
                        ResolveSceneReferences();
                        MoveGuide(ResolveShipTarget());
                        break;
                    case TutorialActionType.MonitorFoodShopArrival:
                        ResolveSceneReferences();
                        BeginArrivalMonitor(
                            TutorialTargetIds.FoodShop,
                            ResolveFoodShopTarget(),
                            cause == TutorialEntryCause.Recovery);
                        break;
                    case TutorialActionType.MonitorShipArrival:
                        ResolveSceneReferences();
                        BeginArrivalMonitor(
                            TutorialTargetIds.Ship,
                            ResolveShipTarget(),
                            cause == TutorialEntryCause.Recovery);
                        break;
                    case TutorialActionType.ClosePanels:
                        yield return ClosePanelsWithTimeout();
                        break;
                    case TutorialActionType.PrepareFoodPriceChange:
                        if (!TryPrepareForcedFoodPriceChange())
                            yield break;
                        break;
                    case TutorialActionType.RestoreFoodPriceChange:
                        if (!TryRestoreForcedFoodPriceChange(prepareWhenMissing: true))
                            yield break;
                        break;
                    case TutorialActionType.RestoreFoodPriceChangeWithoutPreparation:
                        if (!TryRestoreForcedFoodPriceChange(prepareWhenMissing: false))
                            yield break;
                        break;
                    case TutorialActionType.PauseTime:
                        if (TimeManager.HasInstance)
                            TimeManager.Instance.PauseTimeProgress();
                        break;
                    case TutorialActionType.ResumeTime:
                        if (TimeManager.HasInstance && TimeManager.Instance.IsPaused)
                            TimeManager.Instance.ResumeTimeProgress();
                        break;
                    case TutorialActionType.FadeGuide:
                        yield return FadeGuideAndFinish();
                        break;
                    default:
                        Fail($"등록되지 않은 TutorialActionType입니다: {type}");
                        yield break;
                }

                if (_executionFailed)
                    yield break;
            }
        }

        public void ShowReward(Action<bool> callback)
        {
            if (_rewardGranted || _appliedEffectIds.Contains(RewardEffectId))
            {
                _rewardGranted = true;
                callback?.Invoke(true);
                return;
            }

            if (_rewardPending)
            {
                callback?.Invoke(false);
                return;
            }

            if (!UIManager.HasInstance)
            {
                Fail("보상 패널을 열 UIManager가 없습니다");
                callback?.Invoke(false);
                return;
            }

            _rewardPending = true;
            _rewardCallback = callback;
            UIManager.Instance.ShowAlertMessage(
                $"{RewardGold} G\n배 식량 +{RewardFood:0}kg",
                HandleRewardConfirmed,
                "튜토리얼 보상");
        }

        public void ShowFirstWreckReward(Action<bool> callback)
        {
            if (_firstWreckRewardPending)
            {
                callback?.Invoke(false);
                return;
            }

            if (!UIManager.HasInstance)
            {
                Fail("최초 난파 보상 패널을 열 UIManager가 없습니다");
                callback?.Invoke(false);
                return;
            }

            _firstWreckRewardPending = true;
            _firstWreckRewardCallback = callback;
            UIManager.Instance.ShowAlertMessage(
                $"{FirstWreckRewardGold} G\n배 식량 +{FirstWreckRewardFood:0}kg",
                HandleFirstWreckRewardConfirmed,
                "난파 구제");
        }

        public void ImportProgress(TutorialProgressSaveData progress)
        {
            _appliedEffectIds.Clear();
            if (progress?.appliedEffectIds != null)
            {
                for (int i = 0; i < progress.appliedEffectIds.Count; i++)
                {
                    string effectId = progress.appliedEffectIds[i]?.Trim() ?? string.Empty;
                    if (!string.IsNullOrEmpty(effectId))
                        _appliedEffectIds.Add(effectId);
                }
            }

            _forcedFoodPriceTargetDay = Mathf.Max(0, progress?.forcedFoodPriceTargetDay ?? 0);
            _rewardGranted = progress != null
                && (progress.rewardGranted || _appliedEffectIds.Contains(RewardEffectId));
            if (_rewardGranted)
                _appliedEffectIds.Add(RewardEffectId);
        }

        public void CopyAppliedEffectIdsTo(List<string> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.Clear();
            foreach (string effectId in _appliedEffectIds)
                target.Add(effectId);
            target.Sort(StringComparer.Ordinal);
        }

        public void HandleDayChanged(int newDay)
        {
            if (_forcedFoodPriceTargetDay > 0 && newDay >= _forcedFoodPriceTargetDay)
                _forcedFoodPriceTargetDay = 0;
        }

        public void ChangeScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)
                || !SceneChanger.HasInstance
                || SceneChanger.Instance.IsTransitioning)
            {
                return;
            }

            if (TimeManager.HasInstance)
                TimeManager.Instance.ResumeTimeProgress();
            ReleaseControlScope();
            SceneChanger.Instance.ChangeScene(sceneName.Trim());
        }

        public void ClearSceneReferences()
        {
            StopMonitorRoutine();
            ClearGuideReferences();
            _player = null;
            _foodShop = null;
            _ship = null;
        }

        public void ReleaseControlScope()
        {
            _controlScope?.Dispose();
            _controlScope = null;
            if (_player != null)
                _player.SetTutorialMovementConstraint(TutorialMovementConstraint.Unrestricted);
            RefreshPlayerInteractionTarget();
        }

        public void ResetProgress()
        {
            StopOwnedRoutines();
            ReleaseControlScope();
            _appliedEffectIds.Clear();
            _forcedFoodPriceTargetDay = 0;
            _rewardGranted = false;
            _rewardPending = false;
            _rewardCallback = null;
            _firstWreckRewardPending = false;
            _firstWreckRewardCallback = null;
        }

        public void SkipSequence()
        {
            StopOwnedRoutines();
            ReleaseControlScope();
            if (_forcedFoodPriceTargetDay > 0 && RuntimeItemPriceManager.HasInstance)
            {
                RuntimeItemPriceManager.Instance.ClearPreparedPriceChangesForDay(
                    _forcedFoodPriceTargetDay);
            }

            if (_guide != null)
                Destroy(_guide);
            ClearGuideReferences();
            _forcedFoodPriceTargetDay = 0;
            _rewardGranted = true;
            _appliedEffectIds.Add(RewardEffectId);
        }

        #endregion

        #region Event Handlers

        private void HandleRewardConfirmed()
        {
            if (!_rewardGranted && !TryGrantReward(RewardGold, RewardFood))
            {
                Debug.LogError("[TutorialEffectExecutor] 튜토리얼 보상 지급에 실패했습니다");
                if (_rewardRetryRoutine == null)
                    _rewardRetryRoutine = StartCoroutine(ReopenRewardNextFrame());
                return;
            }

            _rewardGranted = true;
            _appliedEffectIds.Add(RewardEffectId);
            _rewardPending = false;
            Action<bool> callback = _rewardCallback;
            _rewardCallback = null;
            callback?.Invoke(true);
        }

        private void HandleFirstWreckRewardConfirmed()
        {
            bool success = TryGrantReward(FirstWreckRewardGold, FirstWreckRewardFood);
            _firstWreckRewardPending = false;
            Action<bool> callback = _firstWreckRewardCallback;
            _firstWreckRewardCallback = null;

            if (!success)
                Debug.LogError("[TutorialEffectExecutor] 최초 난파 보상 지급에 실패했습니다");
            callback?.Invoke(success);
        }

        #endregion

        #region Private Helpers

        private bool TryValidateConfiguration(out string failReason)
        {
            failReason = string.Empty;
            if (_settings == null || _settings.GuidePrefab == null)
            {
                failReason = $"안내자 프리팹 설정이 없습니다: Resources/{SettingsResourcePath}";
                return false;
            }

            GuideMove guideMove = _settings.GuidePrefab.GetComponent<GuideMove>();
            if (guideMove == null)
            {
                failReason = "안내자 프리팹에 GuideMove가 없습니다";
                return false;
            }

            if (_settings.GuidePrefab.GetComponentInChildren<SkeletonAnimation>(true) == null)
            {
                failReason = "안내자 프리팹에 SkeletonAnimation이 없습니다";
                return false;
            }

            if (!guideMove.TryValidateConfiguration(out string animationFailReason))
            {
                failReason = $"안내자 애니메이션 설정이 올바르지 않습니다: {animationFailReason}";
                return false;
            }

            return true;
        }

        private IEnumerator ClosePanelsWithTimeout()
        {
            if (!UIManager.HasInstance)
                yield break;

            UIManager.Instance.CloseAllPanels();
            float startTime = Time.realtimeSinceStartup;
            while (UIManager.Instance.IsAnyPanelOpened
                   && Time.realtimeSinceStartup - startTime < DependencyTimeoutSeconds)
            {
                yield return null;
            }

            if (UIManager.Instance.IsAnyPanelOpened)
                Fail("튜토리얼 진입 전 UI 패널 닫힘 대기 시간이 초과되었습니다");
        }

        private bool TryPrepareForcedFoodPriceChange()
        {
            if (_appliedEffectIds.Contains(PriceEffectId))
                return TryRestoreForcedFoodPriceChange(prepareWhenMissing: false);

            if (!RuntimeItemPriceManager.HasInstance || !TimeManager.HasInstance)
            {
                Fail("가격 또는 시간 매니저가 없어 음식 재료 가격 변동을 준비할 수 없습니다");
                return false;
            }

            int targetDay = TimeManager.Instance.CurrentDay + 1;
            if (!RuntimeItemPriceManager.Instance.TryPrepareForcedPriceChangesForDay(
                    targetDay,
                    FoodCategory,
                    ForcedFoodMultiplier,
                    TutorialItemIds.Potato))
            {
                Fail("음식 재료 가격 변동 준비에 실패했습니다");
                return false;
            }

            _forcedFoodPriceTargetDay = targetDay;
            _appliedEffectIds.Add(PriceEffectId);
            return true;
        }

        private bool TryRestoreForcedFoodPriceChange(bool prepareWhenMissing)
        {
            if (_forcedFoodPriceTargetDay <= 0)
                return !prepareWhenMissing || TryPrepareForcedFoodPriceChange();

            if (!TimeManager.HasInstance || !RuntimeItemPriceManager.HasInstance)
            {
                Fail("저장된 음식 재료 가격 변동을 복구할 매니저가 없습니다");
                return false;
            }

            if (_forcedFoodPriceTargetDay <= TimeManager.Instance.CurrentDay)
            {
                _forcedFoodPriceTargetDay = 0;
                return true;
            }

            if (RuntimeItemPriceManager.Instance.TryPrepareForcedPriceChangesForDay(
                    _forcedFoodPriceTargetDay,
                    FoodCategory,
                    ForcedFoodMultiplier,
                    TutorialItemIds.Potato))
            {
                _appliedEffectIds.Add(PriceEffectId);
                return true;
            }

            Fail($"저장된 음식 재료 가격 변동 복구에 실패했습니다: Day {_forcedFoodPriceTargetDay}");
            return false;
        }

        private static bool TryGrantReward(long gold, float food)
        {
            if (!CurrencyManager.HasInstance || !InventoryManager.HasInstance)
                return false;

            InventoryManager inventory = InventoryManager.Instance;
            if (!inventory.CanAddShipFood(food))
            {
                Debug.LogWarning($"[TutorialEffectExecutor] 배 식량 공간이 부족해 보상을 지급하지 못했습니다: {food:0.##}kg");
                return false;
            }

            long originalGold = CurrencyManager.Instance.GetPlayerBalance(CurrencyType.Gold);
            if (!CurrencyManager.Instance.TryAddPlayer(CurrencyType.Gold, gold))
                return false;
            if (inventory.TryAddShipFood(food))
                return true;

            if (!CurrencyManager.Instance.SetPlayerBalance(CurrencyType.Gold, originalGold))
                Debug.LogError("[TutorialEffectExecutor] 식량 지급 실패 후 골드 롤백에도 실패했습니다");
            return false;
        }

        private IEnumerator ReopenRewardNextFrame()
        {
            yield return null;
            _rewardRetryRoutine = null;
            _rewardPending = false;
            Action<bool> callback = _rewardCallback;
            _rewardCallback = null;
            ShowReward(callback);
        }

        private void ResolveSceneReferences()
        {
            _player = GameManager.HasInstance && GameManager.Instance.Player != null
                ? GameManager.Instance.Player.GetComponent<PlayerController>()
                : FindFirstObjectByType<PlayerController>();

            _foodShop = null;
            ShopInteractable[] shops = FindObjectsByType<ShopInteractable>(FindObjectsSortMode.None);
            for (int i = 0; i < shops.Length; i++)
            {
                if (shops[i] != null && shops[i].ShopId == TutorialItemIds.FoodShop)
                {
                    _foodShop = shops[i];
                    break;
                }
            }

            _ship = null;
            BaseInteractable[] interactables = FindObjectsByType<BaseInteractable>(FindObjectsSortMode.None);
            for (int i = 0; i < interactables.Length; i++)
            {
                if (interactables[i] != null && interactables[i].InteractionType == InteractionType.Ship)
                {
                    _ship = interactables[i];
                    break;
                }
            }
        }

        private void EnsureGuide()
        {
            if (_guide != null || _player == null || _settings?.GuidePrefab == null)
                return;

            Vector2 position = (Vector2)_player.transform.position + Vector2.right * GuideOffset;
            _guide = Instantiate(_settings.GuidePrefab, position, Quaternion.identity);
            _guide.name = "Tutorial Guide";
            _guideMove = _guide.GetComponent<GuideMove>();
            _guideSkeleton = _guide.GetComponentInChildren<SkeletonAnimation>(true);

            Rigidbody2D body = _guide.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.linearVelocity = Vector2.zero;
            }

            Collider2D[] colliders = _guide.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
        }

        private void EnsureGuideAt(Vector2 position)
        {
            EnsureGuide();
            if (_guide != null)
                _guide.transform.position = new Vector3(position.x, position.y, _guide.transform.position.z);
        }

        private void MoveGuide(Vector2 target)
        {
            EnsureGuide();
            if (_guideMove == null)
                return;
            _guideMove.Move(target, _player != null ? _player.EffectiveMoveSpeed : 0f);
        }

        private Vector2 ResolveFoodShopTarget()
        {
            if (_foodShop != null)
                return (Vector2)_foodShop.transform.position - Vector2.right * GuideOffset;
            return _player != null ? _player.transform.position : Vector2.zero;
        }

        private Vector2 ResolveShipTarget()
        {
            if (_ship != null)
            {
                Vector2 target = (Vector2)_ship.transform.position + Vector2.right * GuideOffset;
                target.x = Mathf.Max(target.x, ShipGuideMinimumX);
                return target;
            }

            return _player != null ? _player.transform.position : Vector2.zero;
        }

        private void FacePlayerAndGuide()
        {
            if (_player == null || _guide == null)
                return;
            _player.LookAt(_guide.transform.position);
            _guideMove?.LookAt(_player.transform.position);
        }

        private void BeginArrivalMonitor(string targetId, Vector2 target, bool placeGuideAtTarget)
        {
            StopMonitorRoutine();
            if (placeGuideAtTarget)
                EnsureGuideAt(target);
            _monitorRoutine = StartCoroutine(MonitorPlayerArrival(targetId, target));
        }

        private IEnumerator MonitorPlayerArrival(string targetId, Vector2 target)
        {
            float dependencyStart = Time.realtimeSinceStartup;
            while (_player == null && Time.realtimeSinceStartup - dependencyStart < DependencyTimeoutSeconds)
            {
                ResolveSceneReferences();
                yield return null;
            }

            if (_player == null)
            {
                _monitorRoutine = null;
                Fail("플레이어 위치 감시 준비 시간이 초과되었습니다");
                yield break;
            }

            while (Mathf.Abs(_player.transform.position.x - target.x) > ArrivalThreshold)
                yield return null;

            _monitorRoutine = null;
            TutorialEventReporter.Report(
                TutorialEventType.AreaReached,
                targetId,
                source: TutorialEventSource.World);
        }

        private IEnumerator FadeGuideAndFinish()
        {
            ReleaseControlScope();
            if (_guide == null)
                yield break;
            if (_guideSkeleton == null)
            {
                Destroy(_guide);
                ClearGuideReferences();
                yield break;
            }

            float startAlpha = _guideSkeleton.Skeleton.A;
            float elapsed = 0f;
            while (elapsed < GuideFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _guideSkeleton.Skeleton.A = Mathf.Lerp(startAlpha, 0f, elapsed / GuideFadeDuration);
                yield return null;
            }

            Destroy(_guide);
            ClearGuideReferences();
        }

        private TutorialControlScope EnsureControlScope()
        {
            return _controlScope ??= new TutorialControlScope();
        }

        private void SetMovementConstraint(TutorialMovementConstraint constraint)
        {
            if (_player == null)
                ResolveSceneReferences();
            if (_player != null)
                _player.SetTutorialMovementConstraint(constraint);
        }

        private void PrepareWorldInteraction(InteractionType interactionType)
        {
            SetMovementConstraint(TutorialMovementConstraint.Unrestricted);
            EnsureControlScope().RestrictTo(interactionType);
            RefreshPlayerInteractionTarget();
        }

        private void ResetControlPolicies()
        {
            ReleaseControlScope();
            _controlScope = new TutorialControlScope();
        }

        private void RefreshPlayerInteractionTarget()
        {
            if (_player == null)
                return;
            SeaVillage.Player.Player player = _player.GetComponent<SeaVillage.Player.Player>();
            player?.Interactor?.RefreshCurrentTarget();
        }

        private void StopOwnedRoutines()
        {
            StopMonitorRoutine();
            if (_rewardRetryRoutine != null)
            {
                StopCoroutine(_rewardRetryRoutine);
                _rewardRetryRoutine = null;
            }
        }

        private void StopMonitorRoutine()
        {
            if (_monitorRoutine == null)
                return;
            StopCoroutine(_monitorRoutine);
            _monitorRoutine = null;
        }

        private void ClearGuideReferences()
        {
            _guide = null;
            _guideMove = null;
            _guideSkeleton = null;
        }

        private void Fail(string failReason)
        {
            _executionFailed = true;
            ReleaseControlScope();
            OnFailed?.Invoke(failReason);
        }

        #endregion
    }
}
