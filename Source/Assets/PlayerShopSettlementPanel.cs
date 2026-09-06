using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    public class PlayerShopSettlementPanel : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _expandButton;
        [SerializeField] private Button _gainButton;

        [Header("Texts")]
        [SerializeField] private TMP_Text _headerText;
        [SerializeField] private TMP_Text _elapsedDayText;
        [SerializeField] private TMP_Text _customerCountText;
        [SerializeField] private TMP_Text _revenueText;
        [SerializeField] private TMP_Text _tipText;
        [SerializeField] private TMP_Text _profitText;

        [Header("Sold Items")]
        [SerializeField] private Transform _soldItemContainer;
        [SerializeField] private GameObject _soldItemEntryPrefab;

        private TownKey _townKey = TownKey.Unknown;
        private PlayerShopSalesRecord _settlement;
        private Action _onCollected;
        private bool _expanded;

        public void Initialize(TownKey townKey, Action onCollected = null)
        {
            _townKey = townKey;
            _onCollected = onCollected;
            _expanded = false;
            Refresh();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Refresh();
        }

        protected override void AddListeners()
        {
            base.AddListeners();

            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);

            if (_expandButton != null)
                _expandButton.onClick.AddListener(ToggleExpanded);

            if (_gainButton != null)
                _gainButton.onClick.AddListener(CollectSettlement);
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);

            if (_expandButton != null)
                _expandButton.onClick.RemoveListener(ToggleExpanded);

            if (_gainButton != null)
                _gainButton.onClick.RemoveListener(CollectSettlement);
        }

        public override void OnClose()
        {
            _onCollected = null;
            _settlement = null;
            base.OnClose();
        }

        private void Refresh()
        {
            if (!ValidateReferences())
                return;

            if (_headerText != null)
                _headerText.text = "정산";

            if (_townKey == TownKey.Unknown || !PlayerShopManager.HasInstance)
            {
                SetButtonEnabled(_gainButton, false);
                SetButtonEnabled(_expandButton, false);
                ClearSoldItems();
                return;
            }

            if (!PlayerShopManager.Instance.TryGetSettlementReadOnly(_townKey, out PlayerShopSalesRecord settlement, out string failReason))
            {
                Debug.LogWarning($"[{nameof(PlayerShopSettlementPanel)}] 정산 정보 조회 실패: town={_townKey}, reason={failReason}");
                SetButtonEnabled(_gainButton, false);
                SetButtonEnabled(_expandButton, false);
                ClearSoldItems();
                return;
            }

            _settlement = settlement;
            SetText(_elapsedDayText, $"{settlement.ElapsedDays}일");
            SetText(_customerCountText, $"{settlement.CustomerCount}명");
            SetText(_revenueText, FormatGold(settlement.Revenue));
            SetText(_tipText, FormatGold(settlement.Tip));
            SetText(_profitText, FormatGold(settlement.Profit));

            RefreshSoldItems();
            SetButtonEnabled(_gainButton, true);
            SetButtonEnabled(_expandButton, settlement.SoldItems.Count >= 3);
            EnsureValidSelection();
        }

        private bool ValidateReferences()
        {
            bool isValid = _closeButton != null
                && _expandButton != null
                && _gainButton != null
                && _headerText != null
                && _elapsedDayText != null
                && _customerCountText != null
                && _revenueText != null
                && _tipText != null
                && _profitText != null
                && _soldItemContainer != null
                && _soldItemEntryPrefab != null;

            if (!isValid)
                Debug.LogError($"[{nameof(PlayerShopSettlementPanel)}] 프리팹 직렬화 참조가 누락되었습니다");

            return isValid;
        }

        private void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }

        private string FormatGold(int amount)
        {
            return $"{Mathf.Max(0, amount):N0} G";
        }

        private void RefreshSoldItems()
        {
            ClearSoldItems();
            if (_settlement == null || _soldItemContainer == null || _soldItemEntryPrefab == null)
                return;

            int visibleCount = _expanded ? _settlement.SoldItems.Count : Mathf.Min(3, _settlement.SoldItems.Count);
            for (int i = 0; i < visibleCount; i++)
            {
                PlayerShopItemSalesRecord settlementItem = _settlement.SoldItems[i];
                ItemData itemData = DataManager.HasInstance ? DataManager.Instance.GetItem(settlementItem.ItemId) : null;
                if (itemData == null)
                {
                    Debug.LogWarning($"[{nameof(PlayerShopSettlementPanel)}] 판매 아이템 데이터를 찾을 수 없습니다: itemId={settlementItem.ItemId}");
                    continue;
                }

                GameObject entryObject = Instantiate(_soldItemEntryPrefab, _soldItemContainer);
                entryObject.name = $"Sold Item {settlementItem.ItemId}";
                SettlementItemSlot entry = entryObject.GetComponent<SettlementItemSlot>();
                if (entry == null)
                {
                    Debug.LogError($"[{nameof(PlayerShopSettlementPanel)}] 판매 아이템 프리팹에 SettlementItemSlot이 없습니다");
                    Destroy(entryObject);
                    continue;
                }

                entry.Initialize(itemData, settlementItem.SoldQuantity);
                SetButtonEnabled(entry.GetButton(), false);
            }
        }

        private void ClearSoldItems()
        {
            if (_soldItemContainer == null)
                return;

            for (int i = _soldItemContainer.childCount - 1; i >= 0; i--)
                Destroy(_soldItemContainer.GetChild(i).gameObject);
        }

        private void ToggleExpanded()
        {
            if (_settlement == null || _settlement.SoldItems.Count < 3)
                return;

            _expanded = !_expanded;
            RefreshSoldItems();
        }

        private void CollectSettlement()
        {
            if (_townKey == TownKey.Unknown || !PlayerShopManager.HasInstance)
                return;

            if (!PlayerShopManager.Instance.TryCollectSettlement(_townKey, out int collectedGold, out string failReason))
            {
                Debug.LogWarning($"[{nameof(PlayerShopSettlementPanel)}] 정산 획득 실패: town={_townKey}, reason={failReason}");
                UIManager.Instance.ShowAlertMessage(failReason);
                return;
            }

            Action callback = _onCollected;
            _onCollected = null;
            if (!UIManager.HasInstance)
                return;

            UIManager manager = UIManager.Instance;
            Action onPanelClosed = null;
            onPanelClosed = () =>
            {
                manager.OnPanelClosed -= onPanelClosed;
                manager.ShowAlertMessage($"{collectedGold:N0}G를 획득했다!", callback);
            };
            manager.OnPanelClosed += onPanelClosed;
            manager.CloseCurrentPanel();
        }
    }
}
