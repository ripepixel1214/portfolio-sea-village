using System.Collections.Generic;
using SeaVillage.Core;
using SeaVillage.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    /// <summary>직원 배치 변경 패널</summary>
    public sealed class StaffChangePanel : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _registerButton;

        [Header("Candidates")]
        [SerializeField] private Transform _listContainer;
        [SerializeField] private GameObject _candidateEntryPrefab;

        private readonly List<GameObject> _entries = new List<GameObject>();
        private TownKey _townKey = TownKey.Unknown;
        private StaffRole _role;
        private int _statItemId;
        private bool _showRegisterCandidates;

        public override void OnOpen()
        {
            base.OnOpen();
            SubscribeInventoryEvents();
            Refresh();
        }

        public override void OnClose()
        {
            UnsubscribeInventoryEvents();
            _showRegisterCandidates = false;
            _statItemId = 0;
            ClearEntries();
            base.OnClose();
        }

        protected override void AddListeners()
        {
            base.AddListeners();
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
            if (_registerButton != null)
                _registerButton.onClick.AddListener(OnRegisterClicked);
        }

        protected override void RemoveListeners()
        {
            base.RemoveListeners();
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Close);
            if (_registerButton != null)
                _registerButton.onClick.RemoveListener(OnRegisterClicked);
        }

        public void Initialize(TownKey townKey, StaffRole role)
        {
            _townKey = townKey;
            _role = role;
            _statItemId = 0;
            if (_registerButton != null)
                _registerButton.gameObject.SetActive(true);

            Refresh();
        }

        public void InitializeStatItem(int itemId)
        {
            _statItemId = itemId;
            _showRegisterCandidates = false;
            if (_registerButton != null)
                _registerButton.gameObject.SetActive(false);

            Refresh();
        }

        private void OnRegisterClicked()
        {
            StaffRole otherRole = GetOtherRole();
            int otherStaffId = PlayerShopManager.Instance.GetAssignment(_townKey, otherRole)?.StaffId ?? 0;
            if (StaffRegistrationPolicy.GetRegisterableDollStaff(otherStaffId).Count == 0)
            {
                UIManager.Instance?.ShowAlertMessage("등록할 인형이 없다");
                return;
            }

            _showRegisterCandidates = true;
            Refresh();
        }

        private void AssignRole(int staffId)
        {
            if (!PlayerShopManager.Instance.TryAssignStaff(_townKey, _role, staffId, out string failReason))
            {
                UIManager.Instance?.ShowAlertMessage(failReason);
                return;
            }

            Close();
        }

        private void RegisterStaff(int staffId)
        {
            if (!PlayerShopManager.Instance.TryRegisterStaffFromItem(_townKey, staffId, out string failReason))
            {
                UIManager.Instance?.ShowAlertMessage(failReason);
                Refresh();
                return;
            }

            _showRegisterCandidates = false;
            Refresh();
            UIManager.Instance?.ShowAlertMessage("직원을 등록했다");
        }

        private void UseStatItem(int staffId)
        {
            if (!ItemUseService.TryUseOnStaff(_statItemId, staffId, out string resultMessage))
            {
                UIManager.Instance?.ShowAlertMessage(resultMessage);
                return;
            }

            UIManager manager = UIManager.Instance;
            if (manager == null)
                return;

            void HandlePanelClosed()
            {
                manager.OnPanelClosed -= HandlePanelClosed;
                manager.ShowAlertMessage(resultMessage);
            }

            manager.OnPanelClosed += HandlePanelClosed;
            Close();
        }

        private void HandlePlayerInventoryChanged()
        {
            if (State == PanelState.Open)
                Refresh();
        }

        private void Refresh()
        {
            ClearEntries();
            if (_listContainer == null || _candidateEntryPrefab == null || !PlayerShopManager.HasInstance)
                return;

            if (_statItemId > 0)
            {
                RefreshStatItemCandidates();
                return;
            }

            StaffRole otherRole = GetOtherRole();
            int otherStaffId = PlayerShopManager.Instance.GetAssignment(_townKey, otherRole)?.StaffId ?? 0;
            StaffInfo? current = PlayerShopManager.Instance.GetAssignment(_townKey, _role);
            int currentStaffId = current is { IsAssigned: true } ? current.Value.StaffId : 0;
            string roleName = _role == StaffRole.Cashier ? "계산" : "호객";
            List<StaffDefinition> registerableStaff =
                StaffRegistrationPolicy.GetRegisterableDollStaff(otherStaffId);

            if (currentStaffId > 0)
            {
                bool townHired = PlayerShopManager.Instance.GetHiredStaff(currentStaffId)?.IsTownHired ?? true;
                CreateCandidate(
                    current.Value,
                    !townHired,
                    roleName,
                    false,
                    null);
            }

            foreach (StaffInfo profile in PlayerShopManager.Instance.HiredStaff)
            {
                if (profile.StaffId <= 0 || profile.StaffId == currentStaffId)
                    continue;

                int staffId = profile.StaffId;
                CreateCandidate(profile, !profile.IsTownHired, "변경", true, () => AssignRole(staffId));
            }

            if (_showRegisterCandidates)
                CreateRegisterCandidates(registerableStaff);

            if (_registerButton != null)
            {
                _registerButton.transform.SetAsLastSibling();
                SetButtonEnabled(_registerButton, registerableStaff.Count > 0);
            }
        }

        private void CreateRegisterCandidates(IReadOnlyList<StaffDefinition> definitions)
        {
            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Count; i++)
            {
                StaffDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                int staffId = definition.StaffId;
                CreateCandidate(
                    staffId,
                    definition.Intelligence,
                    definition.Charm,
                    true,
                    "등록",
                    true,
                    () => RegisterStaff(staffId));
            }
        }

        private void RefreshStatItemCandidates()
        {
            foreach (StaffInfo profile in PlayerShopManager.Instance.HiredStaff)
            {
                if (profile.StaffId <= 0)
                    continue;

                int staffId = profile.StaffId;
                CreateCandidate(profile, !profile.IsTownHired, "사용", true, () => UseStatItem(staffId));
            }
        }

        private void CreateCandidate(
            StaffInfo profile,
            bool itemEnabled,
            string buttonLabel,
            bool buttonInteractable,
            System.Action onClick)
        {
            CreateCandidate(
                profile.StaffId,
                profile.Intelligence,
                profile.Charm,
                itemEnabled,
                buttonLabel,
                buttonInteractable,
                onClick);
        }

        private void CreateCandidate(
            int staffId,
            int intelligence,
            int charm,
            bool itemEnabled,
            string buttonLabel,
            bool buttonInteractable,
            System.Action onClick)
        {
            GameObject entryObject = Instantiate(_candidateEntryPrefab, _listContainer);
            entryObject.SetActive(true);
            _entries.Add(entryObject);

            StaffCandidateUI candidate = entryObject.GetComponent<StaffCandidateUI>();
            if (candidate != null)
            {
                candidate.Configure(
                    StaffDisplayUtility.GetSprite(staffId),
                    intelligence,
                    charm,
                    itemEnabled,
                    buttonLabel,
                    buttonInteractable,
                    onClick);
            }
        }

        private StaffRole GetOtherRole()
        {
            return _role == StaffRole.Cashier
                ? StaffRole.Sales
                : StaffRole.Cashier;
        }

        private void ClearEntries()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i] != null)
                    Destroy(_entries[i]);

            _entries.Clear();
        }

        private void SubscribeInventoryEvents()
        {
            if (!InventoryManager.HasInstance)
                return;

            InventoryManager manager = InventoryManager.Instance;
            manager.OnPlayerInventoryReady += HandlePlayerInventoryChanged;
            manager.OnPlayerInventoryChanged += HandlePlayerInventoryChanged;
        }

        private void UnsubscribeInventoryEvents()
        {
            if (!InventoryManager.HasInstance)
                return;

            InventoryManager manager = InventoryManager.Instance;
            manager.OnPlayerInventoryReady -= HandlePlayerInventoryChanged;
            manager.OnPlayerInventoryChanged -= HandlePlayerInventoryChanged;
        }
    }
}
