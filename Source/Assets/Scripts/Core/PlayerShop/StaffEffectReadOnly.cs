namespace SeaVillage.Core
{
    /// <summary>플레이어 가게 직원 효과의 읽기 전용 상태</summary>
    public readonly struct StaffEffectReadOnly
    {
        public static StaffEffectReadOnly Empty => default;

        /// <summary>계산 직원 배치 여부</summary>
        public bool HasCashier { get; }

        /// <summary>계산 직원 지능 수치</summary>
        public int CashierIntelligence { get; }

        /// <summary>계산 직원 기본 계산 시간</summary>
        public float CashierCalculationTime => HasCashier ? StaffRules.CashierCalculationTime : 0f;

        /// <summary>영업 직원 배치 여부</summary>
        public bool HasSalesStaff { get; }

        /// <summary>영업 직원 매력 수치</summary>
        public int SalesCharm { get; }

        /// <summary>영업 직원 기본 고민 시간</summary>
        public float SalesThinkingTime => HasSalesStaff ? StaffRules.SalesThinkingTime : 0f;

        /// <summary>구매 확률에 더할 퍼센트 포인트</summary>
        public int SalesPurchaseProbabilityBonus => HasSalesStaff ? SalesCharm : 0;

        public StaffEffectReadOnly(
            bool hasCashier,
            int cashierIntelligence,
            bool hasSalesStaff,
            int salesCharm)
        {
            HasCashier = hasCashier;
            CashierIntelligence = StaffRules.NormalizeIntelligence(cashierIntelligence);
            HasSalesStaff = hasSalesStaff;
            SalesCharm = StaffRules.NormalizeCharm(salesCharm);
        }
    }
}
