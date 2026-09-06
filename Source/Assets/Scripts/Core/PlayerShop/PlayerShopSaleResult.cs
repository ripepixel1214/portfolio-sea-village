namespace SeaVillage.Core
{
    /// <summary>플레이어 가게 판매 확정 결과</summary>
    public sealed class PlayerShopSaleResult
    {
        public int SoldQuantity { get; }
        public int Revenue { get; }
        public int Tip { get; }
        public int PurchaseCost { get; }
        public int Profit => Revenue - PurchaseCost;

        public PlayerShopSaleResult(
            int soldQuantity,
            int revenue,
            int tip,
            int purchaseCost)
        {
            SoldQuantity = soldQuantity;
            Revenue = revenue;
            Tip = tip;
            PurchaseCost = purchaseCost;
        }
    }
}
