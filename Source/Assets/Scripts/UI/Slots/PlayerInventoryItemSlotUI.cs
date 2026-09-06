using UnityEngine;
using TMPro;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    public sealed class PlayerInventoryItemSlotUI : BaseSlotUI
    {
        [Header("Player Inventory Display")]
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private TextMeshProUGUI marketRateText;

        public override void Initialize(ItemData data, int itemQuantity)
        {
            string marketTown = ResolveMarketTown(data);
            Initialize(data, itemQuantity, marketTown);
        }

        public void Initialize(ItemData data, int itemQuantity, string marketTown)
        {
            if (data == null)
            {
                InitializeEmpty();
                return;
            }

            itemData = data;
            quantity = Mathf.Max(0, itemQuantity);
            isEmpty = false;

            SetItemIcon(data.Image);
            UpdateInfoText();
            UpdateQuantityText();
            UpdateMarketRateText(marketTown);
        }

        public override void InitializeEmpty()
        {
            base.InitializeEmpty();
            SetTextActive(quantityText, false);
            SetMarketRateActive(false);
        }

        public override void UpdateInfoText()
        {
            if (infoText == null)
                return;

            bool hasItem = !isEmpty && itemData != null;
            infoText.gameObject.SetActive(hasItem);
            if (hasItem)
                infoText.text = PlayerInventoryViewPolicy.GetOriginLabel(itemData.Town);
        }

        private void UpdateQuantityText()
        {
            if (quantityText == null)
                return;

            quantityText.gameObject.SetActive(true);
            quantityText.text = $"x{quantity}";
        }

        private void UpdateMarketRateText(string marketTown)
        {
            if (marketRateText == null)
                return;

            if (itemData == null || itemData.OriginPrice <= 0)
            {
                SetMarketRateActive(false);
                return;
            }

            int currentPrice = itemData.OriginPrice;
            if (DataManager.HasInstance && RuntimeItemPriceManager.HasInstance)
                currentPrice = DataManager.Instance.GetItemPrice(itemData.PriceListID, marketTown);

            int marketRate = PlayerInventoryViewPolicy.CalculateMarketRatePercent(
                itemData.OriginPrice,
                currentPrice);
            SetMarketRateActive(true);
            marketRateText.text = $"{marketRate}%";
        }

        private static string ResolveMarketTown(ItemData data)
        {
            if (GameManager.HasInstance && GameManager.Instance.CurrentTownKey != TownKey.Unknown)
                return TownKeyUtility.ToStorageKey(GameManager.Instance.CurrentTownKey);

            return TownKeyUtility.NormalizeStorageKey(data?.Town);
        }

        private static void SetTextActive(TextMeshProUGUI text, bool active)
        {
            if (text != null)
                text.gameObject.SetActive(active);
        }

        private void SetMarketRateActive(bool active)
        {
            if (marketRateText == null)
                return;

            Transform badge = marketRateText.transform.parent;
            if (badge != null)
                badge.gameObject.SetActive(active);
            else
                marketRateText.gameObject.SetActive(active);
        }
    }
}
