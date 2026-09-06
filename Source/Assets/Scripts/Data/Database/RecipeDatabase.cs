using UnityEngine;
using System.Collections.Generic;

namespace SeaVillage.Data
{
    [CreateAssetMenu(fileName = "RecipeDatabase", menuName = "SeaVillage/Data/Recipe Database")]
    public class RecipeDatabase : ScriptableObject
    {
        [SerializeField] private List<RecipeData> recipes = new List<RecipeData>();
        
        public List<RecipeData> Recipes => recipes;
        
        public List<RecipeData> GetRecipe(int id)
        {
            return recipes.FindAll(recipe => recipe.ID == id);
        }
        
        public List<RecipeData> GetRecipesByStuff(int stuff)
        {
            return recipes.FindAll(recipe => recipe.Stuff == stuff);
        }
        
        public void SetRecipes(List<RecipeData> newRecipes)
        {
            recipes = newRecipes;
        }
        
        public void AddRecipe(RecipeData recipe)
        {
            recipes.Add(recipe);
        }
        
        public void ClearRecipes()
        {
            recipes.Clear();
        }
    }
}