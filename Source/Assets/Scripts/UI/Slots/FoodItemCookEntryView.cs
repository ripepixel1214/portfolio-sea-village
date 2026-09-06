using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SeaVillage.UI
{
    /// <summary>
    /// CookPanel의 Food Item 프리팹 루트 바인딩
    /// </summary>
    public class FoodItemCookEntryView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image resultImage;
        [SerializeField] private TextMeshProUGUI resultNameText;
        [SerializeField] private Transform recipeItemsContainer;
        [SerializeField] private RectTransform recipeItemTemplate;
        [SerializeField] private Button cookButton;

        public int OutputItemId { get; set; }

        public Image ResultImage => resultImage;
        public TextMeshProUGUI ResultNameText => resultNameText;
        public Transform RecipeItemsContainer => recipeItemsContainer;
        public RectTransform RecipeItemTemplate => recipeItemTemplate;
        public Button CookButton => cookButton;
    }
}
