using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.Data;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    /// <summary>
    /// 하루 정산 패널. 넘어가기 전 날짜의 시세 소식을 리포트로 보여주고 '진행'으로 다음 날로 넘어간다
    /// </summary>
    public class DailySettlementPanel : UIPanel
    {
        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI _dateChangeText;
        [SerializeField] private TextMeshProUGUI _foodChangeText;
        [SerializeField] private TextMeshProUGUI _portChargeText;

        [Header("Buttons")]
        [SerializeField] private Button _proceedButton;

        [Header("News")]
        [SerializeField] private Transform _newsContainer;
        [SerializeField] private GameObject _specialNewsPrefab;
        [SerializeField] private GameObject _normalNewsPrefab;

        private int _normalNewsCount;

        #region UIPanel Overrides
        public override void OnOpen()
        {
            base.OnOpen();
            TutorialUIBinding.Bind(_proceedButton, TutorialAnchorKeys.SettlementProceedButton);
            BindSettlementCosts();
            Refresh();
        }

        public override void OnClose()
        {
            ClearNews();
            base.OnClose();
        }

        protected override void AddListeners()
        {
            if (_proceedButton != null)
                _proceedButton.onClick.AddListener(OnProceedClicked);
        }

        protected override void RemoveListeners()
        {
            if (_proceedButton != null)
                _proceedButton.onClick.RemoveListener(OnProceedClicked);
        }
        #endregion

        // 정산 내용 갱신 (넘어가기 전 날짜 기준)
        private void Refresh()
        {
            bool hasPreview = HarborSettlementProcessor.TryGetPreview(
                out HarborSettlementProcessor.Preview preview);
            int currentDay = hasPreview
                ? preview.CurrentDay
                : TimeManager.HasInstance ? TimeManager.Instance.CurrentDay : 1;

            if (_dateChangeText != null)
                _dateChangeText.text = $"날짜 변화: Day {currentDay} ▶ {currentDay + 1}";

            if (_foodChangeText != null)
            {
                _foodChangeText.text = hasPreview
                    ? $"식량 변화: {preview.FoodDaysBefore}일치 ▶ {preview.FoodDaysAfter}일치"
                    : "정산 정보를 불러올 수 없습니다";
            }

            if (_portChargeText != null)
            {
                _portChargeText.text = hasPreview
                    ? $"항구 이용료: <color=#FF0000>-{preview.HarborFee} G</color>"
                    : "항구 이용료: -";
            }

            if (_proceedButton != null)
            {
                _proceedButton.interactable = hasPreview;
                var proceedLabel = _proceedButton.GetComponentInChildren<TextMeshProUGUI>();
                if (proceedLabel != null)
                    proceedLabel.text = $"Day {currentDay + 1} 진행";
            }

            if (hasPreview)
                RefreshNews();
            else
                Debug.LogError("[DailySettlementPanel] 정산 컨텍스트가 유효하지 않습니다");
        }

        private void OnProceedClicked()
        {
            if (!HarborSettlementProcessor.TrySettle(out string failureMessage))
            {
                if (UIManager.HasInstance)
                    UIManager.Instance.ShowAlertMessage(failureMessage);
                return;
            }

            TutorialEventReporter.Report(TutorialEventType.UiElementActivated, TutorialTargetIds.SettlementProceed, source: TutorialEventSource.UserInterface);

            if (UIManager.HasInstance)
                UIManager.Instance.CloseCurrentPanel();
        }

        #region News
        // 넘어가기 전 날짜에 활성화된 특수/일반 효과를 신문 소식으로 표시
        private void RefreshNews()
        {
            ClearNews();

            if (_newsContainer == null || !RuntimeItemPriceManager.HasInstance)
                return;

            var priceManager = RuntimeItemPriceManager.Instance;
            int nextDay = (TimeManager.HasInstance ? TimeManager.Instance.CurrentDay : 1) + 1;
            priceManager.PreparePriceChangesForDay(nextDay, expiresBeforeApplication: true);

            CreateSpecialNewsItem(priceManager.GetPreparedSpecialEffect());

            var activeNormals = priceManager.GetPreparedNormalEffects();
            if (activeNormals != null)
                foreach (var effect in activeNormals)
                    CreateNormalNewsItem(effect.Category, effect.GetFluctuationRate());
        }

        private void CreateSpecialNewsItem(SpecialEffectData effectData)
        {
            if (effectData == null || _specialNewsPrefab == null || _newsContainer == null)
                return;

            var item = Instantiate(_specialNewsPrefab, _newsContainer);
            var component = item.GetComponent<SpecialNewsItem>();
            if (component != null)
                component.SetInformation(TownDisplayNames.GetTownDisplayName(effectData.Town), effectData.Name, effectData.Description);
        }

        private void CreateNormalNewsItem(string category, string fluctuationRate)
        {
            if (_normalNewsPrefab == null || _newsContainer == null)
                return;

            var item = Instantiate(_normalNewsPrefab, _newsContainer);
            var component = item.GetComponent<NormalNewsItem>();
            if (component == null)
                return;

            string displayCategory = DataManager.HasInstance
                ? DataManager.Instance.GetItemTypeDisplayName(category)
                : category;

            component.SetInformation(displayCategory, fluctuationRate);

            if (_normalNewsCount > 0)
            {
                _normalNewsCount++;
                return;
            }

            TutorialSignalPointer pointer = item.GetComponent<TutorialSignalPointer>();
            if (pointer == null)
                pointer = item.AddComponent<TutorialSignalPointer>();
            pointer.Configure(
                TutorialEventType.UiElementActivated,
                TutorialTargetIds.FirstNews,
                TutorialAnchorKeys.SettlementFirstNews);
            _normalNewsCount++;
        }

        private void ClearNews()
        {
            _normalNewsCount = 0;
            if (_newsContainer == null)
                return;

            foreach (Transform child in _newsContainer)
                Destroy(child.gameObject);
        }
        #endregion

        #region Tutorial Binding

        private void BindSettlementCosts()
        {
            if (TutorialUIBinding.BindComposite(
                    _foodChangeText,
                    _portChargeText,
                    TutorialAnchorKeys.SettlementCosts) == null)
            {
                Debug.LogError("[DailySettlementPanel] 식량 변화와 항구 이용료 튜토리얼 Anchor를 연결할 수 없습니다");
                return;
            }

            ConfigureSettlementCostPointer(_foodChangeText);
            ConfigureSettlementCostPointer(_portChargeText);
        }

        private static void ConfigureSettlementCostPointer(TextMeshProUGUI target)
        {
            if (target == null)
                return;

            TutorialSignalPointer pointer = target.GetComponent<TutorialSignalPointer>();
            if (pointer == null)
                pointer = target.gameObject.AddComponent<TutorialSignalPointer>();
            pointer.ConfigureEvent(
                TutorialEventType.UiElementActivated,
                TutorialTargetIds.SettlementCosts);
        }

        #endregion
    }
}
