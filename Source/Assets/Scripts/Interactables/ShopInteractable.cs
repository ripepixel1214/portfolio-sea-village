using UnityEngine;
using UnityEngine.Serialization;
using SeaVillage.UI;
using SeaVillage.Core;

public enum ShopMenuMode
{
    Standard = 0,
    PurchaseOnly = 1,
}

public class ShopInteractable : BaseInteractable
{
    [Header("Shop Settings")]
    [SerializeField, FormerlySerializedAs("_interactableId")] private int _shopId;
    [SerializeField] private ShopMenuMode _menuMode = ShopMenuMode.Standard;

    [SerializeField, Tooltip("상점 패널에 표시할 이미지. 비워두면 표시하지 않는다")]
    private Sprite _shopImage;

    public override InteractionType InteractionType => InteractionType.Shop;
    public int ShopId => _shopId;

    public override void Interact()
    {
        int affinity = TownProgressionManager.HasInstance && GameManager.HasInstance
            ? TownProgressionManager.Instance.GetAffinity(GameManager.Instance.CurrentTownKey)
            : 0;
        if (affinity < TownAffinityRules.GeneralShopRequiredAffinity)
        {
            UIManager.Instance?.ShowAlertMessage($"호감도 {TownAffinityRules.GeneralShopRequiredAffinity} 이상 필요");
            return;
        }

        if (!ValidateShopId())
            return;

        var shopPanel = UIManager.Instance.OpenPanel<ShopPanel>();
        if (shopPanel != null)
        {
            shopPanel.Initialize(_shopId, DisplayName, _menuMode, _shopImage);
            if (_shopId == TutorialItemIds.FoodShop)
                TutorialEventReporter.Report(TutorialEventType.InteractionCompleted, TutorialTargetIds.FoodShop, source: TutorialEventSource.World);
        }
    }

    private bool ValidateShopId()
    {
        if (_shopId > 0)
            return true;

        Debug.LogWarning($"{gameObject.name}: 유효하지 않은 상점 ID입니다. id={_shopId}");
        UIManager.Instance?.ShowAlertMessage("[Error] 상점 ID가 설정되지 않았습니다");
        return false;
    }
}
