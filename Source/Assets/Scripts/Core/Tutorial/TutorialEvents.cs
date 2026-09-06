using System;

namespace SeaVillage.Core
{
    public enum TutorialEventType
    {
        None,
        UiElementActivated,
        InteractionCompleted,
        PanelOpened,
        PanelClosed,
        ItemSelected,
        CartUpdated,
        PurchaseCompleted,
        FoodRechargeCompleted,
        SceneEntered,
        AreaReached,
        DateAdvanced,
        PlayerMoved
    }

    public enum TutorialEventSource
    {
        Unknown,
        UserInterface,
        World,
        Domain,
        Scene,
        Time
    }

    public readonly struct TutorialEvent
    {
        public TutorialEvent(
            TutorialEventType type,
            string targetId = "",
            int amount = 0,
            TutorialEventSource source = TutorialEventSource.Unknown)
        {
            Type = type;
            TargetId = targetId?.Trim() ?? string.Empty;
            Amount = amount;
            Source = source;
        }

        public TutorialEventType Type { get; }
        public string TargetId { get; }
        public int Amount { get; }
        public TutorialEventSource Source { get; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(TargetId) ? Type.ToString() : $"{Type}:{TargetId}";
        }
    }

    public readonly struct TutorialEventPattern
    {
        public TutorialEventPattern(TutorialEventType type, string targetId = "")
        {
            Type = type;
            TargetId = targetId?.Trim() ?? string.Empty;
        }

        public TutorialEventType Type { get; }
        public string TargetId { get; }

        public bool Matches(in TutorialEvent tutorialEvent)
        {
            return Type == tutorialEvent.Type
                && (string.IsNullOrEmpty(TargetId)
                    || string.Equals(TargetId, tutorialEvent.TargetId, StringComparison.Ordinal));
        }
    }

    public static class TutorialTargetIds
    {
        public const string FoodShop = "shop.food";
        public const string Ship = "world.ship";
        public const string PurchasePanel = "panel.purchase";
        public const string SettlementPanel = "panel.settlement";
        public const string SailPanel = "panel.sail";
        public const string ShipInventoryPanel = "panel.ship_inventory";
        public const string ShopPanel = "panel.shop";
        public const string ShopPotato = "shop.item.potato";
        public const string InventoryPotato = "inventory.item.potato";
        public const string Potato = "item.potato";
        public const string PriceGraph = "ui.price_graph";
        public const string FirstNews = "ui.news.first";
        public const string SettlementCosts = "ui.settlement.costs";
        public const string SettlementProceed = "ui.settlement.proceed";
        public const string PurchaseMessage = "ui.message.purchase";
        public const string FoodWarning = "ui.sail.food_warning";
        public const string FoodRechargeMessage = "ui.message.food_recharge";
        public const string OceanPlayer = "ocean.player";
        public const string OceanDate = "ui.ocean.date";
        public const string OceanFood = "ui.ocean.food";
    }

    public static class TutorialEventReporter
    {
        public static bool Report(in TutorialEvent tutorialEvent)
        {
            if (tutorialEvent.Type == TutorialEventType.None
                || !TutorialManager.HasInstance
                || !TutorialManager.Instance.IsInitialized)
            {
                return false;
            }

            if (TutorialManager.Instance.TryReportEvent(
                    tutorialEvent,
                    out TutorialSignalResult result,
                    out string failReason))
            {
                return result != TutorialSignalResult.Ignored;
            }

            UnityEngine.Debug.LogWarning($"[TutorialEventReporter] {failReason}");
            return false;
        }

        public static bool Report(
            TutorialEventType type,
            string targetId = "",
            int amount = 0,
            TutorialEventSource source = TutorialEventSource.Unknown)
        {
            var tutorialEvent = new TutorialEvent(type, targetId, amount, source);
            return Report(tutorialEvent);
        }
    }
}
