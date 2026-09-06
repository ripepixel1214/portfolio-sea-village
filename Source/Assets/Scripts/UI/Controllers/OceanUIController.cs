using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    // 항해 씬 UI Controller. 항해 HUD(일차) 갱신 담당. 이동·상호작용·패널네비는 Input System(PlayerInput)이 담당
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
