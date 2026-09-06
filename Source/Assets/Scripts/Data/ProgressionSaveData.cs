using System.Collections.Generic;
using MemoryPack;

namespace SeaVillage.Data
{
    [MemoryPackable]
    public partial class PlayerStatSaveData
    {
        public int strength = 10;
        public int agility = 0;
        public List<int> usedStatItemIds = new List<int>();
    }

    [MemoryPackable]
    public partial class TownProgressSaveData
    {
        public string townKey = string.Empty;
        public int affinity = 0;
        public int purchasedItemCount = 0;
        public int stayedDayCount = 0;
    }

    [MemoryPackable]
    public partial class TownProgressionSaveData
    {
        public List<TownProgressSaveData> towns = new List<TownProgressSaveData>();
        public string harborTownKey = string.Empty;
        public int harborConsecutiveNightCount = 0;
        public int harborLastChargedDay = 0;
    }
}
