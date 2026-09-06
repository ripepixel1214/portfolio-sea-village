using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

namespace SeaVillage.Data
{
    [CreateAssetMenu(fileName = "ItemPriceDatabase", menuName = "SeaVillage/Data/Item Price Database")]
    public class ItemPriceDatabase : SerializedScriptableObject
    {
        [SerializeField] private Dictionary<ItemPriceKey, ItemPriceData> defaultItemPriceDict = new Dictionary<ItemPriceKey, ItemPriceData>();

        public IReadOnlyDictionary<ItemPriceKey, ItemPriceData> DefaultItemPriceDict => defaultItemPriceDict;

        public ItemPriceData GetItemPrice(int id, string town)
        {
            var key = new ItemPriceKey(id, town);

            if (defaultItemPriceDict.TryGetValue(key, out var itemPrice))
                return itemPrice;

            Debug.LogWarning($"ItemPriceDatabase: Item price not found for ID {id} in town '{town}'.");
            return null;
        }

        public List<ItemPriceData> GetItemPricesOfAllTown(int id)
        {
            return defaultItemPriceDict.Values.Where(itemPrice => itemPrice.ID == id).ToList();
        }

        public List<ItemPriceData> GetItemPricesByTown(string town)
        {
            return defaultItemPriceDict.Values.Where(itemPrice => itemPrice.Town == town).ToList();
        }

        public List<ItemPriceData> GetAllItemPrices()
        {
            var result = new List<ItemPriceData>(defaultItemPriceDict.Count);
            CopyAllItemPricesTo(result);
            return result;
        }

        public void CopyAllItemPricesTo(List<ItemPriceData> target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            int index = 0;
            foreach (ItemPriceData source in defaultItemPriceDict.Values)
            {
                ItemPriceData saved = SaveSnapshotList.GetOrCreate(target, index++);
                SaveSnapshotList.CopyItemPriceData(source, saved);
            }

            SaveSnapshotList.Trim(target, index);
        }

        public void SetItemPrice(int id, string town, ItemPriceData newItemPrice)
        {
            var key = new ItemPriceKey(id, town);
            defaultItemPriceDict[key] = newItemPrice;
        }

        public void SetItemPrices(List<ItemPriceData> newItemPrices)
        {
            defaultItemPriceDict.Clear();

            newItemPrices.ForEach(itemPrice =>
            {
                var key = new ItemPriceKey(itemPrice.ID, itemPrice.Town);
                defaultItemPriceDict[key] = itemPrice;
            });
        }

        public void AddItemPrice(ItemPriceData itemPrice)
        {
            var key = new ItemPriceKey(itemPrice.ID, itemPrice.Town);
            defaultItemPriceDict[key] = itemPrice;
        }

        public void ClearItemPrices()
        {
            defaultItemPriceDict.Clear();
        }
    }
}
