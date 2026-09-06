using System;
using System.Collections.Generic;

namespace SeaVillage.Core
{
    public enum StaffStatType
    {
        Intelligence = 0,
        Charm = 1,
    }

    /// <summary>가게 직원 프로필 정보</summary>
    [Serializable]
    public class StaffProfile
    {
        public int staffId;

        public int intelligence;
        public int charm;

        public bool isTownHired;
        public HashSet<int> usedStatItemIds = new HashSet<int>();

        public bool HasUsedStatItem(int itemId)
        {
            return itemId > 0 && usedStatItemIds != null && usedStatItemIds.Contains(itemId);
        }
    }
}
