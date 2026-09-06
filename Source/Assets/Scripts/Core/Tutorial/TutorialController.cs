using System;
using System.Collections;
using System.Collections.Generic;
using SeaVillage.Data;
using SeaVillage.Utilities;
using UnityEngine;

namespace SeaVillage.Core
{
    public sealed class TutorialController : MonoBehaviour
    {
        private const float InitializationTimeoutSeconds = 10f;

        private TutorialManager _manager;
        private TutorialRepository _repository;
        private TutorialEffectExecutor _effectExecutor;
        private SceneChanger _sceneChanger;
        private CurrencyManager _currencyManager;
        private InventoryManager _inventoryManager;
        private Coroutine _entryRoutine;
        private Coroutine _evaluationRoutine;
        private Coroutine _inventoryRecoveryEvaluationRoutine;
        private bool _subscribed;
        private string _lastFailedStepId = string.Empty;
        private int _failureRetryCount;

        #region Properties

        public int ForcedFoodPriceTargetDay =>
            _effectExecutor != null ? _effectExecutor.ForcedFoodPriceTargetDay : 0;
        public bool IsRewardGranted => _effectExecutor != null && _effectExecutor.IsRewardGranted;

        #endregion

        #region MonoBehaviour

        private void OnEnable()
        {
            if (_manager == null || _subscribed)
                return;

            AddListeners();
            ScheduleEvaluation();
        }

        private void OnDisable()
        {
            RemoveListeners();
            StopOwnedRoutines();
            _effectExecutor?.ReleaseControlScope();
        }

        #endregion

        #region Public API

        public bool Initialize(
            TutorialManager manager,
            TutorialRepository repository,
            TutorialEffectExecutor effectExecutor)
        {
            if (manager == null || repository == null || effectExecutor == null)
                return false;

            if (_subscribed)
                return _manager == manager;

            _manager = manager;
            _repository = repository;
            _effectExecutor = effectExecutor;
            if (!_effectExecutor.Initialize(this))
                return false;

            AddListeners();
            ScheduleEvaluation();
            return true;
        }

        public bool TryStartNewGameSequence(out string failReason)
        {
            failReason = string.Empty;
            if (!_subscribed || _manager == null || !_manager.IsInitialized)
            {
                failReason = "튜토리얼 Controller가 초기화되지 않았습니다";
                return false;
            }

            if (_sceneChanger == null || _sceneChanger.CurrentSceneName != "StartTown")
            {
                failReason = $"신규 게임 시작 씬이 아닙니다: {_sceneChanger?.CurrentSceneName ?? "Unknown"}";
                return false;
            }

            if (_manager.IsActive)
            {
                if (_manager.ActiveStepId == _repository.FirstStepId)
                    return true;

                failReason = $"다른 튜토리얼 단계가 이미 진행 중입니다: {_manager.ActiveStepId}";
                return false;
            }

            if (_manager.IsStepCompleted(_repository.FirstStepId))
            {
                failReason = "신규 게임 튜토리얼 완료 상태가 초기화되지 않았습니다";
                return false;
            }

            if (!UI.UIManager.HasInstance || !UI.UIManager.Instance.EnsureTutorialPresentationReady())
            {
                failReason = "튜토리얼 Presentation UI를 준비할 수 없습니다";
                return false;
            }

            return TryEnterStep(_repository.FirstStepId, TutorialEntryCause.NewGame, out failReason);
        }

        public void RestoreEffectState(TutorialProgressSaveData progress)
        {
            _effectExecutor?.ImportProgress(progress);
            ScheduleEvaluation();
        }

        public void CopyAppliedEffectIdsTo(System.Collections.Generic.List<string> target)
        {
            _effectExecutor?.CopyAppliedEffectIdsTo(target);
        }

        public void SkipSequence()
        {
            StopOwnedRoutines();
            _effectExecutor?.SkipSequence();
        }

        public void ResetSequence()
        {
            StopOwnedRoutines();
            _effectExecutor?.ResetProgress();
            ScheduleEvaluation();
        }

        #endregion

        #region Event Handlers

        private void HandleTutorialCompleted(string tutorialId)
        {
            _lastFailedStepId = string.Empty;
            _failureRetryCount = 0;
            if (string.Equals(
                    tutorialId,
                    TutorialDefinitionCatalog.FirstWreckDialogueId,
                    StringComparison.Ordinal))
            {
                _entryRoutine = StartCoroutine(CompleteFirstWreckRecovery());
                return;
            }

            string nextStepId = _manager.GetFirstIncompleteStepId();
            if (string.IsNullOrEmpty(nextStepId))
            {
                _entryRoutine = StartCoroutine(FinishSequenceNextFrame());
                return;
            }

            TryEnterStep(nextStepId, TutorialEntryCause.Completed, out string failReason);
            if (!string.IsNullOrEmpty(failReason))
                FailSequence(failReason);
        }

        private void HandleTutorialCancelled(string _)
        {
            _effectExecutor?.ReleaseControlScope();
            ScheduleEvaluation();
        }

        private void HandleTutorialFailed(string tutorialId, string failReason)
        {
            StopOwnedRoutines();
            _effectExecutor?.ReleaseControlScope();
            Debug.LogError($"[TutorialController] {tutorialId} 실패로 진행을 중단했습니다: {failReason}");

            if (string.Equals(
                    tutorialId,
                    TutorialDefinitionCatalog.FirstWreckDialogueId,
                    StringComparison.Ordinal))
            {
                _manager?.MarkFirstWreckPresentationAttempted();
                return;
            }

            if (!_repository.TryGetByDialogueId(tutorialId, out TutorialStepDefinition failedStep))
                return;
            if (!string.Equals(_lastFailedStepId, failedStep.StepId, StringComparison.Ordinal))
            {
                _lastFailedStepId = failedStep.StepId;
                _failureRetryCount = 0;
            }

            if (_failureRetryCount >= 1)
                return;
            _failureRetryCount++;
            _entryRoutine = StartCoroutine(RetryFailedStepNextFrame(failedStep.StepId));
        }

        private void HandleEventObserved(TutorialEvent tutorialEvent)
        {
            if (_manager == null || !_manager.TryActivatePendingStep(tutorialEvent))
                return;

            TutorialStepDefinition step = _manager.ActiveStepDefinition;
            StartEntryRoutine(step, TutorialEntryCause.ExternalEvent, step.ActivationActions);
        }

        private void HandleProgressReset()
        {
            _effectExecutor?.ResetProgress();
            ScheduleEvaluation();
        }

        private void HandleSceneTransitionCompleted(string sceneName)
        {
            _effectExecutor?.ClearSceneReferences();
            _manager?.TryReportEvent(
                new TutorialEvent(
                    TutorialEventType.SceneEntered,
                    sceneName,
                    source: TutorialEventSource.Scene),
                out _,
                out _);
            ScheduleEvaluation();
        }

        private void HandleDayChanged(int newDay)
        {
            _effectExecutor?.HandleDayChanged(newDay);
            _manager?.TryReportEvent(
                new TutorialEvent(
                    TutorialEventType.DateAdvanced,
                    amount: newDay,
                    source: TutorialEventSource.Time),
                out _,
                out _);
        }

        private void HandlePlayerGoldChanged(long _)
        {
            HandleRecoveryResourceChanged();
        }

        private void HandleShipFoodStorageChanged()
        {
            HandleRecoveryResourceChanged();
        }

        private void HandlePlayerInventoryChanged()
        {
            if (_inventoryRecoveryEvaluationRoutine == null && isActiveAndEnabled)
            {
                _inventoryRecoveryEvaluationRoutine =
                    StartCoroutine(EvaluateRecoveryAfterInventoryMutation());
            }
        }

        private void HandleEffectFailed(string failReason)
        {
            FailSequence(failReason);
        }

        #endregion

        #region Private Helpers

        private bool TryEnterStep(
            string stepId,
            TutorialEntryCause cause,
            out string failReason)
        {
            failReason = string.Empty;
            if (!_repository.TryGet(stepId, out TutorialStepDefinition step))
            {
                failReason = $"튜토리얼 StepId를 찾을 수 없습니다: {stepId}";
                return false;
            }

            if (!IsRequiredSceneActive(step))
            {
                if (step.SceneMismatchPolicy == TutorialSceneMismatchPolicy.ChangeToRequiredScene)
                    _effectExecutor.ChangeScene(step.RequiredScene);
                return true;
            }

            if (!_manager.TrySetPendingStep(step.StepId, out failReason))
                return false;

            if (step.EntryMode == TutorialEntryMode.WaitForEvent
                && cause != TutorialEntryCause.ExternalEvent)
            {
                StartEntryRoutine(step, cause, step.WaitingActions, startDialogue: false);
                return true;
            }

            IReadOnlyList<TutorialActionType> actions =
                cause == TutorialEntryCause.Recovery && step.RecoveryActions.Count > 0
                    ? step.RecoveryActions
                    : step.EntryActions;
            StartEntryRoutine(step, cause, actions);
            return true;
        }

        private void StartEntryRoutine(
            TutorialStepDefinition step,
            TutorialEntryCause cause,
            System.Collections.Generic.IReadOnlyList<TutorialActionType> actions,
            bool startDialogue = true)
        {
            if (_entryRoutine != null)
                StopCoroutine(_entryRoutine);

            _entryRoutine = StartCoroutine(EnterStepRoutine(step, cause, actions, startDialogue));
        }

        private IEnumerator EnterStepRoutine(
            TutorialStepDefinition step,
            TutorialEntryCause cause,
            System.Collections.Generic.IReadOnlyList<TutorialActionType> actions,
            bool startDialogue)
        {
            if (cause == TutorialEntryCause.Completed)
                yield return null;

            if (step.EntryMode == TutorialEntryMode.RewardThenDialogue && startDialogue)
            {
                bool rewardResolved = false;
                bool rewardSucceeded = false;
                _effectExecutor.ShowReward(success =>
                {
                    rewardSucceeded = success;
                    rewardResolved = true;
                });

                while (!rewardResolved)
                    yield return null;

                if (!rewardSucceeded)
                {
                    _entryRoutine = null;
                    yield break;
                }
            }

            yield return _effectExecutor.ExecuteActions(actions, cause);
            _entryRoutine = null;
            if (!startDialogue)
                yield break;

            if (!_manager.TryStartPendingStepDialogue(out string failReason))
                FailSequence(failReason);
        }

        private IEnumerator EvaluateWhenReady()
        {
            float startTime = Time.realtimeSinceStartup;
            while ((!GameManager.HasInstance || !GameManager.Instance.IsAllManagerInitialized)
                   && Time.realtimeSinceStartup - startTime < InitializationTimeoutSeconds)
            {
                yield return null;
            }

            _evaluationRoutine = null;
            if (!GameManager.HasInstance || !GameManager.Instance.IsAllManagerInitialized)
            {
                FailSequence("전체 매니저 초기화 대기 시간이 초과되었습니다");
                yield break;
            }

            if (_manager == null || !_manager.IsInitialized)
                yield break;

            if (_manager.IsActive)
            {
                TutorialStepDefinition activeStep = _manager.ActiveStepDefinition;
                if (activeStep == null || !IsRequiredSceneActive(activeStep))
                    yield break;

                if (_manager.PlaybackState == TutorialPlaybackState.WaitingForActivation)
                {
                    StartEntryRoutine(
                        activeStep,
                        TutorialEntryCause.Recovery,
                        activeStep.WaitingActions,
                        startDialogue: false);
                }
                else if (_manager.PlaybackState == TutorialPlaybackState.WaitingForEffect)
                {
                    IReadOnlyList<TutorialActionType> recoveryActions =
                        activeStep.RecoveryActions.Count > 0
                            ? activeStep.RecoveryActions
                            : activeStep.EntryActions;
                    StartEntryRoutine(activeStep, TutorialEntryCause.Recovery, recoveryActions);
                }
                else
                {
                    _manager.RepublishCurrentDialogue();
                }

                yield break;
            }

            if (TryStartFirstWreckRecovery(out string wreckFailure))
            {
                if (!string.IsNullOrEmpty(wreckFailure))
                    Debug.LogError($"[TutorialController] 최초 난파 구제 시작 실패: {wreckFailure}");
                yield break;
            }

            string nextStepId = _manager.GetFirstIncompleteStepId();
            if (!string.IsNullOrEmpty(nextStepId)
                && !TryEnterStep(nextStepId, TutorialEntryCause.Recovery, out string failReason))
            {
                FailSequence(failReason);
            }
        }

        private IEnumerator FinishSequenceNextFrame()
        {
            yield return null;
            _entryRoutine = null;
            yield return _effectExecutor.ExecuteActions(
                new[]
                {
                    TutorialActionType.ResetControls,
                    TutorialActionType.FadeGuide
                },
                TutorialEntryCause.Completed);
            ScheduleEvaluation();
        }

        private IEnumerator CompleteFirstWreckRecovery()
        {
            yield return null;

            bool rewardResolved = false;
            bool rewardSucceeded = false;
            _effectExecutor.ShowFirstWreckReward(success =>
            {
                rewardSucceeded = success;
                rewardResolved = true;
            });

            while (rewardResolved == false)
                yield return null;

            if (rewardSucceeded)
            {
                _manager.MarkFirstWreckRewardGranted();
                if (SaveLoadManager.HasInstance)
                    SaveLoadManager.Instance.SaveGame(SaveLoadManager.Instance.CurrentSlot);
            }
            else
            {
                yield return null;
                UI.UIManager.Instance?.ShowAlertMessage("[Error] 난파 구제 보상을 지급할 수 없습니다");
            }

            yield return _effectExecutor.ExecuteActions(
                new[]
                {
                    TutorialActionType.ResetControls,
                    TutorialActionType.FadeGuide
                },
                TutorialEntryCause.Completed);
            _entryRoutine = null;
            ScheduleEvaluation();
        }

        private bool TryStartFirstWreckRecovery(out string failReason)
        {
            failReason = string.Empty;
            if (_manager == null)
                return false;
            if (!GameManager.HasInstance
                || GameManager.Instance.CurrentGameState != GameState.Town)
                return false;

            _manager.TryQueueFirstWreckRecovery();
            if (!_manager.CanAttemptFirstWreckRecovery)
                return false;

            _manager.MarkFirstWreckPresentationAttempted();
            if (!UI.UIManager.HasInstance || !UI.UIManager.Instance.EnsureTutorialPresentationReady())
            {
                failReason = "튜토리얼 Presentation UI를 준비할 수 없습니다";
                _effectExecutor?.ReleaseControlScope();
                return true;
            }

            if (!TryEnterStep(
                    TutorialDefinitionCatalog.FirstWreckStepId,
                    TutorialEntryCause.Recovery,
                    out failReason))
            {
                _effectExecutor?.ReleaseControlScope();
            }
            return true;
        }

        private void HandleRecoveryResourceChanged()
        {
            if (_manager == null
                || !GameManager.HasInstance
                || !GameManager.Instance.IsAllManagerInitialized
                || !_manager.TryQueueFirstWreckRecovery())
            {
                return;
            }

            if (_manager.IsActive)
                _manager.CancelActiveTutorial();

            if (UI.UIManager.HasInstance)
                UI.UIManager.Instance.CloseAllPanels();
            ScheduleEvaluation();
        }

        private IEnumerator EvaluateRecoveryAfterInventoryMutation()
        {
            // 식량 전환·거래 중간 상태가 아닌 완료된 자원 상태에서 판정
            yield return null;
            _inventoryRecoveryEvaluationRoutine = null;
            HandleRecoveryResourceChanged();
        }

        private IEnumerator RetryFailedStepNextFrame(string stepId)
        {
            yield return null;
            _entryRoutine = null;
            if (!TryEnterStep(stepId, TutorialEntryCause.Recovery, out string failReason))
                Debug.LogError($"[TutorialController] 안전 재시작 실패: {failReason}");
        }

        private bool IsRequiredSceneActive(TutorialStepDefinition step)
        {
            if (step == null || _sceneChanger == null)
                return false;

            if (string.Equals(
                    step.RequiredScene,
                    TutorialDefinitionCatalog.AnyTownScene,
                    StringComparison.Ordinal))
            {
                return GameManager.HasInstance
                    && GameManager.Instance.CurrentGameState == GameState.Town;
            }

            return string.Equals(
                _sceneChanger.CurrentSceneName,
                step.RequiredScene,
                StringComparison.Ordinal);
        }

        private void ScheduleEvaluation()
        {
            if (!isActiveAndEnabled)
                return;

            if (_evaluationRoutine != null)
                StopCoroutine(_evaluationRoutine);
            _evaluationRoutine = StartCoroutine(EvaluateWhenReady());
        }

        private void FailSequence(string failReason)
        {
            string normalized = string.IsNullOrWhiteSpace(failReason)
                ? "알 수 없는 튜토리얼 실행 오류"
                : failReason.Trim();
            if (_manager == null || !_manager.FailActiveTutorial(normalized))
            {
                _effectExecutor?.ReleaseControlScope();
                Debug.LogError($"[TutorialController] {normalized}");
            }
        }

        private void StopOwnedRoutines()
        {
            if (_entryRoutine != null)
            {
                StopCoroutine(_entryRoutine);
                _entryRoutine = null;
            }

            if (_evaluationRoutine != null)
            {
                StopCoroutine(_evaluationRoutine);
                _evaluationRoutine = null;
            }

            if (_inventoryRecoveryEvaluationRoutine != null)
            {
                StopCoroutine(_inventoryRecoveryEvaluationRoutine);
                _inventoryRecoveryEvaluationRoutine = null;
            }
        }

        private void AddListeners()
        {
            if (_subscribed || _manager == null)
                return;

            _manager.OnTutorialCompleted += HandleTutorialCompleted;
            _manager.OnTutorialCancelled += HandleTutorialCancelled;
            _manager.OnTutorialFailed += HandleTutorialFailed;
            _manager.OnEventObserved += HandleEventObserved;
            _manager.OnProgressReset += HandleProgressReset;

            _sceneChanger = SceneChanger.HasInstance ? SceneChanger.Instance : null;
            if (_sceneChanger != null)
                _sceneChanger.OnSceneTransitionCompleted += HandleSceneTransitionCompleted;

            _currencyManager = CurrencyManager.HasInstance ? CurrencyManager.Instance : null;
            if (_currencyManager != null)
                _currencyManager.OnPlayerGoldChanged += HandlePlayerGoldChanged;

            _inventoryManager = InventoryManager.HasInstance ? InventoryManager.Instance : null;
            if (_inventoryManager != null)
            {
                _inventoryManager.OnShipFoodStorageChanged += HandleShipFoodStorageChanged;
                _inventoryManager.OnPlayerInventoryChanged += HandlePlayerInventoryChanged;
            }

            TimeManager.OnDayChanged += HandleDayChanged;
            _effectExecutor.OnFailed += HandleEffectFailed;
            _subscribed = true;
        }

        private void RemoveListeners()
        {
            if (!_subscribed)
                return;

            if (_manager != null)
            {
                _manager.OnTutorialCompleted -= HandleTutorialCompleted;
                _manager.OnTutorialCancelled -= HandleTutorialCancelled;
                _manager.OnTutorialFailed -= HandleTutorialFailed;
                _manager.OnEventObserved -= HandleEventObserved;
                _manager.OnProgressReset -= HandleProgressReset;
            }

            if (_sceneChanger != null)
                _sceneChanger.OnSceneTransitionCompleted -= HandleSceneTransitionCompleted;
            if (_currencyManager != null)
                _currencyManager.OnPlayerGoldChanged -= HandlePlayerGoldChanged;
            if (_inventoryManager != null)
            {
                _inventoryManager.OnShipFoodStorageChanged -= HandleShipFoodStorageChanged;
                _inventoryManager.OnPlayerInventoryChanged -= HandlePlayerInventoryChanged;
            }
            TimeManager.OnDayChanged -= HandleDayChanged;
            if (_effectExecutor != null)
                _effectExecutor.OnFailed -= HandleEffectFailed;
            _currencyManager = null;
            _inventoryManager = null;
            _subscribed = false;
        }

        #endregion
    }
}
