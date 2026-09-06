using UnityEngine;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    /// <summary>플레이어 가게 정산 아이템 표시 슬롯</summary>
    public class SettlementItemSlot : BaseSlotUI
    {
        /// <summary>정산 아이템과 판매 수량 표시</summary>
        public override void Initialize(ItemData data, int itemQuantity)
        {
            if (data == null || itemQuantity <= 0)
            {
                InitializeEmpty();
                return;
            }

            itemData = data;
            quantity = itemQuantity;
            isEmpty = false;

            SetItemIcon(data.Image);
            UpdateInfoText();
        }

        /// <summary>판매 수량 텍스트 갱신</summary>
        public override void UpdateInfoText()
        {
            if (isEmpty || infoText == null)
                return;

            infoText.text = $"X {Mathf.Max(0, quantity)}";
            infoText.enabled = true;
        }
    }
}
