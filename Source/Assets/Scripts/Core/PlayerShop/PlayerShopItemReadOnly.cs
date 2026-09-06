namespace SeaVillage.Core
{
    /// <summary>플레이어 가게 등록 아이템의 읽기 전용 상태</summary>
    public sealed class PlayerShopItemReadOnly
    {
        public int SlotIndex { get; }
        public int ItemId { get; }
        public int Quantity { get; }
        public int UnitPrice { get; }
        public int AveragePurchasePrice { get; }

        internal PlayerShopItemReadOnly(PlayerShopItem source)
        {
            SlotIndex = source.slotIndex;
            ItemId = source.itemId;
            Quantity = source.quantity;
            UnitPrice = source.unitPrice;
            AveragePurchasePrice = source.averagePurchasePrice;
        }
    }
}
