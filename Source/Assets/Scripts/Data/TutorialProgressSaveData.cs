using System.Collections.Generic;
using MemoryPack;

namespace SeaVillage.Data
{
    [MemoryPackable]
    public partial class TutorialProgressSaveData
    {
        public int definitionVersion = 0;
        public string activeStepId = "";
        public string activeDialogueKey = "";
        public int playbackState = 0;
        public int conditionProgress = 0;
        public List<string> completedStepIds = new List<string>();
        public List<string> appliedEffectIds = new List<string>();
        public int forcedFoodPriceTargetDay = 0;
        public bool rewardGranted = false;
    }
}
