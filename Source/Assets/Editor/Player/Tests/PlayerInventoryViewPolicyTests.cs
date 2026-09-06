using System.Collections.Generic;
using NUnit.Framework;
using SeaVillage.Core;
using SeaVillage.Data;
using SeaVillage.UI;

namespace SeaVillage.Editor.Tests
{
    [TestFixture]
    public sealed class PlayerInventoryViewPolicyTests
    {
        [Test]
        public void ShouldDisplay_HidesOnlySelectedOrigins()
        {
            var hiddenOrigins = new HashSet<TownKey> { TownKey.Forest };
            var forestItem = new ItemData { Town = "Forest" };
            var startItem = new ItemData { Town = "Start" };

            Assert.That(PlayerInventoryViewPolicy.ShouldDisplay(forestItem, hiddenOrigins), Is.False);
            Assert.That(PlayerInventoryViewPolicy.ShouldDisplay(startItem, hiddenOrigins), Is.True);
        }

        [Test]
        public void ShouldDisplay_KeepsUnknownOriginVisible()
        {
            var hiddenOrigins = new HashSet<TownKey> { TownKey.Start };
            var unknownItem = new ItemData { Town = "Event" };

            Assert.That(PlayerInventoryViewPolicy.ShouldDisplay(unknownItem, hiddenOrigins), Is.True);
        }

        [TestCase(100, 70, 70)]
        [TestCase(100, 135, 135)]
        [TestCase(0, 100, 0)]
        public void CalculateMarketRatePercent_ReturnsCurrentPriceRatio(
            int originPrice,
            int currentPrice,
            int expectedRate)
        {
            Assert.That(
                PlayerInventoryViewPolicy.CalculateMarketRatePercent(originPrice, currentPrice),
                Is.EqualTo(expectedRate));
        }
    }
}
