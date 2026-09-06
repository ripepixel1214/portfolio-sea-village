namespace SeaVillage.Town
{
    /// <summary>
    /// 상점 종업원 명부(연출 전용). 단계별 최대 인원만 규정한다: 1단계 1명, 2단계 2명.
    /// 게임플레이(대기열 처리·수요)에는 영향을 주지 않으며, 추후 기획 확장 여지만 열어둔다.
    /// </summary>
    public class ShopStaffRoster
    {
        private int _assignedCount;

        public int AssignedCount => _assignedCount;

        public static int MaxSlots(ShopTier tier) => tier == ShopTier.Tier2 ? 2 : 1;

        public bool CanAssign(ShopTier tier) => _assignedCount < MaxSlots(tier);

        /// <summary>종업원 1명 배치 시도. 단계 정원 초과면 false.</summary>
        public bool TryAssign(ShopTier tier)
        {
            if (!CanAssign(tier)) return false;
            _assignedCount++;
            return true;
        }

        public void Clear() => _assignedCount = 0;
    }
}
