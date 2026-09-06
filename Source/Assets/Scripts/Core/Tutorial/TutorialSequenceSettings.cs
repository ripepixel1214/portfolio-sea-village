using UnityEngine;

namespace SeaVillage.Core
{
    [CreateAssetMenu(fileName = "TutorialSequenceSettings", menuName = "SeaVillage/Tutorial Sequence Settings")]
    public sealed class TutorialSequenceSettings : ScriptableObject
    {
        [SerializeField] private GameObject guidePrefab;

        #region Properties

        public GameObject GuidePrefab => guidePrefab;

        #endregion
    }
}
