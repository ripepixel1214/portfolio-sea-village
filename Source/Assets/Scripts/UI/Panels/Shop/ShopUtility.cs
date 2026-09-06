using UnityEngine;
using SeaVillage.Core;

namespace SeaVillage.UI
{
    /// <summary>
    /// 상점 관련 유틸리티 메서드
    /// </summary>
    public static class ShopUtility
    {
        public const float TradingFeeRate = 0.2f;

        /// <summary>
        /// 현재 씬의 저장용 마을 키 반환
        /// </summary>
        public static string GetCurrentTownStorageKey()
        {
            TownKey town = GameManager.HasInstance ? GameManager.Instance.CurrentTownKey : TownKey.Unknown;
            return TownKeyUtility.ToStorageKey(town);
        }

        /// <summary>
        /// 거래 기준 마을 결정. marketTown이 비어 있으면 fallbackTown 사용
        /// </summary>
        public static string ResolveMarketTown(string marketTown, string fallbackTown)
        {
            return TownKeyUtility.NormalizeStorageKey(string.IsNullOrWhiteSpace(marketTown) ? fallbackTown : marketTown);
        }

        public static int CalculateTradingFee(int totalPrice)
        {
            return Mathf.FloorToInt(totalPrice * TradingFeeRate);
        }

        /// <summary>
        /// 최종 가격 계산 (구매: 총액 + 수수료, 판매: 총액 - 수수료)
        /// </summary>
        public static int CalculateFinalPrice(int totalPrice, bool isPurchase)
        {
            int fee = CalculateTradingFee(totalPrice);
            return isPurchase ? totalPrice + fee : totalPrice - fee;
        }
    }
}
