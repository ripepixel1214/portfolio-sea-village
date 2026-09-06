namespace SeaVillage.Core
{
    /// <summary>플레이어 상점 직원 읽기 전용 정보</summary>
    public readonly struct StaffInfo
    {
        public int StaffId { get; }
        public int Intelligence { get; }
        public int Charm { get; }
        public bool IsTownHired { get; }
        public bool IsAssigned => StaffId > 0;

        internal StaffInfo(StaffProfile source)
        {
            StaffId = source?.staffId ?? 0;
            Intelligence = source?.intelligence ?? 0;
            Charm = source?.charm ?? 0;
            IsTownHired = source?.isTownHired ?? false;
        }
    }
}
