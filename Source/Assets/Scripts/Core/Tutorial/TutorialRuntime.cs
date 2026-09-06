using System;
using System.Collections.Generic;
using SeaVillage.Data;

namespace SeaVillage.Core
{
    public sealed class TutorialRuntime
    {
        private static readonly IReadOnlyList<TutorialData> EmptyDialogues = Array.Empty<TutorialData>();

        private readonly TutorialRepository _repository;
        private readonly HashSet<string> _completedStepIds =
            new HashSet<string>(StringComparer.Ordinal);

        private IReadOnlyList<TutorialData> _activeDialogues = EmptyDialogues;
        private TutorialStepDefinition _activeStep;
        private int _dialogueIndex = -1;
        private int _conditionProgress;

        public TutorialRuntime(TutorialRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        #region Properties

        public bool HasActiveStep => _activeStep != null && PlaybackState != TutorialPlaybackState.Inactive;
        public string ActiveStepId => _activeStep?.StepId ?? string.Empty;
        public string ActiveDialogueId => _activeStep?.DialogueId ?? string.Empty;
        public string ActiveDialogueKey => CurrentDialogueDefinition?.DialogueKey ?? string.Empty;
        public int DialogueIndex => _dialogueIndex;
        public int ConditionProgress => _conditionProgress;
        public TutorialPlaybackState PlaybackState { get; private set; } = TutorialPlaybackState.Inactive;
        public TutorialStepDefinition ActiveStep => _activeStep;
        public TutorialDialogueDefinition CurrentDialogueDefinition =>
            _activeStep != null && _dialogueIndex >= 0 && _dialogueIndex < _activeStep.Dialogues.Count
                ? _activeStep.Dialogues[_dialogueIndex]
                : null;

        #endregion

        #region Public API

        public bool TrySetPendingStep(string stepId, out string failReason)
        {
            failReason = string.Empty;
            if (!_repository.TryGet(stepId, out TutorialStepDefinition step))
            {
                failReason = $"튜토리얼 StepId를 찾을 수 없습니다: {stepId}";
                return false;
            }

            if (_completedStepIds.Contains(step.StepId))
            {
                failReason = $"이미 완료된 튜토리얼 단계입니다: {step.StepId}";
                return false;
            }

            _activeStep = step;
            _activeDialogues = EmptyDialogues;
            _dialogueIndex = -1;
            _conditionProgress = 0;
            PlaybackState = step.EntryMode == TutorialEntryMode.WaitForEvent
                ? TutorialPlaybackState.WaitingForActivation
                : TutorialPlaybackState.WaitingForEffect;
            return true;
        }

        public bool TryStartDialogue(
            IReadOnlyList<TutorialData> dialogues,
            string dialogueKey,
            int conditionProgress,
            out string failReason)
        {
            failReason = string.Empty;
            if (_activeStep == null)
            {
                failReason = "활성 튜토리얼 단계가 없습니다";
                return false;
            }

            if (dialogues == null || dialogues.Count != _activeStep.Dialogues.Count)
            {
                failReason = $"대사 데이터 개수가 정의와 다릅니다: {_activeStep.DialogueId}";
                return false;
            }

            int resolvedIndex = 0;
            if (!string.IsNullOrWhiteSpace(dialogueKey))
            {
                resolvedIndex = FindDialogueIndex(dialogueKey.Trim());
                if (resolvedIndex < 0)
                {
                    failReason = $"저장된 DialogueKey를 찾을 수 없습니다: {dialogueKey}";
                    return false;
                }
            }

            _activeDialogues = dialogues;
            _dialogueIndex = resolvedIndex;
            TutorialDialogueDefinition definition = CurrentDialogueDefinition;
            _conditionProgress = Math.Clamp(conditionProgress, 0, definition.RequiredEvents.Count);
            PlaybackState = TutorialPlaybackState.Presenting;
            return true;
        }

        public bool NotifyCurrentDialoguePresented(out string failReason)
        {
            failReason = string.Empty;
            if (!TryGetCurrentDialogue(out TutorialData dialogue, out failReason))
                return false;

            if (PlaybackState != TutorialPlaybackState.Presenting)
            {
                failReason = $"현재 상태에서는 대사 표시 완료를 처리할 수 없습니다: {PlaybackState}";
                return false;
            }

            PlaybackState = dialogue.Type switch
            {
                TutorialDialogueType.Stop => TutorialPlaybackState.WaitingForInput,
                TutorialDialogueType.Auto => TutorialPlaybackState.WaitingForAutoAdvance,
                TutorialDialogueType.Box => TutorialPlaybackState.WaitingForTrigger,
                _ => TutorialPlaybackState.Inactive
            };

            if (PlaybackState != TutorialPlaybackState.Inactive)
                return true;

            failReason = $"지원하지 않는 튜토리얼 대사 타입입니다: {dialogue.Type}";
            return false;
        }

        public bool TryAdvance(
            TutorialPlaybackState requiredState,
            out bool stepCompleted,
            out string failReason)
        {
            stepCompleted = false;
            failReason = string.Empty;
            if (PlaybackState != requiredState)
            {
                failReason = $"현재 상태에서는 대사를 진행할 수 없습니다: {PlaybackState}";
                return false;
            }

            return AdvanceCurrentDialogue(out stepCompleted, out failReason);
        }

        public bool TryReportEvent(
            in TutorialEvent tutorialEvent,
            out TutorialSignalResult result,
            out bool stepCompleted,
            out string failReason)
        {
            result = TutorialSignalResult.Ignored;
            stepCompleted = false;
            failReason = string.Empty;

            if (tutorialEvent.Type == TutorialEventType.None)
            {
                failReason = "튜토리얼 이벤트 타입이 비어 있습니다";
                return false;
            }

            if (PlaybackState != TutorialPlaybackState.WaitingForTrigger)
                return true;

            TutorialDialogueDefinition definition = CurrentDialogueDefinition;
            if (definition == null || definition.RequiredEvents.Count == 0)
            {
                failReason = "현재 대사의 이벤트 조건이 구성되지 않았습니다";
                return false;
            }

            if (_conditionProgress >= definition.RequiredEvents.Count
                || !definition.RequiredEvents[_conditionProgress].Matches(tutorialEvent))
            {
                return true;
            }

            _conditionProgress++;
            if (_conditionProgress < definition.RequiredEvents.Count)
            {
                result = TutorialSignalResult.ConditionProgressed;
                return true;
            }

            if (!AdvanceCurrentDialogue(out stepCompleted, out failReason))
                return false;

            result = stepCompleted
                ? TutorialSignalResult.TutorialCompleted
                : TutorialSignalResult.DialogueAdvanced;
            return true;
        }

        public bool TryActivatePendingStep(in TutorialEvent tutorialEvent)
        {
            if (PlaybackState != TutorialPlaybackState.WaitingForActivation
                || _activeStep == null
                || !_activeStep.ActivationEvent.Matches(tutorialEvent))
            {
                return false;
            }

            PlaybackState = TutorialPlaybackState.WaitingForEffect;
            return true;
        }

        public bool TryGetCurrentDialogue(out TutorialData dialogue, out string failReason)
        {
            dialogue = null;
            failReason = string.Empty;
            if (_activeStep == null || _activeDialogues.Count == 0)
            {
                failReason = "표시 중인 튜토리얼 대사가 없습니다";
                return false;
            }

            if (_dialogueIndex < 0 || _dialogueIndex >= _activeDialogues.Count)
            {
                failReason = $"튜토리얼 대사 인덱스가 유효하지 않습니다: {_dialogueIndex}";
                return false;
            }

            dialogue = _activeDialogues[_dialogueIndex];
            if (dialogue != null)
                return true;

            failReason = $"튜토리얼 대사 데이터가 비어 있습니다: {ActiveDialogueId}[{_dialogueIndex}]";
            return false;
        }

        public bool IsStepCompleted(string stepId)
        {
            return !string.IsNullOrWhiteSpace(stepId) && _completedStepIds.Contains(stepId.Trim());
        }

        public string GetFirstIncompleteStepId()
        {
            IReadOnlyList<TutorialStepDefinition> steps = _repository.OrderedSteps;
            for (int i = 0; i < steps.Count; i++)
            {
                if (!_completedStepIds.Contains(steps[i].StepId))
                    return steps[i].StepId;
            }

            return string.Empty;
        }

        public void CopyCompletedStepIdsTo(List<string> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.Clear();
            IReadOnlyList<TutorialStepDefinition> steps = _repository.OrderedSteps;
            for (int i = 0; i < steps.Count; i++)
            {
                if (_completedStepIds.Contains(steps[i].StepId))
                    target.Add(steps[i].StepId);
            }
        }

        public void ImportCompletedStepIds(IEnumerable<string> stepIds)
        {
            _completedStepIds.Clear();
            if (stepIds == null)
                return;

            foreach (string stepId in stepIds)
            {
                if (_repository.TryGet(stepId, out TutorialStepDefinition step))
                    _completedStepIds.Add(step.StepId);
            }
        }

        public void CompleteAll()
        {
            _completedStepIds.Clear();
            IReadOnlyList<TutorialStepDefinition> steps = _repository.OrderedSteps;
            for (int i = 0; i < steps.Count; i++)
                _completedStepIds.Add(steps[i].StepId);
            ClearActiveStep();
        }

        public void CancelActiveStep()
        {
            ClearActiveStep();
        }

        public void Reset()
        {
            _completedStepIds.Clear();
            ClearActiveStep();
        }

        #endregion

        #region Private Helpers

        private bool AdvanceCurrentDialogue(
            out bool stepCompleted,
            out string failReason)
        {
            stepCompleted = false;
            failReason = string.Empty;

            int nextDialogueIndex = _dialogueIndex + 1;
            if (nextDialogueIndex < _activeDialogues.Count)
            {
                _dialogueIndex = nextDialogueIndex;
                _conditionProgress = 0;
                PlaybackState = TutorialPlaybackState.Presenting;
                return true;
            }

            string completedStepId = _activeStep.StepId;
            _completedStepIds.Add(completedStepId);
            ClearActiveStep();
            stepCompleted = true;
            return true;
        }

        private int FindDialogueIndex(string dialogueKey)
        {
            for (int i = 0; i < _activeStep.Dialogues.Count; i++)
            {
                if (string.Equals(
                        _activeStep.Dialogues[i].DialogueKey,
                        dialogueKey,
                        StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private void ClearActiveStep()
        {
            _activeStep = null;
            _activeDialogues = EmptyDialogues;
            _dialogueIndex = -1;
            _conditionProgress = 0;
            PlaybackState = TutorialPlaybackState.Inactive;
        }

        #endregion
    }
}
