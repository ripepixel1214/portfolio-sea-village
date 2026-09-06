namespace SeaVillage.Town
{
    /// <summary>상점 식별자. 값은 ShopDatabase 의 ShopID 와 일치해야 한다(NPC 구매 판정이 이 값으로 판매 목록을 조회)</summary>
    public enum ShopType
    {
        Library = 311001,
        DrugShop = 311002,
        Restaurant = 311003,
        GroceryShop = 311004,
        TownHall = 311005,

        AcornShop = 312001,
        SunflowerFarm = 312002,
        Bear = 312003,

        /// <summary>유저 소유 상점. ShopDatabase 행이 없고 판매 목록은 PlayerShopManager 의 런타임 상태에서 온다.</summary>
        PlayerShop = -1
    }
}
