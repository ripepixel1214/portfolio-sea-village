using SeaVillage.Core;
using Spine.Unity;

namespace SeaVillage.Player
{
    public static class PlayerAppearance
    {
        public static bool TryApply(SkeletonAnimation skeletonAnimation, PlayerGender gender)
        {
            if (skeletonAnimation == null)
                return false;

            if (skeletonAnimation.Skeleton == null)
                skeletonAnimation.Initialize(false);
            if (skeletonAnimation.Skeleton == null)
                return false;

            skeletonAnimation.Skeleton.SetSkin(PlayerGenderPolicy.GetSkinName(gender));
            skeletonAnimation.Skeleton.SetSlotsToSetupPose();
            skeletonAnimation.AnimationState.Apply(skeletonAnimation.Skeleton);
            return true;
        }
    }
}
