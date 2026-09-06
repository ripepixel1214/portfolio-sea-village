using MemoryPack;

namespace SeaVillage.Data
{
    [MemoryPackable]
    public partial class FirstWreckRecoverySaveData
    {
        public bool triggered = false;
        public bool pending = false;
        public bool rewardGranted = false;

        public FirstWreckRecoverySaveData Copy()
        {
            return new FirstWreckRecoverySaveData
            {
                triggered = triggered,
                pending = pending,
                rewardGranted = rewardGranted
            };
        }

        public void Normalize()
        {
            if (rewardGranted)
            {
                triggered = true;
                pending = false;
                return;
            }

            pending = triggered;
        }
    }
}
