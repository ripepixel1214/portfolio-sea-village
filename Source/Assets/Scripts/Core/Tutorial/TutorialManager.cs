using System;
using System.Collections.Generic;
using SeaVillage.Data;
using SeaVillage.Utilities;
using UnityEngine;

namespace SeaVillage.Core
{
    public class TutorialManager : Singleton<TutorialManager>
    {
        private TutorialRepository _repository;
        private TutorialRuntime _runtime;
        private TutorialController _controller;
        private TutorialEffectExecutor _effectExecutor;
        private FirstWreckRecoverySaveData _firstWreckRecovery = new FirstWreckRecoverySaveData();
        private bool _firstWreckPresentationAttempted;

        #region Events

        public event Action OnProgressReset;
        public event Action<TutorialDialogueContext> OnDialogueChanged;
        public event Action<TutorialConditionProgressContext> OnConditionProgressChanged;
        public event Action<string> OnTutorialCompleted;
        public event Action<string> OnTutorialCancelled;
        public event Action<string, string> OnTutorialFailed;
        public event Action<TutorialEvent> OnEventObserved;

        #endregion

        #region Properties

        public bool IsInitialized { get; private set; }
        public bool IsActive => _runtime?.HasActiveStep == true;
        public string ActiveStepId => _runtime?.ActiveStepId ?? string.Empty;
        public string ActiveTutorialId => _runtime?.ActiveDialogueId ?? string.Empty;
        public int CurrentDialogueIndex => _runtime?.DialogueIndex ?? -1;
        public int ForcedFoodPriceTargetDay => _controller?.ForcedFoodPriceTargetDay ?? 0;
        public bool IsRewardGranted => _controller != null && _controller.IsRewardGranted;
        public bool HasPendingFirstWreckRecovery =>
            _firstWreckRecovery.pending && !_firstWreckRecovery.rewardGranted;
        public bool IsOceanTutorialInProgress =>
            IsStepCompleted(TutorialDefinitionCatalog.OceanStartStepId)
            && !IsStepCompleted(TutorialDefinitionCatalog.OceanEndStepId);
        public TutorialPlaybackState PlaybackState =>
            _runtime?.PlaybackState ?? TutorialPlaybackState.Inactive;
        internal TutorialStepDefinition ActiveStepDefinition => _runtime?.ActiveStep;

        #endregion

        #region MonoBehaviour

        protected override void Awake()
        {
            base.Awake();
            Initialize();
        }

        #endregion

        #region Public API

        public void Initialize()
        {
            if (IsInitialized)
                return;

            if (!DataManager.HasInstance || DataManager.Instance.TutorialDatabase == null)
            {
                Debug.LogError("[TutorialManager] TutorialDatabase가 준비되지 않아 초기화할 수 없습니다");
                return;
            }

            _repository = TutorialDefinitionCatalog.CreateRepository(
                DataManager.Instance.TutorialDatabase.Tutorials);
            if (!_repository.TryValidate(out string failReason))
            {
                Debug.LogError($"[TutorialManager] 통합 정의 검증 실패: {failReason}");
                return;
            }

            _runtime = new TutorialRuntime(_repository);
            _effectExecutor = GetComponent<TutorialEffectExecutor>();
            if (_effectExecutor == null)
                _effectExecutor = gameObject.AddComponent<TutorialEffectExecutor>();
            _controller = GetComponent<TutorialController>();
            if (_controller == null)
                _controller = gameObject.AddComponent<TutorialController>();

            if (!_controller.Initialize(this, _repository, _effectExecutor))
            {
                Debug.LogError("[TutorialManager] TutorialController 초기화에 실패했습니다");
                return;
            }

            IsInitialized = true;
            Debug.Log(
                $"[TutorialManager] 초기화 완료: {_repository.OrderedSteps.Count}개 단계, "
                + $"{DataManager.Instance.TutorialDatabase.Tutorials.Count}개 대사");
        }

        public bool TryStartNewGameTutorial(out string failReason)
        {
            if (!IsInitialized || _controller == null)
            {
                failReason = "TutorialManager가 준비되지 않았습니다";
                return false;
            }

            return _controller.TryStartNewGameSequence(out failReason);
        }

        public bool TryQueueFirstWreckRecovery()
        {
            if (!IsInitialized
                || _firstWreckRecovery.triggered)
            {
                return false;
            }

            if (!PlayerStateChecker.IsFirstWreckRecoveryRequired())
                return false;

            _firstWreckRecovery.triggered = true;
            _firstWreckRecovery.pending = true;
            _firstWreckRecovery.rewardGranted = false;
            _firstWreckPresentationAttempted = false;
            return true;
        }

        public FirstWreckRecoverySaveData ExportFirstWreckRecovery()
        {
            return _firstWreckRecovery.Copy();
        }

        public void ImportFirstWreckRecovery(FirstWreckRecoverySaveData progress)
        {
            _firstWreckRecovery = progress?.Copy() ?? new FirstWreckRecoverySaveData();
            _firstWreckRecovery.Normalize();
            _firstWreckPresentationAttempted = false;
        }

        public bool TrySkipAllTutorials(out string failReason)
        {
            failReason = string.Empty;
            if (!IsInitialized || _controller == null)
            {
                failReason = "TutorialManager가 준비되지 않았습니다";
                return false;
            }

            string cancelledId = ActiveTutorialId;
            _runtime.CompleteAll();
            if (!string.IsNullOrEmpty(cancelledId))
                OnTutorialCancelled?.Invoke(cancelledId);
            _controller.SkipSequence();
            return true;
        }

        public bool NotifyCurrentDialoguePresented(out string failReason)
        {
            if (!IsInitialized)
            {
                failReason = "TutorialManager가 초기화되지 않았습니다";
                return false;
            }

            return _runtime.NotifyCurrentDialoguePresented(out failReason);
        }

        public bool TryAdvanceFromInput(out string failReason)
        {
            return TryAdvance(TutorialPlaybackState.WaitingForInput, out failReason);
        }

        public bool TryAdvanceAutomatically(out string failReason)
        {
            return TryAdvance(TutorialPlaybackState.WaitingForAutoAdvance, out failReason);
        }

        public bool TryReportEvent(
            in TutorialEvent tutorialEvent,
            out TutorialSignalResult result,
            out string failReason)
        {
            result = TutorialSignalResult.Ignored;
            failReason = string.Empty;
            if (!IsInitialized)
            {
                failReason = "TutorialManager가 초기화되지 않았습니다";
                return false;
            }

            if (tutorialEvent.Type == TutorialEventType.None)
            {
                failReason = "TutorialEventType이 비어 있습니다";
                return false;
            }

            OnEventObserved?.Invoke(tutorialEvent);

            string tutorialId = ActiveTutorialId;
            int dialogueIndex = CurrentDialogueIndex;
            TutorialDialogueDefinition dialogueDefinition = _runtime.CurrentDialogueDefinition;
            int requiredCount = dialogueDefinition?.RequiredEvents.Count ?? 0;
            if (!_runtime.TryReportEvent(
                    tutorialEvent,
                    out result,
                    out bool stepCompleted,
                    out failReason))
            {
                return false;
            }

            if (result != TutorialSignalResult.Ignored)
            {
                int progress = result == TutorialSignalResult.ConditionProgressed
                    ? _runtime.ConditionProgress
                    : requiredCount;
                OnConditionProgressChanged?.Invoke(new TutorialConditionProgressContext(
                    tutorialId,
                    dialogueIndex,
                    tutorialEvent,
                    progress,
                    requiredCount));
            }

            if (stepCompleted)
                OnTutorialCompleted?.Invoke(tutorialId);
            else if (result == TutorialSignalResult.DialogueAdvanced)
                PublishCurrentDialogue();
            return true;
        }

        public bool TryGetCurrentDialogue(out TutorialDialogueContext context)
        {
            context = default;
            if (_runtime == null
                || !_runtime.TryGetCurrentDialogue(out TutorialData dialogue, out _)
                || _runtime.CurrentDialogueDefinition == null)
            {
                return false;
            }

            context = CreateContext(dialogue, _runtime.CurrentDialogueDefinition);
            return true;
        }

        public bool IsStepCompleted(string stepId)
        {
            return _runtime?.IsStepCompleted(stepId) == true;
        }

        public string GetFirstIncompleteStepId()
        {
            return _runtime?.GetFirstIncompleteStepId() ?? string.Empty;
        }

        public void CopyCompletedTutorialIdsTo(List<string> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.Clear();
            IReadOnlyList<TutorialStepDefinition> steps = _repository.OrderedSteps;
            for (int i = 0; i < steps.Count; i++)
            {
                if (_runtime.IsStepCompleted(steps[i].StepId))
                    target.Add(steps[i].DialogueId);
            }
        }

        public TutorialProgressSaveData ExportProgress()
        {
            var progress = new TutorialProgressSaveData
            {
                definitionVersion = TutorialDefinitionCatalog.DefinitionVersion,
                activeStepId = ActiveStepId,
                activeDialogueKey = _runtime?.ActiveDialogueKey ?? string.Empty,
                playbackState = (int)PlaybackState,
                conditionProgress = _runtime?.ConditionProgress ?? 0,
                forcedFoodPriceTargetDay = ForcedFoodPriceTargetDay,
                rewardGranted = IsRewardGranted
            };
            _runtime?.CopyCompletedStepIdsTo(progress.completedStepIds);
            _controller?.CopyAppliedEffectIdsTo(progress.appliedEffectIds);
            return progress;
        }

        public void ImportProgress(
            TutorialProgressSaveData progress,
            IEnumerable<string> legacyTutorialIds,
            int legacyForcedFoodPriceTargetDay,
            bool legacyRewardGranted)
        {
            if (!IsInitialized)
                return;

            _runtime.CancelActiveStep();
            TutorialProgressSaveData normalized = progress != null
                && progress.definitionVersion == TutorialDefinitionCatalog.DefinitionVersion
                    ? progress
                    : CreateProgressFromLegacy(
                        legacyTutorialIds,
                        legacyForcedFoodPriceTargetDay,
                        legacyRewardGranted);

            _runtime.ImportCompletedStepIds(normalized.completedStepIds);
            _controller.RestoreEffectState(normalized);

            if (!string.IsNullOrWhiteSpace(normalized.activeStepId)
                && !_runtime.IsStepCompleted(normalized.activeStepId)
                && _runtime.TrySetPendingStep(normalized.activeStepId, out string failReason))
            {
                TutorialPlaybackState savedState = Enum.IsDefined(
                    typeof(TutorialPlaybackState),
                    normalized.playbackState)
                        ? (TutorialPlaybackState)normalized.playbackState
                        : TutorialPlaybackState.WaitingForEffect;

                if (savedState != TutorialPlaybackState.WaitingForActivation
                    && savedState != TutorialPlaybackState.WaitingForEffect
                    && TryResolveActiveDialogues(out IReadOnlyList<TutorialData> dialogues, out failReason)
                    && _runtime.TryStartDialogue(
                        dialogues,
                        normalized.activeDialogueKey,
                        normalized.conditionProgress,
                        out failReason))
                {
                    // 화면 애니메이션 상태는 대사 재표시 경계로 정규화
                }

                if (!string.IsNullOrEmpty(failReason))
                {
                    Debug.LogWarning($"[TutorialManager] 저장된 활성 단계 복원 실패: {failReason}");
                    _runtime.CancelActiveStep();
                }
            }
        }

        public bool CancelActiveTutorial()
        {
            if (!IsActive)
                return false;

            string cancelledTutorialId = ActiveTutorialId;
            _runtime.CancelActiveStep();
            OnTutorialCancelled?.Invoke(cancelledTutorialId);
            return true;
        }

        public bool FailActiveTutorial(string failReason)
        {
            if (!IsActive)
                return false;

            string failedTutorialId = ActiveTutorialId;
            string normalizedReason = string.IsNullOrWhiteSpace(failReason)
                ? "알 수 없는 표시 오류"
                : failReason.Trim();
            _runtime.CancelActiveStep();
            OnTutorialFailed?.Invoke(failedTutorialId, normalizedReason);
            return true;
        }

        public void ResetProgress()
        {
            string cancelledId = ActiveTutorialId;
            _runtime?.Reset();
            _firstWreckRecovery = new FirstWreckRecoverySaveData();
            _firstWreckPresentationAttempted = false;
            if (!string.IsNullOrEmpty(cancelledId))
                OnTutorialCancelled?.Invoke(cancelledId);
            OnProgressReset?.Invoke();
        }

        public string GetDebugStatus()
        {
            return $"Step={ActiveStepId}, Dialogue={ActiveTutorialId}[{CurrentDialogueIndex}], "
                + $"State={PlaybackState}, Condition={_runtime?.ConditionProgress ?? 0}";
        }

        #endregion

        #region Internal API

        internal bool TrySetPendingStep(string stepId, out string failReason)
        {
            return _runtime.TrySetPendingStep(stepId, out failReason);
        }

        internal bool TryActivatePendingStep(in TutorialEvent tutorialEvent)
        {
            return _runtime?.TryActivatePendingStep(tutorialEvent) == true;
        }

        internal bool TryStartPendingStepDialogue(out string failReason)
        {
            if (!TryResolveActiveDialogues(out IReadOnlyList<TutorialData> dialogues, out failReason))
                return false;
            if (!_runtime.TryStartDialogue(dialogues, string.Empty, 0, out failReason))
                return false;
            PublishCurrentDialogue();
            return true;
        }

        internal void RepublishCurrentDialogue()
        {
            if (_runtime?.PlaybackState == TutorialPlaybackState.Presenting)
                PublishCurrentDialogue();
        }

        internal bool CanAttemptFirstWreckRecovery =>
            HasPendingFirstWreckRecovery
            && !_firstWreckPresentationAttempted
            && !IsActive;

        internal void MarkFirstWreckPresentationAttempted()
        {
            _firstWreckPresentationAttempted = true;
        }

        internal void MarkFirstWreckRewardGranted()
        {
            _firstWreckRecovery.triggered = true;
            _firstWreckRecovery.pending = false;
            _firstWreckRecovery.rewardGranted = true;
        }

        #endregion

        #region Private Helpers

        private bool TryAdvance(TutorialPlaybackState requiredState, out string failReason)
        {
            string tutorialId = ActiveTutorialId;
            if (!_runtime.TryAdvance(
                    requiredState,
                    out bool stepCompleted,
                    out failReason))
            {
                return false;
            }

            if (stepCompleted)
                OnTutorialCompleted?.Invoke(tutorialId);
            else
                PublishCurrentDialogue();
            return true;
        }

        private void PublishCurrentDialogue()
        {
            if (TryGetCurrentDialogue(out TutorialDialogueContext context))
            {
                OnDialogueChanged?.Invoke(context);
                return;
            }

            FailActiveTutorial("현재 튜토리얼 대사 컨텍스트를 만들 수 없습니다");
        }

        private TutorialDialogueContext CreateContext(
            TutorialData dialogue,
            TutorialDialogueDefinition definition)
        {
            return new TutorialDialogueContext(
                ActiveTutorialId,
                dialogue.Type,
                dialogue.Script ?? string.Empty,
                CurrentDialogueIndex,
                ActiveStepDefinition.Dialogues.Count,
                definition.PlacementKey,
                definition.BoxPosition,
                definition.HighlightKeys,
                definition.InputPolicy,
                definition.RequiredEvents.Count > 1,
                _runtime.ConditionProgress,
                definition.RequiredEvents.Count);
        }

        private bool TryResolveActiveDialogues(
            out IReadOnlyList<TutorialData> dialogues,
            out string failReason)
        {
            dialogues = null;
            failReason = string.Empty;
            TutorialStepDefinition step = ActiveStepDefinition;
            if (step == null)
            {
                failReason = "활성 튜토리얼 단계가 없습니다";
                return false;
            }

            if (DataManager.Instance.TryGetTutorialDialogues(step.DialogueId, out dialogues)
                && dialogues != null
                && dialogues.Count == step.Dialogues.Count)
            {
                return true;
            }

            failReason = $"튜토리얼 대사 데이터를 찾을 수 없습니다: {step.DialogueId}";
            return false;
        }

        private TutorialProgressSaveData CreateProgressFromLegacy(
            IEnumerable<string> tutorialIds,
            int forcedFoodPriceTargetDay,
            bool rewardGranted)
        {
            var progress = new TutorialProgressSaveData
            {
                definitionVersion = TutorialDefinitionCatalog.DefinitionVersion,
                forcedFoodPriceTargetDay = Mathf.Max(0, forcedFoodPriceTargetDay),
                rewardGranted = rewardGranted
            };

            if (tutorialIds != null)
            {
                foreach (string tutorialId in tutorialIds)
                {
                    if (_repository.TryGetByDialogueId(tutorialId, out TutorialStepDefinition step))
                        progress.completedStepIds.Add(step.StepId);
                }
            }

            if (rewardGranted)
                progress.appliedEffectIds.Add("tutorial.reward.final");
            return progress;
        }

        #endregion
    }
}
