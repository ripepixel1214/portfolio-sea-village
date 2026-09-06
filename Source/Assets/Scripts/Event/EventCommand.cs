using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        public INpcInteractionService NpcInteractionService { get; }
        public IChoiceService ChoiceService { get; }
        public ExpressionEvaluator Evaluator { get; }

        public EventCommandServices(
            IStatService statService,
            IUIService uiService,
            IQuestService questService,
            INpcInteractionService npcInteractionService,
            IChoiceService choiceService,
            ExpressionEvaluator evaluator)
        {
            StatService = statService;
            UIService = uiService;
            QuestService = questService;
            NpcInteractionService = npcInteractionService;
            ChoiceService = choiceService;
            Evaluator = evaluator;
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
                {EVENT_COMMAND_INTERACTION, new InteractionCommand()}
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
        public IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services)
        {
            var step = sequences[0];

            // 결과 대사 매핑이 있으면 EventDialogue 를 결과 레이아웃으로 표시(확인까지 대기)
            if (EventResultDialogues.TryGet(context.EventData.EventID, step.Step, out int dialogueId))
            {
                yield return ShowResultDialogue(context, step, dialogueId);
                yield break;
            }

            bool isChoicePrompt = IsNextStepChoice(context, step);

            if (services?.UIService != null)
                yield return services.UIService.ShowPanel(step.Target, step.Value, isChoicePrompt);
            else
                yield return new WaitForSeconds(1.5f);

            context.SetNextStep(step.NextStep);
        }

        // 결과 대사를 Event Panel 결과 레이아웃에 표시하고, 확인 후 이어지는 중복 '확인' Choice 는 건너뛴다.
        private static IEnumerator ShowResultDialogue(EventExecutionContext context, EventSequenceData step, int dialogueId)
        {
            string text = ResolveDialogueText(dialogueId, step.Value);

            UI.EventPanel panel = UI.UIManager.HasInstance ? UI.UIManager.Instance.OpenPanel<UI.EventPanel>() : null;
            if (panel == null)
            {
                context.SetNextStep(NextStepSkippingConfirm(context, step));
                yield break;
            }

            bool confirmed = false;
            panel.ShowResultDialogue(text, () => confirmed = true);
            while (!confirmed) yield return null;

            if (UI.UIManager.HasInstance) UI.UIManager.Instance.ClosePanel<UI.EventPanel>();
            context.SetNextStep(NextStepSkippingConfirm(context, step));
        }

        private static string ResolveDialogueText(int dialogueId, string fallback)
        {
            EventDialogueData dialogue = EventManager.Instance != null ? EventManager.Instance.GetDialogue(dialogueId) : null;
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
    }

    public sealed class DialogueCommand : IEventCommand
    {
        public IEnumerator Execute(EventExecutionContext context, List<EventSequenceData> sequences, EventCommandServices services)
        {
            var step = sequences[0];
            
            if (services?.UIService != null)
                yield return services.UIService.ShowDialogue(step.Target, step.Value);
            else
                yield return new WaitForSeconds(1.5f);
            
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
            if (services?.ChoiceService != null)
                yield return services.ChoiceService.Choose(sequences, result => chosenSequence = result);
            else
            {
                chosenSequence = sequences[0];
                Debug.Log("Choice: auto-select first option (no choice service implemented)");
                yield return null;
            }

            if (chosenSequence == null)
            {
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
            if (!TryResolveDelta(seq.Value, services, out int delta))
            {
                Debug.LogWarning($"Calculation value parse failed: {seq.Value}");
                context.SetNextStep(seq.NextStep);
                yield break;
            }

            services?.StatService?.ChangeStat(seq.Target, delta);
            context.SetNextStep(seq.NextStep);
            yield return null;
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
            float probability = ParseFormula(seq.Value, services);
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
        private float ParseFormula(string formula, EventCommandServices services)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(formula))
                    return 50f;

                // 명시적 파싱: "baseValue (+/-) stat" 형식
                // 예: "50+Player_Str", "70 - NPC_Charm", "80"
                var match = System.Text.RegularExpressions.Regex.Match(formula, @"^\s*([\d.]+)\s*([+\-])?\s*([\w_]*)\s*$");
                
                if (!match.Success)
                {
                    Debug.LogWarning($"[PercentCommand] 파싱 실패: '{formula}' (예상 형식: '50', '70+Player_Str', '80-NPC_Charm')");
                    return 50f;
                }

                float baseValue = float.Parse(match.Groups[1].Value);
                
                // 통계 수정자가 있으면 추가
                if (match.Groups[2].Success && match.Groups[3].Success)
                {
                    string op = match.Groups[2].Value;
                    string statName = match.Groups[3].Value;
                    long statValue = services?.StatService?.GetStatLong(statName) ?? 0L;
                    
                    baseValue += op == "+" ? statValue : -statValue;
                }

                return baseValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PercentCommand] 파싱 예외: '{formula}' - {ex.Message}");
                return 50f; // 오류 시 기본값
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
            
            if (services?.NpcInteractionService != null)
                yield return services.NpcInteractionService.StartInteraction(seq.Value);
            else
                yield return null;
            
            context.SetNextStep(seq.NextStep);
        }
    }
}