using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    public class NewsPanel : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        [SerializeField] private GameObject newsContainer;
        [SerializeField] private GameObject specialNewsPrefab;
        [SerializeField] private GameObject normalNewsPrefab;

        #region UIPanel Overrides
        public override void OnOpen()
        {
            base.OnOpen();
            LoadAllNewsItems();
        }

        public override void OnClose()
        {
            ClearItems();
            base.OnClose();
        }

        protected override void AddListeners()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        protected override void RemoveListeners()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }
        #endregion

        private void LoadAllNewsItems()
        {
            LoadActiveSpecialEffects();
            LoadActiveNormalEffects();
        }

        private void ClearItems()
        {
            if (newsContainer == null)
            {
                Debug.LogWarning("News Container is not assigned.");
                return;
            }

            foreach (Transform child in newsContainer.transform)
                Destroy(child.gameObject);
        }

        #region Special Effects
        /// <summary>
        /// 현재 활성화된 특수 효과들을 불러옵니다.
        /// </summary>
        private void LoadActiveSpecialEffects()
        {
            if (newsContainer == null)
            {
                Debug.LogWarning("News Container is not assigned.");
                return;
            }

            var priceManager = RuntimeItemPriceManager.Instance;

            var activeEffects = priceManager.GetActiveSpecialEffects();
            
            if (activeEffects == null || activeEffects.Count == 0)
                return;

            foreach (var kvp in activeEffects)
            {
                var effect = kvp.Value;
                var effectData = effect.EffectData;
                
                CreateSpecialEffectNewsItem(effectData);
            }
        }

        private void CreateSpecialEffectNewsItem(SpecialEffectData effectData)
        {
            if (specialNewsPrefab == null || newsContainer == null)
            {
                Debug.LogWarning("Special news prefab or news container is not assigned.");
                return;
            }

            var newsItem = Instantiate(specialNewsPrefab, newsContainer.transform);
            var newsItemComponent = newsItem.GetComponent<SpecialNewsItem>();
            if (newsItemComponent != null)
                newsItemComponent.SetInformation(TownDisplayNames.GetTownDisplayName(effectData.Town), effectData.Name, effectData.Description);
            else
                Debug.LogWarning("SpecialEffectNewsItem component not found on the instantiated prefab.");
        }
        #endregion

        #region Normal Effects
        private void LoadActiveNormalEffects()
        {
            if (newsContainer == null)
            {
                Debug.LogWarning("News Container is not assigned.");
                return;
            }

            var priceManager = RuntimeItemPriceManager.Instance;
            var activeEffects = priceManager.GetActiveNormalEffects();
            
            if (activeEffects == null || activeEffects.Count == 0)
                return;
            
            foreach (var effect in activeEffects)
                CreateNormalNewsItem(effect.Category, effect.GetFluctuationRate());
        }

        private void CreateNormalNewsItem(string category, string fluctuationRate)
        {
            if (normalNewsPrefab == null || newsContainer == null)
            {
                Debug.LogWarning("Normal news prefab or news container is not assigned.");
                return;
            }

            var newsItem = Instantiate(normalNewsPrefab, newsContainer.transform);
            var newsItemComponent = newsItem.GetComponent<NormalNewsItem>();
            if (newsItemComponent != null)
            {
                string displayCategory = DataManager.HasInstance
                    ? DataManager.Instance.GetItemTypeDisplayName(category)
                    : category;

                newsItemComponent.SetInformation(displayCategory, fluctuationRate);
            }
            else
                Debug.LogWarning("NormalNewsItem component not found on the instantiated prefab.");
        }
        #endregion
    }
}
