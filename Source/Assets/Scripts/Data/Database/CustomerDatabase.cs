using UnityEngine;
using System;
using System.Collections.Generic;

namespace SeaVillage.Data
{
    [CreateAssetMenu(fileName = "CustomerDatabase", menuName = "SeaVillage/Data/Customer Database")]
    public class CustomerDatabase : ScriptableObject
    {
        private const string NoTownEffect = "None";

        [SerializeField] private List<CustomerData> customers = new List<CustomerData>();
        [SerializeField] private List<CustomerDialogueData> customerDialogues = new List<CustomerDialogueData>();
        [SerializeField] private List<CustomerSpawnData> customerSpawns = new List<CustomerSpawnData>();
        
        public List<CustomerData> Customers => customers;
        public List<CustomerDialogueData> CustomerDialogues => customerDialogues;
        public List<CustomerSpawnData> CustomerSpawns => customerSpawns;
        
        #region Customer Data
        public CustomerData GetCustomer(int id)
        {
            return customers.Find(customer => customer.ID == id);
        }
        
        public List<CustomerData> GetCustomersByTown(string town)
        {
            return customers.FindAll(customer => customer.Town == town);
        }
        
        public List<CustomerData> GetCustomersByConsumptionType(string consumptionType)
        {
            return customers.FindAll(customer => customer.ConsumptionType == consumptionType);
        }
        
        public void SetCustomers(List<CustomerData> newCustomers)
        {
            customers = newCustomers;
        }
        
        public void AddCustomer(CustomerData customer)
        {
            customers.Add(customer);
        }
        
        public void ClearCustomers()
        {
            customers.Clear();
        }
        #endregion

        #region Customer Dialogue Data
        
        /// <summary>
        /// 특정 타입의 모든 대사 조회
        /// Type: Item, LoveLv, Board, Crowd
        /// </summary>
        public List<CustomerDialogueData> GetDialoguesByType(string type)
        {
            return customerDialogues.FindAll(dialogue => dialogue.Type == type);
        }

        /// <summary>
        /// 마을과 타입으로 대사 필터링
        /// </summary>
        public List<CustomerDialogueData> GetDialoguesByTownAndType(string town, string type)
        {
            return customerDialogues.FindAll(dialogue => 
                (dialogue.Town == town || dialogue.Town == "All") && dialogue.Type == type);
        }

        /// <summary>
        /// Item 타입 대사 조회 (Condition_0 상황 필터링)
        /// condition0: 0=상점 시세 대비 비쌈, 1=상점 시세 대비 그 외, 2=구매 비쌈,
        /// 3=구매 적정, 4=구매 저렴, 5=최애 아이템 구매, 6=이벤트 아이템 구매
        /// </summary>
        public List<CustomerDialogueData> GetItemDialogues(string town, int condition0)
        {
            var activeEffect = ResolveActiveEffect(town);

            return customerDialogues.FindAll(dialogue =>
            {
                if (dialogue.Type != "Item") return false;
                if (dialogue.Town != town && dialogue.Town != "All") return false;
                if (!MatchesTownEffect(dialogue.TownEffect, activeEffect)) return false;

                // 조건이 비어있거나 정수가 아니면 모든 상황에 적용
                if (string.IsNullOrEmpty(dialogue.Condition0)) return true;
                if (!int.TryParse(dialogue.Condition0, out int parsed)) return true;

                return parsed == condition0;
            });
        }

        /// <summary>
        /// LoveLv 타입 대사 조회
        /// 호감도가 Condition_0 이상이고 Condition_1 이하일 때 출력 가능
        /// </summary>
        public List<CustomerDialogueData> GetLoveLevelDialogues(string town, int currentLoveLv)
        {
            var activeEffect = ResolveActiveEffect(town);

            return customerDialogues.FindAll(dialogue =>
            {
                if (dialogue.Type != "LoveLv") return false;
                if (dialogue.Town != town && dialogue.Town != "All") return false;
                if (!MatchesTownEffect(dialogue.TownEffect, activeEffect)) return false;

                bool minLevelMet = string.IsNullOrEmpty(dialogue.Condition0) || 
                    (!int.TryParse(dialogue.Condition0, out int minLv) || currentLoveLv >= minLv);

                bool maxLevelMet = string.IsNullOrEmpty(dialogue.Condition1) || 
                    (!int.TryParse(dialogue.Condition1, out int maxLv) || currentLoveLv <= maxLv);

                return minLevelMet && maxLevelMet;
            });
        }

        /// <summary>
        /// Board 타입 대사 (마을 게시판에서만 출력)
        /// </summary>
        public List<CustomerDialogueData> GetBoardDialogues(string town)
        {
            var activeEffect = ResolveActiveEffect(town);

            return customerDialogues.FindAll(dialogue =>
                dialogue.Type == "Board" &&
                (dialogue.Town == town || dialogue.Town == "All") &&
                MatchesTownEffect(dialogue.TownEffect, activeEffect));
        }

        /// <summary>
        /// Crowd 타입 대사 (가게 혼잡도에 따른 이동 시 출력)
        /// </summary>
        public List<CustomerDialogueData> GetCrowdDialogues(string town)
        {
            var activeEffect = ResolveActiveEffect(town);

            return customerDialogues.FindAll(dialogue =>
                dialogue.Type == "Crowd" &&
                (dialogue.Town == town || dialogue.Town == "All") &&
                MatchesTownEffect(dialogue.TownEffect, activeEffect));
        }

        private static ActiveSpecialEffect ResolveActiveEffect(string town)
        {
            if (!RuntimeItemPriceManager.HasInstance) return null;

            var active = RuntimeItemPriceManager.Instance.GetActiveSpecialEffects();
            if (active == null || active.Count == 0) return null;

            if (!string.IsNullOrEmpty(town) && active.TryGetValue(town, out var townEffect) &&
                townEffect?.EffectData != null)
            {
                return townEffect;
            }

            return active.TryGetValue("All", out var globalEffect) && globalEffect?.EffectData != null
                ? globalEffect
                : null;
        }

        private static bool MatchesTownEffect(string townEffect, ActiveSpecialEffect activeEffect)
        {
            if (string.IsNullOrEmpty(townEffect) || townEffect == NoTownEffect) return true;
            if (activeEffect?.EffectData == null) return false;

            return string.Equals(activeEffect.EffectData.Name, townEffect, StringComparison.OrdinalIgnoreCase) ||
                   activeEffect.EffectData.ID.ToString() == townEffect;
        }

        public void SetCustomerDialogues(List<CustomerDialogueData> newDialogues)
        {
            customerDialogues = newDialogues;
        }

        public void AddCustomerDialogue(CustomerDialogueData dialogue)
        {
            customerDialogues.Add(dialogue);
        }

        public void ClearCustomerDialogues()
        {
            customerDialogues.Clear();
        }
        #endregion

        #region Customer Spawn Data
        
        /// <summary>
        /// 현재 호감도 이하의 가장 가까운 티어에서 고객을 가중치 기반으로 선택
        /// </summary>
        public string GetRandomCustomerIDBySpawnProbability(string town, int loveLv)
        {
            int selectedLevel = ResolveSpawnLevel(town, loveLv);
            if (selectedLevel == int.MaxValue)
                return null;

            float totalProbability = 0f;
            for (int i = 0; i < customerSpawns.Count; i++)
            {
                CustomerSpawnData spawn = customerSpawns[i];
                if (IsSpawnTierMatch(spawn, town, selectedLevel))
                    totalProbability += Mathf.Max(0f, spawn.SpawnProbability);
            }

            if (totalProbability <= 0f)
                return null;

            float randomValue = UnityEngine.Random.value * totalProbability;
            float accumulated = 0f;
            string lastCustomerId = null;

            for (int i = 0; i < customerSpawns.Count; i++)
            {
                CustomerSpawnData spawn = customerSpawns[i];
                if (!IsSpawnTierMatch(spawn, town, selectedLevel) || spawn.SpawnProbability <= 0f)
                    continue;

                lastCustomerId = spawn.CustomerID;
                accumulated += spawn.SpawnProbability;
                if (randomValue <= accumulated)
                    return spawn.CustomerID;
            }

            return lastCustomerId;
        }

        private int ResolveSpawnLevel(string town, int loveLv)
        {
            int selectedLevel = int.MinValue;
            int lowestLevel = int.MaxValue;

            for (int i = 0; i < customerSpawns.Count; i++)
            {
                CustomerSpawnData spawn = customerSpawns[i];
                if (spawn == null || spawn.Town != town)
                    continue;

                lowestLevel = Math.Min(lowestLevel, spawn.LoveLv);
                if (spawn.LoveLv <= loveLv && spawn.LoveLv > selectedLevel)
                    selectedLevel = spawn.LoveLv;
            }

            return selectedLevel == int.MinValue ? lowestLevel : selectedLevel;
        }

        private static bool IsSpawnTierMatch(CustomerSpawnData spawn, string town, int loveLv)
        {
            return spawn != null && spawn.Town == town && spawn.LoveLv == loveLv;
        }

        public void SetCustomerSpawns(List<CustomerSpawnData> newSpawns)
        {
            customerSpawns = newSpawns;
        }

        public void AddCustomerSpawn(CustomerSpawnData spawn)
        {
            customerSpawns.Add(spawn);
        }

        public void ClearCustomerSpawns()
        {
            customerSpawns.Clear();
        }
        #endregion
    }
}
