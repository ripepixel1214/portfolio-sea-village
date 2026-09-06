using System;
using UnityEngine;
using SeaVillage.Core;

namespace SeaVillage.Event.Services
{
    /// <summary>
    /// 기본 스탯 서비스 구현체<br/>
    /// </summary>
    public class DefaultStatService : IStatService
    {
        private const string LoveLevelKey = "LoveLv";
        private const string LoveLevelPrefix = "LoveLv_";

        public long GetStatLong(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0L;

            key = TownKeyUtility.NormalizeLoveLevelKey(key);

            // Gold
            if (key == EventManager.CONTEXT_KEY_GOLD)
                return Math.Max(0L, CurrencyManager.Instance.GetPlayerBalance(CurrencyType.Gold));

            // Boat Food (배 식량)
            if (key == EventManager.CONTEXT_KEY_BOAT_FOOD)
                return InventoryManager.HasInstance
                    ? (long)Mathf.Max(0f, InventoryManager.Instance.ShipFoodStorage)
                    : 0L;

            // Boat Level (배 등급)
            if (key == EventManager.CONTEXT_KEY_BOAT_LEVEL)
                return InventoryManager.HasInstance ? InventoryManager.Instance.ShipLevel : 0L;

            // Player Stats
            if (key.StartsWith("Player_"))
                return GetPlayerStat(key);

            if (TryResolveAffinityTownKey(key, out TownKey townKey))
            {
                return TownProgressionManager.HasInstance
                    ? TownProgressionManager.Instance.GetAffinity(townKey)
                    : 0L;
            }

            return 0L;
        }

        public int GetStat(string key)
        {
            return (int)Math.Clamp(GetStatLong(key), 0L, int.MaxValue);
        }

        public void ChangeStat(string key, int delta)
        {
            if (string.IsNullOrEmpty(key) || delta == 0) return;

            key = TownKeyUtility.NormalizeLoveLevelKey(key);

            // Gold
            if (key == EventManager.CONTEXT_KEY_GOLD)
            {
                bool changed = delta > 0
                    ? CurrencyManager.Instance.TryAddPlayer(CurrencyType.Gold, delta)
                    : CurrencyManager.Instance.TrySpendPlayer(CurrencyType.Gold, -delta);

                if (changed)
                {
                    var saveData = Data.SaveLoadManager.Instance?.CurrentGameData;
                    if (saveData != null)
                    {
                        long currentGold = CurrencyManager.Instance.GetPlayerBalance(CurrencyType.Gold);
                        saveData.gold = Math.Max(0L, currentGold);
                    }
                }

                return;
            }

            // Boat Food (배 식량)
            if (key == EventManager.CONTEXT_KEY_BOAT_FOOD)
            {
                InventoryManager inventory = InventoryManager.Instance;
                if (inventory != null)
                    inventory.ShipFoodStorage += delta;
                return;
            }

            // Boat Level (배 등급) — 이벤트 시퀀스에서 Calculate Boat_Level N 으로 등급 상승
            if (key == EventManager.CONTEXT_KEY_BOAT_LEVEL)
            {
                InventoryManager inventory = InventoryManager.Instance;
                if (inventory != null)
                    inventory.UpgradeShip(inventory.ShipLevel + delta);
                return;
            }

            // Player Stats
            if (key.StartsWith("Player_"))
            {
                ChangePlayerStat(key, delta);
                return;
            }

            if (TryResolveAffinityTownKey(key, out TownKey townKey))
            {
                if (TownProgressionManager.HasInstance)
                    TownProgressionManager.Instance.ChangeAffinity(townKey, delta);
                return;
            }
        }

        #region Player Stat Helpers
        private static bool TryResolveAffinityTownKey(string key, out TownKey townKey)
        {
            townKey = TownKey.Unknown;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            string normalized = key.Trim();
            if (string.Equals(normalized, LoveLevelKey, StringComparison.OrdinalIgnoreCase))
            {
                if (!GameManager.HasInstance)
                    return false;

                townKey = TownProgressionManager.NormalizeTownKey(GameManager.Instance.CurrentTownKey);
                return townKey != TownKey.Unknown;
            }

            string townPart;
            if (normalized.StartsWith(LoveLevelPrefix, StringComparison.OrdinalIgnoreCase))
                townPart = normalized.Substring(LoveLevelPrefix.Length);
            else if (normalized.EndsWith(LoveLevelKey, StringComparison.OrdinalIgnoreCase))
                townPart = normalized.Substring(0, normalized.Length - LoveLevelKey.Length);
            else
                return false;

            if (!TownKeyUtility.TryParse(townPart, out townKey))
                return false;

            townKey = TownProgressionManager.NormalizeTownKey(townKey);
            return townKey != TownKey.Unknown;
        }

        private int GetPlayerStat(string key)
        {
            string statName = key.Substring("Player_".Length);
            if (statName == "Strength" || statName == "Str")
            {
                return PlayerStatManager.HasInstance
                    ? PlayerStatManager.Instance.Strength
                    : PlayerStatManager.DefaultStrength;
            }

            if (statName == "Agility" || statName == "Dex")
            {
                return PlayerStatManager.HasInstance
                    ? PlayerStatManager.Instance.Agility
                    : PlayerStatManager.DefaultAgility;
            }

            return LogAndReturnZero($"[StatService] Unknown player stat: {statName}");
        }

        private void ChangePlayerStat(string key, int delta)
        {
            string statName = key.Substring("Player_".Length);
            PlayerStatType? managedStatType = statName switch
            {
                "Strength" or "Str" => PlayerStatType.Strength,
                "Agility" or "Dex" => PlayerStatType.Agility,
                _ => null,
            };

            if (managedStatType.HasValue)
            {
                PlayerStatManager.Instance.ChangeStat(managedStatType.Value, delta);
                return;
            }

            Debug.LogWarning($"[StatService] Unknown player stat: {statName}");
        }

        private static int LogAndReturnZero(string message)
        {
            Debug.LogWarning(message);
            return 0;
        }
        #endregion

    }
}
