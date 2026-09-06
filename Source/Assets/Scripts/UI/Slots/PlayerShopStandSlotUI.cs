using SeaVillage.Data;
using TMPro;
using UnityEngine;

namespace SeaVillage.UI
{
    /// <summary>판매대 한 칸, 재고 수·판매가·현재 시세를 표시하고 미해금 칸은 잠금 표시로 전환</summary>
    public class PlayerShopStandSlotUI : BaseSlotUI
    {
        [Header("Player Shop Stand")]
        [SerializeField] private TextMeshProUGUI _priceText;
        [SerializeField] private TextMeshProUGUI _marketPriceText;
        [SerializeField] private GameObject _lockedRoot;

        public bool IsLocked { get; private set; }

        public override void Initialize(ItemData data, int itemQuantity)
            => Initialize(data, itemQuantity, 0, 0);

        /// <summary>판매 중인 아이템으로 칸 채움</summary>
        public void Initialize(ItemData data, int itemQuantity, int unitPrice, int marketPrice)
        {
            IsLocked = false;
            isEmpty = false;
            itemData = data;
            quantity = itemQuantity;

            SetLockedVisible(false);

            if (itemIcon != null)
                itemIcon.enabled = true;

            SetItemIcon(data != null ? data.Image : string.Empty);
            UpdateInfoText();
            SetPriceTexts(unitPrice, marketPrice);
        }

        public override void InitializeEmpty()
        {
            IsLocked = false;
            base.InitializeEmpty();

            SetLockedVisible(false);
            SetPriceTexts(0, 0);
        }

        /// <summary>가게 레벨이 모자라 아직 쓸 수 없는 칸으로 설정</summary>
        public void InitializeLocked()
        {
            base.InitializeEmpty();
            IsLocked = true;

            SetLockedVisible(true);
            SetPriceTexts(0, 0);
        }

        public override void UpdateInfoText()
        {
            if (infoText == null)
                return;

            infoText.enabled = !isEmpty;
            infoText.text = isEmpty ? string.Empty : quantity.ToString();
        }

        private void SetPriceTexts(int unitPrice, int marketPrice)
        {
            if (_priceText != null)
                _priceText.text = unitPrice > 0 ? $"{unitPrice} G" : "-";

            if (_marketPriceText != null)
                _marketPriceText.text = marketPrice > 0 ? $"({marketPrice} G)" : "-";
        }

        private void SetLockedVisible(bool isVisible)
        {
            if (_lockedRoot != null)
                _lockedRoot.SetActive(isVisible);
        }
    }
}
