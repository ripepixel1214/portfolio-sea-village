using UnityEngine;
using SeaVillage.Town;
using SeaVillage.UI;
using SeaVillage.Core;

/// <summary>플레이어 가게 부지 상호작용 공통 뼈대</summary>
public abstract class PlayerShopInteractableBase : BaseInteractable
{
    private PlayerShop _shop;

    /// <summary>같은 부지의 PlayerShop 반환</summary>
    protected PlayerShop LinkedShop
    {
        get
        {
            if (_shop == null)
            {
                Transform scope = transform.parent != null ? transform.parent : transform;
                _shop = scope.GetComponentInChildren<PlayerShop>(true);
            }

            return _shop;
        }
    }

    protected TownKey TownKey => LinkedShop != null ? LinkedShop.TownKey : TownKey.Unknown;

    /// <summary>오류 메시지에 사용할 가게 명칭</summary>
    protected abstract string ShopLabel { get; }

    /// <summary>가게 연결과 마을 식별자 유효성 검증</summary>
    protected bool ValidateShop()
    {
        if (LinkedShop == null)
        {
            Debug.LogWarning($"{gameObject.name}: 같은 부지에서 {nameof(PlayerShop)} 컴포넌트를 찾을 수 없습니다");
            UIManager.Instance.ShowAlertMessage($"[Error] {ShopLabel} 설정을 찾을 수 없습니다");
            return false;
        }

        if (TownKey != TownKey.Unknown)
            return true;

        Debug.LogWarning($"{gameObject.name}: 유효하지 않은 {ShopLabel} 마을 식별자입니다");
        UIManager.Instance.ShowAlertMessage($"[Error] {ShopLabel} 마을 식별자를 확인할 수 없습니다");
        return false;
    }
}
