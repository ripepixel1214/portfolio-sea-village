using UnityEngine;

namespace SeaVillage.UI
{
    public static class ItemInformationViewPolicy
    {
        public static int ResolveAveragePurchasePrice(
            int inventoryAveragePurchasePrice,
            int originPrice)
        {
            return inventoryAveragePurchasePrice > 0
                ? inventoryAveragePurchasePrice
                : Mathf.Max(0, originPrice);
        }
    }
}
