using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace SeaVillage.Data
{
    [CreateAssetMenu(fileName = "ItemTypeDatabase", menuName = "SeaVillage/Data/Item Type Database")]
    public class ItemTypeDatabase : SerializedScriptableObject
    {
        [SerializeField] private List<ItemTypeData> itemTypes = new List<ItemTypeData>();

        [NonSerialized] private Dictionary<string, ItemTypeData> itemTypeLookup;

        public IReadOnlyList<ItemTypeData> ItemTypes => itemTypes;

        private void OnEnable()
        {
            RebuildLookup();
        }

        public ItemTypeData GetItemType(string itemType)
        {
            if (string.IsNullOrWhiteSpace(itemType))
                return null;

            EnsureLookup();
            return itemTypeLookup.TryGetValue(itemType.Trim(), out var data) ? data : null;
        }

        public string GetItemTypeDisplayName(string itemType)
        {
            var data = GetItemType(itemType);
            if (data == null || string.IsNullOrWhiteSpace(data.Name))
                return itemType?.Trim() ?? string.Empty;

            return data.Name.Trim();
        }

        public string GetItemTypeIconName(string itemType)
        {
            var data = GetItemType(itemType);
            return data?.Icon?.Trim() ?? string.Empty;
        }

        public void SetItemTypes(List<ItemTypeData> newItemTypes)
        {
            itemTypes = newItemTypes ?? new List<ItemTypeData>();
            RebuildLookup();
        }

        private void EnsureLookup()
        {
            if (itemTypeLookup == null)
                RebuildLookup();
        }

        private void RebuildLookup()
        {
            itemTypeLookup = new Dictionary<string, ItemTypeData>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < itemTypes.Count; i++)
            {
                ItemTypeData data = itemTypes[i];
                if (data == null || string.IsNullOrWhiteSpace(data.ItemType))
                    continue;

                string key = data.ItemType.Trim();
                itemTypeLookup[key] = data;
            }
        }
    }
}
