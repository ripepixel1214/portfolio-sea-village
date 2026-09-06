using System.Collections.Generic;
using SeaVillage.Core;
using SeaVillage.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    public class DollPanel : SpecialShopDetailPanelBase
    {
        private readonly List<Button> _actionButtons = new();

        public void Initialize()
        {
            SpecialShopPanelUtility.SetText(_headerText, "인형");
            RebuildEntries();
        }

        private void RebuildEntries()
        {
            ClearEntries();
            _actionButtons.Clear();

            SpecialShopContentCatalog catalog = DataManager.HasInstance
                ? DataManager.Instance.SpecialShopContentCatalog
                : null;
            if (catalog == null)
                return;

            for (int i = 0; i < catalog.Dolls.Count; i++)
            {
                DollRewardDefinition definition = catalog.Dolls[i];
                if (definition != null)
                    CreateDollEntry(definition);
            }

            RefreshNavigation(_actionButtons);
        }

        private void CreateDollEntry(DollRewardDefinition definition)
        {
            GameObject entryObject = CreateEntryObject($"Doll ({definition.DollItemId})");
            if (entryObject == null)
                return;

            DollEntry entry = entryObject.GetComponent<DollEntry>();
            if (entry == null)
                return;

            int progress = DollUnlockPolicy.GetProgress(definition);
            bool claimed = DollUnlockPolicy.IsClaimed(definition.DollItemId);
            bool unlocked = DollUnlockPolicy.IsUnlocked(definition);

            entry.Configure(
                SpecialShopPanelUtility.GetItem(definition.DollItemId),
                GetEffectDescription(definition),
                claimed ? "획득 완료" : FormatCondition(definition, progress),
                claimed ? "획득 완료" : "획득");

            SetButtonEnabled(entry.ActionButton, unlocked && !claimed);
            if (entry.ActionButton != null)
            {
                entry.ActionButton.onClick.AddListener(() => TryClaim(definition));
                _actionButtons.Add(entry.ActionButton);
            }
        }

        private static string GetEffectDescription(DollRewardDefinition definition)
        {
            if (definition.StaffId > 0 && DataManager.HasInstance)
            {
                StaffCatalog catalog = DataManager.Instance.StaffCatalog;
                if (catalog != null && catalog.TryGetByStaffId(definition.StaffId, out StaffDefinition staffDefinition))
                    return $"지능 : {staffDefinition.Intelligence}\n매력 : {staffDefinition.Charm}";
            }

            return definition.EffectDescription;
        }

        private static string FormatCondition(DollRewardDefinition definition, int progress)
        {
            return $"{GetConditionLabel(definition)}\n{progress:N0} / {definition.ConditionValue:N0}";
        }

        private static string GetConditionLabel(DollRewardDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(definition.ConditionLabel))
                return definition.ConditionLabel;

            switch (definition.Condition)
            {
                case DollUnlockCondition.TotalPlayerShopRevenue:
                    return "누적 골드";
                case DollUnlockCondition.TotalPlayerShopSales:
                    return "인형 수거";
                case DollUnlockCondition.OwnItem:
                    return $"{SpecialShopPanelUtility.GetItemName(definition.ConditionItemId)} 제작";
                case DollUnlockCondition.TotalTownLove:
                    return "전체 마을 호감도";
                case DollUnlockCondition.TotalDollCount:
                    return "인형 수거";
                default:
                    return "조건";
            }
        }

        private void TryClaim(DollRewardDefinition definition)
        {
            if (!SpecialShopAccessPolicy.CanUseCurrentTown(SpecialShopFeature.SpecialContent))
            {
                UIManager.Instance?.ShowAlertMessage($"호감도 {SpecialShopAccessPolicy.GetRequiredAffinity(SpecialShopFeature.SpecialContent)} 이상 필요");
                RebuildEntries();
                return;
            }

            if (!DollUnlockPolicy.IsUnlocked(definition))
            {
                UIManager.Instance?.ShowAlertMessage("아직 인형을 획득할 조건을 채우지 못했다");
                RebuildEntries();
                return;
            }

            if (!DollRewardProcessor.TryGrantDoll(definition.DollItemId, out string failReason))
            {
                UIManager.Instance?.ShowAlertMessage(failReason);
                RebuildEntries();
                return;
            }

            UIManager.Instance?.ShowAlertMessage(
                $"{GetDollDisplayName(definition)}을(를) 획득했다",
                null,
                "인형 획득");
            RebuildEntries();
        }

        private static string GetDollDisplayName(DollRewardDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(definition.DisplayName))
                return definition.DisplayName;

            return SpecialShopPanelUtility.GetItemName(definition.DollItemId);
        }
    }
}
