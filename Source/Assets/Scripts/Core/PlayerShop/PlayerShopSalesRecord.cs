using System.Collections.Generic;

namespace SeaVillage.Core
{
    public sealed class PlayerShopItemSalesRecord
    {
        public int ItemId { get; }
        public int SoldQuantity { get; }

        public PlayerShopItemSalesRecord(int itemId, int soldQuantity)
        {
            ItemId = itemId;
            SoldQuantity = soldQuantity;
        }
    }

    public sealed class PlayerShopSalesRecord
    {
        public int ElapsedDays { get; }
        public int CustomerCount { get; }
        public int Revenue { get; }
        public int Tip { get; }
        public int PurchaseCost { get; }
        public int Profit => Revenue - PurchaseCost;
        public int Reward => Revenue + Tip;
        public IReadOnlyList<PlayerShopItemSalesRecord> SoldItems { get; }

        public PlayerShopSalesRecord(
            int elapsedDays,
            int customerCount,
            int revenue,
            int tip,
            int purchaseCost,
            IReadOnlyList<PlayerShopItemSalesRecord> soldItems)
        {
            ElapsedDays = elapsedDays;
            CustomerCount = customerCount;
            Revenue = revenue;
            Tip = tip;
            PurchaseCost = purchaseCost;
            SoldItems = soldItems;
        }
    }
}
