using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    /// <summary>
    /// 아이템 카테고리 칩 목록 표시 뷰
    /// </summary>
    public class ItemCategoryView : MonoBehaviour
    {
        [SerializeField] private GameObject categoryChipPrefab;

        private readonly List<GameObject> spawnedChips = new List<GameObject>();

        public void SetCategoriesWithTown(List<string> categories, string town)
        {
            ClearChips();

            if (categoryChipPrefab == null)
                return;

            HashSet<string> uniqueCategories = new HashSet<string>();

            AddChip(town, uniqueCategories, false);

            if (categories == null || categories.Count == 0)
                return;

            for (int i = 0; i < categories.Count; i++)
                AddChip(categories[i], uniqueCategories, true);
        }

        public void SetCategories(List<string> categories)
        {
            SetCategoriesWithTown(categories, null);
        }

        private void AddChip(string categoryValue, HashSet<string> uniqueCategories, bool useItemTypeMapping)
        {
            string category = categoryValue?.Trim();
            if (string.IsNullOrEmpty(category))
                return;

            if (!uniqueCategories.Add(category))
                return;

            GameObject chipObject = Instantiate(categoryChipPrefab, transform);
            var label = chipObject.GetComponentInChildren<TextMeshProUGUI>(true);

            string displayName = category;
            string iconName = null;

            if (!useItemTypeMapping)
            {
                displayName = Data.TownDisplayNames.GetTownDisplayName(category);
            }
            else
            {
                var dataManager = Data.DataManager.Instance;
                if (dataManager != null)
                {
                    displayName = dataManager.GetItemTypeDisplayName(category);
                    iconName = dataManager.GetItemTypeIconName(category);
                }
            }

            if (label != null)
                label.text = displayName;

            ApplyIconToChip(chipObject, iconName);

            spawnedChips.Add(chipObject);
        }

        private void ApplyIconToChip(GameObject chipObject, string iconName)
        {
            if (chipObject == null || string.IsNullOrWhiteSpace(iconName) || !UIManager.HasInstance)
                return;

            Image targetImage = FindChipIconImage(chipObject);
            if (targetImage == null)
                return;

            Sprite iconSprite = UIManager.Instance.LoadItemIcon(iconName);
            if (iconSprite != null)
            {
                targetImage.sprite = iconSprite;
                targetImage.enabled = true;
            }
        }

        private static Image FindChipIconImage(GameObject chipObject)
        {
            var images = chipObject.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || image.gameObject == chipObject)
                    continue;

                if (image.name.IndexOf("icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return image;
            }

            return null;
        }

        private void ClearChips()
        {
            for (int i = 0; i < spawnedChips.Count; i++)
            {
                if (spawnedChips[i] != null)
                    Destroy(spawnedChips[i]);
            }

            spawnedChips.Clear();
        }
    }
}
