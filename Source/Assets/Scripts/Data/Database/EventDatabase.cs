using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace SeaVillage.Data
{
    [CreateAssetMenu(fileName = "EventDatabase", menuName = "SeaVillage/Data/Event Database")]
    public class EventDatabase : SerializedScriptableObject
    {
        [SerializeField] private List<EventData> _events = new List<EventData>();
        [SerializeField] private List<EventConditionData> _conditions = new List<EventConditionData>();
        [SerializeField] private List<EventSequenceData> _sequences = new List<EventSequenceData>();
        [SerializeField] private List<EventDialogueData> _dialogues = new List<EventDialogueData>();

        // Properties
        public IReadOnlyList<EventData> Events => _events;
        public IReadOnlyList<EventConditionData> Conditions => _conditions;
        public IReadOnlyList<EventSequenceData> Sequences => _sequences;
        public IReadOnlyList<EventDialogueData> Dialogues => _dialogues;

        private void OnEnable()
        {
            Initialize();
        }

        public void Initialize()
        {
            _events ??= new List<EventData>();
            _conditions ??= new List<EventConditionData>();
            _sequences ??= new List<EventSequenceData>();
            _dialogues ??= new List<EventDialogueData>();
        }

        public EventData GetEvent(string eventID)
            => FindEvent(eventID);

        public List<EventConditionData> GetEventConditions(string eventID)
            => FindByEventId(_conditions, eventID);

        public List<EventSequenceData> GetEventSequences(string eventID)
            => FindByEventId(_sequences, eventID);

        public EventDialogueData GetDialogue(int dialogueID)
        {
            Initialize();
            for (int i = 0; i < _dialogues.Count; i++)
            {
                EventDialogueData dialogue = _dialogues[i];
                if (dialogue != null && dialogue.ID == dialogueID)
                    return dialogue;
            }

            return null;
        }

        public void SetData(List<EventData> newEvents, List<EventConditionData> newConditions, 
            List<EventSequenceData> newSequences, List<EventDialogueData> newDialogues)
        {
            _events = newEvents;
            _conditions = newConditions;
            _sequences = newSequences;
            _dialogues = newDialogues;
            Initialize();
        }
        
        public void ClearAllEventCollections()
        {
            _events.Clear();
            _conditions.Clear();
            _sequences.Clear();
            _dialogues.Clear();
        }

        private EventData FindEvent(string eventID)
        {
            Initialize();
            if (string.IsNullOrEmpty(eventID))
                return null;

            for (int i = 0; i < _events.Count; i++)
            {
                EventData eventData = _events[i];
                if (eventData != null && string.Equals(eventData.EventID, eventID, System.StringComparison.OrdinalIgnoreCase))
                    return eventData;
            }

            return null;
        }

        private static List<T> FindByEventId<T>(List<T> source, string eventID)
            where T : class
        {
            if (source == null || string.IsNullOrEmpty(eventID))
                return null;

            List<T> matches = null;
            for (int i = 0; i < source.Count; i++)
            {
                T entry = source[i];
                string entryEventId = entry switch
                {
                    EventConditionData condition => condition.EventID,
                    EventSequenceData sequence => sequence.EventID,
                    _ => null
                };

                if (!string.Equals(entryEventId, eventID, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                matches ??= new List<T>();
                matches.Add(entry);
            }

            return matches;
        }
    }
}
