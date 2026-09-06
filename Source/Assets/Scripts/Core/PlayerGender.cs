namespace SeaVillage.Core
{
    public enum PlayerGender
    {
        Male = 0,
        Female = 1
    }

    public static class PlayerGenderPolicy
    {
        public const string MaleSkinName = "Pllayer_Boy/Player_Boy";
        public const string FemaleSkinName = "Player_Girl/Player_Girl";

        public static bool IsValid(PlayerGender gender)
        {
            return gender == PlayerGender.Male || gender == PlayerGender.Female;
        }

        public static PlayerGender Normalize(PlayerGender gender)
        {
            return IsValid(gender) ? gender : PlayerGender.Male;
        }

        public static string GetSkinName(PlayerGender gender)
        {
            return Normalize(gender) == PlayerGender.Female
                ? FemaleSkinName
                : MaleSkinName;
        }
    }
}
