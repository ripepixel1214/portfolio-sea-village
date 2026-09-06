using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    /// <summary>
    /// 배 - 아이템 정보 패널
    /// </summary>
    public class ItemInformationPanel : UIPanel, IContextualPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        [Header("Purchase Information Settings")]
        [SerializeField] private Image itemImage;
        [SerializeField] private TextMeshProUGUI itemName;
        [SerializeField] private TextMeshProUGUI itemTown;
        [SerializeField] private TextMeshProUGUI itemQuantity;
        [SerializeField] private TextMeshProUGUI unitWeight;
        [SerializeField] private TextMeshProUGUI averagePurchasePrice;

        protected override void AddListeners()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        protected override void RemoveListeners()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }

        /// <summary>
        /// 아이템 정보 패널 초기화
        /// </summary>
        public void InitializeItemInformation(
            ItemData itemData,
            int quantity,
            int inventoryAveragePurchasePrice)
        {
            if (itemData == null)
            {
                Debug.LogError("Item Information Panel: Received NULL item data for initialization.");
                return;
            }

            itemName.text = DataManager.Instance.GetScriptTextKR(itemData.Name);
            itemTown.text = TownDisplayNames.GetTownDisplayName(itemData.Town);
            itemQuantity.text = quantity.ToString();
            unitWeight.text = $"{itemData.Weight:F1} kg";
            int displayPurchasePrice = ItemInformationViewPolicy.ResolveAveragePurchasePrice(
                inventoryAveragePurchasePrice,
                itemData.OriginPrice);
            averagePurchasePrice.text = $"{displayPurchasePrice:N0} G";

            string itemImageText = itemData.Image;
            if (string.IsNullOrEmpty(itemImageText))
            {
                Debug.LogError($"Purchase Information Panel: Item data '{itemData.Name}' has no associated image.");
                return;
            }

            if (itemData != null && itemData.Icon != null)
                itemImage.sprite = itemData.Icon;
            else
                itemImage.sprite = UIManager.Instance.LoadItemIcon(itemImageText);
        }
    }
}
