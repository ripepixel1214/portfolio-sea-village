namespace SeaVillage.UI
{
    public class CookPanel : SpecialShopRecipePanelBase
    {
        public void Initialize(int restaurantShopId, int cookOutputCount)
        {
            InitializeRecipeCraft(
                restaurantShopId,
                cookOutputCount,
                "요리하기",
                "요리",
                "요리 완료");
        }
    }
}
