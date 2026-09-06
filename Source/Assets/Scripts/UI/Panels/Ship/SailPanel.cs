using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    /// <summary>
    /// 출항 확인 패널 (무게/식량 게이지, 경고, 출항/취소)
    /// </summary>
    public class SailPanel : UIPanel, IContextualPanel
    {
        // 식량 경고 임계값(일치). 잔여 일수가 이 값 이하면 경고 표시
        private const float FoodWarningThreshold = 10f;

        [Header("Inventory Weight Texts")]
        [SerializeField] private TextMeshProUGUI shipInventoryWeigthText;
        [SerializeField] private TextMeshProUGUI playerInventoryWeigthText;
        [SerializeField] private TextMeshProUGUI foodStorageText;

        [Header("Gauges")]
        [SerializeField] private Gauge shipInventoryGauge;
        [SerializeField] private Gauge playerInventoryGauge;
        [SerializeField] private Gauge foodGauge;

        [Header("Food Display")]
        [SerializeField] private GameObject foodWarningObject;
        [SerializeField] private Color normalFoodColor = Color.green;
        [SerializeField] private Color warningFoodColor = Color.red;

        [Header("Buttons")]
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button sailButton;

        [Space(10)]
        public string sailingSceneName = "Ocean";

        protected override void AddListeners()
        {
            if (cancelButton != null)
                cancelButton.onClick.AddListener(CancelSailing);

            if (sailButton != null)
                sailButton.onClick.AddListener(GoToSailScene);

            BindInventoryEvents();
        }

        protected override void RemoveListeners()
        {
            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(CancelSailing);

            if (sailButton != null)
                sailButton.onClick.RemoveListener(GoToSailScene);

            UnbindInventoryEvents();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            TutorialUIBinding.Bind(cancelButton, TutorialAnchorKeys.SailCancelButton);
            TutorialUIBinding.Bind(sailButton, TutorialAnchorKeys.SailConfirmButton);

            if (foodWarningObject != null)
            {
                TutorialSignalPointer pointer = foodWarningObject.GetComponent<TutorialSignalPointer>();
                if (pointer == null)
                    pointer = foodWarningObject.AddComponent<TutorialSignalPointer>();
                pointer.Configure(TutorialEventType.UiElementActivated, TutorialTargetIds.FoodWarning, TutorialAnchorKeys.SailFoodWarning);
            }

            RefreshAllDisplays();
        }

        public override void OnClose()
        {
            base.OnClose();
            UnbindInventoryEvents();
        }

        private void UpdateInventoryWeights()
        {
            var invManager = InventoryManager.Instance;

            if (invManager == null || invManager.ShipInventory == null || invManager.PlayerInventory == null)
                return;

            shipInventoryWeigthText.text =
                $"{invManager.ShipInventory.CurrentWeight:F1} / {invManager.ShipMaxCapacity:F1}kg";

            if (shipInventoryGauge != null)
                shipInventoryGauge.RefreshGauge();

            playerInventoryWeigthText.text =
                $"{invManager.PlayerInventory.CurrentWeight:F1} / {invManager.PlayerInventory.MaxWeight:F1}kg";

            if (playerInventoryGauge != null)
                playerInventoryGauge.RefreshGauge();
        }

        private void UpdateFoodInfo()
        {
            var invManager = InventoryManager.Instance;

            int foodDays = invManager.ShipFoodDays;
            bool isWarning = foodDays <= FoodWarningThreshold;

            if (foodStorageText != null)
            {
                foodStorageText.text = $"{foodDays}일";
                foodStorageText.color = isWarning ? warningFoodColor : normalFoodColor;
            }

            if (foodWarningObject != null)
                foodWarningObject.SetActive(isWarning);

            if (foodGauge != null)
                foodGauge.RefreshGauge();
        }

        private void RefreshAllDisplays()
        {
            UpdateInventoryWeights();
            UpdateFoodInfo();
        }

        private void BindInventoryEvents()
        {
            var invManager = InventoryManager.Instance;
            if (invManager == null)
                return;

            invManager.OnShipInventoryChanged -= RefreshAllDisplays;
            invManager.OnPlayerInventoryChanged -= RefreshAllDisplays;
            invManager.OnShipFoodStorageChanged -= UpdateFoodInfo;

            invManager.OnShipInventoryChanged += RefreshAllDisplays;
            invManager.OnPlayerInventoryChanged += RefreshAllDisplays;
            invManager.OnShipFoodStorageChanged += UpdateFoodInfo;
        }

        private void UnbindInventoryEvents()
        {
            var invManager = InventoryManager.Instance;
            if (invManager == null)
                return;

            invManager.OnShipInventoryChanged -= RefreshAllDisplays;
            invManager.OnPlayerInventoryChanged -= RefreshAllDisplays;
            invManager.OnShipFoodStorageChanged -= UpdateFoodInfo;
        }

        private void GoToSailScene()
        {
            InventoryManager invManager = InventoryManager.Instance;
            if (invManager != null && invManager.ShipFoodStorage <= 0f)
            {
                UIManager.Instance?.ShowAlertMessage("식량이 없어 출항할 수 없다");
                return;
            }

            Utilities.SceneChanger.Instance.ChangeScene(
                sailingSceneName,
                onComplete: () => TutorialEventReporter.Report(TutorialEventType.SceneEntered, sailingSceneName, source: TutorialEventSource.Scene));
        }

        private void CancelSailing()
        {
            Close();
            TutorialEventReporter.Report(TutorialEventType.PanelClosed, TutorialTargetIds.SailPanel, source: TutorialEventSource.UserInterface);
        }
    }
}
