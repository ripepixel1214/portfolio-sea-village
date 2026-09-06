using TMPro;
using SeaVillage.Data;
using UnityEngine;
using UnityEngine.UI;
using SeaVillage.UI;

public class DollEntry : MonoBehaviour
{
    [SerializeField] private Image _dollImage;
    [SerializeField] private TMP_Text _statText;
    [SerializeField] private TMP_Text _conditionText;
    [SerializeField] private Button _actionButton;
    [SerializeField] private TMP_Text _actionButtonText;

    /// <summary>인형 획득 버튼 반환</summary>
    public Button ActionButton => _actionButton;

    /// <summary>아이템 아이콘·능력치·조건·획득 버튼 표시</summary>
    public void Configure(ItemData itemData, string statText, string conditionText, string actionLabel)
    {
        SetItemIcon(itemData);
        SpecialShopPanelUtility.SetText(_statText, statText);
        SpecialShopPanelUtility.SetText(_conditionText, conditionText);
        SpecialShopPanelUtility.SetText(_actionButtonText, actionLabel);
    }

    private void SetItemIcon(ItemData itemData)
    {
        if (_dollImage == null)
            return;

        Sprite icon = itemData?.Icon;
        if (icon == null && itemData != null && UIManager.HasInstance)
            icon = UIManager.Instance.LoadItemIcon(itemData.Image);

        _dollImage.sprite = icon;
        _dollImage.enabled = icon != null;
    }
}
