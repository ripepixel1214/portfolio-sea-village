namespace SeaVillage.Core
{
    /// <summary>플레이어 가게 직원 역할별 고정 규칙</summary>
    public static class StaffRules
    {
        public const float CashierCalculationTime = 5f;
        public const float SalesThinkingTime = 5f;

        public static int NormalizeIntelligence(int intelligence)
            => NormalizeStat(intelligence);

        public static int NormalizeCharm(int charm)
            => NormalizeStat(charm);

        /// <summary>구매 금액과 지능으로 손님 추가 지불액 계산</summary>
        public static long CalculateIntelligenceBonus(long purchaseAmount, int intelligence)
        {
            int normalizedIntelligence = NormalizeIntelligence(intelligence);
            if (purchaseAmount <= 0 || normalizedIntelligence == 0)
                return 0L;

            if (purchaseAmount > long.MaxValue / normalizedIntelligence)
                return long.MaxValue;

            return purchaseAmount * normalizedIntelligence / 100L;
        }

        public static long CalculateCustomerPayment(long purchaseAmount, int intelligence)
        {
            long bonus = CalculateIntelligenceBonus(purchaseAmount, intelligence);
            return purchaseAmount > long.MaxValue - bonus ? long.MaxValue : purchaseAmount + bonus;
        }

        private static int NormalizeStat(int value)
            => value < 0 ? 0 : value;
    }
}
