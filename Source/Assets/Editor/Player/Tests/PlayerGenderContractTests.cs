using MemoryPack;
using NUnit.Framework;
using SeaVillage.Core;
using SeaVillage.Data;
using UnityEditor;

namespace SeaVillage.Editor.Tests
{
    [TestFixture]
    public sealed class PlayerGenderContractTests
    {
        [Test]
        public void SaveData_RoundTripsSelectedGender()
        {
            var original = new SaveData
            {
                playerGender = PlayerGender.Female
            };

            SaveData restored = MemoryPackSerializer.Deserialize<SaveData>(
                MemoryPackSerializer.Serialize(original));

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.playerGender, Is.EqualTo(PlayerGender.Female));
        }

        [Test]
        public void Policy_NormalizesUnknownValueToMale()
        {
            var unknown = (PlayerGender)99;

            Assert.That(PlayerGenderPolicy.IsValid(unknown), Is.False);
            Assert.That(PlayerGenderPolicy.Normalize(unknown), Is.EqualTo(PlayerGender.Male));
            Assert.That(PlayerGenderPolicy.GetSkinName(unknown), Is.EqualTo(PlayerGenderPolicy.MaleSkinName));
        }

        [Test]
        public void Policy_MapsEachGenderToItsSkin()
        {
            Assert.That(PlayerGenderPolicy.GetSkinName(PlayerGender.Male),
                Is.EqualTo("Player_Boy/Player_Boy"));
            Assert.That(PlayerGenderPolicy.GetSkinName(PlayerGender.Female),
                Is.EqualTo("Player_Girl/Player_Girl"));
        }

        [Test]
        public void StaffCatalog_DefaultCashierIcons_AreDirectlyAssigned()
        {
            const string catalogPath = "Assets/Resources/Data/ScriptableObjects/StaffCatalog.asset";
            const string maleIconPath = "Assets/Resources/Sprites/UI/Player/Player_Boy_Icon.png";
            const string femaleIconPath = "Assets/Resources/Sprites/UI/Player/Player_Girl_Icon.png";

            StaffCatalog catalog = AssetDatabase.LoadAssetAtPath<StaffCatalog>(catalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.MalePlayerIcon, Is.Not.Null);
            Assert.That(catalog.FemalePlayerIcon, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(catalog.MalePlayerIcon), Is.EqualTo(maleIconPath));
            Assert.That(AssetDatabase.GetAssetPath(catalog.FemalePlayerIcon), Is.EqualTo(femaleIconPath));

            Assert.That(catalog.TryGet(0, out StaffDefinition firstMaleStaff), Is.True);
            Assert.That(catalog.TryGet(1, out StaffDefinition firstFemaleStaff), Is.True);
            Assert.That(catalog.TryGet(2, out StaffDefinition secondMaleStaff), Is.True);
            Assert.That(catalog.TryGet(3, out StaffDefinition secondFemaleStaff), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(firstMaleStaff.Sprite), Is.EqualTo(maleIconPath));
            Assert.That(AssetDatabase.GetAssetPath(firstFemaleStaff.Sprite), Is.EqualTo(femaleIconPath));
            Assert.That(AssetDatabase.GetAssetPath(secondMaleStaff.Sprite), Is.EqualTo(maleIconPath));
            Assert.That(AssetDatabase.GetAssetPath(secondFemaleStaff.Sprite), Is.EqualTo(femaleIconPath));
        }
    }
}
