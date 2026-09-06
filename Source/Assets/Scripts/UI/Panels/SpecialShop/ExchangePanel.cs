using SeaVillage.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    public class ExchangePanel : UIPanel, IContextualPanel
    {
        [SerializeField] private TextMeshProUGUI _headerText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private ExchangeOfferView[] _offerViews = new ExchangeOfferView[4];

        public void Initialize()
        {
            RefreshState();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshState();
        }

        protected override void AddListeners()
        {
            base.AddListeners();

            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);

            for (int i = 0; i < _offerViews.Length; i++)
            {
                if (_offerViews[i] != null)
                    _offerViews[i].ExchangeRequested += HandleExchangeRequested;
            }

            TimeManager.OnDayChanged += HandleDayChanged;
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);

            for (int i = 0; i < _offerViews.Length; i++)
            {
                if (_offerViews[i] != null)
                    _offerViews[i].ExchangeRequested -= HandleExchangeRequested;
            }

            TimeManager.OnDayChanged -= HandleDayChanged;
        }

        private void RefreshState()
        {
            if (_headerText != null)
                _headerText.text = "교환하기";

            InventoryData inventory = InventoryManager.PlayerInventoryOrNull;
            int currentDay = TimeManager.HasInstance ? TimeManager.Instance.CurrentDay : 1;

            navigableButtons.Clear();
            if (_closeButton != null)
                navigableButtons.Add(_closeButton);

            for (int i = 0; i < _offerViews.Length; i++)
            {
                ExchangeOfferView view = _offerViews[i];
                if (view == null)
                    continue;

                MineExchangeDefinition definition = MineExchangeCatalog.Get(view.Reward);
                int optionIndex = DailyExchangePolicy.SelectOptionIndex(currentDay, definition.Costs.Count);
                MineExchangeCost cost = definition.Costs[optionIndex];
                int ownedCount = inventory?.GetItemCount(cost.ItemId) ?? 0;
                bool canExchange = inventory != null && ownedCount >= cost.Count;

                view.Refresh(optionIndex, cost.Count, definition.RewardCount, canExchange);
                SetButtonEnabled(view.ExchangeButton, canExchange);

                if (view.ExchangeButton != null)
                    navigableButtons.Add(view.ExchangeButton);
            }

            int defaultIndex = navigableButtons.Count > 1 ? 1 : 0;
            defaultSelectedButtonIndex = defaultIndex;
            currentSelectedButtonIndex = defaultIndex;
            EnsureValidSelection();
        }

        private void HandleDayChanged(int currentDay)
        {
            RefreshState();
        }

        private void HandleExchangeRequested(ExchangeOfferView view)
        {
            if (!SpecialShopAccessPolicy.CanUseCurrentTown(SpecialShopFeature.SpecialContent))
            {
                UIManager.Instance?.ShowAlertMessage($"호감도 {SpecialShopAccessPolicy.GetRequiredAffinity(SpecialShopFeature.SpecialContent)} 이상 필요");
                RefreshState();
                return;
            }

            MineExchangeDefinition definition = MineExchangeCatalog.Get(view.Reward);
            int currentDay = TimeManager.HasInstance ? TimeManager.Instance.CurrentDay : 1;
            int optionIndex = DailyExchangePolicy.SelectOptionIndex(currentDay, definition.Costs.Count);
            MineExchangeCost cost = definition.Costs[optionIndex];
            var offer = new ExchangeOffer(
                cost.ItemId,
                cost.Count,
                definition.RewardItemId,
                definition.RewardCount);

            if (!ExchangeProcessor.TryExchange(offer, out string failReason))
            {
                UIManager.Instance.ShowAlertMessage(failReason);
                RefreshState();
                return;
            }

            UIManager.Instance.ShowAlertMessage($"{definition.RewardDisplayName}을(를) 획득했다", null, "교환 완료");
            RefreshState();
        }
    }
}
