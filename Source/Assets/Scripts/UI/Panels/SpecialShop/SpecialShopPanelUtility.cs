using SeaVillage.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    internal static class SpecialShopPanelUtility
    {
        public static ItemData GetItem(int itemId)
        {
            return DataManager.HasInstance ? DataManager.Instance.GetItem(itemId) : null;
        }

        public static string GetItemName(int itemId)
        {
            ItemData item = GetItem(itemId);
            if (item == null)
                return itemId.ToString();

            string itemName = DataManager.Instance.GetScriptTextKR(item.Name);
            return string.IsNullOrWhiteSpace(itemName) ? itemId.ToString() : itemName;
        }

        public static void SetItemIcon(Image image, int itemId)
        {
            if (image == null)
                return;

            ItemData item = GetItem(itemId);
            Sprite icon = item?.Icon;
            if (icon == null && UIManager.HasInstance)
                icon = UIManager.Instance.LoadItemIcon(item?.Image);

            image.sprite = icon;
            image.enabled = icon != null;
        }

        public static void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value ?? string.Empty;
        }
    }
}
