using System.Collections.Generic;
using SeaVillage.Core;
using SeaVillage.Town;
using SeaVillage.Utilities;
using UnityEngine;

namespace SeaVillage.NPC
{
    public class NPCDestinationSelector
    {
        private const float PlayerShopFavorabilityScale = 0.04f;

        private readonly List<IShop> _purchasedShops = new();
        private IShop _lastCompletedShop;
        private int _visitedPlaceCount;
        private bool _lastCompletedWasBoard;

        public int VisitedPlaceCount => _visitedPlaceCount;

        public NPCDestination? Select(
            ITownContext town,
            NPCDestination? currentDestination,
            int favorability,
            float npcX)
        {
            var shopCandidates = BuildShopCandidates(town, currentDestination, favorability);
            bool boardAvailable = IsBoardAvailable(town, currentDestination);

            if (shopCandidates.Count == 0 && !boardAvailable)
            {
                return null;
            }

            var candidates = new List<NPCDestination>(shopCandidates);
            if (boardAvailable)
            {
                candidates.Add(NPCDestination.FromBoard(town.NoticeBoard!, -1, Vector2.zero, 1f));
            }

            var picked = PickWeightedRandom(candidates);

            if (picked.Type != DestinationType.NoticeBoard)
            {
                return picked;
            }

            if (!picked.Board!.TryReserveSlot(npcX, out int slotIndex, out Vector2 slotPosition))
            {
                return shopCandidates.Count > 0 ? PickWeightedRandom(shopCandidates) : (NPCDestination?)null;
            }

            return NPCDestination.FromBoard(picked.Board, slotIndex, slotPosition, picked.Weight);
        }

        public void OnDestinationCompleted(NPCDestination destination, bool didPurchase)
        {
            if (destination.Type == DestinationType.Shop && destination.Shop != null)
            {
                _lastCompletedShop = destination.Shop;
                if (didPurchase)
                {
                    _purchasedShops.Add(destination.Shop);
                }

                _visitedPlaceCount++;
                _lastCompletedWasBoard = false;
            }
            else if (destination.Type == DestinationType.NoticeBoard)
            {
                _lastCompletedShop = null;
                _visitedPlaceCount++;
                _lastCompletedWasBoard = true;
            }
        }

        public void Reset()
        {
            _purchasedShops.Clear();
            _lastCompletedShop = null;
            _visitedPlaceCount = 0;
            _lastCompletedWasBoard = false;
        }

        private List<NPCDestination> BuildShopCandidates(
            ITownContext town,
            NPCDestination? currentDestination,
            int favorability)
        {
            var result = new List<NPCDestination>();

            foreach (var shop in town.GetAvailableShops())
            {
                if ((currentDestination is { Type: DestinationType.Shop, Shop: { } current } && shop == current)
                    || shop == _lastCompletedShop)
                {
                    continue;
                }

                if (_purchasedShops.Contains(shop))
                {
                    continue;
                }

                float weight = 1f;
                if (shop.IsPlayerOwned)
                {
                    weight += Mathf.Max(
                        0f,
                        (favorability - TownAffinityRules.PlayerShopRequiredAffinity) * PlayerShopFavorabilityScale);
                }

                result.Add(NPCDestination.FromShop(shop, weight));
            }

            return result;
        }

        private bool IsBoardAvailable(ITownContext town, NPCDestination? currentDestination)
        {
            if (_lastCompletedWasBoard)
            {
                return false;
            }

            var board = town.NoticeBoard;
            if (board is not { IsOpen: true, HasEmptySlot: true })
            {
                return false;
            }

            return currentDestination is not { Type: DestinationType.NoticeBoard };
        }

        private static NPCDestination PickWeightedRandom(List<NPCDestination> candidates)
        {
            return RandomUtils.PickWeighted(candidates, d => d.Weight);
        }
    }
}
