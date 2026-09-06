using System;
using System.Collections.Generic;
using SeaVillage.Data;
using UnityEngine;

namespace SeaVillage.Core
{
    public static class TutorialDefinitionCatalog
    {
        public const int DefinitionVersion = 2;
        public const string OceanStartStepId = "ship.start_sailing";
        public const string OceanEndStepId = "town.return";
        public const string FirstWreckStepId = "wreck.first_recovery";
        public const string FirstWreckDialogueId = "Wreck_001";
        public const string AnyTownScene = "Town";

        private static readonly TutorialStepDefinition[] Steps =
        {
            new TutorialStepDefinition(
                "town.intro", "Tutorial_001", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("town.intro.greeting", TutorialDialogueType.Stop, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null), new TutorialDialogueDefinition("town.intro.trade", TutorialDialogueType.Stop, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null), new TutorialDialogueDefinition("town.intro.follow", TutorialDialogueType.Stop, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                new TutorialActionType[] { TutorialActionType.BlockMovement, TutorialActionType.BlockAllInteractions, TutorialActionType.EnsureGuide, TutorialActionType.FacePlayerAndGuide }, Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "town.overview", "Tutorial_002", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("town.overview.place", TutorialDialogueType.Auto, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null), new TutorialDialogueDefinition("town.overview.people", TutorialDialogueType.Auto, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                new TutorialActionType[] { TutorialActionType.AllowRightMovement, TutorialActionType.BlockAllInteractions, TutorialActionType.MoveGuideToFoodShop }, Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "shop.arrival", "Tutorial_003", "StartTown", TutorialEntryMode.WaitForEvent,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("shop.arrival.intro", TutorialDialogueType.Stop, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null), new TutorialDialogueDefinition("shop.arrival.interact", TutorialDialogueType.Stop, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null) },
                new TutorialEventPattern(TutorialEventType.AreaReached, "shop.food"), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), new TutorialActionType[] { TutorialActionType.AllowRightMovement, TutorialActionType.BlockAllInteractions, TutorialActionType.MonitorFoodShopArrival }, new TutorialActionType[] { TutorialActionType.BlockMovement, TutorialActionType.FacePlayerAndGuide }),
            new TutorialStepDefinition(
                "shop.open_purchase", "Tutorial_004", "StartTown", TutorialEntryMode.WaitForEvent,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("shop.open_purchase.button", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.PanelOpened, "panel.purchase") }, new string[] { "shop.purchase_button" }, new Vector2(480f, 0f)) },
                new TutorialEventPattern(TutorialEventType.InteractionCompleted, "shop.food"), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), new TutorialActionType[] { TutorialActionType.ResetControls, TutorialActionType.RestrictToShop }, new TutorialActionType[] { TutorialActionType.ResetControls }),
            new TutorialStepDefinition(
                "shop.select_potato", "Tutorial_005", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("shop.select_potato.slot", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.ItemSelected, "shop.item.potato") }, new string[] { "shop.potato_slot" }, new Vector2(-560f, 328f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "shop.price_graph", "Tutorial_006", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("shop.price_graph.explain", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.UiElementActivated, "ui.price_graph") }, new string[] { "shop.price_graph" }, null), new TutorialDialogueDefinition("shop.price_graph.try", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.UiElementActivated, "ui.price_graph") }, new string[] { "shop.price_graph" }, null) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "settlement.open", "Tutorial_007", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("settlement.open.button", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.PanelOpened, "panel.settlement") }, new string[] { "date.button" }, new Vector2(-662f, 297f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                new TutorialActionType[] { TutorialActionType.ClosePanels, TutorialActionType.PrepareFoodPriceChange }, new TutorialActionType[] { TutorialActionType.RestoreFoodPriceChange }, Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "settlement.first_news", "Tutorial_008", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("settlement.first_news.item", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.UiElementActivated, "ui.news.first") }, new string[] { "date.first_news" }, new Vector2(-196f, -57f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), new TutorialActionType[] { TutorialActionType.RestoreFoodPriceChange }, Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "settlement.costs", "Tutorial_009", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("settlement.costs.explanation", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.UiElementActivated, TutorialTargetIds.SettlementCosts) }, new string[] { TutorialAnchorKeys.SettlementCosts }, new Vector2(20f, 70f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), new TutorialActionType[] { TutorialActionType.RestoreFoodPriceChange }, Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "trade.return_to_shop", "Tutorial_010", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("trade.return_to_shop.sequence", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.UiElementActivated, "ui.settlement.proceed"), new TutorialEventPattern(TutorialEventType.InteractionCompleted, "shop.food"), new TutorialEventPattern(TutorialEventType.PanelOpened, "panel.purchase") }, new string[] { "date.proceed_button", "ui.interaction_button", "shop.purchase_button" }, new Vector2(20f, -209f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), new TutorialActionType[] { TutorialActionType.RestoreFoodPriceChangeWithoutPreparation }, Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "trade.select_and_graph", "Tutorial_011", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("trade.select_and_graph.sequence", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.ItemSelected, "shop.item.potato"), new TutorialEventPattern(TutorialEventType.UiElementActivated, "ui.price_graph") }, new string[] { "shop.potato_slot", "shop.price_graph" }, new Vector2(-519f, 330f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "trade.add_cart", "Tutorial_012", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("trade.add_cart.button", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.CartUpdated, "item.potato") }, new string[] { "shop.add_to_cart_button" }, new Vector2(620f, -240f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "trade.purchase", "Tutorial_013", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("trade.purchase.button", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.PurchaseCompleted, "item.potato") }, new string[] { "shop.purchase_confirm_button" }, new Vector2(620f, -240f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "trade.confirm_purchase", "Tutorial_014", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("trade.confirm_purchase.message", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.UiElementActivated, "ui.message.purchase") }, new string[] { "ui.system_message_confirm_button" }, new Vector2(-522f, 223f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "trade.close_shop", "Tutorial_015", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("trade.close_shop.button", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.PanelClosed, "panel.shop") }, new string[] { "shop.close_button" }, new Vector2(-522f, 223f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "ship.depart_guide", "Tutorial_016", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ship.depart_guide.explain", TutorialDialogueType.Auto, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null), new TutorialDialogueDefinition("ship.depart_guide.follow", TutorialDialogueType.Auto, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                new TutorialActionType[] { TutorialActionType.AllowLeftMovement, TutorialActionType.BlockAllInteractions, TutorialActionType.MoveGuideToShip }, Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "ship.arrival", "Tutorial_017", "StartTown", TutorialEntryMode.WaitForEvent,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ship.arrival.interact", TutorialDialogueType.Stop, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null) },
                new TutorialEventPattern(TutorialEventType.AreaReached, "world.ship"), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), new TutorialActionType[] { TutorialActionType.AllowLeftMovement, TutorialActionType.BlockAllInteractions, TutorialActionType.MonitorShipArrival }, new TutorialActionType[] { TutorialActionType.BlockMovement, TutorialActionType.FacePlayerAndGuide }),
            new TutorialStepDefinition(
                "ship.open_sail", "Tutorial_018", "StartTown", TutorialEntryMode.WaitForEvent,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ship.open_sail.button", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.PanelOpened, "panel.sail") }, new string[] { "ship.sail_menu_button" }, new Vector2(-522f, 223f)) },
                new TutorialEventPattern(TutorialEventType.InteractionCompleted, "world.ship"), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), new TutorialActionType[] { TutorialActionType.ResetControls, TutorialActionType.RestrictToShip }, new TutorialActionType[] { TutorialActionType.ResetControls }),
            new TutorialStepDefinition(
                "ship.food_warning", "Tutorial_019", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ship.food_warning.info", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.UiElementActivated, "ui.sail.food_warning") }, new string[] { "ship.food_warning" }, new Vector2(610f, 150f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "ship.cancel_sail", "Tutorial_020", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ship.cancel_sail.button", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.PanelClosed, "panel.sail") }, new string[] { "ship.sail_cancel_button" }, new Vector2(468f, -148f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "ship.open_inventory", "Tutorial_021", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ship.open_inventory.button", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.PanelOpened, "panel.ship_inventory") }, new string[] { "ship.inventory_button" }, new Vector2(500f, -30f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "ship.select_potato", "Tutorial_022", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ship.select_potato.slot", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.ItemSelected, "inventory.item.potato") }, new string[] { "inventory.potato_slot" }, new Vector2(320f, 30f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "ship.recharge_food", "Tutorial_023", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ship.recharge_food.button", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.FoodRechargeCompleted, "item.potato") }, new string[] { "inventory.food_recharge_button" }, new Vector2(-250f, 200f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "ship.confirm_recharge", "Tutorial_024", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ship.confirm_recharge.message", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.UiElementActivated, "ui.message.food_recharge") }, new string[] { "ui.system_message_confirm_button" }, new Vector2(393f, 174f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "ship.start_sailing", "Tutorial_025", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ship.start_sailing.sequence", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.PanelClosed, "panel.ship_inventory"), new TutorialEventPattern(TutorialEventType.PanelOpened, "panel.sail"), new TutorialEventPattern(TutorialEventType.SceneEntered, "Ocean") }, new string[] { "ship.inventory_close_button", "ship.sail_menu_button", "ship.sail_confirm_button" }, new Vector2(-385f, 176f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "ocean.move", "Tutorial_026", "Ocean", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ocean.move.input", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.PlayerMoved, "ocean.player") }, Array.Empty<string>(), new Vector2(-460f, 80f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                new TutorialActionType[] { TutorialActionType.ResetControls }, Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "ocean.date", "Tutorial_027", "Ocean", TutorialEntryMode.WaitForEvent,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ocean.date.indicator", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.UiElementActivated, "ui.ocean.date") }, new string[] { "ocean.date_indicator" }, new Vector2(-460f, 80f)) },
                new TutorialEventPattern(TutorialEventType.DateAdvanced, ""), TutorialSceneMismatchPolicy.Wait,
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), new TutorialActionType[] { TutorialActionType.ResetControls, TutorialActionType.ResumeTime }, new TutorialActionType[] { TutorialActionType.ResetControls, TutorialActionType.PauseTime, TutorialActionType.BlockCommandInput }),
            new TutorialStepDefinition(
                "ocean.food", "Tutorial_028", "Ocean", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("ocean.food.indicator", TutorialDialogueType.Box, new TutorialEventPattern[] { new TutorialEventPattern(TutorialEventType.UiElementActivated, "ui.ocean.food") }, new string[] { "ocean.food_indicator" }, new Vector2(-460f, 80f)) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                new TutorialActionType[] { TutorialActionType.PauseTime, TutorialActionType.BlockCommandInput }, Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "town.return", "Tutorial_029", "StartTown", TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("town.return.success", TutorialDialogueType.Stop, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null), new TutorialDialogueDefinition("town.return.finish", TutorialDialogueType.Stop, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.ChangeToRequiredScene,
                new TutorialActionType[] { TutorialActionType.BlockMovement, TutorialActionType.BlockAllInteractions, TutorialActionType.EnsureGuide, TutorialActionType.FacePlayerAndGuide }, Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                "tutorial.reward", "Tutorial_030", "StartTown", TutorialEntryMode.RewardThenDialogue,
                new TutorialDialogueDefinition[] { new TutorialDialogueDefinition("tutorial.reward.complete", TutorialDialogueType.Stop, Array.Empty<TutorialEventPattern>(), Array.Empty<string>(), null) },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                new TutorialActionType[] { TutorialActionType.BlockMovement, TutorialActionType.BlockAllInteractions, TutorialActionType.EnsureGuide, TutorialActionType.FacePlayerAndGuide }, Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>()),
            new TutorialStepDefinition(
                FirstWreckStepId, FirstWreckDialogueId, AnyTownScene, TutorialEntryMode.Immediate,
                new TutorialDialogueDefinition[]
                {
                    new TutorialDialogueDefinition("wreck.first_recovery.warning", TutorialDialogueType.Stop),
                    new TutorialDialogueDefinition("wreck.first_recovery.help", TutorialDialogueType.Stop),
                    new TutorialDialogueDefinition("wreck.first_recovery.reminder", TutorialDialogueType.Stop)
                },
                new TutorialEventPattern(TutorialEventType.None, ""), TutorialSceneMismatchPolicy.Wait,
                new TutorialActionType[] { TutorialActionType.ClosePanels, TutorialActionType.BlockMovement, TutorialActionType.BlockAllInteractions, TutorialActionType.EnsureGuide, TutorialActionType.FacePlayerAndGuide },
                new TutorialActionType[] { TutorialActionType.ClosePanels, TutorialActionType.BlockMovement, TutorialActionType.BlockAllInteractions, TutorialActionType.EnsureGuide, TutorialActionType.FacePlayerAndGuide },
                Array.Empty<TutorialActionType>(), Array.Empty<TutorialActionType>(), isOrdered: false)
        };

        public static TutorialRepository CreateRepository(IReadOnlyList<TutorialData> dialogues) =>
            new TutorialRepository(Steps, dialogues);
    }
}
