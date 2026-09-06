using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaVillage.UI.Shop
{
    /// <summary>
    /// 상점 View 공통 인터페이스
    /// </summary>
    public interface IShopView
    {
        // Events
        event Action OnConfirmButtonClicked;
        event Action<int, Vector2> OnCartSlotClicked;

        void ShowMessage(string message, Action onConfirm = null);
    }

    /// <summary>
    /// 구매 패널 View 인터페이스
    /// </summary>
    public interface IPurchaseView : IShopView
    {
        event Action<int> OnSlotClicked;

        void RefreshSourceSlots(IReadOnlyDictionary<int, int> items);
        void RefreshTargetSlots(IReadOnlyDictionary<int, int> items);
        void UpdatePriceDisplay(int totalPrice, int tradingFee, int finalPrice);
        void UpdateWeightGauge(float maxWeight, float currentWeight, float cartWeight);
    }

    /// <summary>
    /// 판매 패널 View 인터페이스
    /// </summary>
    public interface ISellView : IShopView
    {
        void RefreshPlayerInventorySlots(IReadOnlyDictionary<int, int> items);
        void RefreshShipInventorySlots(IReadOnlyDictionary<int, int> items);
        void RefreshSellListSlots(IReadOnlyDictionary<int, int> items);
        void UpdatePriceDisplay(int totalPrice, int tradingFee, int finalPrice, int purchasePrice);
    }
}
