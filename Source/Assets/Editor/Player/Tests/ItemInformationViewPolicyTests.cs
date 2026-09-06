using NUnit.Framework;
using SeaVillage.UI;

namespace SeaVillage.Editor.Tests
{
    [TestFixture]
    public sealed class ItemInformationViewPolicyTests
    {
        [TestCase(75, 100, 75)]
        [TestCase(0, 100, 100)]
        [TestCase(0, -1, 0)]
        public void ResolveAveragePurchasePrice_PrefersRecordedInventoryPrice(
            int inventoryAveragePurchasePrice,
            int originPrice,
            int expectedPrice)
        {
            int result = ItemInformationViewPolicy.ResolveAveragePurchasePrice(
                inventoryAveragePurchasePrice,
                originPrice);

            Assert.That(result, Is.EqualTo(expectedPrice));
        }
    }
}
