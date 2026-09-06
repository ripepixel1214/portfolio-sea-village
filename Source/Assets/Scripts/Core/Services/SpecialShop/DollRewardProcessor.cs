namespace SeaVillage.Core
{
    public static class DollRewardProcessor
    {
        public static bool TryGrantDoll(int dollItemId, out string failReason)
        {
            if (DollUnlockPolicy.IsClaimed(dollItemId))
            {
                failReason = "이미 획득한 인형이다";
                return false;
            }

            InventoryData inventory = InventoryManager.PlayerInventoryOrNull;
            return InventoryTransaction.TryGrantItem(inventory, dollItemId, 1, out failReason);
        }
    }
}
