using MemoryPack;
using NUnit.Framework;
using SeaVillage.Core;
using SeaVillage.Data;

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
                Is.EqualTo("Pllayer_Boy/Player_Boy"));
            Assert.That(PlayerGenderPolicy.GetSkinName(PlayerGender.Female),
                Is.EqualTo("Player_Girl/Player_Girl"));
        }
    }
}
