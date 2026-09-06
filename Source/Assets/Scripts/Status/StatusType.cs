namespace SeaVillage.Status
{
    public enum StatusType
    {
        None = 0,

        // Player & NPC 공통 스탯
        Intelligence,   // 지능
        Charm,          // 매력

        // Player 전용 스탯
        Strength,       // 힘
        Dexterity,      // 민첩

        // NPC 전용 스탯
        Loyalty,        // 충성
        Speed,          // 이동 속도
        BehaviourCount, // 최대 행동 횟수
        Satisfaction,   // 만족도 (기본 100)
        PurchaseRate,   // 구매 확률 (기본 30)
    }
}