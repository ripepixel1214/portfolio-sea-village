using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SeaVillage.Core;
using SeaVillage.Data;
using SeaVillage.Event.Services;

using ExpressionEvaluator = SeaVillage.Utilities.ExpressionEvaluator;

namespace SeaVillage.Event
{
    public interface IEventCommand
    {
        IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services);
    }

    public sealed class EventCommandServices
    {
        public IStatService StatService { get; }
        public IUIService UIService { get; }
        public IQuestService QuestService { get; }
        public IEventNpcRuntime EventNpcRuntime { get; }
        public IChoiceService ChoiceService { get; }
        public ExpressionEvaluator Evaluator { get; }
        public EventItemReservationLedger ItemReservationLedger { get; }
        public EventUiCoordinator UiCoordinator { get; }

        public EventCommandServices(
            IStatService statService,
            IUIService uiService,
            IQuestService questService,
            IEventNpcRuntime eventNpcRuntime,
            IChoiceService choiceService,
            ExpressionEvaluator evaluator,
            EventItemReservationLedger itemReservationLedger = null,
            EventUiCoordinator uiCoordinator = null)
        {
            StatService = statService;
            UIService = uiService;
            QuestService = questService;
            EventNpcRuntime = eventNpcRuntime;
            ChoiceService = choiceService;
            Evaluator = evaluator;
            ItemReservationLedger = itemReservationLedger;
            UiCoordinator = uiCoordinator;
        }
    }

    public sealed class EventCommandFactory
    {
        public const string EVENT_COMMAND_PANEL = "Panel";
        public const string EVENT_COMMAND_DIALOGUE = "Dialogue";
        public const string EVENT_COMMAND_CHOICE = "Choice";
        public const string EVENT_COMMAND_CHECKSTAT = "CheckStat";
        public const string EVENT_COMMAND_CALCULATE = "Calculate";
        public const string EVENT_COMMAND_PERCENT = "Percent";
        public const string EVENT_COMMAND_QUEST = "Quest";
        public const string EVENT_COMMAND_INTERACTION = "Interaction";
        public const string EVENT_COMMAND_SETACTIVE = "SetActive";
        public const string EVENT_COMMAND_SUBMIT = "Submit";

        private readonly Dictionary<string, IEventCommand> _commands;

        public EventCommandFactory()
        {
            _commands = new Dictionary<string, IEventCommand>(StringComparer.OrdinalIgnoreCase)
            {
                {EVENT_COMMAND_PANEL, new PanelCommand()},
                {EVENT_COMMAND_DIALOGUE, new DialogueCommand()},
                {EVENT_COMMAND_CHOICE, new ChoiceCommand()},
                {EVENT_COMMAND_CHECKSTAT, new CheckStatCommand()},
                {EVENT_COMMAND_CALCULATE, new CalculateCommand()},
                {EVENT_COMMAND_PERCENT, new PercentCommand()},
                {EVENT_COMMAND_QUEST, new QuestCommand()},
                {EVENT_COMMAND_INTERACTION, new InteractionCommand()},
                {EVENT_COMMAND_SETACTIVE, new SetActiveCommand()},
                {EVENT_COMMAND_SUBMIT, new SubmitCommand()}
            };
        }

        public IEventCommand Get(string command)
        {
            if (string.IsNullOrEmpty(command)) return null;
            return _commands.TryGetValue(command, out var cmd) ? cmd : null;
        }
    }

    public sealed class PanelCommand : IEventCommand
    {
        private const string OceanSceneName = "Ocean";

        public IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services)
        {
            var step = sequences[0];

            // 결과 대사 매핑이 있으면 EventDialogue 를 결과 레이아웃으로 표시(확인까지 대기)
            if (EventResultDialogues.TryGet(context.EventData.EventID, step.Step, out int dialogueId))
            {
                if (services?.UiCoordinator != null)
                    yield return services.UiCoordinator.Acquire(context.RuntimeId);

                try
                {
                    yield return ShowResultDialogue(context, step, dialogueId);
                }
                finally
                {
                    services?.UiCoordinator?.Release(context.RuntimeId);
                }
                yield break;
            }

            if (IsTownEvent(context)
                && TryGetSingleConfirmation(context, step, out EventSequenceData confirmation))
            {
                bool completed = false;
                if (services?.UiCoordinator != null)
                    yield return services.UiCoordinator.Acquire(context.RuntimeId);

                try
                {
                    if (services?.UIService != null)
                    {
                        string popupContent = step.Value;
                        if (IsQuestCompletionPanel(step) && EventManager.HasInstance)
                        {
                            popupContent = EventManager.Instance.BuildQuestCompletionPanelContent(
                                context,
                                step,
                                confirmation,
                                services);
                        }

                        yield return services.UIService.ShowPopup(
                            context.EventData.Name,
                            popupContent,
                            confirmation.Value,
                            result => completed = result);
                    }
                }
                finally
                {
                    services?.UiCoordinator?.Release(context.RuntimeId);
                }

                if (!completed)
                {
                    context.State = EventState.Failed;
                    context.SetNextStep(-1);
                    yield break;
                }

                if (confirmation.NextStep < 0 && EventSequenceEntryResolver.IsQuestFlow(context.Sequences))
                {
                    if (services?.QuestService != null)
                        yield return services.QuestService.StartQuest(context.EventData.EventID);

                    if (services?.QuestService == null
                        || !services.QuestService.IsQuestActive(context.EventData.EventID))
                    {
                        Debug.LogError($"[PanelCommand] 이벤트 퀘스트를 시작하지 못했습니다: {context.EventData.EventID}");
                        context.State = EventState.Failed;
                        context.SetNextStep(-1);
                        yield break;
                    }
                }

                context.SetNextStep(confirmation.NextStep);
                yield break;
            }

            bool isChoicePrompt = IsNextStepChoice(context, step);

            if (services?.UiCoordinator != null)
                yield return services.UiCoordinator.Acquire(context.RuntimeId);

            try
            {
                if (services?.UIService != null)
                    yield return services.UIService.ShowPanel(step.Target, step.Value, isChoicePrompt);
                else
                {
                    Debug.LogError($"[PanelCommand] 이벤트 UI 서비스가 없습니다: {context.EventData.EventID}, step={step.Step}");
                    context.State = EventState.Failed;
                    context.SetNextStep(-1);
                    yield break;
                }
            }
            finally
            {
                // 선택지가 이어지는 동안에는 같은 Event Panel을 다음 ChoiceCommand가 소유한다
                if (!isChoicePrompt)
                    services?.UiCoordinator?.Release(context.RuntimeId);
            }

            context.SetNextStep(step.NextStep);
        }

        // 결과 대사를 Event Panel 결과 레이아웃에 표시하고, 확인 후 이어지는 중복 '확인' Choice 는 건너뛴다.
        private static IEnumerator ShowResultDialogue(EventExecutionContext context, EventSequenceData step, int dialogueId)
        {
            string text = ResolveDialogueText(dialogueId, step.Value);

            UI.EventPanel panel = UI.UIManager.HasInstance ? UI.UIManager.Instance.OpenPanel<UI.EventPanel>() : null;
            if (panel == null)
            {
                context.State = EventState.Failed;
                context.SetNextStep(-1);
                yield break;
            }

            bool confirmed = false;
            panel.ShowResultDialogue(text, () => confirmed = true);
            while (!confirmed)
            {
                if (panel == null || panel.State == UI.PanelState.Closed)
                {
                    context.State = EventState.Failed;
                    context.SetNextStep(-1);
                    yield break;
                }

                yield return null;
            }

            if (UI.UIManager.HasInstance)
            {
                UI.UIManager.Instance.ClosePanel<UI.EventPanel>();
                while (panel != null && panel.State != UI.PanelState.Closed)
                    yield return null;
            }

            context.SetNextStep(NextStepSkippingConfirm(context, step));
        }

        private static string ResolveDialogueText(int dialogueId, string fallback)
        {
            EventDialogueData dialogue = EventManager.HasInstance ? EventManager.Instance.GetDialogue(dialogueId) : null;
            return dialogue != null && !string.IsNullOrEmpty(dialogue.Script) ? dialogue.Script : fallback;
        }

        // 결과 직후 이어지는 스텝이 단일 '확인' Choice 면 건너뛴다(결과 레이아웃이 이미 확인을 처리하므로).
        private static int NextStepSkippingConfirm(EventExecutionContext context, EventSequenceData step)
        {
            int next = step.NextStep;
            var seqs = context.GetSequencesByStep(next);
            if (seqs != null && seqs.Count == 1
                && string.Equals(seqs[0].Command, EventCommandFactory.EVENT_COMMAND_CHOICE, StringComparison.OrdinalIgnoreCase))
                return seqs[0].NextStep;
            return next;
        }

        private static bool IsNextStepChoice(EventExecutionContext context, EventSequenceData step)
        {
            var next = context.GetSequencesByStep(step.NextStep);
            return next != null && next.Count > 0
                && string.Equals(next[0].Command, EventCommandFactory.EVENT_COMMAND_CHOICE, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTownEvent(EventExecutionContext context)
        {
            return context?.EventData != null
                && string.Equals(
                    context.EventData.TriggerType,
                    EventManager.EVENT_TRIGGER_ONENTERMAP,
                    StringComparison.OrdinalIgnoreCase)
                && !string.Equals(context.EventData.SceneName, OceanSceneName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsQuestCompletionPanel(EventSequenceData step)
        {
            return step != null
                && string.Equals(step.Value?.Trim(), "퀘스트 클리어", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetSingleConfirmation(
            EventExecutionContext context,
            EventSequenceData step,
            out EventSequenceData confirmation)
        {
            confirmation = null;
            List<EventSequenceData> next = context.GetSequencesByStep(step.NextStep);
            if (next == null
                || next.Count != 1
                || !string.Equals(next[0].Command, EventCommandFactory.EVENT_COMMAND_CHOICE, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            confirmation = next[0];
            return true;
        }

    }

    public sealed class DialogueCommand : IEventCommand
    {
        public IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services)
        {
            var step = sequences[0];

            bool completed = false;
            if (services?.UiCoordinator != null)
                yield return services.UiCoordinator.Acquire(context.RuntimeId);

            try
            {
                if (services?.EventNpcRuntime != null)
                    yield return services.EventNpcRuntime.ShowDialogue(step.Target, step.Value, result => completed = result);
            }
            finally
            {
                services?.UiCoordinator?.Release(context.RuntimeId);
            }

            if (!completed)
            {
                context.State = EventState.Failed;
                context.SetNextStep(-1);
                yield break;
            }

            context.SetNextStep(step.NextStep);
        }
    }

    public sealed class ChoiceCommand : IEventCommand
    {
        public IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services)
        {
            if (sequences == null || sequences.Count == 0)
            {
                context.SetNextStep(-1);
                yield break;
            }

            EventSequenceData chosenSequence = null;
            if (services?.UiCoordinator != null)
                yield return services.UiCoordinator.Acquire(context.RuntimeId);

            try
            {
                if (services?.ChoiceService != null)
                    yield return services.ChoiceService.Choose(sequences, result => chosenSequence = result);
                else
                {
                    chosenSequence = sequences[0];
                    Debug.Log("Choice: auto-select first option (no choice service implemented)");
                    yield return null;
                }
            }
            finally
            {
                services?.UiCoordinator?.Release(context.RuntimeId);
            }

            if (chosenSequence == null)
            {
                context.State = EventState.Failed;
                context.SetNextStep(-1);
                yield break;
            }

            context.SetNextStep(chosenSequence.NextStep);
            yield return null;
        }
    }

    public sealed class CheckStatCommand : IEventCommand
    {
        public IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services)
        {
            var seq = sequences[0];
            if (!long.TryParse(seq.Value, out var refValue))
            {
                Debug.LogWarning($"CheckStat value parse failed: {seq.Value}");
                context.SetNextStep(seq.FailStep);
                yield break;
            }

            long stat = services?.StatService?.GetStatLong(seq.Target) ?? 0L;
            bool pass = stat >= refValue;
            context.SetNextStep(pass ? seq.NextStep : seq.FailStep);
            yield return null;
        }
    }

    public sealed class CalculateCommand : IEventCommand
    {
        public IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services)
        {
            var seq = sequences[0];
            if (string.Equals(seq.Target, "SubmitItem", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteSubmitItemCheck(context, seq, services);
                yield break;
            }

            if (int.TryParse(seq.Target?.Trim(), out int itemId) && itemId > 0)
            {
                yield return ExecuteInventoryItem(context, seq, services, itemId);
                yield break;
            }

            if (!TryResolveDelta(seq.Value, services, out int delta))
            {
                Fail(context, $"계산 수식을 해석할 수 없습니다: {seq.Value}");
                yield break;
            }

            if (context.HasPendingItemRemovals
                && string.Equals(seq.Target, EventManager.CONTEXT_KEY_GOLD, StringComparison.OrdinalIgnoreCase))
            {
                context.MarkQuestCompletionPending();
            }

            services?.StatService?.ChangeStat(seq.Target, delta);
            context.SetNextStep(seq.NextStep);
            yield break;
        }

        private static void ExecuteSubmitItemCheck(
            EventExecutionContext context,
            EventSequenceData sequence,
            EventCommandServices services)
        {
            if (!int.TryParse(sequence.Value?.Trim(), out int itemId) || itemId <= 0)
            {
                Fail(context, $"제출 아이템 ID가 올바르지 않습니다: {sequence.Value}");
                return;
            }

            InventoryData inventory = InventoryManager.PlayerInventoryOrNull;
            if (inventory == null)
            {
                Fail(context, $"플레이어 인벤토리가 준비되지 않았습니다: itemId={itemId}");
                return;
            }

            bool matchesSelection = context.SubmittedItemId <= 0 || context.SubmittedItemId == itemId;
            bool success = matchesSelection
                && (services?.ItemReservationLedger != null
                    ? services.ItemReservationLedger.TryReserve(context.RuntimeId, itemId, 1, inventory)
                    : inventory.HasItem(itemId, context.GetPendingItemRemoval(itemId) + 1));
            if (success)
            {
                context.ReserveItemRemoval(itemId, 1);
            }

            context.SetNextStep(success ? sequence.NextStep : sequence.FailStep);
        }

        private static IEnumerator ExecuteInventoryItem(
            EventExecutionContext context,
            EventSequenceData sequence,
            EventCommandServices services,
            int itemId)
        {
            InventoryData inventory = InventoryManager.PlayerInventoryOrNull;
            if (inventory == null)
            {
                Fail(context, $"플레이어 인벤토리가 준비되지 않았습니다: itemId={itemId}");
                yield break;
            }

            if (!DataManager.HasInstance || DataManager.Instance.GetItem(itemId) == null)
            {
                Fail(context, $"이벤트 아이템 데이터가 없습니다: itemId={itemId}");
                yield break;
            }

            if (sequence.Value?.Trim() == "?")
            {
                context.SetNextStep(inventory.HasItem(itemId) ? sequence.NextStep : sequence.FailStep);
                yield break;
            }

            if (!int.TryParse(sequence.Value, out int delta) || delta == 0 || delta == int.MinValue)
            {
                Fail(context, $"아이템 연산값이 올바르지 않습니다: {sequence.Value}");
                yield break;
            }

            bool isQuestToken = IsQuestToken(context, itemId);
            IQuestService questService = services?.QuestService;
            string eventId = context.EventData.EventID;

            if (delta > 0)
            {
                if (!inventory.AddItem(itemId, delta))
                {
                    Fail(context, $"아이템을 인벤토리에 추가하지 못했습니다: itemId={itemId}, quantity={delta}");
                    yield break;
                }

                if (isQuestToken)
                {
                    if (questService != null)
                        yield return questService.StartQuest(eventId);

                    if (questService == null || !questService.IsQuestActive(eventId))
                    {
                        inventory.RemoveItem(itemId, delta);
                        Fail(context, $"이벤트 퀘스트를 시작하지 못했습니다: {eventId}");
                        yield break;
                    }
                }
            }
            else
            {
                int quantity = -delta;
                if (context.GetPendingItemRemoval(itemId) >= quantity)
                {
                    context.SetNextStep(sequence.NextStep);
                    yield break;
                }

                if (isQuestToken && (questService == null || !questService.IsQuestActive(eventId)))
                {
                    Fail(context, $"완료할 이벤트 퀘스트가 활성 상태가 아닙니다: {eventId}");
                    yield break;
                }

                if (isQuestToken && services?.ItemReservationLedger != null)
                {
                    if (!services.ItemReservationLedger.TryReserve(
                            context.RuntimeId,
                            itemId,
                            quantity,
                            inventory))
                    {
                        Fail(context, $"이벤트 아이템이 다른 퀘스트에서 예약되어 있습니다: itemId={itemId}");
                        yield break;
                    }

                    context.ReserveItemRemoval(itemId, quantity);
                    context.MarkQuestCompletionPending();
                    context.SetNextStep(sequence.NextStep);
                    yield break;
                }

                if (!inventory.RemoveItem(itemId, quantity))
                {
                    Fail(context, $"제거할 아이템 수량이 부족합니다: itemId={itemId}, quantity={quantity}");
                    yield break;
                }

                if (isQuestToken)
                    context.MarkQuestCompletionPending();

            }

            context.SetNextStep(sequence.NextStep);
            yield break;
        }

        private static bool IsQuestToken(EventExecutionContext context, int itemId)
        {
            bool hasInventoryCheck = false;
            bool hasRemoval = false;

            IReadOnlyList<EventSequenceData> sequences = context.Sequences;
            for (int i = 0; i < sequences.Count; i++)
            {
                EventSequenceData candidate = sequences[i];
                if (!int.TryParse(candidate.Target?.Trim(), out int candidateItemId)
                    || candidateItemId != itemId)
                {
                    continue;
                }

                string value = candidate.Value?.Trim();
                if (value == "?")
                    hasInventoryCheck = true;
                else if (int.TryParse(value, out int candidateDelta) && candidateDelta < 0)
                    hasRemoval = true;

                if (hasInventoryCheck && hasRemoval)
                    return true;
            }

            return false;
        }

        private static void Fail(EventExecutionContext context, string message)
        {
            Debug.LogError($"[CalculateCommand] {message}");
            context.State = EventState.Failed;
            context.SetNextStep(-1);
        }

        private static bool TryResolveDelta(string value, EventCommandServices services, out int delta)
        {
            if (int.TryParse(value, out delta))
                return true;

            if (services?.Evaluator != null && services.StatService != null
                && services.Evaluator.TryEvaluateNumber(value, key => services.StatService.GetStatLong(key), out float evaluated))
            {
                delta = Mathf.RoundToInt(evaluated);
                return true;
            }

            delta = 0;
            return false;
        }
    }

    public sealed class PercentCommand : IEventCommand
    {
        public IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services)
        {
            var seq = sequences[0];
            if (!TryParseFormula(seq.Value, services, out float probability))
            {
                context.State = EventState.Failed;
                context.SetNextStep(-1);
                yield break;
            }

            probability = Mathf.Clamp(probability, 0f, 100f);
            
            float roll = UnityEngine.Random.Range(0f, 100f);
            bool success = roll < probability;
            
            Debug.Log($"[Percent] Formula: {seq.Value}, Final: {probability}%, Roll: {roll:F1}%, Result: {(success ? "Success" : "Fail")}");
            
            context.SetNextStep(success ? seq.NextStep : seq.FailStep);
            yield return null;
        }

        /// <summary>
        /// 확률 수식 파싱: "50", "70+Player_Str", "80-NPC_Charm" 등
        /// </summary>
        private bool TryParseFormula(string formula, EventCommandServices services, out float probability)
        {
            probability = 0f;
            try
            {
                if (string.IsNullOrWhiteSpace(formula))
                {
                    Debug.LogError("[PercentCommand] 확률 수식이 비어 있습니다");
                    return false;
                }

                // 명시적 파싱: "baseValue (+/-) stat" 형식
                // 예: "50+Player_Str", "70 - NPC_Charm", "80"
                var match = System.Text.RegularExpressions.Regex.Match(formula, @"^\s*([\d.]+)\s*([+\-])?\s*([\w_]*)\s*$");
                
                if (!match.Success)
                {
                    Debug.LogError($"[PercentCommand] 확률 수식을 해석할 수 없습니다: '{formula}'");
                    return false;
                }

                if (!float.TryParse(match.Groups[1].Value, out float baseValue))
                {
                    Debug.LogError($"[PercentCommand] 기본 확률을 해석할 수 없습니다: '{formula}'");
                    return false;
                }
                
                // 통계 수정자가 있으면 추가
                if (match.Groups[2].Success && match.Groups[3].Success)
                {
                    string op = match.Groups[2].Value;
                    string statName = match.Groups[3].Value;
                    long statValue = services?.StatService?.GetStatLong(statName) ?? 0L;
                    
                    baseValue += op == "+" ? statValue : -statValue;
                }

                probability = baseValue;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PercentCommand] 파싱 예외: '{formula}' - {ex.Message}");
                return false;
            }
        }
    }

    public sealed class QuestCommand : IEventCommand
    {
        public IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services)
        {
            var seq = sequences[0];
            
            if (services?.QuestService != null)
                yield return services.QuestService.StartQuest(seq.Value);
            else
                yield return null;
            
            context.SetNextStep(seq.NextStep);
        }
    }

    public sealed class InteractionCommand : IEventCommand
    {
        public IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services)
        {
            var seq = sequences[0];
            string npcId = !string.IsNullOrWhiteSpace(seq.Value) ? seq.Value : seq.Target;
            bool completed = false;
            context.SetLastInteractionNpc(npcId);

            if (services?.EventNpcRuntime != null)
                yield return services.EventNpcRuntime.WaitForInteraction(npcId, result => completed = result);
            else
                yield return null;

            if (!completed)
            {
                context.State = EventState.Failed;
                context.SetNextStep(-1);
                yield break;
            }

            context.SetNextStep(seq.NextStep);
        }
    }

    public sealed class SubmitCommand : IEventCommand
    {
        private const string OwlBookEventId = "EVT_Book";
        private const string UnmetConditionDialogue = "?";

        public IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services)
        {
            EventSequenceData sequence = sequences[0];
            HashSet<int> allowedItemIds = ResolveAllowedItemIds(context, sequence.NextStep);
            if (allowedItemIds.Count == 0 || !UI.UIManager.HasInstance)
            {
                Debug.LogError($"[SubmitCommand] 제출 가능한 아이템을 찾을 수 없습니다: {context.EventData.EventID}, step={sequence.Step}");
                context.State = EventState.Failed;
                context.SetNextStep(-1);
                yield break;
            }

            // 제출 가능한 책이 없으면 빈 제출창을 띄우지 않고 이벤트 NPC의 미충족 대사를 표시
            if (IsOwlBookEvent(context) && !HasSelectableItem(allowedItemIds))
            {
                string npcId = ResolvePreviousInteractionNpcId(context, sequence.Step);
                bool completed = false;
                if (!string.IsNullOrWhiteSpace(npcId) && services?.EventNpcRuntime != null)
                {
                    if (services.UiCoordinator != null)
                        yield return services.UiCoordinator.Acquire(context.RuntimeId);

                    try
                    {
                        yield return services.EventNpcRuntime.ShowDialogue(
                            npcId,
                            UnmetConditionDialogue,
                            result => completed = result);
                    }
                    finally
                    {
                        services.UiCoordinator?.Release(context.RuntimeId);
                    }
                }

                if (!completed)
                    context.State = EventState.Failed;

                context.SetNextStep(-1);
                yield break;
            }

            bool selected = false;
            int selectedItemId = 0;
            if (services?.UiCoordinator != null)
                yield return services.UiCoordinator.Acquire(context.RuntimeId);

            SubmissionPanel panel = null;
            try
            {
                panel = UI.UIManager.Instance.OpenPanel<SubmissionPanel>();
                if (panel == null)
                {
                    Debug.LogError("[SubmitCommand] SubmissionPanel을 열 수 없습니다");
                    context.State = EventState.Failed;
                    context.SetNextStep(-1);
                    yield break;
                }

                panel.ConfigureItemSelection(
                    allowedItemIds,
                    itemId =>
                    {
                        selectedItemId = itemId;
                        selected = true;
                    },
                    "아이템을\n선택하세요");

                while (!selected && panel != null && panel.gameObject.activeInHierarchy)
                    yield return null;
            }
            finally
            {
                services?.UiCoordinator?.Release(context.RuntimeId);
            }

            if (!selected)
            {
                context.SetNextStep(-1);
                yield break;
            }

            context.SetSubmittedItem(selectedItemId);
            context.SetNextStep(sequence.NextStep);
        }

        private static HashSet<int> ResolveAllowedItemIds(EventExecutionContext context, int firstStep)
        {
            var result = new HashSet<int>();
            var visited = new HashSet<int>();
            int step = firstStep;

            while (step >= 0 && visited.Add(step))
            {
                List<EventSequenceData> candidates = context.GetSequencesByStep(step);
                if (candidates == null || candidates.Count != 1)
                    break;

                EventSequenceData candidate = candidates[0];
                if (!string.Equals(candidate.Command, EventCommandFactory.EVENT_COMMAND_CALCULATE, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(candidate.Target, "SubmitItem", StringComparison.OrdinalIgnoreCase)
                    || !int.TryParse(candidate.Value?.Trim(), out int itemId)
                    || itemId <= 0)
                {
                    break;
                }

                result.Add(itemId);
                step = candidate.FailStep;
            }

            return result;
        }

        private static bool HasSelectableItem(IReadOnlyCollection<int> allowedItemIds)
        {
            InventoryData inventory = InventoryManager.PlayerInventoryOrNull;
            if (inventory == null)
                return false;

            foreach (int itemId in allowedItemIds)
            {
                if (inventory.HasItem(itemId))
                    return true;
            }

            return false;
        }

        private static bool IsOwlBookEvent(EventExecutionContext context)
        {
            return context?.EventData != null
                && string.Equals(context.EventData.EventID, OwlBookEventId, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolvePreviousInteractionNpcId(EventExecutionContext context, int submitStep)
        {
            string npcId = string.Empty;
            int nearestStep = int.MinValue;
            foreach (EventSequenceData candidate in context.Sequences)
            {
                if (!string.Equals(candidate.Command, EventCommandFactory.EVENT_COMMAND_INTERACTION, StringComparison.OrdinalIgnoreCase)
                    || candidate.NextStep != submitStep
                    || candidate.Step <= nearestStep)
                {
                    continue;
                }

                npcId = !string.IsNullOrWhiteSpace(candidate.Value)
                    ? candidate.Value.Trim()
                    : candidate.Target?.Trim();
                nearestStep = candidate.Step;
            }

            return npcId;
        }
    }

    public sealed class SetActiveCommand : IEventCommand
    {
        public IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services)
        {
            var seq = sequences[0];
            bool completed = services?.EventNpcRuntime?.TrySpawn(seq.Target) == true;

            if (!completed)
            {
                context.State = EventState.Failed;
                context.SetNextStep(-1);
                yield break;
            }

            int nextStep = seq.NextStep;
            if (nextStep < 0)
                context.TryGetNextStepAfter(seq.Step, out nextStep);

            context.SetNextStep(nextStep);
        }
    }
}
