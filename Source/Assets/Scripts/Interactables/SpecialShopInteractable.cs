using UnityEngine;
using UnityEngine.Serialization;
using SeaVillage.Core;
using SeaVillage.UI;
using Sirenix.OdinInspector;

public class SpecialShopInteractable : BaseInteractable
{
    [Header("Special Shop Settings")]
    [Tooltip("특수 상점의 건물 정체성. 상호작용 시 이 타입에 맞는 전용 패널을 연다.")]
    [SerializeField] private SpecialShopType _specialShopType = SpecialShopType.Restaurant;
    [Tooltip("구매, 직원 고용, 내 가게 상태 조회에 사용할 상점/오피스 데이터 ID")]
    [SerializeField, FormerlySerializedAs("_interactableId")] private int _shopId;

    [Tooltip("상점 패널에 표시할 이미지. 비워두면 표시하지 않는다")]
    [SerializeField] private Sprite _shopImage;

    public SpecialShopType ShopType => _specialShopType;

    protected override void InitializeInteractable()
    {
        base.InitializeInteractable();
        _interactionType = InteractionType.SpecialShop;
    }

    public override void Interact()
    {
        int requiredAffinity = SpecialShopAccessPolicy.GetPanelRequiredAffinity(_specialShopType);
        if (!SpecialShopAccessPolicy.CanOpenCurrentTown(_specialShopType))
        {
            UIManager.Instance?.ShowAlertMessage($"호감도 {requiredAffinity} 이상 필요");
            return;
        }

        OpenPanelByShopType();
    }

    private void OpenPanelByShopType()
    {
        switch (_specialShopType)
        {
            case SpecialShopType.Restaurant:
                OpenRestaurantPanel();
                break;
            case SpecialShopType.TownOffice:
                OpenOfficePanel();
                break;
            case SpecialShopType.AcornWorkshop:
                OpenAcornWorkshopPanel();
                break;
            case SpecialShopType.RockExchange:
                OpenExchangerPanel();
                break;
            case SpecialShopType.Forge:
                OpenForgePanel();
                break;
            case SpecialShopType.Stage:
                OpenStagePanel();
                break;
            case SpecialShopType.PotionShop:
                OpenPotionShopPanel();
                break;
            case SpecialShopType.TailorHouse:
                OpenTailorHousePanel();
                break;
            case SpecialShopType.Devil:
                OpenDevilPanel();
                break;
            default:
                Debug.LogWarning($"{gameObject.name}: 지원하지 않는 특수 가게 타입입니다. type={_specialShopType}");
                ShowMissingDedicatedPanelMessage();
                break;
        }
    }

    private void OpenAcornWorkshopPanel()
    {
        AcornWorkshopPanel panel = UIManager.Instance?.OpenPanel<AcornWorkshopPanel>();
        if (panel != null)
            panel.Initialize(GetSpecialShopDisplayName(), _shopId, _shopImage);
    }

    private void OpenExchangerPanel()
    {
        ExchangerPanel panel = UIManager.Instance.OpenPanel<ExchangerPanel>();
        if (panel != null)
            panel.Initialize(GetSpecialShopDisplayName(), _shopId, _shopImage);
    }

    private void OpenForgePanel()
    {
        ForgePanel panel = UIManager.Instance?.OpenPanel<ForgePanel>();
        if (panel != null)
            panel.Initialize(GetSpecialShopDisplayName(), _shopId, _shopImage);
    }

    private void OpenStagePanel()
    {
        StagePanel panel = UIManager.Instance?.OpenPanel<StagePanel>();
        if (panel != null)
            panel.Initialize(GetSpecialShopDisplayName(), _shopId, _shopImage);
    }

    private void OpenPotionShopPanel()
    {
        PotionShopPanel panel = UIManager.Instance?.OpenPanel<PotionShopPanel>();
        if (panel != null)
            panel.Initialize(GetSpecialShopDisplayName(), _shopId, _shopImage);
    }

    private void OpenTailorHousePanel()
    {
        TailorHousePanel panel = UIManager.Instance?.OpenPanel<TailorHousePanel>();
        if (panel != null)
            panel.Initialize(GetSpecialShopDisplayName(), _shopId, _shopImage);
    }

    private void OpenDevilPanel()
    {
        DevilPanel panel = UIManager.Instance?.OpenPanel<DevilPanel>();
        if (panel != null)
            panel.Initialize(GetSpecialShopDisplayName(), _shopId, _shopImage);
    }

    private void OpenRestaurantPanel()
    {
        RestaurantPanel restaurantPanel = UIManager.Instance?.OpenPanel<RestaurantPanel>();
        if (restaurantPanel != null)
            restaurantPanel.Initialize(GetSpecialShopDisplayName(), _shopId, _shopImage);
    }

    private void OpenOfficePanel()
    {
        OfficePanel officePanel = UIManager.Instance?.OpenPanel<OfficePanel>();
        if (officePanel != null)
            officePanel.Initialize(GetSpecialShopDisplayName(), _shopId, _shopImage);
    }

    private void ShowMissingDedicatedPanelMessage()
    {
        string displayName = GetSpecialShopDisplayName();
        UIManager.Instance?.ShowAlertMessage($"[Error] {displayName} 전용 UI가 필요합니다");
    }

    private string GetSpecialShopDisplayName()
    {
        return string.IsNullOrWhiteSpace(DisplayName) ? gameObject.name : DisplayName;
    }
}
