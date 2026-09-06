namespace SeaVillage.Core
{
    public static class HarborSettlementProcessor
    {
        public readonly struct Preview
        {
            public Preview(
                int currentDay,
                int nextDay,
                int foodDaysBefore,
                int foodDaysAfter,
                int consecutiveNightCount,
                long harborFee)
            {
                CurrentDay = currentDay;
                NextDay = nextDay;
                FoodDaysBefore = foodDaysBefore;
                FoodDaysAfter = foodDaysAfter;
                ConsecutiveNightCount = consecutiveNightCount;
                HarborFee = harborFee;
            }

            public int CurrentDay { get; }
            public int NextDay { get; }
            public int FoodDaysBefore { get; }
            public int FoodDaysAfter { get; }
            public int ConsecutiveNightCount { get; }
            public long HarborFee { get; }
        }

        private const long FirstNightFee = 100L;
        private const long ConsecutiveNightFee = 1000L;

        public static bool TryGetPreview(out Preview preview)
        {
            preview = default;
            if (!GameManager.HasInstance
                || !TimeManager.HasInstance
                || !InventoryManager.HasInstance
                || !CurrencyManager.HasInstance
                || !TownProgressionManager.HasInstance)
            {
                return false;
            }

            TownProgressionManager progressionManager = TownProgressionManager.Instance;
            TownKey townKey = TownProgressionManager.NormalizeTownKey(GameManager.Instance.CurrentTownKey);
            int currentDay = TimeManager.Instance.CurrentDay;
            if (!progressionManager.IsInitialized
                || !TownProgressionManager.IsSupportedTown(townKey)
                || currentDay <= 0
                || currentDay == int.MaxValue)
            {
                return false;
            }

            int consecutiveNightCount = GetNextConsecutiveNightCount(
                townKey,
                currentDay,
                progressionManager);
            int foodDaysBefore = InventoryManager.Instance.ShipFoodDays;

            preview = new Preview(
                currentDay,
                currentDay + 1,
                foodDaysBefore,
                foodDaysBefore > 0 ? foodDaysBefore - 1 : 0,
                consecutiveNightCount,
                GetFee(consecutiveNightCount));
            return true;
        }

        public static bool TrySettle(out string failureMessage)
        {
            failureMessage = string.Empty;
            if (!TryGetPreview(out Preview preview))
            {
                failureMessage = "[Error] 하루 정산을 진행할 수 없습니다";
                return false;
            }

            InventoryManager inventoryManager = InventoryManager.Instance;
            CurrencyManager currencyManager = CurrencyManager.Instance;

            if (!currencyManager.CanPlayerSpend(CurrencyType.Gold, preview.HarborFee))
            {
                failureMessage = "항구 이용료가 부족합니다";
                return false;
            }

            if (inventoryManager.ShipFoodStorage < InventoryManager.FoodStatPerDay)
            {
                failureMessage = "식량이 부족합니다";
                return false;
            }

            if (!currencyManager.TrySpendPlayer(CurrencyType.Gold, preview.HarborFee))
            {
                failureMessage = "항구 이용료가 부족합니다";
                return false;
            }

            inventoryManager.ConsumeShipFood(InventoryManager.FoodStatPerDay);

            TownKey townKey = TownProgressionManager.NormalizeTownKey(GameManager.Instance.CurrentTownKey);
            TownProgressionManager.Instance.SetHarborState(
                townKey,
                preview.ConsecutiveNightCount,
                preview.NextDay);
            TimeManager.Instance.AdvanceDay(1);
            return true;
        }

        private static long GetFee(int consecutiveNightCount)
        {
            return consecutiveNightCount <= 1
                ? FirstNightFee
                : (consecutiveNightCount - 1L) * ConsecutiveNightFee;
        }

        private static int GetNextConsecutiveNightCount(
            TownKey townKey,
            int currentDay,
            TownProgressionManager progressionManager)
        {
            bool continuesStay = progressionManager.HarborTownKey == townKey
                && progressionManager.HarborLastChargedDay == currentDay
                && progressionManager.HarborConsecutiveNightCount > 0;
            if (!continuesStay)
                return 1;

            return progressionManager.HarborConsecutiveNightCount + 1;
        }
    }
}
