using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;

namespace SeaVillage.UI
{
    public class DevilPanel : SpecialShopMenuPanelBase
    {
        [SerializeField] private Button _enhancementButton;

        public override void Initialize(string displayName, int shopId, Sprite shopImage = null)
        {
            base.Initialize(displayName, shopId, shopImage);
            RefreshActionState();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshActionState();
        }

        protected override void AddListeners()
        {
            base.AddListeners();

            if (_enhancementButton != null)
                _enhancementButton.onClick.AddListener(OpenEnhancement);
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_enhancementButton != null)
                _enhancementButton.onClick.RemoveListener(OpenEnhancement);
        }

        private void OpenEnhancement()
        {
            if (!EnsureFeatureAccess(SpecialShopFeature.SpecialContent))
                return;

            EnhancementPanel panel = UIManager.Instance?.OpenPanel<EnhancementPanel>();
            panel?.Initialize();
        }

        private void RefreshActionState()
        {
            RefreshFeatureButton(_enhancementButton, SpecialShopFeature.SpecialContent);
            EnsureValidSelection();
        }
    }
}
