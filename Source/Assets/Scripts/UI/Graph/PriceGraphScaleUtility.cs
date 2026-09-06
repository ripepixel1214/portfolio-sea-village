using System.Collections.Generic;
using UnityEngine;

namespace SeaVillage.UI
{
    public static class PriceGraphScaleUtility
    {
        public const int PointRadius = 4;
        public const float VerticalPaddingRatio = 0.1f;

        public static void ComputeRange(IReadOnlyList<int> prices, int fallbackPrice, out int minPrice, out int maxPrice)
        {
            minPrice = fallbackPrice;
            maxPrice = fallbackPrice;

            if (prices == null || prices.Count == 0)
            {
                ApplyFlatRangeFallback(ref minPrice, ref maxPrice);
                return;
            }

            bool hasValue = false;
            for (int i = 0; i < prices.Count; i++)
            {
                int price = prices[i];
                if (price <= 0)
                    continue;

                if (!hasValue)
                {
                    minPrice = price;
                    maxPrice = price;
                    hasValue = true;
                    continue;
                }

                if (price < minPrice) minPrice = price;
                if (price > maxPrice) maxPrice = price;
            }

            if (!hasValue)
            {
                minPrice = fallbackPrice;
                maxPrice = fallbackPrice;
            }

            ApplyFlatRangeFallback(ref minPrice, ref maxPrice);
        }

        /// <summary>
        /// 가격축 값 계산
        /// </summary>
        public static int GetTickValue(int maxPrice, int minPrice, int tickIndex, int tickCount)
        {
            if (tickCount <= 1)
                return maxPrice;

            float t = Mathf.Clamp01(tickIndex / (float)(tickCount - 1));
            return Mathf.RoundToInt(Mathf.Lerp(maxPrice, minPrice, t));
        }

        /// <summary>
        /// 가격축 정규화값 계산
        /// </summary>
        public static float GetTickNormalizedY(int tickIndex, int tickCount)
        {
            if (tickCount <= 1)
                return ApplyVerticalPadding(0.5f);

            float rawNormalized = Mathf.Clamp01(tickIndex / (float)(tickCount - 1));
            return ApplyVerticalPadding(rawNormalized);
        }

        public static float ValueToNormalizedY(int value, int minPrice, int maxPrice)
        {
            float range = maxPrice - minPrice;
            float rawNormalized = range > 0f
                ? 1f - (value - minPrice) / range
                : 0.5f;

            return ApplyVerticalPadding(rawNormalized);
        }

        public static float NormalizedYToPixelY(float normalizedY, float areaHeight)
        {
            return normalizedY * (areaHeight - PointRadius * 2) + PointRadius;
        }

        public static float NormalizedYToAnchoredY(float normalizedY, float containerHeight)
        {
            return Mathf.Lerp(containerHeight * 0.5f, -containerHeight * 0.5f, normalizedY);
        }

        private static float ApplyVerticalPadding(float rawNormalized)
        {
            float padding = Mathf.Clamp01(VerticalPaddingRatio);
            return Mathf.Lerp(padding, 1f - padding, Mathf.Clamp01(rawNormalized));
        }

        private static void ApplyFlatRangeFallback(ref int minPrice, ref int maxPrice)
        {
            if (minPrice != maxPrice)
                return;

            minPrice = Mathf.Max(0, minPrice - 10);
            maxPrice += 10;
        }
    }
}
