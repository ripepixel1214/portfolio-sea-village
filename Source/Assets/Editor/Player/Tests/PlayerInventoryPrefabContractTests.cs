using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NUnit.Framework;
using SeaVillage.Core;

namespace SeaVillage.Editor.Tests
{
    [TestFixture]
    public sealed class PlayerInventoryPrefabContractTests
    {
        private const string PrefabPath =
            "Assets/Resources/Prefabs/UI/Panels/Player Inventory Panel.prefab";

        [Test]
        public void ItemsGridPanel_UsesVerticalScrollViewContract()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            Transform gridPanel = prefab.transform.Find("Window/Items Grid Panel");
            Transform viewport = gridPanel?.Find("Viewport");
            Transform content = viewport?.Find("Items Content");

            Assert.That(gridPanel, Is.Not.Null);
            Assert.That(viewport, Is.Not.Null);
            Assert.That(content, Is.Not.Null);
            Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(content.GetComponent<GridLayoutGroup>(), Is.Not.Null);
            Assert.That(content.GetComponent<ContentSizeFitter>(), Is.Not.Null);

            ScrollRect scrollRect = gridPanel.GetComponent<ScrollRect>();
            Assert.That(scrollRect, Is.Not.Null);
            Assert.That(scrollRect.horizontal, Is.False);
            Assert.That(scrollRect.vertical, Is.True);
            Assert.That(scrollRect.viewport, Is.SameAs(viewport));
            Assert.That(scrollRect.content, Is.SameAs(content));
        }

        [Test]
        public void PlayerInventoryPanel_ReferencesScrollRectContent()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            SeaVillage.UI.PlayerInventoryPanel panel =
                prefab.GetComponent<SeaVillage.UI.PlayerInventoryPanel>();
            SerializedObject serializedPanel = new SerializedObject(panel);

            ScrollRect scrollRect = prefab.transform
                .Find("Window/Items Grid Panel")
                .GetComponent<ScrollRect>();
            Transform content = scrollRect.content;

            Assert.That(
                serializedPanel.FindProperty("itemScrollRect").objectReferenceValue,
                Is.SameAs(scrollRect));
            Assert.That(
                serializedPanel.FindProperty("itemGridContainer").objectReferenceValue,
                Is.SameAs(content));
        }

        [Test]
        public void OnOpen_WithoutInventory_DoesNotCreateDummySlots()
        {
            Assert.That(InventoryManager.HasInstance, Is.False);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = Object.Instantiate(prefab);
            SeaVillage.UI.PlayerInventoryPanel panel =
                instance.GetComponent<SeaVillage.UI.PlayerInventoryPanel>();

            try
            {
                instance.SetActive(true);
                panel.OnOpen();

                Transform content = instance.transform.Find(
                    "Window/Items Grid Panel/Viewport/Items Content");
                Assert.That(content.childCount, Is.Zero);
            }
            finally
            {
                panel.OnClose();
                Object.DestroyImmediate(instance);
            }
        }
    }
}
