using System;

namespace SeaVillage.Core
{
    /// <summary>
    /// 날짜에 해당하는 교환 비용 후보 순번 계산
    /// </summary>
    public static class DailyExchangePolicy
    {
        public static int SelectOptionIndex(int currentDay, int candidateCount)
        {
            if (candidateCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(candidateCount), "교환 비용 후보가 하나 이상 필요합니다");

            return (Math.Max(1, currentDay) - 1) % candidateCount;
        }
    }
}
