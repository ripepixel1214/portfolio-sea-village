using System;
using System.Collections.Generic;
using SeaVillage.Data;
using SeaVillage.Utilities;

namespace SeaVillage.Core
{
    /// <summary>마을별 호감도와 누적 진행 상태를 관리</summary>
    public sealed class TownProgressionManager : Singleton<TownProgressionManager>
    {
        public const int SoldItemMilestone = 100;
        public const int PurchasedItemMilestone = 1000;
        public const int StayedDayMilestone = 30;

        private static readonly TownDefinition[] ProgressionTowns =
        {
            new TownDefinition(TownKey.Start, 5),
            new TownDefinition(TownKey.Forest, 3),
            new TownDefinition(TownKey.Mine, 3),
            new TownDefinition(TownKey.Sea, 0),
            new TownDefinition(TownKey.Dessert, 0),
        };

        private readonly Dictionary<TownKey, TownProgressState> _progressByTown = new Dictionary<TownKey, TownProgressState>();
        private TownKey _harborTownKey = TownKey.Unknown;
        private int _harborConsecutiveNightCount;
        private int _harborLastChargedDay;
        private int _lastObservedDay;

        public event Action<TownKey, int> OnAffinityChanged;

        public bool IsInitialized { get; private set; }
        public TownKey HarborTownKey => _harborTownKey;
        public int HarborConsecutiveNightCount => _harborConsecutiveNightCount;
        public int HarborLastChargedDay => _harborLastChargedDay;

        protected override void Awake()
        {
            base.Awake();
            ResetProgression();
            IsInitialized = true;
        }

        private void OnEnable()
        {
            TimeManager.OnDayChanged += HandleDayChanged;
        }

        private void OnDisable()
        {
            TimeManager.OnDayChanged -= HandleDayChanged;
        }

        public static TownKey NormalizeTownKey(TownKey townKey)
        {
            return townKey == TownKey.Cave ? TownKey.Mine : townKey;
        }

        public int GetAffinity(TownKey townKey)
        {
            return TryGetState(townKey, out TownProgressState state) ? state.Affinity : 0;
        }

        public bool ChangeAffinity(TownKey townKey, int delta)
        {
            if (delta == 0 || !TryGetState(townKey, out TownProgressState state))
                return false;

            int nextAffinity = (int)Math.Clamp(
                (long)state.Affinity + delta,
                TownAffinityRules.MinAffinity,
                TownAffinityRules.MaxAffinity);
            if (nextAffinity == state.Affinity)
                return false;

            state.Affinity = nextAffinity;
            TownKey normalizedTownKey = NormalizeTownKey(townKey);
            OnAffinityChanged?.Invoke(normalizedTownKey, nextAffinity);
            return true;
        }

        public bool RecordPurchasedItems(TownKey townKey, int quantity)
        {
            if (quantity <= 0 || !TryGetState(townKey, out TownProgressState state))
                return false;

            int previousCount = state.PurchasedItemCount;
            state.PurchasedItemCount = SaturatingAdd(state.PurchasedItemCount, quantity);
            return CrossedMilestone(previousCount, state.PurchasedItemCount, PurchasedItemMilestone)
                && IncreaseAffinity(townKey);
        }

        public bool RecordStayedDays(TownKey townKey, int dayCount)
        {
            if (dayCount <= 0 || !TryGetState(townKey, out TownProgressState state))
                return false;

            int previousCount = state.StayedDayCount;
            state.StayedDayCount = SaturatingAdd(state.StayedDayCount, dayCount);
            return CrossedMilestone(previousCount, state.StayedDayCount, StayedDayMilestone)
                && IncreaseAffinity(townKey);
        }

        public bool RecordSoldItems(TownKey townKey, int previousTotalSoldCount, int totalSoldCount)
        {
            if (!CrossedMilestone(previousTotalSoldCount, totalSoldCount, SoldItemMilestone)
                || !IsSupportedTown(NormalizeTownKey(townKey)))
            {
                return false;
            }

            return IncreaseAffinity(townKey);
        }

        public void SetHarborState(TownKey townKey, int consecutiveNightCount, int lastChargedDay)
        {
            TownKey normalizedTownKey = NormalizeTownKey(townKey);
            if (!IsSupportedTown(normalizedTownKey) || consecutiveNightCount <= 0 || lastChargedDay <= 0)
            {
                ResetHarborState();
                return;
            }

            _harborTownKey = normalizedTownKey;
            _harborConsecutiveNightCount = consecutiveNightCount;
            _harborLastChargedDay = lastChargedDay;
        }

        public void ResetHarborState()
        {
            _harborTownKey = TownKey.Unknown;
            _harborConsecutiveNightCount = 0;
            _harborLastChargedDay = 0;
        }

        public TownProgressionSaveData ExportSaveData()
        {
            var result = new TownProgressionSaveData();
            CopySaveDataTo(result);
            return result;
        }

        public void CopySaveDataTo(TownProgressionSaveData target)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.harborTownKey = TownKeyUtility.ToStorageKey(_harborTownKey);
            target.harborConsecutiveNightCount = _harborConsecutiveNightCount;
            target.harborLastChargedDay = _harborLastChargedDay;
            target.towns ??= new List<TownProgressSaveData>();

            for (int i = 0; i < ProgressionTowns.Length; i++)
            {
                TownKey townKey = ProgressionTowns[i].TownKey;
                TownProgressState state = _progressByTown[townKey];
                TownProgressSaveData saved = SaveSnapshotList.GetOrCreate(target.towns, i);
                saved.townKey = TownKeyUtility.ToStorageKey(townKey);
                saved.affinity = state.Affinity;
                saved.purchasedItemCount = state.PurchasedItemCount;
                saved.stayedDayCount = state.StayedDayCount;
            }

            SaveSnapshotList.Trim(target.towns, ProgressionTowns.Length);
        }

        public void ImportSaveData(TownProgressionSaveData data)
        {
            ResetProgression();
            List<TownProgressSaveData> towns = data?.towns;
            if (towns != null)
            {
                for (int i = 0; i < towns.Count; i++)
                {
                    TownProgressSaveData saved = towns[i];
                    if (saved == null || !TownKeyUtility.TryParse(saved.townKey, out TownKey townKey))
                        continue;

                    townKey = NormalizeTownKey(townKey);
                    if (!_progressByTown.TryGetValue(townKey, out TownProgressState state))
                        continue;

                    state.Affinity = TownAffinityRules.Clamp(saved.affinity);
                    state.PurchasedItemCount = Math.Max(0, saved.purchasedItemCount);
                    state.StayedDayCount = Math.Max(0, saved.stayedDayCount);
                }
            }

            if (data != null && TownKeyUtility.TryParse(data.harborTownKey, out TownKey harborTownKey))
                SetHarborState(harborTownKey, data.harborConsecutiveNightCount, data.harborLastChargedDay);

            for (int i = 0; i < ProgressionTowns.Length; i++)
            {
                TownKey townKey = ProgressionTowns[i].TownKey;
                OnAffinityChanged?.Invoke(townKey, _progressByTown[townKey].Affinity);
            }
        }

        public void ResetProgression()
        {
            _progressByTown.Clear();
            for (int i = 0; i < ProgressionTowns.Length; i++)
            {
                TownDefinition definition = ProgressionTowns[i];
                _progressByTown.Add(
                    definition.TownKey,
                    new TownProgressState { Affinity = definition.InitialAffinity });
            }

            _lastObservedDay = TimeManager.HasInstance ? TimeManager.Instance.CurrentDay : 1;
            ResetHarborState();
        }

        public static bool IsSupportedTown(TownKey townKey)
        {
            townKey = NormalizeTownKey(townKey);
            for (int i = 0; i < ProgressionTowns.Length; i++)
            {
                if (ProgressionTowns[i].TownKey == townKey)
                    return true;
            }

            return false;
        }

        private bool TryGetState(TownKey townKey, out TownProgressState state)
        {
            return _progressByTown.TryGetValue(NormalizeTownKey(townKey), out state);
        }

        private bool IncreaseAffinity(TownKey townKey)
        {
            return ChangeAffinity(townKey, 1);
        }

        private void HandleDayChanged(int currentDay)
        {
            int elapsedDays = currentDay - _lastObservedDay;
            _lastObservedDay = currentDay;
            if (elapsedDays <= 0 || !GameManager.HasInstance)
                return;

            TownKey townKey = NormalizeTownKey(GameManager.Instance.CurrentTownKey);
            if (IsSupportedTown(townKey))
                RecordStayedDays(townKey, elapsedDays);
        }

        private static int SaturatingAdd(int current, int amount)
        {
            return current > int.MaxValue - amount ? int.MaxValue : current + amount;
        }

        private static bool CrossedMilestone(int previousValue, int currentValue, int milestone)
        {
            return previousValue < milestone && currentValue >= milestone;
        }

        private sealed class TownProgressState
        {
            public int Affinity;
            public int PurchasedItemCount;
            public int StayedDayCount;
        }

        private readonly struct TownDefinition
        {
            public TownDefinition(TownKey townKey, int initialAffinity)
            {
                TownKey = townKey;
                InitialAffinity = initialAffinity;
            }

            public TownKey TownKey { get; }
            public int InitialAffinity { get; }
        }
    }
}
