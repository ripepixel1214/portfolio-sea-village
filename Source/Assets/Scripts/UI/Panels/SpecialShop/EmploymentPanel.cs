using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;
using SeaVillage.Data;

namespace SeaVillage.UI
{
    /// <summary>플레이어 가게 직원 고용 패널</summary>
    public class EmploymentPanel : UIPanel, IContextualPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _closeButton;

        [Header("Offers")]
        [SerializeField] private Transform _listContainer;
        [SerializeField] private GameObject _offerEntryPrefab;

        private TownKey _townKey = TownKey.Unknown;
        private readonly List<GameObject> _entries = new List<GameObject>();

        public void Initialize(TownKey townKey)
        {
            _townKey = townKey;
            Refresh();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            Refresh();
        }

        public override void OnClose()
        {
            ClearEntries();
            base.OnClose();
        }

        #region Event Listeners
        protected override void AddListeners()
        {
            base.AddListeners();

            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();

            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);
        }
        #endregion

        private void Refresh()
        {
            ClearEntries();

            if (_listContainer == null || _offerEntryPrefab == null)
                return;

            StaffCatalog catalog = DataManager.HasInstance ? DataManager.Instance.StaffCatalog : null;
            if (catalog == null)
                return;

            int currentLove = GetCurrentTownLoveLevel();
            long currentGold = CurrencyManager.HasInstance
                ? CurrencyManager.Instance.GetPlayerBalance(CurrencyType.Gold)
                : 0L;

            IReadOnlyList<StaffDefinition> definitions = catalog.All;
            for (int i = 0; i < definitions.Count; i++)
            {
                StaffDefinition definition = definitions[i];
                if (definition != null && !definition.IsItemStaff)
                    CreateOffer(definition, currentLove, currentGold);
            }
        }

        private void CreateOffer(StaffDefinition definition, int currentLove, long currentGold)
        {
            int requiredLove = definition.RequiredLoveLevel;
            bool isLoveLocked = currentLove < requiredLove;
            bool isHired = PlayerShopManager.HasInstance && PlayerShopManager.Instance.IsStaffHired(definition.StaffId);
            bool hasEnoughGold = currentGold >= Mathf.Max(0, definition.ContractCost);

            string requirement = isLoveLocked ? $"호감도 {requiredLove} 필요" : (isHired ? "고용 완료" : string.Empty);
            Color requirementColor = isLoveLocked ? Color.red : Color.black;
            bool warnGold = !isLoveLocked && !isHired && !hasEnoughGold;
            bool canHire = !isLoveLocked && !isHired && hasEnoughGold;
            string hireLabel = isHired ? "완료" : "고용";

            GameObject entryObject = Instantiate(_offerEntryPrefab, _listContainer);
            entryObject.SetActive(true);
            _entries.Add(entryObject);

            StaffOfferUI offer = entryObject.GetComponent<StaffOfferUI>();
            if (offer == null)
                return;

            int staffId = definition.StaffId;
            offer.Configure(definition.Sprite, definition.Intelligence, definition.Charm, definition.ContractCost, warnGold,
                requirement, requirementColor, hireLabel, canHire, () => Hire(staffId));
        }

        private void Hire(int staffId)
        {
            if (!PlayerShopManager.HasInstance)
                return;

            StaffCatalog catalog = DataManager.HasInstance ? DataManager.Instance.StaffCatalog : null;
            StaffDefinition definition = catalog != null && catalog.TryGetByStaffId(staffId, out StaffDefinition foundDefinition)
                ? foundDefinition
                : null;
            if (definition == null)
                return;

            var profile = new StaffProfile
            {
                staffId = definition.StaffId,
                intelligence = Mathf.Max(0, definition.Intelligence),
                charm = Mathf.Max(0, definition.Charm),
                isTownHired = true,
            };

            if (!PlayerShopManager.Instance.TryHireStaff(_townKey, profile, Mathf.Max(0, definition.ContractCost), out string failReason))
            {
                UIManager.Instance?.ShowAlertMessage(string.IsNullOrWhiteSpace(failReason) ? "직원을 고용할 수 없다" : failReason);
                Refresh();
                return;
            }

            Refresh();
            UIManager.Instance?.ShowAlertMessage("직원을 고용했다");
        }

        private void ClearEntries()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i] != null)
                    Destroy(_entries[i]);

            _entries.Clear();
        }

        private int GetCurrentTownLoveLevel()
        {
            if (_townKey == TownKey.Unknown)
                return 0;

            return TownProgressionManager.HasInstance
                ? TownProgressionManager.Instance.GetAffinity(_townKey)
                : 0;
        }
    }
}
