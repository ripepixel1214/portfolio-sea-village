namespace SeaVillage.Core
{
    /// <summary>
    /// 인벤토리 아이템 정보를 캐싱하는 구조체
    /// </summary>
    public struct InventoryItem
    {
        public int id;
        public int quantity;
        public float unitWeight; // ItemData.Weight를 캐싱
        public int totalPurchasePrice; // 총 구매가
        public int averagePurchasePrice; // 구매평균가
    }
}
