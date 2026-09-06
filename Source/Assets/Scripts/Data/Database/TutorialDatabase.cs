using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaVillage.Data
{
    [CreateAssetMenu(fileName = "TutorialDatabase", menuName = "SeaVillage/Data/Tutorial Database")]
    public class TutorialDatabase : ScriptableObject
    {
        [SerializeField] private List<TutorialData> tutorials = new List<TutorialData>();

        private Dictionary<string, List<TutorialData>> _tutorialsById;

        public IReadOnlyList<TutorialData> Tutorials => tutorials;

        private void OnEnable()
        {
            InitializeLookup();
        }

        public bool TryGetDialogues(string tutorialId, out IReadOnlyList<TutorialData> dialogues)
        {
            dialogues = null;
            if (string.IsNullOrWhiteSpace(tutorialId))
                return false;

            EnsureLookup();
            if (!_tutorialsById.TryGetValue(tutorialId.Trim(), out List<TutorialData> entries))
                return false;

            dialogues = entries;
            return true;
        }

        public void SetTutorials(List<TutorialData> newTutorials)
        {
            tutorials = newTutorials ?? new List<TutorialData>();
            InitializeLookup();
        }

        private void EnsureLookup()
        {
            if (_tutorialsById == null)
                InitializeLookup();
        }

        private void InitializeLookup()
        {
            tutorials ??= new List<TutorialData>();
            _tutorialsById = new Dictionary<string, List<TutorialData>>(StringComparer.Ordinal);

            for (int i = 0; i < tutorials.Count; i++)
            {
                TutorialData tutorial = tutorials[i];
                if (tutorial == null || string.IsNullOrWhiteSpace(tutorial.ID))
                    continue;

                string id = tutorial.ID.Trim();
                if (!_tutorialsById.TryGetValue(id, out List<TutorialData> entries))
                {
                    entries = new List<TutorialData>();
                    _tutorialsById.Add(id, entries);
                }

                entries.Add(tutorial);
            }
        }
    }
}
