using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using System.Linq;

namespace SeaVillage.Data
{
    [CreateAssetMenu(fileName = "EventDatabase", menuName = "SeaVillage/Data/Event Database")]
    public class EventDatabase : SerializedScriptableObject
    {
        [SerializeField] private List<EventData> _events = new List<EventData>();
        [SerializeField] private List<EventConditionData> _conditions = new List<EventConditionData>();
        [SerializeField] private List<EventSequenceData> _sequences = new List<EventSequenceData>();
        [SerializeField] private List<EventDialogueData> _dialogues = new List<EventDialogueData>();

        // Dictionaries for quick lookup at runtime
        private Dictionary<string, EventData> _eventDict;
        private Dictionary<string, List<EventConditionData>> _conditionDict;
        private Dictionary<string, List<EventSequenceData>> _sequenceDict;
        private Dictionary<int, EventDialogueData> _dialogueDict;

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
            if (_events == null) _events = new List<EventData>();
            if (_conditions == null) _conditions = new List<EventConditionData>();
            if (_sequences == null) _sequences = new List<EventSequenceData>();
            if (_dialogues == null) _dialogues = new List<EventDialogueData>();

            _eventDict = new Dictionary<string, EventData>();
            foreach (var evt in _events)
                if (!_eventDict.ContainsKey(evt.EventID))
                    _eventDict.Add(evt.EventID, evt);

            _conditionDict = _conditions
                .GroupBy(x => x.EventID)
                .ToDictionary(g => g.Key, g => g.ToList());

            _sequenceDict = _sequences
                .GroupBy(x => x.EventID)
                .ToDictionary(g => g.Key, g => g.ToList());
                
            _dialogueDict = new Dictionary<int, EventDialogueData>();
            foreach (var dialogue in _dialogues)
                if (!_dialogueDict.ContainsKey(dialogue.ID))
                    _dialogueDict.Add(dialogue.ID, dialogue);
        }

        public EventData GetEvent(string eventID)
        {
            if (_eventDict == null) Initialize();
            return _eventDict.TryGetValue(eventID, out var data) ? data : null;
        }

        public List<EventConditionData> GetEventConditions(string eventID)
        {
            if (_conditionDict == null) Initialize();
            return _conditionDict.TryGetValue(eventID, out var list) ? list : null;
        }

        public List<EventSequenceData> GetEventSequences(string eventID)
        {
            if (_sequenceDict == null) Initialize();
            return _sequenceDict.TryGetValue(eventID, out var list) ? list : null;
        }

        public EventDialogueData GetDialogue(int dialogueID)
        {
            if (_dialogueDict == null) Initialize();
            return _dialogueDict.TryGetValue(dialogueID, out var data) ? data : null;
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
            _eventDict?.Clear();
            _conditionDict?.Clear();
            _sequenceDict?.Clear();
            _dialogueDict?.Clear();
        }
    }
}