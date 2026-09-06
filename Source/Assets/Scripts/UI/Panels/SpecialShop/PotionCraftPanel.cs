namespace SeaVillage.UI
{
    public class PotionCraftPanel : SpecialShopRecipePanelBase
    {
        public void Initialize(int potionShopId)
        {
            InitializeRecipeCraft(
                potionShopId,
                1,
                "포션 제조하기",
                "제조",
                "포션 제조 완료");
        }
    }
}
