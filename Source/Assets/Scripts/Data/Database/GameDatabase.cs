using UnityEngine;
using System.Collections.Generic;

namespace SeaVillage.Data
{
    /// <summary>
    /// 정적 데이터를 통합 관리하는 데이터베이스
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "SeaVillage/Data/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        [SerializeField] private List<CustomerSpawnData> customerSpawns = new List<CustomerSpawnData>();
        [SerializeField] private List<CustomerDialogueData> customerDialogues = new List<CustomerDialogueData>();
        [SerializeField] private List<VariableData> variables = new List<VariableData>();
        [SerializeField] private List<EnumData> enums = new List<EnumData>();
        
        #region Customer Spawn
        public List<CustomerSpawnData> CustomerSpawns => customerSpawns;
        
        public List<CustomerSpawnData> GetCustomerSpawnsByTown(string town)
        {
            return customerSpawns.FindAll(spawn => spawn.Town == town);
        }
        
        public List<CustomerSpawnData> GetCustomerSpawnsByLoveLevel(string town, int loveLv)
        {
            return customerSpawns.FindAll(spawn => spawn.Town == town && spawn.LoveLv == loveLv);
        }

        public void SetCustomerSpawns(List<CustomerSpawnData> newCustomerSpawns)
        {
            customerSpawns = newCustomerSpawns;
        }
        #endregion
        
        #region Customer Dialogue
        public List<CustomerDialogueData> CustomerDialogues => customerDialogues;
        
        public List<CustomerDialogueData> GetCustomerDialogues(string town, string type)
        {
            return customerDialogues.FindAll(dialogue => dialogue.Town == town && dialogue.Type == type);
        }
        
        public CustomerDialogueData GetCustomerDialogue(int id)
        {
            return customerDialogues.Find(dialogue => dialogue.ID == id);
        }

        public void SetCustomerDialogues(List<CustomerDialogueData> newCustomerDialogues)
        {
            customerDialogues = newCustomerDialogues;
        }
        #endregion
        
        #region Variable
        public List<VariableData> Variables => variables;

        public VariableData GetVariable(string variableName)
        {
            return variables.Find(variable => variable.Variable == variableName);
        }
        
        /// <summary>
        /// value 필드를 T 타입으로 변환하는 함수
        /// </summary>
        public T GetVariableValue<T>(string variableName)
        {
            var variable = GetVariable(variableName);
            if (variable == null) return default;
            
            try { return (T)System.Convert.ChangeType(variable.Value, typeof(T)); }
            catch { return default; }
        }

        public void SetVariables(List<VariableData> newVariables)
        {
            variables = newVariables;
        }
        #endregion

        #region Enum
        public List<EnumData> Enums => enums;

        public EnumData GetEnum(string enumName)
        {
            return enums.Find(e => e.EnumName == enumName);
        }

        public T[] GetEnumValues<T>(string enumName)
        {
            var enumData = GetEnum(enumName);
            if (enumData == null) return new T[0];

            var values = enumData.Value.Split(',');
            var result = new List<T>();
            foreach (var value in values)
            {
                try
                {
                    var convertedValue = (T)System.Convert.ChangeType(value.Trim(), typeof(T));
                    result.Add(convertedValue);
                }
                catch { /* 변환 실패 시 무시 */ }
            }
            return result.ToArray();
        }

        public void SetEnums(List<EnumData> newEnums)
        {
            enums = newEnums;
        }

        #endregion

        // 전체 데이터 클리어
        public void ClearAllData()
        {
            customerSpawns.Clear();
            customerDialogues.Clear();
            variables.Clear();
        }
    }
}