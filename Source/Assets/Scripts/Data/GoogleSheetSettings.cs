#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaVillage.Data
{
    /// <summary>
    /// Google Spreadsheet 다운로드 자동화를 위한 설정 ScriptableObject
    /// </summary>
    [CreateAssetMenu(fileName = "GoogleSheetSettings", menuName = "SeaVillage/Google Sheet Settings")]
    public class GoogleSheetSettings : ScriptableObject
    {
        public static readonly SheetMapping[] DefaultSheetMappings =
        {
            new SheetMapping("Item",                     DataManager.ItemCSVFileName),
            new SheetMapping("ItemType",                 DataManager.ItemTypeCSVFileName),
            new SheetMapping("ItemPrice",                DataManager.ItemPriceCSVFileName),
            new SheetMapping("Recipe",                   DataManager.RecipeCSVFileName),
            new SheetMapping("Shop",                     DataManager.ShopCSVFileName),
            new SheetMapping("Customer",                 DataManager.CustomerCSVFileName),
            new SheetMapping("Board",                    DataManager.BoardCSVFileName),
            new SheetMapping("CustomerSpawn",            DataManager.CustomerSpawnCSVFileName),
            new SheetMapping("CustomerDialogue",         DataManager.CustomerDialogueCSVFileName),
            new SheetMapping("Event",                    DataManager.EventCSVFileName),
            new SheetMapping("EventCondition",           DataManager.EventConditionCSVFileName),
            new SheetMapping("EventSequence",            DataManager.EventSequenceCSVFileName),
            new SheetMapping("EventDialogue",            DataManager.EventDialogueCSVFileName),
            new SheetMapping("Script",                   DataManager.ScriptCSVFileName),
            new SheetMapping("Tutorial",                 DataManager.TutorialCSVFileName),
            new SheetMapping("Variable",                 DataManager.VariableCSVFileName),
            new SheetMapping("SpecialEffect",            DataManager.SpecialEffectCSVFileName),
            new SheetMapping("SpecialEffectItemChange", DataManager.SpecialEffectItemChangeCSVFileName),
            new SheetMapping("Enum",                     DataManager.EnumCSVFileName),
        };

        [Header("Google Spreadsheet ID")]
        [Tooltip("스프레드시트 URL에서 /d/ 와 /edit 사이의 문자열")]
        public string spreadsheetId = "";

        [Header("시트 <-> CSV 파일 매핑")]
        [Tooltip("Google Spreadsheet의 시트 이름과 로컬 CSV 파일명을 매핑")]
        public List<SheetMapping> sheetMappings = new List<SheetMapping>(DefaultSheetMappings);

        public bool EnsureDefaultMappings()
        {
            bool changed = false;
            sheetMappings ??= new List<SheetMapping>();

            foreach (var mapping in DefaultSheetMappings)
            {
                bool exists = sheetMappings.Exists(existing => existing != null
                    && string.Equals(existing.csvFileName, mapping.csvFileName, StringComparison.OrdinalIgnoreCase));
                if (exists)
                    continue;

                sheetMappings.Add(new SheetMapping(mapping.sheetName, mapping.csvFileName));
                changed = true;
            }

            return changed;
        }
    }
}
#endif
