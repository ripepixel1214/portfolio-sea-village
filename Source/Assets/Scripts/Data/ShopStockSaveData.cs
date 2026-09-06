using MemoryPack;

namespace SeaVillage.Data
{
    /// <summary>
    /// NPC 상점의 당일 판매량 저장 모델.
    /// 남은 재고는 ShopDatabase의 저작 수량에서 이 값을 빼 계산하므로 판매량만 보관한다.
    /// </summary>
    [MemoryPackable]
    public partial class ShopStockSaveData
    {
        public int shopId = 0;
        public int itemId = 0;
        public int soldCount = 0;

        public ShopStockSaveData() { }
    }
}
