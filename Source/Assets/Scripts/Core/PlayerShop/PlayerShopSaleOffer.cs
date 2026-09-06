using SeaVillage.Data;

namespace SeaVillage.Core
{
    /// <summary>NPC 구매 판단에 제공하는 플레이어 가게 판매 상품</summary>
    public sealed class PlayerShopSaleOffer
    {
        public int ItemId { get; }
        public ItemData Item { get; }
        public int UnitPrice { get; }
        public int AvailableQuantity { get; }
        public int AveragePurchasePrice { get; }

        public PlayerShopSaleOffer(
            int itemId,
            ItemData item,
            int unitPrice,
            int availableQuantity,
            int averagePurchasePrice)
        {
            ItemId = itemId;
            Item = item;
            UnitPrice = unitPrice;
            AvailableQuantity = availableQuantity;
            AveragePurchasePrice = averagePurchasePrice;
        }
    }
}
