using TMPro;
using SeaVillage.Data;
using UnityEngine;
using System.Collections.Generic;

namespace SeaVillage.UI
{
    /// <summary>
    /// 상점 아이템 가격 그래프/인디케이터 공통 로직
    /// </summary>
    public class ShopItemPriceGraphView : MonoBehaviour
    {
        [Header("Indicator")]
        [SerializeField] private TextMeshProUGUI currentMarketPriceText;
        [SerializeField] private TextMeshProUGUI lowestMarketPriceText;
        [SerializeField] private TextMeshProUGUI highestMarketPriceText;

        [Header("Price Axis")]
        [SerializeField] private RectTransform axisLabelContainer;
        [SerializeField] private TextMeshProUGUI axisLabelPrefab;
        [SerializeField, Min(2)] private int axisTickCount = 6;

        [Header("Price History Graph")]
        [SerializeField] private PriceHistoryGraphDisplay priceHistoryGraph;

        private readonly List<TextMeshProUGUI> axisLabelPool = new List<TextMeshProUGUI>();

        public void UpdateGraph(ItemData itemData, int currentPrice)
        {
            UpdateGraph(itemData, currentPrice, null);
        }

        /// <summary>아이템이 없을 때 인디케이터·그래프를 빈 상태로 표시</summary>
        public void Clear()
        {
            if (currentMarketPriceText != null)
                currentMarketPriceText.text = "현재 시세 -";
            if (lowestMarketPriceText != null)
                lowestMarketPriceText.text = "최저 -";
            if (highestMarketPriceText != null)
                highestMarketPriceText.text = "최고 -";

            for (int i = 0; i < axisLabelPool.Count; i++)
                if (axisLabelPool[i] != null)
                    axisLabelPool[i].gameObject.SetActive(false);

            if (priceHistoryGraph != null)
                priceHistoryGraph.ShowPriceHistory(new List<int>());
        }

        public void UpdateGraph(ItemData itemData, int currentPrice, string marketTown)
        {
            if (itemData == null || !DataManager.HasInstance)
                return;

            string resolvedMarketTown = string.IsNullOrWhiteSpace(marketTown) ? itemData.Town : marketTown.Trim();
            List<int> priceHistory = DataManager.Instance.GetItemPriceHistory(itemData.PriceListID, resolvedMarketTown);
            PriceGraphScaleUtility.ComputeRange(priceHistory, currentPrice, out int minPrice, out int maxPrice);

            UpdateMarketPriceIndicators(currentPrice, minPrice, maxPrice);
            UpdatePriceAxisTexts(minPrice, maxPrice);
            UpdatePriceHistoryGraph(priceHistory, itemData);
        }

        private void UpdateMarketPriceIndicators(int currentPrice, int lowestPrice, int highestPrice)
        {
            if (currentMarketPriceText != null)
                currentMarketPriceText.text = $"현재 시세 {currentPrice}골드";
            if (lowestMarketPriceText != null)
                lowestMarketPriceText.text = $"최저 {lowestPrice}골드";
            if (highestMarketPriceText != null)
                highestMarketPriceText.text = $"최고 {highestPrice}골드";
        }

        private void UpdatePriceAxisTexts(int minPrice, int maxPrice)
        {
            if (!EnsureAxisLabels())
                return;

            int tickCount = Mathf.Max(2, axisTickCount);
            for (int i = 0; i < axisLabelPool.Count; i++)
            {
                TextMeshProUGUI label = axisLabelPool[i];
                if (label == null)
                    continue;

                bool isIntermediate = i > 0 && i < tickCount - 1;
                bool isVisible = !isIntermediate || (maxPrice - minPrice > 10);

                label.gameObject.SetActive(isVisible);
                if (!isVisible)
                    continue;

                label.text = PriceGraphScaleUtility.GetTickValue(maxPrice, minPrice, i, tickCount).ToString();
                UpdateAxisLabelPosition(label, i, tickCount, axisLabelContainer);
            }
        }

        private void UpdateAxisLabelPosition(TextMeshProUGUI label, int tickIndex, int tickCount, RectTransform container)
        {
            if (container == null)
                return;

            RectTransform labelRect = label.rectTransform;
            float normalizedY = PriceGraphScaleUtility.GetTickNormalizedY(tickIndex, tickCount);
            Vector2 anchoredPosition = labelRect.anchoredPosition;
            anchoredPosition.y = PriceGraphScaleUtility.NormalizedYToAnchoredY(normalizedY, container.rect.height);
            labelRect.anchoredPosition = anchoredPosition;
        }

        private bool EnsureAxisLabels()
        {
            if (axisLabelContainer == null || axisLabelPrefab == null)
                return false;

            int targetCount = Mathf.Max(2, axisTickCount);

            while (axisLabelPool.Count < targetCount)
            {
                TextMeshProUGUI labelInstance = Instantiate(axisLabelPrefab, axisLabelContainer);
                axisLabelPool.Add(labelInstance);
            }

            for (int i = 0; i < axisLabelPool.Count; i++)
                if (axisLabelPool[i] != null)
                    axisLabelPool[i].gameObject.SetActive(i < targetCount);

            return true;
        }

        private void UpdatePriceHistoryGraph(List<int> priceHistory, ItemData itemData)
        {
            if (priceHistoryGraph == null || itemData == null)
                return;

            priceHistoryGraph.ShowPriceHistory(priceHistory);
        }
    }
}
