using UnityEngine;
using TMPro;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    /// <summary>
    /// 상점 슬롯 UI 컴포넌트
    /// </summary>
    public class ShopSlotUI : BaseSlotUI
    {
        private enum SlotInfoType
        {
            UnitPrice,
            UnitWeight,
            End
        }

        private int unitPrice;
        private string townName;
        private SlotInfoType infoType = SlotInfoType.UnitPrice;
        
        [SerializeField] private TextMeshProUGUI itemQuantityText;
        [SerializeField] private TextMeshProUGUI marketPriceText;

        // Properties
        public bool IsCartSlot { get; private set; }

        public override void Initialize(ItemData data, int itemQuantity)
        {
            Initialize(data, itemQuantity, false);
        }

        public void Initialize(ItemData data, int itemQuantity, bool isCart = false)
        {
            Initialize(data, itemQuantity, isCart, null);
        }

        public void Initialize(ItemData data, int itemQuantity, bool isCart, string marketTown)
        {
            itemData = data;
            quantity = itemQuantity;
            townName = string.IsNullOrWhiteSpace(marketTown) ? data.Town : marketTown.Trim();
            isEmpty = false;
            IsCartSlot = isCart;

            // 해당 마을에서의 아이템 가격 가져오기
            unitPrice = DataManager.Instance.GetItemPrice(data.PriceListID, townName);

            if (data.Icon != null)
                itemIcon.sprite = data.Icon;
            else
                SetItemIcon(data.Image);
            UpdateQuantityText();
            UpdateMarketPriceText();
            UpdateInfoText();
        }

        public override void InitializeEmpty()
        {
            base.InitializeEmpty();
            DisableItemQuantityText();
            DisableMarketPriceText();
        }

        public void ChangeInfoType()
        {
            if (++infoType == SlotInfoType.End)
                infoType = SlotInfoType.UnitPrice;
            
            UpdateInfoText();
        }

        public void UpdateQuantityText()
        {
            if (itemQuantityText != null)
            {
                itemQuantityText.text = "X" + quantity;
                itemQuantityText.enabled = !isEmpty;
            }
        }

        public void UpdateMarketPriceText()
        {
            if (marketPriceText == null)
                return;

            if (isEmpty || itemData == null || itemData.OriginPrice <= 0)
            {
                marketPriceText.text = "-";
                marketPriceText.enabled = false;
                return;
            }

            float marketRate = (unitPrice / (float)itemData.OriginPrice) * 100f;
            marketPriceText.gameObject.SetActive(true);
            marketPriceText.text = marketRate.ToString("F0") + "%";
            marketPriceText.enabled = true;
        }

        public override void UpdateInfoText()
        {
            if (isEmpty || itemData == null || infoText == null)
                return;

            switch (infoType)
            {
                case SlotInfoType.UnitWeight:
                    infoText.text = itemData.Weight.ToString("F1") + " kg";
                    break;
                case SlotInfoType.UnitPrice:
                    infoText.text = unitPrice.ToString() + " G";
                    break;
            }

            infoText.enabled = true;
        }

        public int GetUnitPrice()
        {
            return unitPrice;
        }

        public string GetTownName()
        {
            return townName;
        }

        public void DisableItemQuantityText()
        {
            if (itemQuantityText != null)
                itemQuantityText.gameObject.SetActive(false);
        }

        public void DisableMarketPriceText()
        {
            if (marketPriceText != null)
                marketPriceText.gameObject.SetActive(false);
        }
    }
}
