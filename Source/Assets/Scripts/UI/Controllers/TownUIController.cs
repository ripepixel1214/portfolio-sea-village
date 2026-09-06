using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    /// <summary>
    /// 마을 씬 UI Controller
    /// </summary>
    public class TownUIController : BaseUIController
    {
        [Header("Town UI Texts")]
        [SerializeField] protected TextMeshProUGUI townNameText;
        [SerializeField] protected TextMeshProUGUI dayText;
        [SerializeField] protected TextMeshProUGUI goldText;

        [Header("Town Favorability")]
        [SerializeField] protected FavorabilityDisplay favorabilityDisplay;

        [Header("Town UI Buttons")]
        [SerializeField] protected Button playerInfoButton;
        [SerializeField] protected Button inventoryButton;
        [SerializeField] protected Button newspaperButton;
        [SerializeField] protected Button dayButton;
        [SerializeField] protected Button changeInfoTypeButton;
        [SerializeField] protected Sprite changeInfoTypeButtonSprite;
        [SerializeField] protected Sprite toolbarButtonExpandedSprite;

        private bool _isToolbarPanelExpanded;
        private Sprite _toolbarButtonDefaultSprite;
        private Animator _toolbarButtonAnimator;

        protected PlayerInventoryPanel inventoryPanel => uiManager.GetPanel<PlayerInventoryPanel>();
        protected PurchasePanel purchasePanel => uiManager.GetPanel<PurchasePanel>();

        private CurrencyManager _currencyManager;
        private GameManager _gameManager;
        private TownProgressionManager _progressionManager;

        private void OnCurrentTownChanged(TownKey townKey) => RefreshTownContext(townKey);

        private void OnEnable()
        {
            SubscribeProgression();
        }

        private void OnDisable()
        {
            UnsubscribeProgression();
        }

        protected override void InitializeUIElements()
        {
            base.InitializeUIElements();
            UpdateDayText(TimeManager.Instance.CurrentDay);
            if (_currencyManager != null)
                UpdateGoldText(CurrencyType.Gold, _currencyManager.GetPlayerBalance(CurrencyType.Gold));

            // 씬 활성화 시점에 갱신된 현재 마을 값으로 초기 동기화
            RefreshTownContext(GameManager.Instance.CurrentTownKey);
        }

        protected override void AttachClickSfxToButtons()
        {
            base.AttachClickSfxToButtons();
            AttachClickSfx(changeInfoTypeButton, inventoryButton, newspaperButton, dayButton);
        }

        protected override void BindTutorialAnchors()
        {
            base.BindTutorialAnchors();
            TutorialUIBinding.Bind(dayButton, TutorialAnchorKeys.DateButton);
        }

        protected override void FindBaseUIElements()
        {
            base.FindBaseUIElements();

            if (townNameText == null)
                townNameText = GameObject.Find("Main Canvas/Town Name Text")?.GetComponent<TextMeshProUGUI>();

            if (dayText == null)
                dayText = GameObject.Find("Main Canvas/Day Text")?.GetComponent<TextMeshProUGUI>();

            if (goldText == null)
                goldText = GameObject.Find("Main Canvas/Player Profile/Gold Text")?.GetComponent<TextMeshProUGUI>();

            if (inventoryButton == null)
                inventoryButton = GameObject.Find("Main Canvas/Toolbar Button/Toolbar Panel/Inventory Button")?.GetComponent<Button>();

            if (newspaperButton == null)
                newspaperButton = GameObject.Find("Main Canvas/Toolbar Button/Toolbar Panel/Newspaper Button")?.GetComponent<Button>();

            if (dayButton == null)
            {
                Transform dayButtonTransform = transform.Find("Main Canvas/Day Progress Button");
                if (dayButtonTransform != null)
                    dayButton = dayButtonTransform.GetComponent<Button>();
            }

            if (changeInfoTypeButton == null)
                changeInfoTypeButton = GameObject.Find("Main Canvas/Toolbar Button")?.GetComponent<Button>();

            if (favorabilityDisplay == null)
                Debug.LogError("[TownUIController] FavorabilityDisplay 참조가 없습니다", this);
        }

        /// <summary>
        /// 현재 마을에 의존하는 UI를 일괄 갱신
        /// </summary>
        private void RefreshTownContext(TownKey townKey)
        {
            UpdateTownNameText(townKey);
            RefreshFavorability(townKey);
        }

        // 마을 표시명으로 이름 텍스트 갱신(내부 town키를 그대로 노출하지 않음)
        private void UpdateTownNameText(TownKey townKey)
        {
            if (townNameText != null)
                townNameText.text = Data.TownDisplayNames.GetTownDisplayName(townKey);
        }

        // 현재 마을 호감도 표시 갱신
        private void RefreshFavorability(TownKey townKey)
        {
            if (favorabilityDisplay == null)
                return;

            if (townKey == TownKey.Unknown)
            {
                Debug.LogWarning("[Error] 현재 마을 정보를 확인할 수 없어 호감도 표시를 갱신하지 못했습니다");
                favorabilityDisplay.SetLevel(0);
                return;
            }

            int affinity = TownProgressionManager.HasInstance
                ? TownProgressionManager.Instance.GetAffinity(townKey)
                : 0;
            favorabilityDisplay.SetLevel(affinity);
        }

        #region Town Specific Events
        protected override void RegisterSceneSpecificEvents()
        {
            RegisterTownEvents();
        }

        /// <summary>
        /// 마을 전용 이벤트 등록
        /// </summary>
        protected virtual void RegisterTownEvents()
        {
            TimeManager.OnDayChanged += UpdateDayText;

            _currencyManager = CurrencyManager.Instance;
            if (_currencyManager != null)
                _currencyManager.OnPlayerGoldChanged += UpdatePlayerGoldText;

            if (playerInfoButton != null)
                RegisterButton(playerInfoButton, () => OnPlayerInfoButtonClicked(), "PlayerInfo");

            if (inventoryButton != null)
                RegisterButton(inventoryButton, () => OpenPlayerInventory(), "Inventory");

            if (newspaperButton != null)
                RegisterButton(newspaperButton, () => OpenNewspaper(), "Newspaper");

            if (dayButton != null)
                RegisterButton(dayButton, () => OpenDailySettlement(), "DaySettlement");
            else
                Debug.LogError("[TownUIController] Day Progress Button 참조가 없습니다");

            if (changeInfoTypeButton != null)
            {
                CacheToolbarButtonState();
                RegisterButton(changeInfoTypeButton, () => OnToolbarButtonClicked(), "ToolbarOrInfoToggle");
                RefreshToolbarButtonState();
            }

            if (uiManager != null)
            {
                uiManager.OnPanelOpened += RefreshToolbarButtonState;
                uiManager.OnPanelClosed += RefreshToolbarButtonState;
                uiManager.OnAllPanelsClosed += RefreshToolbarButtonState;
            }

            // 현재 마을이 바뀔 때(씬 진입·하위 마을 전환) 호감도 갱신
            _gameManager = GameManager.Instance;
            if (_gameManager != null)
                _gameManager.OnCurrentTownChanged += OnCurrentTownChanged;

            SubscribeProgression();
        }

        private void HandleAffinityChanged(TownKey townKey, int affinity)
        {
            if (_gameManager == null)
                return;

            TownKey currentTownKey = TownProgressionManager.NormalizeTownKey(_gameManager.CurrentTownKey);
            if (townKey == currentTownKey)
                favorabilityDisplay?.SetLevel(affinity);
        }

        private void UpdateDayText(int newDay)
        {
            if (dayText != null)
                dayText.text = $"Day {newDay}";
            else
                Debug.LogWarning("TownUIController: dayText is not assigned.");
        }

        private void UpdateGoldText(CurrencyType currency, long newBalance)
        {
            if (currency != CurrencyType.Gold)
                return;

            if (goldText != null)
                goldText.text = $"{newBalance} G";
            else
                Debug.LogWarning("TownUIController: goldText is not assigned.");
        }

        private void UpdatePlayerGoldText(long newBalance)
        {
            UpdateGoldText(CurrencyType.Gold, newBalance);
        }

        private void OnDestroy()
        {
            UnsubscribeProgression();
            TimeManager.OnDayChanged -= UpdateDayText;

            if (_currencyManager != null)
            {
                _currencyManager.OnPlayerGoldChanged -= UpdatePlayerGoldText;
                _currencyManager = null;
            }

            if (uiManager != null)
            {
                uiManager.OnPanelOpened -= RefreshToolbarButtonState;
                uiManager.OnPanelClosed -= RefreshToolbarButtonState;
                uiManager.OnAllPanelsClosed -= RefreshToolbarButtonState;
            }

            if (_gameManager != null)
            {
                _gameManager.OnCurrentTownChanged -= OnCurrentTownChanged;
                _gameManager = null;
            }
        }

        private void SubscribeProgression()
        {
            UnsubscribeProgression();
            if (!TownProgressionManager.HasInstance)
                return;

            _progressionManager = TownProgressionManager.Instance;
            _progressionManager.OnAffinityChanged += HandleAffinityChanged;
        }

        private void UnsubscribeProgression()
        {
            if (_progressionManager == null)
                return;

            _progressionManager.OnAffinityChanged -= HandleAffinityChanged;
            _progressionManager = null;
        }

        /// <summary>
        /// 플레이어 정보 버튼 클릭 처리
        /// </summary>
        protected virtual void OnPlayerInfoButtonClicked()
        {
            OpenPanel<PlayerInfoPanel>();
        }

        /// <summary>
        /// 인벤토리 버튼 클릭 처리
        /// </summary>
        protected virtual void OpenPlayerInventory()
        {
            uiManager.OpenPanel<PlayerInventoryPanel>();
        }

        /// <summary>
        /// 신문 버튼 클릭 처리
        /// </summary>
        protected virtual void OpenNewspaper()
        {
            uiManager.OpenPanel<NewsPanel>();
        }

        /// <summary>
        /// 날짜(Day) 버튼 클릭 처리 - 하루 정산 패널 오픈
        /// </summary>
        protected virtual void OpenDailySettlement()
        {
            DailySettlementPanel panel = uiManager.OpenPanel<DailySettlementPanel>();
            if (panel != null)
                TutorialEventReporter.Report(TutorialEventType.PanelOpened, TutorialTargetIds.SettlementPanel, source: TutorialEventSource.UserInterface);
        }

        /// <summary>
        /// 툴바 버튼 클릭 처리. 현재 열려있는 패널이 정보 토글 패널인지 확인 후 토글 또는 애니메이션 재생
        /// </summary>
        protected virtual void OnToolbarButtonClicked()
        {
            TriggerToolbarButtonAnimator();
        }

        private void CacheToolbarButtonState()
        {
            if (changeInfoTypeButton == null)
                return;

            if (_toolbarButtonDefaultSprite == null && changeInfoTypeButton.image != null)
                _toolbarButtonDefaultSprite = changeInfoTypeButton.image.sprite;

            if (_toolbarButtonAnimator == null)
                _toolbarButtonAnimator = changeInfoTypeButton.GetComponent<Animator>();
        }

        private void RefreshToolbarButtonState()
        {
            if (changeInfoTypeButton == null)
                return;

            CacheToolbarButtonState();

            Sprite targetSprite = GetToolbarButtonTargetSprite();
            if (targetSprite == null)
            {
                Debug.LogWarning("TownUIController: 툴바 버튼 상태에 대응하는 스프라이트가 할당되지 않았습니다.");
                return;
            }

            ApplyToolbarButtonSprite(targetSprite);
        }

        private void TriggerToolbarButtonAnimator()
        {
            _isToolbarPanelExpanded = !_isToolbarPanelExpanded;
            RefreshToolbarButtonState();

            if (_toolbarButtonAnimator != null)
                _toolbarButtonAnimator.SetTrigger("Toggle");
        }

        private Sprite GetToolbarButtonTargetSprite()
        {
            if (_isToolbarPanelExpanded && toolbarButtonExpandedSprite != null)
                return toolbarButtonExpandedSprite;

            return _toolbarButtonDefaultSprite;
        }

        private void ApplyToolbarButtonSprite(Sprite targetSprite)
        {
            if (changeInfoTypeButton == null || changeInfoTypeButton.image == null || targetSprite == null)
                return;

            if (changeInfoTypeButton.image.sprite == targetSprite)
                return;

            Debug.Log($"툴바 버튼 스프라이트를 코드에서 변경합니다. Current Sprite: {changeInfoTypeButton.image.sprite?.name}, Target Sprite: {targetSprite.name}");
            changeInfoTypeButton.image.sprite = targetSprite;
        }

        #endregion

        #region Utilities
        // 아래 public 메서드들은 씬의 UnityEvent(버튼 onClick)로 배선된다 — 코드 호출자가 없어도 살아있는 API.
        public void OpenSailPanel() { OpenPanel<SailPanel>(); }
        public void OpenShipInventoryPanel() { OpenPanel<ShipInventoryPanel>(); }
        public void OpenPurchasePanel() { OpenPanel<PurchasePanel>(); }
        public void OpenSellPanel() { OpenPanel<SellPanel>(); }
        public void OpenQuestPanel() { OpenPanel<QuestPanel>(); }

        public void MoveToVoyageScene()
        {
            Utilities.SceneChanger.Instance.ChangeScene("Ocean");
        }
        #endregion
    }
}
