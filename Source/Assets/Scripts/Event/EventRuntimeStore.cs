using System;
using System.Collections.Generic;
using System.Linq;
using SeaVillage.Data;

namespace SeaVillage.Event
{
    /// <summary>
    /// 런타임 이벤트 데이터 저장소
    /// </summary>
    public sealed class EventRuntimeStore
    {
        private readonly Dictionary<string, EventData> _eventDict;
        private readonly Dictionary<string, List<EventConditionData>> _conditionDict;
        private readonly Dictionary<string, List<EventSequenceData>> _sequenceDict;
        private readonly Dictionary<int, EventDialogueData> _dialogueDict;
        private readonly Dictionary<string, List<EventData>> _triggerDict;

        private static readonly HashSet<string> UnregisteredEventIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "EVT_Famine"
        };

        private static readonly HashSet<string> TriggerExcludedEventIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "EVT_Treasure"
        };

        private EventRuntimeStore(
            Dictionary<string, EventData> eventsById, 
            Dictionary<string, List<EventConditionData>> conditionsByEvent,
            Dictionary<string, List<EventSequenceData>> sequencesByEvent, 
            Dictionary<int, EventDialogueData> dialoguesById,
            Dictionary<string, List<EventData>> triggerDict)
        {
            _eventDict = eventsById;
            _conditionDict = conditionsByEvent;
            _sequenceDict = sequencesByEvent;
            _dialogueDict = dialoguesById;
            _triggerDict = triggerDict;
        }

        public static EventRuntimeStore Build(EventDatabase database)
        {
            if (database == null) return null;

            var events = database.Events ?? Array.Empty<EventData>();
            var conditions = database.Conditions ?? Array.Empty<EventConditionData>();
            var sequences = database.Sequences ?? Array.Empty<EventSequenceData>();
            var dialogues = database.Dialogues ?? Array.Empty<EventDialogueData>();

            var eventDict = new Dictionary<string, EventData>();
            var triggerDict = new Dictionary<string, List<EventData>>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var evt in events)
            {
                if (string.IsNullOrEmpty(evt?.EventID)) continue;
                if (UnregisteredEventIds.Contains(evt.EventID)) continue;

                if (!eventDict.ContainsKey(evt.EventID))
                    eventDict.Add(evt.EventID, evt);

                if (string.IsNullOrEmpty(evt.TriggerType)) continue;
                if (TriggerExcludedEventIds.Contains(evt.EventID)) continue;

                if (!triggerDict.TryGetValue(evt.TriggerType, out var list))
                {
                    list = new List<EventData>();
                    triggerDict[evt.TriggerType] = list;
                }
                list.Add(evt);
            }

            var conditionDict = conditions
                .Where(x => x != null && !string.IsNullOrEmpty(x.EventID) && !UnregisteredEventIds.Contains(x.EventID))
                .GroupBy(x => x.EventID)
                .ToDictionary(g => g.Key, g => g.ToList());

            var sequenceDict = sequences
                .Where(x => x != null && !string.IsNullOrEmpty(x.EventID) && !UnregisteredEventIds.Contains(x.EventID))
                .GroupBy(x => x.EventID)
                .ToDictionary(g => g.Key, g => g.ToList());
                
            var dialogueDict = new Dictionary<int, EventDialogueData>();
            foreach (var dialogue in dialogues)
            {
                if (dialogue != null && !dialogueDict.ContainsKey(dialogue.ID))
                    dialogueDict.Add(dialogue.ID, dialogue);
            }

            return new EventRuntimeStore(eventDict, conditionDict, sequenceDict, dialogueDict, triggerDict);
        }

        public bool TryGetEvent(string eventId, out EventData data) => _eventDict.TryGetValue(eventId, out data);

        public bool TryGetConditions(string eventId, out List<EventConditionData> conditions) => _conditionDict.TryGetValue(eventId, out conditions);

        public bool TryGetSequences(string eventId, out List<EventSequenceData> sequences) => _sequenceDict.TryGetValue(eventId, out sequences);

        public bool TryGetDialogue(int dialogueId, out EventDialogueData dialogue) => _dialogueDict.TryGetValue(dialogueId, out dialogue);

        public IEnumerable<EventData> GetAllEvents() => _eventDict.Values;
        
        /// <summary>
        /// 주어진 트리거 타입의 모든 이벤트를 반환
        /// </summary>
        public IEnumerable<EventData> GetEventsByTrigger(string triggerType)
        {
            if (string.IsNullOrEmpty(triggerType) || !_triggerDict.TryGetValue(triggerType, out var events))
                return Enumerable.Empty<EventData>();
            return events;
        }
    }
}
