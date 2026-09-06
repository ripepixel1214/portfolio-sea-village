using System;
using System.Collections.Generic;

namespace SeaVillage.Core
{
    public enum MineExchangeReward
    {
        Crystal,
        Mushroom,
        Gem,
        Ring,
    }

    public readonly struct MineExchangeCost
    {
        public readonly int ItemId;
        public readonly int Count;

        public MineExchangeCost(int itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }

    public sealed class MineExchangeDefinition
    {
        private readonly MineExchangeCost[] _costs;

        public MineExchangeReward Reward { get; }
        public int RewardItemId { get; }
        public string RewardDisplayName { get; }
        public int RewardCount { get; }
        public IReadOnlyList<MineExchangeCost> Costs => _costs;

        public MineExchangeDefinition(
            MineExchangeReward reward,
            int rewardItemId,
            string rewardDisplayName,
            int rewardCount,
            params MineExchangeCost[] costs)
        {
            if (rewardItemId <= 0)
                throw new ArgumentOutOfRangeException(nameof(rewardItemId));
            if (string.IsNullOrWhiteSpace(rewardDisplayName))
                throw new ArgumentException("교환 보상 표시명이 필요합니다", nameof(rewardDisplayName));
            if (rewardCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(rewardCount));
            if (costs == null || costs.Length == 0)
                throw new ArgumentException("교환 비용 후보가 하나 이상 필요합니다", nameof(costs));

            Reward = reward;
            RewardItemId = rewardItemId;
            RewardDisplayName = rewardDisplayName;
            RewardCount = rewardCount;
            _costs = costs;
        }
    }

    /// <summary>
    /// MineTown 고정 교환 품목 정의 제공
    /// </summary>
    public static class MineExchangeCatalog
    {
        private static readonly MineExchangeDefinition[] Definitions =
        {
            new(
                MineExchangeReward.Crystal,
                117001,
                "수정",
                1,
                new MineExchangeCost(112007, 3),
                new MineExchangeCost(112008, 1),
                new MineExchangeCost(112013, 2)),
            new(
                MineExchangeReward.Mushroom,
                117002,
                "버섯",
                1,
                new MineExchangeCost(111011, 1),
                new MineExchangeCost(111012, 1),
                new MineExchangeCost(111013, 1)),
            new(
                MineExchangeReward.Gem,
                117003,
                "보석",
                1,
                new MineExchangeCost(116010, 3),
                new MineExchangeCost(116011, 3),
                new MineExchangeCost(116012, 3)),
            new(
                MineExchangeReward.Ring,
                117004,
                "반지",
                1,
                new MineExchangeCost(114015, 1),
                new MineExchangeCost(114016, 1),
                new MineExchangeCost(114017, 1),
                new MineExchangeCost(114018, 1)),
        };

        public static int Count => Definitions.Length;

        public static MineExchangeDefinition Get(MineExchangeReward reward)
        {
            int index = (int)reward;
            if (index < 0 || index >= Definitions.Length)
                throw new ArgumentOutOfRangeException(nameof(reward));

            MineExchangeDefinition definition = Definitions[index];
            if (definition.Reward != reward)
                throw new InvalidOperationException($"교환 품목 순서가 일치하지 않습니다: {reward}");

            return definition;
        }
    }
}
