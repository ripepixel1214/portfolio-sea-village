using System;
using SeaVillage.Core;
using SeaVillage.Data;
using UnityEngine;

namespace SeaVillage.UI.Tutorial
{
    public enum TutorialRecoveryView
    {
        None, Shop, Purchase, PurchaseInformation, Cart, PurchaseMessage,
        Settlement, Ship, Sail, Inventory, FoodMenu, FoodMessage
    }

    /// <summary>거래를 재실행하지 않고 현재 단계의 클릭 대상 UI를 재구성</summary>
    public static class TutorialUIRecovery
    {
        public static TutorialRecoveryView GetView(string stepId, int progress)
        {
            return stepId switch
            {
                "shop.open_purchase" or "trade.close_shop" => TutorialRecoveryView.Shop,
                "shop.select_potato" => TutorialRecoveryView.Purchase,
                "shop.price_graph" or "trade.add_cart" => TutorialRecoveryView.PurchaseInformation,
                "trade.select_and_graph" => progress == 0 ? TutorialRecoveryView.Purchase : TutorialRecoveryView.PurchaseInformation,
                "trade.purchase" => TutorialRecoveryView.Cart,
                "trade.confirm_purchase" => TutorialRecoveryView.PurchaseMessage,
                "settlement.first_news" or "settlement.costs" => TutorialRecoveryView.Settlement,
                "trade.return_to_shop" => progress == 0 ? TutorialRecoveryView.Settlement
                    : progress == 1 ? TutorialRecoveryView.None : TutorialRecoveryView.Shop,
                "ship.open_sail" or "ship.open_inventory" => TutorialRecoveryView.Ship,
                "ship.food_warning" or "ship.cancel_sail" => TutorialRecoveryView.Sail,
                "ship.select_potato" => TutorialRecoveryView.Inventory,
                "ship.recharge_food" => TutorialRecoveryView.FoodMenu,
                "ship.confirm_recharge" => TutorialRecoveryView.FoodMessage,
                "ship.start_sailing" => progress == 0 ? TutorialRecoveryView.Inventory
                    : progress == 1 ? TutorialRecoveryView.Ship : TutorialRecoveryView.Sail,
                "town.intro" or "town.overview" or "shop.arrival" or "settlement.open"
                    or "ship.depart_guide" or "ship.arrival" or "ocean.move" or "ocean.date"
                    or "ocean.food" or "town.return" or "tutorial.reward"
                    or TutorialDefinitionCatalog.FirstWreckStepId => TutorialRecoveryView.None,
                _ => throw new ArgumentException($"복원할 튜토리얼 UI 정의가 없습니다: {stepId}")
            };
        }

        public static bool TryRestore(string stepId, int progress, out string failReason)
        {
            failReason = string.Empty;
            try
            {
                TutorialRecoveryView view = GetView(stepId, progress);
                if (view == TutorialRecoveryView.None)
                    return true;
                if (!UIManager.HasInstance || !DataManager.HasInstance)
                    throw new InvalidOperationException("UI 또는 데이터 매니저가 준비되지 않았습니다");

                switch (view)
                {
                    case TutorialRecoveryView.Shop:
                        OpenShop();
                        break;
                    case TutorialRecoveryView.Purchase:
                    case TutorialRecoveryView.PurchaseInformation:
                    case TutorialRecoveryView.Cart:
                        OpenShop();
                        PurchasePanel purchase = Open<PurchasePanel>();
                        purchase.SetShopContext(TutorialItemIds.FoodShop, ShopUtility.GetCurrentTownStorageKey());
                        if (view == TutorialRecoveryView.Cart)
                        {
                            // Tutorial_012에서 지정한 감자 한 개의 미결제 장바구니 복원
                            if (purchase.Presenter == null || !purchase.Presenter.SetCartQuantity(TutorialItemIds.Potato, 1))
                                throw new InvalidOperationException("튜토리얼 장바구니를 복원할 수 없습니다");
                        }
                        else if (view == TutorialRecoveryView.PurchaseInformation)
                        {
                            ItemData potato = GetPotato();
                            int quantity = purchase.Presenter.GetOriginalMaxQuantity(potato.ID);
                            Open<PurchaseInformationPanel>().InitializePurchaseInformation(potato, quantity, purchase.MarketTown);
                        }
                        break;
                    case TutorialRecoveryView.PurchaseMessage:
                        OpenShop();
                        ShowConfirmation("구매가 완료되었습니다", TutorialTargetIds.PurchaseMessage);
                        break;
                    case TutorialRecoveryView.Settlement:
                        if (!HarborSettlementProcessor.TryGetPreview(out _))
                            throw new InvalidOperationException("정산 정보가 준비되지 않았습니다");
                        Open<DailySettlementPanel>();
                        break;
                    default:
                        Open<ShipPanel>();
                        if (view == TutorialRecoveryView.Sail)
                            Open<SailPanel>();
                        else if (view != TutorialRecoveryView.Ship)
                        {
                            Open<ShipInventoryPanel>();
                            if (view == TutorialRecoveryView.FoodMenu)
                            {
                                ItemData potato = GetPotato();
                                int quantity = InventoryManager.PlayerInventoryOrNull?.GetItemCount(potato.ID) ?? 0;
                                if (quantity <= 0)
                                    throw new InvalidOperationException("식량 충전에 필요한 감자가 없습니다");
                                Canvas.ForceUpdateCanvases();
                                if (!TutorialUIAnchorRegistry.TryGetHighlight(TutorialAnchorKeys.InventoryPotatoSlot, out TutorialUIAnchor anchor)
                                    || !anchor.TryGetComponent(out BaseSlotUI slot))
                                    throw new InvalidOperationException("감자 선택 슬롯을 찾을 수 없습니다");
                                Open<MenuBarPanel>().InitializeInventoryMenu(potato.ID, MenuBarContext.PlayerInventory,
                                    slot.GetSlotScreenPosition(), potato.Usage == "Food", quantity);
                            }
                            else if (view == TutorialRecoveryView.FoodMessage)
                                ShowConfirmation("식량을 충전했다", TutorialTargetIds.FoodRechargeMessage);
                        }
                        break;
                }
                return true;
            }
            catch (Exception exception)
            {
                failReason = $"튜토리얼 UI 복원 실패 ({stepId}): {exception.Message}";
                return false;
            }
        }

        private static T Open<T>() where T : UIPanel
        {
            T panel = UIManager.Instance.OpenPanel<T>();
            if (panel == null)
                throw new InvalidOperationException($"{typeof(T).Name} 패널을 열 수 없습니다");
            return panel;
        }

        private static void OpenShop()
        {
            foreach (ShopInteractable shop in UnityEngine.Object.FindObjectsByType<ShopInteractable>(FindObjectsSortMode.None))
            {
                if (shop.ShopId != TutorialItemIds.FoodShop)
                    continue;
                shop.Interact();
                ShopPanel panel = UIManager.Instance.GetPanel<ShopPanel>();
                if (panel == null || panel.State != PanelState.Open || panel.GetShopID() != TutorialItemIds.FoodShop)
                    throw new InvalidOperationException("식료품점을 열 수 없습니다");
                return;
            }
            throw new InvalidOperationException("현재 씬에 튜토리얼 식료품점이 없습니다");
        }

        private static ItemData GetPotato()
        {
            return DataManager.Instance.ItemDatabase.GetItem(TutorialItemIds.Potato)
                ?? throw new InvalidOperationException("감자 데이터가 없습니다");
        }

        private static void ShowConfirmation(string message, string targetId)
        {
            Open<SystemMessagePanel>().SetupAlert(message,
                () => TutorialEventReporter.Report(TutorialEventType.UiElementActivated, targetId,
                    source: TutorialEventSource.UserInterface));
        }
    }
}
