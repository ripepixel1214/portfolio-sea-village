using SeaVillage.Core;

namespace SeaVillage.UI
{
    public class CraftPanel : SpecialShopRecipePanelBase
    {
        public void Initialize()
        {
            Initialize(MineForgeCatalog.RockTownCraftSourceShopId, "제작하기", "제작", "제작 완료");
        }

        public void Initialize(int sourceShopId, string header, string actionLabel, string completionTitle)
        {
            InitializeRecipeCraft(sourceShopId, 1, header, actionLabel, completionTitle);
        }
    }
}
