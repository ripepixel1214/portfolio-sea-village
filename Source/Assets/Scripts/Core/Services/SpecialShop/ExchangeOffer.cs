namespace SeaVillage.Core
{
    public readonly struct ExchangeOffer
    {
        public readonly int RequiredItemId;
        public readonly int RequiredCount;
        public readonly int RewardItemId;
        public readonly int RewardCount;

        public ExchangeOffer(int requiredItemId, int requiredCount, int rewardItemId, int rewardCount)
        {
            RequiredItemId = requiredItemId;
            RequiredCount = requiredCount;
            RewardItemId = rewardItemId;
            RewardCount = rewardCount;
        }
    }
}
