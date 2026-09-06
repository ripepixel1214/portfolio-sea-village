using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace SeaVillage.Data
{
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "SeaVillage/Data/Item Database")]
    public class ItemDatabase : SerializedScriptableObject
    {
        [SerializeField] private List<ItemData> items = new List<ItemData>();
        
        public IReadOnlyList<ItemData> Items => items;
        
        public ItemData GetItem(int id)
        {
            return items.Find(item => item.ID == id);
        }
        
        public ItemData GetItemByName(int name)
        {
            return items.Find(item => item.Name == name);
        }

        public ItemData GetItemByItemPriceID(int itemPriceID)
        {
            return items.Find(item => item.PriceListID == itemPriceID);
        }
        
        public List<ItemData> GetItemsByType(string type)
        {
            return items.FindAll(item => item.Type != null && item.Type.Contains(type));
        }
        
        public List<ItemData> GetItemsByTown(string town)
        {
            return items.FindAll(item => item.Town == town);
        }

        public int GetItemOriginalPrice(int id)
        {
            return items.Find(item => item.ID == id)?.OriginPrice ?? 0;
        }
        
        public void SetItems(List<ItemData> newItems)
        {
            items = newItems;
        }
        
        public void AddItem(ItemData item)
        {
            items.Add(item);
        }
        
        public void ClearItems()
        {
            items.Clear();
        }
    }
}