using SeaVillage.Core;
using SeaVillage.UI;

/// <summary>건설이 완료된 플레이어 가게 상호작용</summary>
public class PlayerShopInteractable : PlayerShopInteractableBase
{
    public override InteractionType InteractionType => InteractionType.PlayerShop;

    protected override string ShopLabel => "내 가게";

    public override void Interact()
    {
        if (!ValidateShop())
            return;

        if (!PlayerShopManager.HasInstance || !PlayerShopManager.Instance.HasPendingSettlement(TownKey))
        {
            OpenShopPanel();
            return;
        }

        PlayerShopSettlementPanel settlementPanel = UIManager.Instance.OpenPanel<PlayerShopSettlementPanel>();
        if (settlementPanel != null)
            settlementPanel.Initialize(TownKey, OpenShopPanel);
    }

    private void OpenShopPanel()
    {
        PlayerShopPanel shopPanel = UIManager.Instance.OpenPanel<PlayerShopPanel>();
        if (shopPanel != null)
            shopPanel.Initialize(TownKey);
    }
}
