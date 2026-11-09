using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.Ocean;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    // 항해 HUD 갱신과 터치 버튼 연결을 담당한다. 이동은 Input System(PlayerInput)을 사용한다.
    public class OceanUIController : BaseUIController
    {
        [Header("Ocean UI Texts")]
        [SerializeField] protected TextMeshProUGUI dayText;
        [SerializeField] protected TextMeshProUGUI goldText;
        [SerializeField] private RectTransform foodIndicator;

        [Header("Ocean UI Buttons")]
        [SerializeField] protected Button? inventoryButton;
        [SerializeField] protected Button? newspaperButton;
        [SerializeField] protected Button? toolbarButton;

        [Header("Ocean References")]
        [SerializeField] private ShipController shipController;

        private CurrencyManager _currencyManager;

        protected override void InitializeUIElements()
        {
            base.InitializeUIElements();
            UpdateDayText(TimeManager.HasInstance ? TimeManager.Instance.CurrentDay : 1);
            if (_currencyManager != null)
                UpdateGoldText(CurrencyType.Gold, _currencyManager.GetPlayerBalance(CurrencyType.Gold));
        }

        protected override void RegisterSceneSpecificEvents()
        {
            RegisterButton(inventoryButton, () => OpenPanel<ShipInventoryPanel>(), "ShipInventory");
            RegisterButton(newspaperButton, () => OpenPanel<NewsPanel>(), "Newspaper");

            // 기존 씬의 Animator UnityEvent 연결이 있으면 그대로 사용한다.
            if (toolbarButton != null && toolbarButton.onClick.GetPersistentEventCount() == 0)
                RegisterButton(toolbarButton, () => toolbarButton.GetComponent<Animator>()?.SetTrigger("Toggle"), "Toolbar");

            TimeManager.OnDayChanged += UpdateDayText;

            _currencyManager = CurrencyManager.Instance;
            if (_currencyManager != null)
                _currencyManager.OnPlayerGoldChanged += UpdatePlayerGoldText;
        }

        protected override void BindTutorialAnchors()
        {
            base.BindTutorialAnchors();

            Transform dateTarget = dayText != null ? dayText.transform.parent : null;
            ConfigurePointer(
                dateTarget,
                TutorialTargetIds.OceanDate,
                TutorialAnchorKeys.OceanDateIndicator);

            if (foodIndicator == null)
            {
                Transform found = transform.Find("Main Canvas/Food Gauge Holder");
                if (found == null)
                    found = GameObject.Find("UI/OceanUIController/Main Canvas/Food Gauge Holder")?.transform;
                foodIndicator = found as RectTransform;
            }

            ConfigurePointer(
                foodIndicator,
                TutorialTargetIds.OceanFood,
                TutorialAnchorKeys.OceanFoodIndicator);
        }

        protected override void AttachClickSfxToButtons()
        {
            base.AttachClickSfxToButtons();
            AttachClickSfx(toolbarButton, inventoryButton, newspaperButton);
        }

        protected override void FindBaseUIElements()
        {
            base.FindBaseUIElements();

            if (shipController == null)
                shipController = FindFirstObjectByType<ShipController>();

            if (dayText == null)
                dayText = GameObject.Find("UI/OceanUIController/Main Canvas/Day Holder/Day Text").GetComponent<TextMeshProUGUI>();

            if (goldText == null)
                goldText = GameObject.Find("UI/OceanUIController/Main Canvas/Player Gold Holder/Background/Gold Text").GetComponent<TextMeshProUGUI>();

            if (inventoryButton == null)
                inventoryButton = GameObject.Find("UI/OceanUIController/Main Canvas/Toolbar Button/Toolbar Panel/Inventory Button")?.GetComponent<Button>();

            if (newspaperButton == null)
                newspaperButton = GameObject.Find("UI/OceanUIController/Main Canvas/Toolbar Button/Toolbar Panel/Newspaper Button")?.GetComponent<Button>();

            if (toolbarButton == null)
                toolbarButton = GameObject.Find("UI/OceanUIController/Main Canvas/Toolbar Button")?.GetComponent<Button>();
        }

        protected override void OnInteractionButtonClicked()
        {
            if (uiManager.IsAnyPanelOpened)
                base.OnInteractionButtonClicked();
            else
                shipController?.TryInteract();
        }

        private void UpdateDayText(int currentDay)
        {
            if (dayText != null)
                dayText.text = $"Day {currentDay}";
        }

        private void UpdateGoldText(CurrencyType currency, long newBalance)
        {
            if (currency != CurrencyType.Gold)
                return;

            if (goldText != null)
                goldText.text = $"{newBalance} G";
        }

        private void UpdatePlayerGoldText(long newBalance)
        {
            UpdateGoldText(CurrencyType.Gold, newBalance);
        }

        private static void ConfigurePointer(Transform target, string targetId, string anchorKey)
        {
            if (target == null)
                return;

            TutorialSignalPointer pointer = target.GetComponent<TutorialSignalPointer>();
            if (pointer == null)
                pointer = target.gameObject.AddComponent<TutorialSignalPointer>();
            pointer.Configure(TutorialEventType.UiElementActivated, targetId, anchorKey);
        }

        private void OnDestroy()
        {
            TimeManager.OnDayChanged -= UpdateDayText;

            if (_currencyManager != null)
            {
                _currencyManager.OnPlayerGoldChanged -= UpdatePlayerGoldText;
                _currencyManager = null;
            }
        }
    }
}
