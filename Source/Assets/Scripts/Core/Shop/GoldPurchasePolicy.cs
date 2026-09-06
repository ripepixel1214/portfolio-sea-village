namespace SeaVillage.Core
{
    /// <summary>
    /// 골드 구매가 허용되는 아이템인지 판정
    /// </summary>
    public static class GoldPurchasePolicy
    {
        public static bool CanPurchase(int itemId)
        {
            return itemId != 117001
                && itemId != 117002
                && itemId != 117003
                && itemId != 117004;
        }
    }
}
