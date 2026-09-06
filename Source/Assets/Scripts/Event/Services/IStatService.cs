namespace SeaVillage.Event.Services
{
    /// <summary>
    /// 플레이어 스탯 조회 및 변경 기능을 담당하는 서비스 인터페이스
    /// 이벤트에서 사용하는 골드, 플레이어 능력치, 마을 호감도 접근 계약
    /// </summary>
    public interface IStatService
    {
        /// <summary>
        /// 지정된 스탯 값을 long으로 조회(Gold)
        /// </summary>
        long GetStatLong(string key);

        /// <summary>
        /// 지정된 스탯 값 조회
        /// </summary>
        int GetStat(string key);

        /// <summary>
        /// 지정된 스탯 값 변경
        /// </summary>
        /// <param name="key">스탯 키</param>
        /// <param name="delta">변경량 (음수면 감소)</param>
        void ChangeStat(string key, int delta);
    }
}
