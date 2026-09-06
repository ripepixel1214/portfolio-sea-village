using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;

namespace SeaVillage.Data
{
    /// <summary>
    /// 상점 데이터베이스 (Key: 상점 ID + 아이템 ID)
    /// </summary>
    [CreateAssetMenu(fileName = "ShopDatabase", menuName = "SeaVillage/Data/Shop Database")]
    public class ShopDatabase : SerializedScriptableObject
    {
        [SerializeField]
        private Dictionary<ShopKey, ShopData> shopDict = new Dictionary<ShopKey, ShopData>();
        
        // Properties
        public Dictionary<ShopKey, ShopData> ShopDict => shopDict;
        

        public ShopData GetShop(int id, int itemID)
        {
            var key = new ShopKey(id, itemID);
            return shopDict.TryGetValue(key, out var shop) ? shop : null;
        }

        public List<ShopData> GetShopsById(int id)
        {
            return shopDict.Values.Where(shop => shop.ID == id).ToList();
        }
        
        public List<ShopData> GetShopsByItemID(int itemID)
        {
            return shopDict.Values.Where(shop => shop.ItemID == itemID).ToList();
        }

        public List<ShopData> GetAllShops()
        {
            return shopDict.Values.ToList();
        }
        
        public void SetShops(List<ShopData> newShops)
        {
            shopDict.Clear();

            newShops.ForEach(shop => shopDict[new ShopKey(shop.ID, shop.ItemID)] = shop);
        }
        
        public void AddShop(ShopData shop)
        {
            shopDict[new ShopKey(shop.ID, shop.ItemID)] = shop;
        }
        
        public void ClearShops()
        {
            shopDict.Clear();
        }
    }
}