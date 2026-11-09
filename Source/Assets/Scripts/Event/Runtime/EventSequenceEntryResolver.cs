using System;
using System.Collections.Generic;
using System.Linq;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.Event
{
    /// <summary>활성 이벤트 퀘스트의 현재 마을 진입 스텝을 시퀀스와 NPC Catalog에서 결정</summary>
    public static class EventSequenceEntryResolver
    {
        public static bool IsQuestFlow(IReadOnlyList<EventSequenceData> sequences)
        {
            if (sequences == null || sequences.Count == 0)
                return false;

            for (int i = 0; i < sequences.Count; i++)
            {
                EventSequenceData panel = sequences[i];
                if (!string.Equals(panel.Command, EventCommandFactory.EVENT_COMMAND_PANEL, StringComparison.OrdinalIgnoreCase))
                    continue;

                List<EventSequenceData> confirmation = sequences
                    .Where(candidate => candidate.Step == panel.NextStep)
                    .ToList();
                if (confirmation.Count != 1
                    || !string.Equals(confirmation[0].Command, EventCommandFactory.EVENT_COMMAND_CHOICE, StringComparison.OrdinalIgnoreCase)
                    || confirmation[0].NextStep >= 0)
                {
                    continue;
                }

                if (sequences.Any(candidate => candidate.Step > panel.Step
                    && string.Equals(candidate.Command, EventCommandFactory.EVENT_COMMAND_INTERACTION, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryResolveActiveEntry(
            IReadOnlyList<EventSequenceData> sequences,
            EventNpcCatalog npcCatalog,
            TownKey currentTown,
            out int entryStep)
        {
            entryStep = -1;
            if (sequences == null || sequences.Count == 0 || npcCatalog == null || currentTown == TownKey.Unknown)
                return false;

            int initialStep = sequences.Min(sequence => sequence.Step);
            HashSet<int> referencedSteps = BuildReferencedSteps(sequences);
            int[] rootSteps = sequences
                .Select(sequence => sequence.Step)
                .Distinct()
                .Where(step => step > initialStep && !referencedSteps.Contains(step))
                .OrderBy(step => step)
                .ToArray();

            for (int i = 0; i < rootSteps.Length; i++)
            {
                int rootStep = rootSteps[i];
                if (!TryResolveNpcId(sequences, rootStep, out string npcId)
                    || !npcCatalog.TryGet(npcId, out EventNpcDefinition definition)
                    || definition.Town != currentTown)
                {
                    continue;
                }

                entryStep = rootStep;
                return true;
            }

            return false;
        }

        private static HashSet<int> BuildReferencedSteps(IReadOnlyList<EventSequenceData> sequences)
        {
            var referenced = new HashSet<int>();
            int[] orderedSteps = sequences
                .Select(sequence => sequence.Step)
                .Distinct()
                .OrderBy(step => step)
                .ToArray();

            for (int i = 0; i < sequences.Count; i++)
            {
                EventSequenceData sequence = sequences[i];
                if (sequence.NextStep >= 0)
                    referenced.Add(sequence.NextStep);
                if (sequence.FailStep >= 0)
                    referenced.Add(sequence.FailStep);

                if (!string.Equals(sequence.Command, EventCommandFactory.EVENT_COMMAND_SETACTIVE, StringComparison.OrdinalIgnoreCase)
                    || sequence.NextStep >= 0)
                {
                    continue;
                }

                for (int stepIndex = 0; stepIndex < orderedSteps.Length; stepIndex++)
                {
                    if (orderedSteps[stepIndex] <= sequence.Step)
                        continue;

                    referenced.Add(orderedSteps[stepIndex]);
                    break;
                }
            }

            return referenced;
        }

        private static bool TryResolveNpcId(
            IReadOnlyList<EventSequenceData> sequences,
            int rootStep,
            out string npcId)
        {
            npcId = string.Empty;
            for (int i = 0; i < sequences.Count; i++)
            {
                EventSequenceData sequence = sequences[i];
                if (sequence.Step != rootStep)
                    continue;

                if (string.Equals(sequence.Command, EventCommandFactory.EVENT_COMMAND_INTERACTION, StringComparison.OrdinalIgnoreCase))
                    npcId = !string.IsNullOrWhiteSpace(sequence.Value) ? sequence.Value.Trim() : sequence.Target?.Trim();
                else if (string.Equals(sequence.Command, EventCommandFactory.EVENT_COMMAND_SETACTIVE, StringComparison.OrdinalIgnoreCase))
                    npcId = sequence.Target?.Trim();

                return !string.IsNullOrWhiteSpace(npcId);
            }

            return false;
        }
    }
}
