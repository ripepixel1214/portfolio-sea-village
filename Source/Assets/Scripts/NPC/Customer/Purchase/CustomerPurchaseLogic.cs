using System.Collections.Generic;
using SeaVillage.Core;
using SeaVillage.Data;
using SeaVillage.Town;
using UnityEngine;

namespace SeaVillage.NPC
{
    /// <summary>
    /// 상점이 내놓은 판매 목록과 고객 선호도, 시세, 보유 골드를 바탕으로 구매 결과를 계산한다.
    /// </summary>
    public static class CustomerPurchaseLogic
    {
        private const float BasePriceProbability = 40f;
        private const float MaxPreferenceBonus = 50f;
        private const float LikedCategoryBonus = 20f;
        private const float FavoriteItemBonus = 10f;
        private const float EventItemBonus = 20f;
        private const float CartItemProbabilityPenalty = 30f;
        private const int MaxCartCount = PlayerShopOperatingRules.MaxItemsPerCustomerPurchase;

        public static float CalculateNextPurchaseProbability(
            float combinedProbability,
            int cartItemCount)
        {
            float clampedProbability = Mathf.Clamp(combinedProbability, 0f, 100f);
            float cartPenalty = Mathf.Max(0, cartItemCount) * CartItemProbabilityPenalty;
            return Mathf.Max(0f, clampedProbability - cartPenalty);
        }

        public static PurchaseResult Evaluate(
            CustomerData customer,
            IReadOnlyList<ShopListing> listings,
            string villageName,
            long currentGold)
        {
            var dm = DataManager.Instance;
            if (dm?.ItemDatabase == null || listings == null || listings.Count == 0)
            {
                return PurchaseResult.Empty;
            }

            var eventItems = RuntimeItemPriceManager.HasInstance
                ? RuntimeItemPriceManager.Instance.GetEventItemsForTown(villageName)
                : new HashSet<int>();

            var candidates = BuildCandidates(listings, villageName, dm);
            if (candidates.Count == 0)
            {
                return PurchaseResult.Empty;
            }

            var result = FillCart(customer, candidates, currentGold, eventItems);
            result.Condition0 = CustomerItemConditionPolicy.Select(
                result,
                customer,
                candidates,
                eventItems);

            return result;
        }

        /// <summary>
        /// 레스토랑 방문은 구매 확률을 적용하지 않고, 보유 골드로 살 수 있는 메뉴 하나를 반드시 고른다.
        /// </summary>
        public static PurchaseResult EvaluateGuaranteed(
            CustomerData customer,
            IReadOnlyList<ShopListing> listings,
            string villageName,
            long currentGold)
        {
            var dm = DataManager.Instance;
            if (dm?.ItemDatabase == null || listings == null || listings.Count == 0)
            {
                return PurchaseResult.Empty;
            }

            var candidates = BuildCandidates(listings, villageName, dm);
            candidates.RemoveAll(candidate => candidate.SellingPrice > currentGold);
            if (candidates.Count == 0)
            {
                return PurchaseResult.Empty;
            }

            var eventItems = RuntimeItemPriceManager.HasInstance
                ? RuntimeItemPriceManager.Instance.GetEventItemsForTown(villageName)
                : new HashSet<int>();
            PricedItem selected = candidates[Random.Range(0, candidates.Count)];
            var result = new PurchaseResult
            {
                Cart = new List<CartItem> { new(selected.Item, selected.SellingPrice) },
                TotalCost = selected.SellingPrice
            };
            result.Condition0 = CustomerItemConditionPolicy.Select(
                result,
                customer,
                candidates,
                eventItems);
            return result;
        }

        public static PurchaseResult Evaluate(
            CustomerData customer,
            IPlayerShopSalesTarget playerShop,
            string villageName,
            long currentGold)
        {
            if (playerShop == null)
            {
                return PurchaseResult.Empty;
            }

            return Evaluate(
                customer,
                playerShop.GetSaleOffers(),
                playerShop.StaffEffects,
                villageName,
                currentGold);
        }

        /// <summary>
        /// 씬에 가게 인스턴스가 없는 상황(오프라인 정산)에서도 같은 구매 판단을 수행한다.
        /// </summary>
        public static PurchaseResult Evaluate(
            CustomerData customer,
            IReadOnlyList<PlayerShopSaleOffer> offers,
            StaffEffectReadOnly staffEffects,
            string villageName,
            long currentGold)
        {
            var dm = DataManager.Instance;
            if (dm?.ItemDatabase == null || offers == null || offers.Count == 0)
            {
                return PurchaseResult.Empty;
            }

            var eventItems = RuntimeItemPriceManager.HasInstance
                ? RuntimeItemPriceManager.Instance.GetEventItemsForTown(villageName)
                : new HashSet<int>();

            var candidates = BuildCandidates(offers, villageName, dm);
            if (candidates.Count == 0)
            {
                return PurchaseResult.Empty;
            }

            var result = FillCart(
                customer,
                candidates,
                currentGold,
                eventItems,
                staffEffects.SalesPurchaseProbabilityBonus,
                staffEffects.CashierIntelligence);
            result.Condition0 = CustomerItemConditionPolicy.Select(
                result,
                customer,
                candidates,
                eventItems);

            return result;
        }

        #region Cart

        private static PurchaseResult FillCart(
            CustomerData customer,
            List<PricedItem> candidates,
            long currentGold,
            HashSet<int> eventItems,
            float probabilityBonus = 0f,
            int cashierIntelligence = 0)
        {
            var cart = new List<CartItem>(MaxCartCount);
            long cartTotal = 0;

            while (cart.Count < MaxCartCount)
            {
                var candidate = candidates[Random.Range(0, candidates.Count)];
                if (CountInCart(cart, candidate.Item.ID) >= candidate.AvailableQuantity)
                {
                    break;
                }

                bool isEvent = eventItems.Contains(candidate.Item.ID);

                float combinedProbability = CalculateProbability(
                    candidate.Item, customer, candidate.SellingPrice, candidate.MarketPrice, isEvent)
                    + probabilityBonus;
                float prob = CalculateNextPurchaseProbability(combinedProbability, cart.Count);

                if (Random.Range(0f, 100f) >= prob)
                {
                    break;
                }

                long nextCartTotal = cartTotal + candidate.SellingPrice;
                long nextPayment = StaffRules.CalculateCustomerPayment(
                    nextCartTotal,
                    cashierIntelligence);
                if (nextPayment > currentGold)
                {
                    break;
                }

                cart.Add(new CartItem(candidate.Item, candidate.SellingPrice));
                cartTotal = nextCartTotal;
            }

            return new PurchaseResult
            {
                Cart = cart,
                TotalCost = StaffRules.CalculateCustomerPayment(cartTotal, cashierIntelligence),
            };
        }

        #endregion

        #region Candidates

        private static List<PricedItem> BuildCandidates(
            IReadOnlyList<ShopListing> listings,
            string villageName,
            DataManager dm)
        {
            var result = new List<PricedItem>(listings.Count);

            foreach (var listing in listings)
            {
                var item = dm.ItemDatabase.GetItem(listing.ItemId);
                if (item == null || listing.UnitPrice <= 0)
                {
                    continue;
                }

                int marketPrice = dm.GetItemPrice(item.PriceListID, villageName);
                if (marketPrice <= 0)
                {
                    marketPrice = item.OriginPrice;
                }

                result.Add(new PricedItem(item, listing.UnitPrice, marketPrice));
            }

            return result;
        }

        private static List<PricedItem> BuildCandidates(
            IReadOnlyList<PlayerShopSaleOffer> offers,
            string villageName,
            DataManager dm)
        {
            var result = new List<PricedItem>(offers.Count);

            foreach (var offer in offers)
            {
                if (offer?.Item == null || offer.UnitPrice <= 0 || offer.AvailableQuantity <= 0)
                {
                    continue;
                }

                int marketPrice = dm.GetItemPrice(offer.Item.PriceListID, villageName);
                if (marketPrice <= 0)
                {
                    marketPrice = offer.Item.OriginPrice;
                }

                result.Add(new PricedItem(offer.Item, offer.UnitPrice, marketPrice, offer.AvailableQuantity));
            }

            return result;
        }

        #endregion

        #region Probability

        private static float CalculateProbability(
            ItemData item,
            CustomerData customer,
            int sellingPrice,
            int marketPrice,
            bool isEvent)
        {
            if (marketPrice <= 0)
            {
                return 0f;
            }

            float priceDiffPercent = ((float)sellingPrice / marketPrice - 1f) * 100f;
            float priceProb = BasePriceProbability - priceDiffPercent;

            float prefProb = 0f;
            if (IsLikedCategory(item, customer))
            {
                prefProb += LikedCategoryBonus;
            }

            if (item.ID == customer.Favorite)
            {
                prefProb += FavoriteItemBonus;
            }

            if (isEvent)
            {
                prefProb += EventItemBonus;
            }

            prefProb = Mathf.Min(prefProb, MaxPreferenceBonus);

            return Mathf.Clamp(priceProb + prefProb, 0f, 100f);
        }

        #endregion

        #region Utilities

        private static int CountInCart(IReadOnlyList<CartItem> cart, int itemId)
        {
            int count = 0;
            for (int i = 0; i < cart.Count; i++)
            {
                if (cart[i].Item.ID == itemId)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsLikedCategory(ItemData item, CustomerData customer)
        {
            if (string.IsNullOrEmpty(customer.Like) || item.Type == null)
            {
                return false;
            }

            foreach (var liked in customer.Like.Split(','))
            {
                if (item.Type.Contains(liked.Trim()))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
