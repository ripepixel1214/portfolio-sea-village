using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SeaVillage.UI
{
    /// <summary>
    /// CookPanel의 레시피 재료 슬롯 바인딩
    /// </summary>
    public class FoodItemRecipeSlotView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image ingredientIcon;
        [SerializeField] private TextMeshProUGUI requirementText;

        public Image IngredientIcon => ingredientIcon;
        public TextMeshProUGUI RequirementText => requirementText;
    }
}
