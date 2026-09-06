using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    public abstract class SpecialShopMenuPanelBase : UIPanel
    {
        [SerializeField] protected TextMeshProUGUI _headerText;
        [SerializeField] protected Image _shopImage;
        [SerializeField] protected Button _closeButton;

        private string _displayName;
        private int _shopId;

        protected string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? gameObject.name : _displayName;
        protected int ShopId => _shopId;

        public virtual void Initialize(string displayName, int shopId, Sprite shopImage = null)
        {
            _displayName = string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
            _shopId = shopId;

            if (_headerText != null)
                _headerText.text = _displayName;

            ApplyShopImage(shopImage);
        }

        // 이미지가 없으면 빈 사각형이 남지 않도록 숨긴다
        protected void ApplyShopImage(Sprite shopImage)
        {
            if (_shopImage == null)
                return;

            _shopImage.sprite = shopImage;
            _shopImage.enabled = shopImage != null;
        }

        protected override void AddListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        protected override void RemoveListeners()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);
        }

        protected bool ValidateShopId(string targetName)
        {
            if (_shopId > 0)
                return true;

            UIManager.Instance?.ShowAlertMessage($"[Error] {targetName} ID가 설정되지 않았습니다");
            return false;
        }

        protected void OpenPurchasePanel()
        {
            if (!EnsureFeatureAccess(SpecialShopFeature.GeneralTrading))
                return;

            if (!ValidateShopId(DisplayName))
                return;

            PurchasePanel purchasePanel = UIManager.Instance?.OpenPanel<PurchasePanel>();
            if (purchasePanel == null)
            {
                UIManager.Instance?.ShowAlertMessage("[Error] 구매 창을 열 수 없습니다");
                return;
            }

            purchasePanel.SetShopContext(_shopId, TownKeyUtility.ToStorageKey(GetCurrentTownKey()));
        }

        protected void OpenSellPanel()
        {
            if (!EnsureFeatureAccess(SpecialShopFeature.GeneralTrading))
                return;

            SellPanel sellPanel = UIManager.Instance?.OpenPanel<SellPanel>();
            if (sellPanel != null)
                sellPanel.SetMarketTown(TownKeyUtility.ToStorageKey(GetCurrentTownKey()));
        }

        protected void OpenOfficePanel()
        {
            if (!EnsureFeatureAccess(SpecialShopFeature.SpecialContent))
                return;

            if (!ValidateShopId(DisplayName))
                return;

            OfficePanel officePanel = UIManager.Instance?.OpenPanel<OfficePanel>();
            if (officePanel == null)
            {
                UIManager.Instance?.ShowAlertMessage("[Error] 사무소 패널을 열 수 없습니다");
                return;
            }

            officePanel.Initialize(DisplayName, _shopId);
        }

        protected void OpenEmploymentPanel()
        {
            if (!EnsureFeatureAccess(SpecialShopFeature.PlayerShop))
                return;

            if (!ValidateShopId(DisplayName))
                return;

            if (!PlayerShopManager.HasInstance)
            {
                UIManager.Instance?.ShowAlertMessage("[Error] 내 가게 관리자를 찾을 수 없습니다");
                return;
            }

            PlayerShopStateReadOnly state = PlayerShopManager.Instance.GetState(GetCurrentTownKey());
            if (state == null || !state.IsBuilt)
            {
                UIManager.Instance?.ShowAlertMessage("내 가게를 먼저 구매해야 한다");
                return;
            }

            EmploymentPanel employmentPanel = UIManager.Instance?.OpenPanel<EmploymentPanel>();
            if (employmentPanel == null)
            {
                UIManager.Instance?.ShowAlertMessage("[Error] 직원 고용 패널을 열 수 없습니다");
                return;
            }

            employmentPanel.Initialize(GetCurrentTownKey());
        }

        protected void OpenCraftPanel(string targetName, int cookOutputCount = 1)
        {
            if (!EnsureFeatureAccess(SpecialShopFeature.SpecialContent))
                return;

            if (!ValidateShopId(targetName))
                return;

            CookPanel cookPanel = UIManager.Instance?.OpenPanel<CookPanel>();
            if (cookPanel == null)
            {
                UIManager.Instance?.ShowAlertMessage($"[Error] {targetName} 패널을 열 수 없습니다");
                return;
            }

            cookPanel.Initialize(_shopId, cookOutputCount);
        }

        protected bool CanUseFeature(SpecialShopFeature feature)
        {
            return SpecialShopAccessPolicy.CanUseCurrentTown(feature);
        }

        protected bool EnsureFeatureAccess(SpecialShopFeature feature)
        {
            if (CanUseFeature(feature))
                return true;

            int requiredAffinity = SpecialShopAccessPolicy.GetRequiredAffinity(feature);
            UIManager.Instance?.ShowAlertMessage($"호감도 {requiredAffinity} 이상 필요");
            return false;
        }

        protected void RefreshFeatureButton(Button button, SpecialShopFeature feature)
        {
            SetButtonEnabled(button, CanUseFeature(feature));
        }

        protected void ShowPendingFeature(string featureName)
        {
            UIManager.Instance?.ShowAlertMessage($"[Error] {DisplayName}의 {featureName} 기능은 아직 상세 UI가 필요합니다");
        }

        /// <summary>
        /// 가게 구매/직원 고용 버튼의 활성 상태와 라벨을 갱신하고, 선택이 비활성 버튼에 남지 않도록 보정한다
        /// </summary>
        /// <param name="employmentButton">직원 고용 버튼이 없는 패널은 null을 넘긴다</param>
        protected void RefreshPlayerShopButtons(Button playerShopButton, Button employmentButton)
        {
            RefreshPlayerShopActionButton(playerShopButton);
            RefreshEmploymentButton(employmentButton);
            EnsureValidSelection();
        }

        private void RefreshPlayerShopActionButton(Button button)
        {
            if (button == null)
                return;

            PlayerShopActionMode mode = GetPlayerShopActionMode();
            SetButtonEnabled(button, mode != PlayerShopActionMode.Disabled);

            if (mode != PlayerShopActionMode.Disabled)
                SetButtonText(button, GetPlayerShopActionLabel(mode));
        }

        private void RefreshEmploymentButton(Button button)
        {
            SetButtonEnabled(button, PlayerShopTownProcessor.CanHireStaff(GetCurrentTownKey()));
        }

        protected void OpenPlayerShopAction(Action onCompleted = null)
        {
            if (!EnsureFeatureAccess(SpecialShopFeature.PlayerShop))
            {
                onCompleted?.Invoke();
                return;
            }

            if (!ValidateShopId(DisplayName))
                return;

            PlayerShopActionMode mode = GetPlayerShopActionMode();
            if (mode == PlayerShopActionMode.Disabled)
            {
                UIManager.Instance?.ShowAlertMessage("지금은 진행할 수 없다");
                onCompleted?.Invoke();
                return;
            }

            string actionName = mode == PlayerShopActionMode.Upgrade ? "가게 Lv 2" : "가게 Lv 1";
            string message = $"{actionName} 아이템을 구매하시겠습니까?";

            UIManager.Instance?.ShowConfirmMessage(
                message,
                () => ConfirmPlayerShopAction(mode, actionName, onCompleted),
                null,
                "구매 확인");
        }

        protected static void SetButtonText(Button button, string label)
        {
            if (button == null)
                return;

            TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                tmpText.text = label;
                return;
            }

            Text legacyText = button.GetComponentInChildren<Text>(true);
            if (legacyText != null)
                legacyText.text = label;
        }

        private void ConfirmPlayerShopAction(
            PlayerShopActionMode mode,
            string actionName,
            Action onCompleted)
        {
            bool success = PlayerShopTownProcessor.TryExecute(
                mode,
                GetCurrentTownKey(),
                out string failReason);

            if (!success)
            {
                Debug.LogWarning($"[{GetType().Name}] {actionName} 아이템 구매 실패: {failReason}");
                ShowMessageAfterConfirmClose(failReason, null);
                onCompleted?.Invoke();
                return;
            }

            // 구매·업그레이드 완료 팝업에서 확인 시 열린 패널 모두 닫기
            ShowMessageAfterConfirmClose($"{actionName} 아이템을 손에 넣었다", () => UIManager.Instance?.CloseAllPanels());
            onCompleted?.Invoke();
        }

        // 구매 확인 다이얼로그가 닫힌 뒤 알림 표시(같은 SystemMessagePanel 재사용으로 즉시 닫히는 문제 회피)
        private void ShowMessageAfterConfirmClose(string message, Action onConfirm)
        {
            UIManager manager = UIManager.Instance;
            if (manager == null)
                return;

            void Handler()
            {
                manager.OnPanelClosed -= Handler;
                manager.ShowAlertMessage(message, onConfirm);
            }

            manager.OnPanelClosed += Handler;
        }

        private PlayerShopActionMode GetPlayerShopActionMode()
        {
            return PlayerShopTownProcessor.GetActionMode(GetCurrentTownKey());
        }

        private string GetPlayerShopActionLabel(PlayerShopActionMode mode)
        {
            if (mode == PlayerShopActionMode.Upgrade)
                return "가게 업그레이드";

            // 건설이 끝난 가게는 진행 불가 상태에서도 업그레이드 라벨을 유지한다
            return PlayerShopTownProcessor.IsShopBuilt(GetCurrentTownKey()) ? "가게 업그레이드" : "가게 구매";
        }

        protected static TownKey GetCurrentTownKey()
        {
            return GameManager.HasInstance ? GameManager.Instance.CurrentTownKey : TownKey.Unknown;
        }
    }
}
