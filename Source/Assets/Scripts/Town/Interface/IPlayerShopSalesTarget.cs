using System.Collections.Generic;
using SeaVillage.Core;

namespace SeaVillage.Town
{
    /// <summary>NPC가 플레이어 가게 판매 기능에 접근하는 경계</summary>
    public interface IPlayerShopSalesTarget
    {
        TownKey TownKey { get; }

        StaffEffectReadOnly StaffEffects { get; }

        int OfflineCustomerCount { get; }

        IReadOnlyList<PlayerShopSaleOffer> GetSaleOffers();

        /// <summary>구매하지 않은 손님의 방문만 기록</summary>
        bool TryRecordCustomerVisit(out string failReason);

        /// <summary>손님 한 명의 방문과 구매를 하나의 판매 요청으로 기록</summary>
        bool TryRecordCustomerPurchase(
            IReadOnlyList<PlayerShopSaleRequest> requests,
            long customerPayment,
            out PlayerShopSaleResult receipt,
            out string failReason);
    }
}
