using UnityEngine;
using System.Collections.Generic;

namespace SeaVillage.Data
{
    [CreateAssetMenu(fileName = "ScriptDatabase", menuName = "SeaVillage/Data/Script Database")]
    public class ScriptDatabase : ScriptableObject
    {
        [SerializeField] private List<ScriptData> scripts = new List<ScriptData>();
        
        public List<ScriptData> Scripts => scripts;
        
        public ScriptData GetScript(int id)
        {
            return scripts.Find(script => script.ID == id);
        }
        
        public string GetScriptKR(int id)
        {
            var script = GetScript(id);
            return script?.KR ?? "";
        }
        
        public void SetScripts(List<ScriptData> newScripts)
        {
            scripts = newScripts;
        }
        
        public void AddScript(ScriptData script)
        {
            scripts.Add(script);
        }
        
        public void ClearScripts()
        {
            scripts.Clear();
        }
    }
}