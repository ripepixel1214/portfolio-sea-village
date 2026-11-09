using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SeaVillage.Town;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SeaVillage.Editor.Tests
{
    public sealed class PlayerShopNpcContractTests
    {
        [TestCase("StartTown")]
        [TestCase("ForestTown")]
        [TestCase("SeaTown")]
        [TestCase("DessertTown")]
        public void BuiltShop_RegistersWithOwningTown(string sceneName)
        {
            var scene = EditorSceneManager.OpenPreviewScene($"Assets/Scenes/{sceneName}.unity");
            try
            {
                var town = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TownController>(true)).Single();
                var shop = town.GetComponentsInChildren<PlayerShop>(true).Single();
                var manager = new SerializedObject(town)
                    .FindProperty("_shopManager").objectReferenceValue as ShopManager;

                Assert.That(manager, Is.Not.Null);
                Assert.That(shop.GetComponentInParent<ShopManager>(true), Is.SameAs(manager),
                    $"{sceneName}: 플레이어 가게가 마을 상점 관리자의 하위 계층에 있어야 NPC에게 등록됩니다.");

                // Preview scenes do not run MonoBehaviour callbacks; exercise the runtime registration explicitly.
                shop.gameObject.SetActive(true);
                InvokeLifecycle(shop, "Awake");
                InvokeLifecycle(shop, "OnEnable");
                InvokeLifecycle(shop, "OnEnable");
                Assert.That(manager.GetAll().Count(candidate => ReferenceEquals(candidate, shop)), Is.EqualTo(1));
                Assert.That(manager.GetBrowsePoint(ShopType.PlayerShop), Is.EqualTo(shop.BrowsePoint));

                System.Action<int> onQueueChanged = _ => { };
                Assert.That(shop.EnterQueue(onQueueChanged), Is.Zero);
                Assert.That(shop.GetQueuePosition(0), Is.EqualTo(shop.PayPoint));
                shop.ExitQueue(onQueueChanged);

                shop.gameObject.SetActive(false);
                InvokeLifecycle(shop, "OnDisable");
                Assert.That(manager.GetAll().Contains(shop), Is.False);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void InvokeLifecycle(PlayerShop shop, string method)
            => typeof(ShopBase).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(shop, null);
    }
}
