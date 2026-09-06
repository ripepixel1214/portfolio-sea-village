using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    public abstract class SpecialShopRecipePanelBase : UIPanel, IContextualPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private TextMeshProUGUI _headerText;
        [SerializeField] private TMP_Text _actionColumnHeader;

        [Header("Food Item Display")]
        [SerializeField] private Transform _cookEntryContainer;
        [SerializeField] private GameObject _cookEntryPrefab;

        private readonly List<FoodItemCookEntryView> _entryViews = new List<FoodItemCookEntryView>();
        private int _sourceShopId;
        private int _outputCount = 1;
        private string _actionLabel = "요리";
        private string _completionTitle = "요리 완료";

        private static readonly Color InsufficientIngredientColor = new Color32(220, 75, 75, 255);
        private static readonly Color SufficientIngredientColor = new Color32(65, 150, 65, 255);

        protected void InitializeRecipeCraft(
            int sourceShopId,
            int outputCount,
            string header,
            string actionLabel,
            string completionTitle)
        {
            _sourceShopId = sourceShopId;
            _outputCount = Mathf.Max(1, outputCount);
            _actionLabel = string.IsNullOrWhiteSpace(actionLabel) ? "제작" : actionLabel.Trim();
            _completionTitle = string.IsNullOrWhiteSpace(completionTitle) ? "제작 완료" : completionTitle.Trim();

            if (_headerText != null)
                _headerText.text = string.IsNullOrWhiteSpace(header) ? "제작하기" : header.Trim();
            if (_actionColumnHeader != null)
                _actionColumnHeader.text = _actionLabel;

            RebuildCookEntries();
            RefreshCookPanelState();
        }

        public override void OnClose()
        {
            ClearCookEntries();
            _sourceShopId = 0;
            if (_cookEntryContainer != null)
                _cookEntryContainer.gameObject.SetActive(true);
            base.OnClose();
        }

        protected override void AddListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);

            RefreshCookEntryViews();
        }

        protected override void RemoveListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);
        }

        private void OnEntryCookButtonClicked(int outputItemId)
        {
            if (!SpecialShopAccessPolicy.CanUseCurrentTown(SpecialShopFeature.SpecialContent))
            {
                UIManager.Instance?.ShowAlertMessage($"호감도 {SpecialShopAccessPolicy.GetRequiredAffinity(SpecialShopFeature.SpecialContent)} 이상 필요");
                RefreshCookPanelState();
                return;
            }

            if (!TryCookItem(outputItemId, _outputCount, out string message, out int cookedItemId))
            {
                UIManager.Instance?.ShowAlertMessage(message);
                RefreshCookPanelState();
                return;
            }

            UIManager.Instance?.ShowAlertMessage($"{GetItemDisplayName(cookedItemId)}을(를) 획득했다", null, _completionTitle);
            RefreshCookPanelState();
        }

        private void RefreshCookPanelState()
        {
            RefreshCookEntryViews();
            RefreshRecipeNavigation();
            RefreshEmptyState();
        }

        public static bool TryCookItem(int outputItemId, int cookOutputCount, out string message, out int cookedItemId)
        {
            RecipeCraftProcessor.CraftResult result = RecipeCraftProcessor.TryCraftItem(outputItemId, cookOutputCount);
            message = result.Message;
            cookedItemId = result.OutputItemId;
            return result.Success;
        }

        private static bool HasAllIngredients(InventoryData inventory, List<RecipeData> recipe)
        {
            return RecipeCraftProcessor.HasAllIngredients(inventory, recipe);
        }

        private static List<RecipeData> GetValidRecipeForItem(int outputItemId)
        {
            return RecipeCraftProcessor.GetValidRecipeForItem(outputItemId);
        }

        private static string GetItemDisplayName(int itemId)
        {
            return RecipeCraftProcessor.GetItemDisplayName(itemId);
        }

        private void RebuildCookEntries()
        {
            ClearCookEntries();

            if (_cookEntryContainer != null)
                _cookEntryContainer.gameObject.SetActive(true);

            if (_sourceShopId <= 0)
                return;

            if (_cookEntryContainer == null || _cookEntryPrefab == null)
            {
                Debug.LogWarning("[CookPanel] cookEntryContainer 또는 cookEntryPrefab이 설정되지 않았습니다.");
                return;
            }

            List<int> cookableItemIds = GetCookableItemIdsForShop(_sourceShopId);
            for (int i = 0; i < cookableItemIds.Count; i++)
            {
                int outputItemId = cookableItemIds[i];
                GameObject entryObject = Instantiate(_cookEntryPrefab, _cookEntryContainer);
                entryObject.name = $"Food Item ({outputItemId})";

                FoodItemCookEntryView entryView = entryObject.GetComponent<FoodItemCookEntryView>();
                if (entryView == null)
                {
                    Debug.LogWarning($"[CookPanel] FoodItemCookEntryView가 프리팹에 없습니다. itemId={outputItemId}");
                    Destroy(entryObject);
                    continue;
                }

                entryView.OutputItemId = outputItemId;
                if (entryView.CookButton != null)
                {
                    int capturedItemId = outputItemId;
                    TMP_Text actionText = entryView.CookButton.GetComponentInChildren<TMP_Text>(true);
                    if (actionText != null)
                        actionText.text = _actionLabel;
                    entryView.CookButton.onClick.AddListener(() => OnEntryCookButtonClicked(capturedItemId));
                }

                _entryViews.Add(entryView);
            }
        }

        private void ClearCookEntries()
        {
            for (int i = 0; i < _entryViews.Count; i++)
            {
                FoodItemCookEntryView entry = _entryViews[i];
                if (entry != null && entry.CookButton != null)
                    entry.CookButton.onClick.RemoveAllListeners();

                if (entry != null)
                    Destroy(entry.gameObject);
            }

            _entryViews.Clear();
            navigableButtons.Clear();
        }

        private void RefreshCookEntryViews()
        {
            InventoryData playerInventory = InventoryManager.PlayerInventoryOrNull;

            for (int i = 0; i < _entryViews.Count; i++)
            {
                FoodItemCookEntryView entry = _entryViews[i];
                if (entry == null)
                    continue;

                ItemData outputItem = DataManager.Instance?.ItemDatabase?.GetItem(entry.OutputItemId);
                if (outputItem == null)
                {
                    if (entry.CookButton != null)
                        entry.CookButton.interactable = false;
                    continue;
                }

                if (entry.ResultImage != null)
                {
                    if (outputItem.Icon != null)
                        entry.ResultImage.sprite = outputItem.Icon;
                    else
                        entry.ResultImage.sprite = UIManager.Instance?.LoadItemIcon(outputItem.Image);
                }

                if (entry.ResultNameText != null)
                    entry.ResultNameText.text = DataManager.Instance?.GetScriptTextKR(outputItem.Name) ?? outputItem.ID.ToString();

                List<RecipeData> recipe = GetValidRecipeForItem(entry.OutputItemId);
                if (recipe == null || recipe.Count == 0)
                {
                    SetRecipeSlotCount(entry, 0);
                    if (entry.CookButton != null)
                        entry.CookButton.interactable = false;
                    continue;
                }

                SetRecipeSlotCount(entry, recipe.Count);
                bool canCook = true;

                for (int recipeIndex = 0; recipeIndex < recipe.Count; recipeIndex++)
                {
                    RecipeData ingredient = recipe[recipeIndex];
                    FoodItemRecipeSlotView slot = GetRecipeSlot(entry, recipeIndex);
                    if (ingredient == null || slot == null)
                    {
                        canCook = false;
                        continue;
                    }

                    ItemData ingredientItem = DataManager.Instance?.ItemDatabase?.GetItem(ingredient.Stuff);
                    if (slot.IngredientIcon != null)
                    {
                        if (ingredientItem != null && ingredientItem.Icon != null)
                            slot.IngredientIcon.sprite = ingredientItem.Icon;
                        else
                            slot.IngredientIcon.sprite = UIManager.Instance?.LoadItemIcon(ingredientItem?.Image);
                    }

                    int ownedCount = playerInventory?.GetItemCount(ingredient.Stuff) ?? 0;
                    bool hasEnough = ownedCount >= ingredient.Count;
                    canCook &= hasEnough;

                    if (slot.RequirementText != null)
                    {
                        slot.RequirementText.text = $"{ownedCount}/{ingredient.Count}";
                        slot.RequirementText.color = hasEnough ? SufficientIngredientColor : InsufficientIngredientColor;
                    }
                }

                if (entry.CookButton != null)
                    entry.CookButton.interactable = canCook;
            }
        }

        private void RefreshRecipeNavigation()
        {
            navigableButtons.Clear();
            if (_closeButton != null)
                navigableButtons.Add(_closeButton);

            for (int i = 0; i < _entryViews.Count; i++)
            {
                Button button = _entryViews[i]?.CookButton;
                if (button != null)
                    navigableButtons.Add(button);
            }

            int defaultIndex = navigableButtons.Count > 1 ? 1 : 0;
            defaultSelectedButtonIndex = defaultIndex;
            currentSelectedButtonIndex = defaultIndex;
            EnsureValidSelection();
        }

        private void RefreshEmptyState()
        {
            bool isEmpty = _entryViews.Count == 0;
            if (_cookEntryContainer != null)
                _cookEntryContainer.gameObject.SetActive(!isEmpty);

            if (_actionColumnHeader != null)
                _actionColumnHeader.text = isEmpty ? $"{_actionLabel} 가능한 상품이 없다" : _actionLabel;
        }

        private void SetRecipeSlotCount(FoodItemCookEntryView entry, int requiredCount)
        {
            if (entry == null || entry.RecipeItemsContainer == null || entry.RecipeItemTemplate == null)
                return;

            int currentCount = entry.RecipeItemsContainer.childCount;
            while (currentCount < requiredCount)
            {
                RectTransform created = Instantiate(entry.RecipeItemTemplate, entry.RecipeItemsContainer);
                FoodItemRecipeSlotView slotView = created.GetComponent<FoodItemRecipeSlotView>();
                if (slotView == null)
                {
                    Debug.LogWarning("[CookPanel] FoodItemRecipeSlotView가 Recipe Item 프리팹에 없습니다.");
                    Destroy(created.gameObject);
                    break;
                }

                created.gameObject.SetActive(true);
                currentCount++;
            }

            for (int i = 0; i < entry.RecipeItemsContainer.childCount; i++)
            {
                Transform child = entry.RecipeItemsContainer.GetChild(i);
                if (child != null)
                    child.gameObject.SetActive(i < requiredCount);
            }
        }

        private FoodItemRecipeSlotView GetRecipeSlot(FoodItemCookEntryView entry, int index)
        {
            if (entry == null || entry.RecipeItemsContainer == null)
                return null;

            if (index < 0 || index >= entry.RecipeItemsContainer.childCount)
                return null;

            return entry.RecipeItemsContainer.GetChild(index).GetComponent<FoodItemRecipeSlotView>();
        }

        private static List<int> GetCookableItemIdsForShop(int shopId)
        {
            return RecipeCraftProcessor.GetCraftableItemIdsForShop(shopId);
        }
    }
}
