using System;

namespace SeaVillage.Core
{
    /// <summary>플레이어 상점에 등록된 아이템 정보</summary>
    [Serializable]
    public class PlayerShopItem
    {
        public int slotIndex;
        public int itemId;
        public int quantity;

        public int unitPrice;
        public int averagePurchasePrice;
    }
}