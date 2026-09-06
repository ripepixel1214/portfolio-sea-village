namespace SeaVillage.Core
{
    /// <summary>NPC가 플레이어 가게에 전달하는 손님 한 명의 상품 구매 요청</summary>
    public readonly struct PlayerShopSaleRequest
    {
        public int ItemId { get; }
        public int Quantity { get; }

        public PlayerShopSaleRequest(int itemId, int quantity)
        {
            ItemId = itemId;
            Quantity = quantity;
        }
    }
}
