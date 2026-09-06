using UnityEngine;
using TMPro;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    /// <summary>
    /// 인벤토리 슬롯 UI 컴포넌트
    /// </summary>
    public class InventorySlotUI : BaseSlotUI
    {
        public enum SlotInfoType
        {
            Quantity,
            TotalWeight,
            Town,
            End
        }

        private SlotInfoType infoType = SlotInfoType.Quantity;
        private string townName;

        /// <summary>
        /// 기본 초기화 (BaseSlotUI 추상 메서드 구현)
        /// </summary>
        public override void Initialize(ItemData data, int itemQuantity)
        {
            Initialize(data, itemQuantity, SlotInfoType.Quantity);
        }

        /// <summary>
        /// 정보 타입을 지정하여 초기화
        /// </summary>
        public void Initialize(ItemData data, int itemQuantity, SlotInfoType initialInfoType)
        {
            itemData = data;
            quantity = itemQuantity;
            townName = data.Town;
            isEmpty = false;
            infoType = initialInfoType;

            SetItemIcon(data.Image);
            UpdateInfoText();
        }

        public override void InitializeEmpty()
        {
            base.InitializeEmpty();
        }

        /// <summary>
        /// 정보 타입 순환 변경
        /// </summary>
        public void ChangeInfoType()
        {
            if (++infoType == SlotInfoType.End)
                infoType = SlotInfoType.Quantity;
            
            UpdateInfoText();
        }

        public override void UpdateInfoText()
        {
            if (isEmpty || itemData == null || infoText == null)
                return;

            switch (infoType)
            {
                case SlotInfoType.Quantity:
                    infoText.text = "X " + (quantity >= 1 ? quantity.ToString() : "");
                    break;
                case SlotInfoType.TotalWeight:
                    float totalWeight = Mathf.Round(itemData.Weight * quantity * 10f) / 10f;
                    infoText.text = totalWeight.ToString("F1") + " kg";
                    break;
                case SlotInfoType.Town:
                    infoText.text = TownDisplayNames.GetTownDisplayName(townName);
                    break;
            }

            infoText.enabled = true;
        }

        #region Getters
        public string GetTownName()
        {
            return townName;
        }

        public SlotInfoType GetCurrentInfoType()
        {
            return infoType;
        }
        #endregion
    }
}
