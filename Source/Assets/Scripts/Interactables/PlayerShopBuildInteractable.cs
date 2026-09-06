using System.Collections;
using UnityEngine;
using SeaVillage.Core;
using SeaVillage.UI;

/// <summary>플레이어 가게 건설 부지 상호작용</summary>
public class PlayerShopBuildInteractable : PlayerShopInteractableBase
{
    private Coroutine _buildStageRoutine;

    public override InteractionType InteractionType => InteractionType.PlayerShopLot;

    protected override string ShopLabel => "플레이어 가게 부지";

    protected override void OnEnable()
    {
        base.OnEnable();
        _buildStageRoutine = StartCoroutine(BindWhenReady());
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (_buildStageRoutine != null)
        {
            StopCoroutine(_buildStageRoutine);
            _buildStageRoutine = null;
        }

        if (PlayerShopManager.HasInstance)
            PlayerShopManager.Instance.OnShopStateChanged -= HandleShopStateChanged;
    }

    public override void Interact()
    {
        if (!ValidateShop())
            return;

        int affinity = TownProgressionManager.HasInstance
            ? TownProgressionManager.Instance.GetAffinity(TownKey)
            : 0;
        if (affinity < TownAffinityRules.PlayerShopRequiredAffinity)
        {
            UIManager.Instance?.ShowAlertMessage($"호감도 {TownAffinityRules.PlayerShopRequiredAffinity} 이상 필요");
            return;
        }

        PlayerShopBuildPanel buildPanel = UIManager.Instance.OpenPanel<PlayerShopBuildPanel>();
        if (buildPanel != null)
            buildPanel.InitializeBuild(TownKey);
    }

    private IEnumerator BindWhenReady()
    {
        yield return new WaitUntil(() => PlayerShopManager.HasInstance && PlayerShopManager.Instance.IsReady);

        PlayerShopManager.Instance.OnShopStateChanged += HandleShopStateChanged;
        _buildStageRoutine = null;
        ApplyBuildStage();
    }

    private void HandleShopStateChanged(TownKey townKey)
    {
        if (townKey == TownKey)
            ApplyBuildStage();
    }

    private void ApplyBuildStage()
    {
        if (LinkedShop == null)
            return;

        bool isBuilt = PlayerShopTownProcessor.IsShopBuilt(TownKey);

        LinkedShop.gameObject.SetActive(isBuilt);
        gameObject.SetActive(!isBuilt);
    }
}
