using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    public class StagePanel : SpecialShopMenuPanelBase
    {
        [SerializeField] private Button _bgmChangeButton;

        public override void OnOpen()
        {
            base.OnOpen();
            SetButtonEnabled(_bgmChangeButton, false);
            SetButtonText(_bgmChangeButton, "BGM 변경 (음원 준비 중)");
            EnsureValidSelection();
        }

        protected override void AddListeners()
        {
            base.AddListeners();

            if (_bgmChangeButton != null)
                _bgmChangeButton.onClick.AddListener(OpenBgmChange);
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_bgmChangeButton != null)
                _bgmChangeButton.onClick.RemoveListener(OpenBgmChange);
        }

        private void OpenBgmChange()
        {
            UIManager.Instance?.ShowAlertMessage("변경할 수 있는 음원이 아직 없다");
        }
    }
}
