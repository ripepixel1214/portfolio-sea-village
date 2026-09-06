using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    /// <summary>
    /// 배 상호작용 패널
    /// </summary>
    public class ShipPanel : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button sailButton;
        [SerializeField] private Button shipInventoryButton;

        public override void OnOpen()
        {
            base.OnOpen();
            TutorialUIBinding.Bind(sailButton, TutorialAnchorKeys.ShipSailMenuButton);
            TutorialUIBinding.Bind(shipInventoryButton, TutorialAnchorKeys.ShipInventoryButton);
        }

        protected override void AddListeners()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (sailButton != null)
                sailButton.onClick.AddListener(OpenSailPanel);

            if (shipInventoryButton != null)
                shipInventoryButton.onClick.AddListener(OpenShipInventoryPanel);
        }

        protected override void RemoveListeners()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);

            if (sailButton != null)
                sailButton.onClick.RemoveListener(OpenSailPanel);

            if (shipInventoryButton != null)
                shipInventoryButton.onClick.RemoveListener(OpenShipInventoryPanel);
        }

        private void OpenSailPanel()
        {
            if (UIManager.Instance.OpenPanel<SailPanel>() != null)
                TutorialEventReporter.Report(TutorialEventType.PanelOpened, TutorialTargetIds.SailPanel, source: TutorialEventSource.UserInterface);
        }

        private void OpenShipInventoryPanel()
        {
            if (UIManager.Instance.OpenPanel<ShipInventoryPanel>() != null)
                TutorialEventReporter.Report(TutorialEventType.PanelOpened, TutorialTargetIds.ShipInventoryPanel, source: TutorialEventSource.UserInterface);
        }
    }
}
