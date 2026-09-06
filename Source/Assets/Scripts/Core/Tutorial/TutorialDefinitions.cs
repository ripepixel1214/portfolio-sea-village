using System;
using System.Collections.Generic;
using SeaVillage.Data;
using UnityEngine;

namespace SeaVillage.Core
{
    public enum TutorialEntryMode
    {
        Immediate,
        WaitForEvent,
        RewardThenDialogue
    }

    public enum TutorialEntryCause
    {
        Completed,
        Recovery,
        ExternalEvent,
        NewGame
    }

    public enum TutorialSceneMismatchPolicy
    {
        Wait,
        ChangeToRequiredScene
    }

    public enum TutorialActionType
    {
        ResetControls,
        BlockMovement,
        AllowRightMovement,
        AllowLeftMovement,
        BlockCommandInput,
        BlockAllInteractions,
        RestrictToShop,
        RestrictToShip,
        EnsureGuide,
        FacePlayerAndGuide,
        MoveGuideToFoodShop,
        MoveGuideToShip,
        MonitorFoodShopArrival,
        MonitorShipArrival,
        ClosePanels,
        PrepareFoodPriceChange,
        RestoreFoodPriceChange,
        RestoreFoodPriceChangeWithoutPreparation,
        PauseTime,
        ResumeTime,
        FadeGuide
    }

    public sealed class TutorialDialogueDefinition
    {
        private static readonly IReadOnlyList<TutorialEventPattern> EmptyEvents =
            Array.Empty<TutorialEventPattern>();
        private static readonly IReadOnlyList<string> EmptyKeys = Array.Empty<string>();

        public TutorialDialogueDefinition(
            string dialogueKey,
            TutorialDialogueType type,
            IReadOnlyList<TutorialEventPattern> requiredEvents = null,
            IReadOnlyList<string> highlightKeys = null,
            Vector2? boxPosition = null,
            string placementKey = TutorialAnchorKeys.DefaultBox)
        {
            DialogueKey = dialogueKey ?? string.Empty;
            Type = type;
            RequiredEvents = requiredEvents ?? EmptyEvents;
            HighlightKeys = highlightKeys ?? EmptyKeys;
            BoxPosition = boxPosition;
            PlacementKey = placementKey ?? string.Empty;
        }

        public string DialogueKey { get; }
        public TutorialDialogueType Type { get; }
        public IReadOnlyList<TutorialEventPattern> RequiredEvents { get; }
        public IReadOnlyList<string> HighlightKeys { get; }
        public Vector2? BoxPosition { get; }
        public string PlacementKey { get; }
        public TutorialInputPolicy InputPolicy => HighlightKeys.Count > 0
            ? TutorialInputPolicy.BlockOutsidePrimaryHighlight
            : TutorialInputPolicy.Passthrough;
    }

    public sealed class TutorialStepDefinition
    {
        private static readonly IReadOnlyList<TutorialActionType> EmptyActions =
            Array.Empty<TutorialActionType>();

        public TutorialStepDefinition(
            string stepId,
            string dialogueId,
            string requiredScene,
            TutorialEntryMode entryMode,
            IReadOnlyList<TutorialDialogueDefinition> dialogues,
            TutorialEventPattern activationEvent = default,
            TutorialSceneMismatchPolicy sceneMismatchPolicy = TutorialSceneMismatchPolicy.Wait,
            IReadOnlyList<TutorialActionType> entryActions = null,
            IReadOnlyList<TutorialActionType> recoveryActions = null,
            IReadOnlyList<TutorialActionType> waitingActions = null,
            IReadOnlyList<TutorialActionType> activationActions = null,
            bool isOrdered = true)
        {
            StepId = stepId ?? string.Empty;
            DialogueId = dialogueId ?? string.Empty;
            RequiredScene = requiredScene ?? string.Empty;
            EntryMode = entryMode;
            Dialogues = dialogues ?? Array.Empty<TutorialDialogueDefinition>();
            ActivationEvent = activationEvent;
            SceneMismatchPolicy = sceneMismatchPolicy;
            EntryActions = entryActions ?? EmptyActions;
            RecoveryActions = recoveryActions ?? EmptyActions;
            WaitingActions = waitingActions ?? EmptyActions;
            ActivationActions = activationActions ?? EmptyActions;
            IsOrdered = isOrdered;
        }

        public string StepId { get; }
        public string DialogueId { get; }
        public string RequiredScene { get; }
        public TutorialEntryMode EntryMode { get; }
        public IReadOnlyList<TutorialDialogueDefinition> Dialogues { get; }
        public TutorialEventPattern ActivationEvent { get; }
        public TutorialSceneMismatchPolicy SceneMismatchPolicy { get; }
        public IReadOnlyList<TutorialActionType> EntryActions { get; }
        public IReadOnlyList<TutorialActionType> RecoveryActions { get; }
        public IReadOnlyList<TutorialActionType> WaitingActions { get; }
        public IReadOnlyList<TutorialActionType> ActivationActions { get; }
        public bool IsOrdered { get; }
    }

    public sealed class TutorialRepository
    {
        private readonly IReadOnlyList<TutorialStepDefinition> _orderedSteps;
        private readonly IReadOnlyList<TutorialStepDefinition> _allSteps;
        private readonly Dictionary<string, TutorialStepDefinition> _stepsById;
        private readonly Dictionary<string, TutorialStepDefinition> _stepsByDialogueId;
        private readonly Dictionary<string, List<TutorialData>> _sourceDialoguesById;
        private readonly string _configurationFailure;

        public TutorialRepository(
            IReadOnlyList<TutorialStepDefinition> definitions,
            IReadOnlyList<TutorialData> sourceDialogues)
        {
            definitions ??= Array.Empty<TutorialStepDefinition>();
            sourceDialogues ??= Array.Empty<TutorialData>();

            var orderedSteps = new List<TutorialStepDefinition>(definitions.Count);
            var allSteps = new List<TutorialStepDefinition>(definitions.Count);
            _stepsById = new Dictionary<string, TutorialStepDefinition>(StringComparer.Ordinal);
            _stepsByDialogueId = new Dictionary<string, TutorialStepDefinition>(StringComparer.Ordinal);
            _sourceDialoguesById = new Dictionary<string, List<TutorialData>>(StringComparer.Ordinal);

            string configurationFailure = string.Empty;
            for (int i = 0; i < definitions.Count; i++)
            {
                TutorialStepDefinition step = definitions[i];
                if (step == null)
                {
                    configurationFailure = $"튜토리얼 단계 정의[{i}]가 null입니다";
                    continue;
                }

                if (string.IsNullOrWhiteSpace(step.StepId)
                    || !_stepsById.TryAdd(step.StepId, step))
                {
                    configurationFailure = $"StepId가 비어 있거나 중복되었습니다: {step.StepId}";
                }

                if (string.IsNullOrWhiteSpace(step.DialogueId)
                    || !_stepsByDialogueId.TryAdd(step.DialogueId, step))
                {
                    configurationFailure = $"DialogueId가 비어 있거나 중복되었습니다: {step.DialogueId}";
                }

                allSteps.Add(step);
            }

            string previousDialogueId = string.Empty;
            var completedDialogueIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < sourceDialogues.Count; i++)
            {
                TutorialData source = sourceDialogues[i];
                if (source == null || string.IsNullOrWhiteSpace(source.ID))
                {
                    configurationFailure = $"스프레드시트 튜토리얼 행[{i}]의 ID가 비어 있습니다";
                    continue;
                }

                string dialogueId = source.ID.Trim();
                if (!string.Equals(previousDialogueId, dialogueId, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(previousDialogueId))
                        completedDialogueIds.Add(previousDialogueId);
                    if (completedDialogueIds.Contains(dialogueId))
                        configurationFailure = $"튜토리얼 ID가 비연속 구간에서 다시 등장합니다: {dialogueId}";

                    previousDialogueId = dialogueId;
                    if (!_stepsByDialogueId.TryGetValue(dialogueId, out TutorialStepDefinition step))
                    {
                        configurationFailure = $"진행 규칙이 없는 튜토리얼 ID입니다: {dialogueId}";
                    }
                    else if (step.IsOrdered)
                    {
                        orderedSteps.Add(step);
                    }
                }

                if (!_sourceDialoguesById.TryGetValue(dialogueId, out List<TutorialData> dialogues))
                {
                    dialogues = new List<TutorialData>();
                    _sourceDialoguesById.Add(dialogueId, dialogues);
                }
                dialogues.Add(source);
            }

            int orderedDefinitionCount = 0;
            for (int i = 0; i < allSteps.Count; i++)
            {
                if (allSteps[i].IsOrdered)
                    orderedDefinitionCount++;
            }

            if (orderedSteps.Count != orderedDefinitionCount && string.IsNullOrEmpty(configurationFailure))
                configurationFailure = "스프레드시트와 진행 규칙의 튜토리얼 개수가 다릅니다";

            _orderedSteps = orderedSteps;
            _allSteps = allSteps;
            _configurationFailure = configurationFailure;
        }

        public IReadOnlyList<TutorialStepDefinition> OrderedSteps => _orderedSteps;
        public IReadOnlyList<TutorialStepDefinition> AllSteps => _allSteps;
        public string FirstStepId => _orderedSteps.Count > 0 ? _orderedSteps[0].StepId : string.Empty;

        public bool TryGet(string stepId, out TutorialStepDefinition step)
        {
            step = null;
            return !string.IsNullOrWhiteSpace(stepId)
                && _stepsById.TryGetValue(stepId.Trim(), out step);
        }

        public bool TryGetByDialogueId(string dialogueId, out TutorialStepDefinition step)
        {
            step = null;
            return !string.IsNullOrWhiteSpace(dialogueId)
                && _stepsByDialogueId.TryGetValue(dialogueId.Trim(), out step);
        }

        public bool TryValidate(out string failReason)
        {
            failReason = _configurationFailure;
            if (!string.IsNullOrEmpty(failReason))
                return false;

            if (_orderedSteps.Count == 0)
            {
                failReason = "튜토리얼 단계 정의가 비어 있습니다";
                return false;
            }

            for (int i = 0; i < _allSteps.Count; i++)
            {
                TutorialStepDefinition step = _allSteps[i];
                if (string.IsNullOrWhiteSpace(step.RequiredScene))
                {
                    failReason = $"필수 씬이 비어 있습니다: {step.StepId}";
                    return false;
                }

                if (!_sourceDialoguesById.TryGetValue(step.DialogueId, out List<TutorialData> sourceDialogues)
                    || sourceDialogues.Count != step.Dialogues.Count)
                {
                    failReason = $"대사 개수가 정의와 다릅니다: {step.DialogueId}";
                    return false;
                }

                var dialogueKeys = new HashSet<string>(StringComparer.Ordinal);
                for (int dialogueIndex = 0; dialogueIndex < step.Dialogues.Count; dialogueIndex++)
                {
                    TutorialDialogueDefinition dialogue = step.Dialogues[dialogueIndex];
                    TutorialData source = sourceDialogues[dialogueIndex];
                    if (dialogue == null
                        || string.IsNullOrWhiteSpace(dialogue.DialogueKey)
                        || !dialogueKeys.Add(dialogue.DialogueKey))
                    {
                        failReason = $"대사 키가 비어 있거나 중복되었습니다: {step.StepId}[{dialogueIndex}]";
                        return false;
                    }

                    if (source == null || source.Type != dialogue.Type || source.Sequence != dialogueIndex)
                    {
                        failReason = $"대사 타입 또는 순서가 정의와 다릅니다: {step.DialogueId}[{dialogueIndex}]";
                        return false;
                    }

                    if (dialogue.Type == TutorialDialogueType.Box)
                    {
                        if (dialogue.RequiredEvents.Count == 0)
                        {
                            failReason = $"Box 대사의 이벤트가 비어 있습니다: {dialogue.DialogueKey}";
                            return false;
                        }

                        for (int eventIndex = 0; eventIndex < dialogue.RequiredEvents.Count; eventIndex++)
                        {
                            if (dialogue.RequiredEvents[eventIndex].Type != TutorialEventType.None)
                                continue;
                            failReason = $"Box 대사에 등록되지 않은 이벤트가 있습니다: {dialogue.DialogueKey}";
                            return false;
                        }

                        for (int highlightIndex = 0; highlightIndex < dialogue.HighlightKeys.Count; highlightIndex++)
                        {
                            if (!string.IsNullOrWhiteSpace(dialogue.HighlightKeys[highlightIndex]))
                                continue;
                            failReason = $"강조 대상 키가 비어 있습니다: {dialogue.DialogueKey}";
                            return false;
                        }

                        if (dialogue.HighlightKeys.Count > 0
                            && dialogue.HighlightKeys.Count != dialogue.RequiredEvents.Count)
                        {
                            failReason = $"이벤트와 강조 대상 개수가 다릅니다: {dialogue.DialogueKey}";
                            return false;
                        }
                    }
                    else if (dialogue.RequiredEvents.Count > 0)
                    {
                        failReason = $"{dialogue.Type} 대사에는 이벤트 조건을 지정할 수 없습니다: {dialogue.DialogueKey}";
                        return false;
                    }
                }

                if (step.EntryMode == TutorialEntryMode.WaitForEvent
                    && step.ActivationEvent.Type == TutorialEventType.None)
                {
                    failReason = $"대기 단계의 활성화 이벤트가 없습니다: {step.StepId}";
                    return false;
                }


                if (!TryValidateActions(step.EntryActions, step.StepId, out failReason)
                    || !TryValidateActions(step.RecoveryActions, step.StepId, out failReason)
                    || !TryValidateActions(step.WaitingActions, step.StepId, out failReason)
                    || !TryValidateActions(step.ActivationActions, step.StepId, out failReason))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryValidateActions(
            IReadOnlyList<TutorialActionType> actions,
            string stepId,
            out string failReason)
        {
            failReason = string.Empty;
            for (int i = 0; i < actions.Count; i++)
            {
                if (Enum.IsDefined(typeof(TutorialActionType), actions[i]))
                    continue;
                failReason = $"등록되지 않은 행동입니다: {stepId}[{i}]";
                return false;
            }

            return true;
        }
    }
}
