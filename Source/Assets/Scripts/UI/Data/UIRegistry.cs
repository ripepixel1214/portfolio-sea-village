using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace SeaVillage.UI
{
    [CreateAssetMenu(fileName = "UIRegistry", menuName = "SeaVillage/UI/UI Registry")]
    public class UIRegistry : SerializedScriptableObject
    {
        [InfoBox("Panel Type (Class Name)과 Prefab을 매핑합니다.")]
        [SerializeField]
        private Dictionary<string, GameObject> panelPrefabs = new Dictionary<string, GameObject>();

        public GameObject GetPrefab(string typeName)
        {
            if (panelPrefabs.TryGetValue(typeName, out GameObject prefab))
                return prefab;

            return null;
        }

        public GameObject GetPrefab<T>() where T : UIPanel
        {
            return GetPrefab(typeof(T).Name);
        }

#if UNITY_EDITOR
        [Button("Validate Registry")]
        private void Validate()
        {
            bool hasErrors = false;

            foreach (var kvp in panelPrefabs)
            {
                if (kvp.Value == null)
                {
                    Debug.LogWarning($"[UI Registry] Prefab for '{kvp.Key}' is null!");
                    hasErrors = true;
                    continue;
                }

                if (kvp.Value.GetComponent<UIPanel>() == null)
                {
                    Debug.LogError($"[UI Registry] Prefab for '{kvp.Key}' does not have a UIPanel component!");
                    hasErrors = true;
                }
            }

            if (!hasErrors)
            {
                Debug.Log($"[UI Registry] Validation passed! ({panelPrefabs.Count} prefabs verified)");
            }
        }
#endif
    }
}
