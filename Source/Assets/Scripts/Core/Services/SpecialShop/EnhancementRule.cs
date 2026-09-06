namespace SeaVillage.Core
{
    /// <summary>
    /// 아이템 강화에 필요한 규칙을 정의하는 구조체
    /// </summary>
    public readonly struct EnhancementRule
    {
        public readonly int RequiredCrystalCount;
        public readonly int RequiredGold;
        public readonly int SuccessRatePercent;

        public EnhancementRule(int requiredCrystalCount, int requiredGold, int successRatePercent)
        {
            RequiredCrystalCount = requiredCrystalCount;
            RequiredGold = requiredGold;
            SuccessRatePercent = successRatePercent;
        }
    }
}
