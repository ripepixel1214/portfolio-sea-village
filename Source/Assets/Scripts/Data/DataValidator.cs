#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using SeaVillage.Core;

namespace SeaVillage.Data
{
    public class DataValidator : EditorWindow
    {
        private string csvFolderPath = DataManager.CSV_FOLDER_PATH;

        private Vector2 scrollPosition;
        private List<ValidationResult> validationResults = new List<ValidationResult>();

        [MenuItem("SeaVillage/Data Validator")]
        public static void ShowWindow()
        {
            GetWindow<DataValidator>("Data Validator");
        }

        private void OnGUI()
        {
            GUILayout.Label("Sea Village Data Validator", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // 경로 설정
            csvFolderPath = EditorGUILayout.TextField("CSV Folder Path", csvFolderPath);
            GUILayout.Space(10);

            // 검증 버튼
            if (GUILayout.Button("Validate All CSV Files"))
            {
                ValidateAllCSVFiles();
            }

            GUILayout.Space(10);

            // 검증 결과 표시
            if (validationResults.Count > 0)
            {
                GUILayout.Label("Validation Results", EditorStyles.boldLabel);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                foreach (var result in validationResults)
                {
                    var style = result.IsValid ? EditorStyles.helpBox : GUI.skin.GetStyle("ErrorLabel");
                    var color = result.IsValid ? Color.green : Color.red;

                    GUI.color = color;
                    GUILayout.Label($"[{result.FileName}] {result.Message}", style);
                    GUI.color = Color.white;

                    if (!result.IsValid && result.Details.Count > 0)
                    {
                        EditorGUI.indentLevel++;
                        foreach (var detail in result.Details)
                            GUILayout.Label($"* {detail}", EditorStyles.miniLabel);
                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void ValidateAllCSVFiles()
        {
            validationResults.Clear();

            ValidateItemCSV();
            ValidateItemTypeCSV();
            ValidateItemPriceCSV();
            ValidateRecipeCSV();
            ValidateShopCSV();
            ValidateCustomerCSV();
            ValidateBoardCSV();
            ValidateCustomerSpawnCSV();
            ValidateCustomerDialogueCSV();
            ValidateScriptCSV();
            ValidateTutorialCSV();
            ValidateVariableCSV();
            ValidateEventCSV();
            ValidateEventConditionCSV();
            ValidateEventSequenceCSV();
            ValidateEventDialogueCSV();

            // 참조 무결성 검사
            ValidateReferences();

            Debug.Log($"데이터 검증 완료: {validationResults.Count}개 항목 검사됨");
        }

        private void ValidateItemCSV()
        {
            var result = new ValidationResult { FileName = DataManager.ItemCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.ItemCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                // 데이터 행 검증
                var itemIds = new HashSet<int>();
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);

                    // ID 검증
                    if (!int.TryParse(values[0], out int id))
                        result.Details.Add($"행 {i + 1}: ID '{values[0]}'는 유효한 정수가 아닙니다");
                    else if (itemIds.Contains(id))
                        result.Details.Add($"행 {i + 1}: ID '{id}'가 중복됩니다");
                    else
                        itemIds.Add(id);

                    // Weight 검증
                    if (!float.TryParse(values[5], out float weight))
                        result.Details.Add($"행 {i + 1}: Weight '{values[5]}'는 유효한 실수가 아닙니다");
                    else if (!Mathf.Approximately(weight, Mathf.Round(weight * 10f) / 10f))
                        result.Details.Add($"행 {i + 1}: Weight '{values[5]}'는 소수점 한 자리까지 입력해야 합니다");

                    // Unsellable 검증
                    var unsellableValue = values[8].ToLower();
                    if (unsellableValue != "true" && unsellableValue != "false")
                        result.Details.Add($"행 {i + 1}: Unsellable '{values[7]}'는 true 또는 false여야 합니다");

                    // Use 검증
                    var useValue = values[10];
                    if (!string.IsNullOrEmpty(useValue) && useValue != "NULL")
                    {
                        var validUses = new[] { "Food", "Cal", "Charm", "Str", "Dex" };
                        if (!validUses.Contains(useValue))
                            result.Details.Add($"행 {i + 1}: Use '{useValue}'는 유효하지 않습니다 (Food, Cal, Charm, Str, Dex, NULL만 가능)");

                        // Value 검증 (Use가 있으면 Value도 있어야 함)
                        if (!int.TryParse(values[11], out _))
                            result.Details.Add($"행 {i + 1}: Use가 설정된 경우 Value는 유효한 정수여야 합니다");
                    }
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 아이템)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateItemTypeCSV()
        {
            var result = new ValidationResult { FileName = DataManager.ItemTypeCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.ItemTypeCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                var itemTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCSVLine(line);
                    if (values.Length < 2)
                    {
                        result.Details.Add($"행 {i + 1}: ItemType, Name 컬럼이 필요합니다");
                        continue;
                    }

                    string itemType = values[0]?.Trim();
                    string displayName = values[1]?.Trim();

                    if (string.IsNullOrEmpty(itemType))
                    {
                        result.Details.Add($"행 {i + 1}: ItemType이 비어 있습니다");
                        continue;
                    }

                    if (string.IsNullOrEmpty(displayName))
                        result.Details.Add($"행 {i + 1}: Name이 비어 있습니다");

                    if (!itemTypes.Add(itemType))
                        result.Details.Add($"행 {i + 1}: ItemType '{itemType}'가 중복됩니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 타입)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateItemPriceCSV()
        {
            var result = new ValidationResult { FileName = DataManager.ItemPriceCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.ItemPriceCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);

                    if (!float.TryParse(values[2], out float distance) || distance < 1 || distance > 4)
                        result.Details.Add($"행 {i + 1}: ID '{values[2]}'는 1 이상 4 이하의 유효한 실수가 아닙니다");

                    if (!float.TryParse(values[3], out float preference) || preference < -1 || preference > 1)
                        result.Details.Add($"행 {i + 1}: Price '{values[3]}'는 -1 이상 -1 이하의 실수여야 합니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 가격 정보)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateRecipeCSV()
        {
            var result = new ValidationResult { FileName = DataManager.RecipeCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.RecipeCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);

                    if (!int.TryParse(values[0], out _))
                        result.Details.Add($"행 {i + 1}: ID '{values[0]}'는 유효한 정수가 아닙니다");

                    if (!int.TryParse(values[2], out int count) || count <= 0)
                        result.Details.Add($"행 {i + 1}: Count '{values[2]}'는 양의 정수여야 합니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 레시피 재료)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateShopCSV()
        {
            var result = new ValidationResult { FileName = DataManager.ShopCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.ShopCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);

                    // Count 검증
                    if (!int.TryParse(values[2], out int count) || count < 0)
                        result.Details.Add($"행 {i + 1}: ID '{values[2]}'는 0 이상의 정수여야 합니다");

                    // Condition 검증
                    if (!int.TryParse(values[3], out int condition) || condition < 0)
                        result.Details.Add($"행 {i + 1}: Condition '{values[3]}'는 0 이상의 정수여야 합니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 상점 아이템)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateCustomerCSV()
        {
            var result = new ValidationResult { FileName = DataManager.CustomerCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.CustomerCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                var customerIds = new HashSet<int>();
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);

                    if (!int.TryParse(values[0], out int id))
                        result.Details.Add($"행 {i + 1}: ID '{values[0]}'는 유효한 정수가 아닙니다");
                    else if (customerIds.Contains(id))
                        result.Details.Add($"행 {i + 1}: ID '{id}'가 중복됩니다");
                    else
                        customerIds.Add(id);

                    // Money 검증
                    if (!int.TryParse(values[2], out int money) || money < 0)
                        result.Details.Add($"행 {i + 1}: Money '{values[2]}'는 0 이상의 정수여야 합니다");

                    var consumptionType = values[3];
                    if (consumptionType != "Rich" && consumptionType != "Poor" && consumptionType != "Common")
                        result.Details.Add($"행 {i + 1}: ConsumptionType '{consumptionType}'는 Rich, Poor 또는 Common이어야 합니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 고객)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateBoardCSV()
        {
            var result = new ValidationResult { FileName = DataManager.BoardCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.BoardCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                var itemIds = GetItemIds();
                var validTowns = GetEnumValues("Town");
                var uniqueKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCSVLine(line);
                    if (values.Length < 5)
                    {
                        result.Details.Add($"행 {i + 1}: 필드 수가 부족합니다");
                        continue;
                    }

                    if (ShouldSkipBoardRow(values))
                        continue;

                    string town = values[0];
                    if (string.IsNullOrWhiteSpace(town))
                        result.Details.Add($"행 {i + 1}: Town은 비어 있을 수 없습니다");
                    else if (validTowns.Count > 0 && !validTowns.Contains(town))
                        result.Details.Add($"행 {i + 1}: Town '{town}'은 Enum.csv의 Town 정의에 없습니다");

                    if (!bool.TryParse(values[1], out _))
                        result.Details.Add($"행 {i + 1}: StartQuest '{values[1]}'는 true 또는 false여야 합니다");

                    if (!int.TryParse(values[2], out int itemId) || itemId <= 0)
                        result.Details.Add($"행 {i + 1}: ItemID '{values[2]}'는 양의 정수여야 합니다");
                    else if (itemIds.Count > 0 && !itemIds.Contains(itemId))
                        result.Details.Add($"행 {i + 1}: ItemID '{itemId}'를 Item.csv에서 찾을 수 없습니다");

                    if (string.IsNullOrWhiteSpace(values[3]))
                        result.Details.Add($"행 {i + 1}: Description은 비어 있을 수 없습니다");

                    if (!int.TryParse(values[4], out int reward) || reward < 0)
                        result.Details.Add($"행 {i + 1}: Reward '{values[4]}'는 0 이상의 정수여야 합니다");

                    if (!string.IsNullOrWhiteSpace(town) && itemId > 0)
                    {
                        string uniqueKey = $"{town}:{itemId}";
                        if (!uniqueKeys.Add(uniqueKey))
                            result.Details.Add($"행 {i + 1}: Town '{town}'과 ItemID '{itemId}' 조합이 중복됩니다");
                    }
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 게시판 행)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateCustomerSpawnCSV()
        {
            var result = new ValidationResult { FileName = DataManager.CustomerSpawnCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.CustomerSpawnCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);

                    if (!int.TryParse(values[0], out int id))
                        result.Details.Add($"행 {i + 1}: ID '{values[0]}'는 유효한 정수가 아닙니다");

                    if (string.IsNullOrEmpty(values[1]))
                        result.Details.Add($"행 {i + 1}: Town은 비어 있을 수 없습니다");

                    if (!int.TryParse(values[2], out int loveLv)
                        || loveLv < TownAffinityRules.MinAffinity
                        || loveLv > TownAffinityRules.MaxAffinity)
                    {
                        result.Details.Add(
                            $"행 {i + 1}: LoveLv '{values[2]}'는 {TownAffinityRules.MinAffinity}~{TownAffinityRules.MaxAffinity} 정수여야 합니다");
                    }

                    if (string.IsNullOrEmpty(values[3]))
                        result.Details.Add($"행 {i + 1}: CustomerID는 비어 있을 수 없습니다");

                    if (!float.TryParse(values[4], out float probability) || probability < 0f || probability > 100f)
                        result.Details.Add($"행 {i + 1}: SpawnProbability '{values[4]}'는 0 ~ 100 사이의 수여야 합니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 스폰 정보)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateCustomerDialogueCSV()
        {
            var result = new ValidationResult { FileName = DataManager.CustomerDialogueCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.CustomerDialogueCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                var ids = new HashSet<int>();
                var validTypes = new[] { "LoveLv", "Board", "Crowd", "Item" };
                var validItemCondition0 = new HashSet<int> { 0, 1, 2, 3, 4, 5, 6 }; // Condition_0 for Item

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);
                    if (values.Length < 7)
                    {
                        result.Details.Add($"행 {i + 1}: 필드 수가 부족합니다");
                        continue;
                    }

                    if (!int.TryParse(values[0], out int id))
                        result.Details.Add($"행 {i + 1}: ID '{values[0]}'는 유효한 정수가 아닙니다");
                    else if (!ids.Add(id))
                        result.Details.Add($"행 {i + 1}: ID '{id}'가 중복되었습니다");

                    var type = values[3];
                    if (!validTypes.Contains(type))
                        result.Details.Add($"행 {i + 1}: Type '{type}'는 유효하지 않습니다 (LoveLv, Board, Crowd, Item만 가능)");

                    // Condition_0 / Condition_1 검증 (타입별 규칙 적용)
                    if (type == "Item")
                    {
                        if (!string.IsNullOrEmpty(values[4]) && (!int.TryParse(values[4], out int c0) || !validItemCondition0.Contains(c0)))
                            result.Details.Add($"행 {i + 1}: Condition_0 '{values[4]}'는 Item 타입에서 0~6 또는 비어 있어야 합니다");
                    }
                    else if (type == "LoveLv")
                    {
                        if (!string.IsNullOrEmpty(values[4]) && !int.TryParse(values[4], out _))
                            result.Details.Add($"행 {i + 1}: Condition_0 '{values[4]}'는 LoveLv 타입에서 정수여야 합니다");
                        if (!string.IsNullOrEmpty(values[5]) && !int.TryParse(values[5], out _))
                            result.Details.Add($"행 {i + 1}: Condition_1 '{values[5]}'는 LoveLv 타입에서 정수여야 합니다");
                    }

                    if (string.IsNullOrWhiteSpace(values[6]))
                        result.Details.Add($"행 {i + 1}: ScriptID(대사) 값이 비어 있습니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 대화)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateEventCSV()
        {
            var result = new ValidationResult { FileName = DataManager.EventCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.EventCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                var eventIds = new HashSet<string>();
                var header = lines[0].Split(',');

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);

                    var eventId = values[0];
                    if (string.IsNullOrEmpty(eventId))
                        result.Details.Add($"행 {i + 1}: EventID가 비어있습니다");
                    else if (eventIds.Contains(eventId))
                        result.Details.Add($"행 {i + 1}: EventID '{eventId}'가 중복됩니다");
                    else
                        eventIds.Add(eventId);

                    var repeatable = values[4].ToLower();
                    if (repeatable != "true" && repeatable != "false")
                        result.Details.Add($"행 {i + 1}: Repeatable '{values[4]}'는 true 또는 false여야 합니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 이벤트)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateEventConditionCSV()
        {
            var result = new ValidationResult { FileName = DataManager.EventConditionCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.EventConditionCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                var validTypes = new[] { "LoveLv", "Percent", "AfterDay", "Favorite", "Quest" };

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);

                    if (string.IsNullOrEmpty(values[0]))
                        result.Details.Add($"행 {i + 1}: EventID가 비어있습니다");

                    var type = values[1];
                    if (!validTypes.Contains(type))
                        result.Details.Add($"행 {i + 1}: ConditionType '{type}'는 유효하지 않습니다");

                    if (!int.TryParse(values[2], out int val))
                        result.Details.Add($"행 {i + 1}: Value '{values[2]}'는 유효한 정수가 아닙니다");
                    else if (type == "Percent" && (val < 0 || val > 100))
                        result.Details.Add($"행 {i + 1}: Percent Value '{val}'는 0~100 사이여야 합니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 조건)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateEventSequenceCSV()
        {
            var result = new ValidationResult { FileName = DataManager.EventSequenceCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.EventSequenceCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);

                    if (string.IsNullOrEmpty(values[0]))
                        result.Details.Add($"행 {i + 1}: EventID가 비어있습니다");

                    if (!int.TryParse(values[1], out _))
                        result.Details.Add($"행 {i + 1}: Step '{values[1]}'는 유효한 정수가 아닙니다");

                    if (!string.IsNullOrEmpty(values[5]) && !int.TryParse(values[5], out _))
                        result.Details.Add($"행 {i + 1}: NextStep '{values[5]}'는 빈 값 또는 정수여야 합니다");

                    if (!string.IsNullOrEmpty(values[6]) && !int.TryParse(values[6], out _))
                        result.Details.Add($"행 {i + 1}: FailStep '{values[6]}'는 빈 값 또는 정수여야 합니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 시퀀스)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateEventDialogueCSV()
        {
            var result = new ValidationResult { FileName = DataManager.EventDialogueCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.EventDialogueCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);

                    if (!int.TryParse(values[0], out _))
                        result.Details.Add($"행 {i + 1}: ID '{values[0]}'는 유효한 정수가 아닙니다");

                    if (!string.IsNullOrEmpty(values[1]) && !int.TryParse(values[1], out _))
                        result.Details.Add($"행 {i + 1}: Speaker '{values[1]}'는 빈 값 또는 정수여야 합니다");

                    if (string.IsNullOrEmpty(values[2]))
                        result.Details.Add($"행 {i + 1}: Script 텍스트가 비어있습니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 이벤트 대화)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateScriptCSV()
        {
            var result = new ValidationResult { FileName = DataManager.ScriptCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.ScriptCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                var scriptIds = new HashSet<int>();
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);

                    if (!int.TryParse(values[0], out int id))
                        result.Details.Add($"행 {i + 1}: ID '{values[0]}'는 유효한 정수가 아닙니다");
                    else if (scriptIds.Contains(id))
                        result.Details.Add($"행 {i + 1}: ID '{id}'가 중복됩니다");
                    else
                        scriptIds.Add(id);

                    if (string.IsNullOrEmpty(values[1]))
                        result.Details.Add($"행 {i + 1}: KR 텍스트가 비어있습니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 스크립트)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateTutorialCSV()
        {
            var result = new ValidationResult { FileName = DataManager.TutorialCSVFileName };
            string csvPath = Path.Combine(csvFolderPath, DataManager.TutorialCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            bool isValid = TutorialCsvParser.TryParse(
                csvPath,
                out List<TutorialData> tutorials,
                out List<string> errors);

            result.IsValid = isValid;
            result.Details.AddRange(errors);
            result.Message = isValid
                ? $"검증 통과 ({tutorials.Count}개 튜토리얼 대사)"
                : $"검증 실패 ({errors.Count}개 오류)";
            validationResults.Add(result);
        }

        private void ValidateVariableCSV()
        {
            var result = new ValidationResult { FileName = DataManager.VariableCSVFileName };
            var csvPath = Path.Combine(csvFolderPath, DataManager.VariableCSVFileName);

            if (!File.Exists(csvPath))
            {
                result.IsValid = false;
                result.Message = "파일을 찾을 수 없습니다";
                validationResults.Add(result);
                return;
            }

            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    result.IsValid = false;
                    result.Message = "데이터가 없습니다";
                    validationResults.Add(result);
                    return;
                }

                var variableNames = new HashSet<string>();
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;
                    var values = ParseCSVLine(line);

                    var variableName = values[0];
                    if (string.IsNullOrEmpty(variableName))
                        result.Details.Add($"행 {i + 1}: Variable 이름이 비어있습니다");
                    else if (variableNames.Contains(variableName))
                        result.Details.Add($"행 {i + 1}: Variable '{variableName}'가 중복됩니다");
                    else
                        variableNames.Add(variableName);

                    var type = values[1];
                    var validTypes = new[] { "string", "bool", "enum", "int", "float" };
                    if (!validTypes.Contains(type))
                        result.Details.Add($"행 {i + 1}: Type '{type}'는 유효하지 않습니다 (string, bool, enum, int, float만 가능)");

                    if (string.IsNullOrEmpty(values[2]))
                        result.Details.Add($"행 {i + 1}: Value가 비어있습니다");
                }

                result.IsValid = result.Details.Count == 0;
                result.Message = result.IsValid ? $"검증 통과 ({lines.Length - 1}개 변수)" : $"검증 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"검증 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        #region Ref Validation
        private void ValidateReferences()
        {
            var result = new ValidationResult { FileName = "References", Message = "참조 무결성 검사" };

            try
            {
                // Item과 Script 참조 검사
                ValidateItemScriptReferences(result);

                // Recipe 참조 검사
                ValidateRecipeReferences(result);

                // Shop과 Item 참조 검사
                ValidateShopItemReferences(result);

                // Board와 Item 참조 검사
                ValidateBoardReferences(result);

                // Customer_Dialogue와 Script 참조 검사
                ValidateCustomerDialogueScriptReferences(result);

                // Event 관련 참조 검사
                ValidateEventConditionReferences(result);
                ValidateEventSequenceReferences(result);

                result.IsValid = result.Details.Count == 0;
                if (result.IsValid)
                    result.Message = "참조 무결성 검사 통과";
                else
                    result.Message = $"참조 무결성 검사 실패 ({result.Details.Count}개 오류)";
            }
            catch (Exception e)
            {
                result.IsValid = false;
                result.Message = $"참조 검사 중 오류: {e.Message}";
            }

            validationResults.Add(result);
        }

        private void ValidateItemScriptReferences(ValidationResult result)
        {
            var scriptIds = GetScriptIds();
            var itemPath = Path.Combine(csvFolderPath, DataManager.ItemCSVFileName);

            if (!File.Exists(itemPath) || scriptIds.Count == 0) return;

            var lines = File.ReadAllLines(itemPath);
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCSVLine(lines[i]);
                if (values.Length > 1 && int.TryParse(values[1], out int scriptId))
                {
                    if (!scriptIds.Contains(scriptId))
                        result.Details.Add($"Item 행 {i + 1}: Script ID '{scriptId}'를 찾을 수 없습니다");
                }
            }
        }

        private void ValidateRecipeReferences(ValidationResult result)
        {
            var itemIds = GetItemIds();
            var recipePath = Path.Combine(csvFolderPath, DataManager.RecipeCSVFileName);

            if (!File.Exists(recipePath) || itemIds.Count == 0) return;

            var lines = File.ReadAllLines(recipePath);
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCSVLine(lines[i]);
                if (values.Length > 1)
                {
                    var stuff = values[1];
                    if (stuff != "Gold" && int.TryParse(stuff, out int itemId))
                    {
                        if (!itemIds.Contains(itemId))
                            result.Details.Add($"Recipe 행 {i + 1}: Item ID '{itemId}'를 찾을 수 없습니다");
                    }
                }
            }
        }

        private void ValidateShopItemReferences(ValidationResult result)
        {
            var itemIds = GetItemIds();
            var shopPath = Path.Combine(csvFolderPath, DataManager.ShopCSVFileName);

            if (!File.Exists(shopPath) || itemIds.Count == 0) return;

            var lines = File.ReadAllLines(shopPath);
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCSVLine(lines[i]);
                if (values.Length > 1 && int.TryParse(values[1], out int itemId))
                {
                    if (!itemIds.Contains(itemId))
                        result.Details.Add($"Shop 행 {i + 1}: Item ID '{itemId}'를 찾을 수 없습니다");
                }
            }
        }

        private void ValidateCustomerDialogueScriptReferences(ValidationResult result)
        {
            var dialoguePath = Path.Combine(csvFolderPath, DataManager.CustomerDialogueCSVFileName);

            if (!File.Exists(dialoguePath)) return;

            var lines = File.ReadAllLines(dialoguePath);
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCSVLine(lines[i]);
                if (values.Length > 6 && string.IsNullOrWhiteSpace(values[6]))
                {
                    result.Details.Add($"Customer_Dialogue 행 {i + 1}: ScriptID(대사) 값이 비어 있습니다");
                }
            }
        }

        private void ValidateBoardReferences(ValidationResult result)
        {
            var itemIds = GetItemIds();
            var boardPath = Path.Combine(csvFolderPath, DataManager.BoardCSVFileName);

            if (!File.Exists(boardPath) || itemIds.Count == 0) return;

            var lines = File.ReadAllLines(boardPath);
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCSVLine(lines[i]);
                if (values.Length < 5 || ShouldSkipBoardRow(values))
                    continue;

                if (int.TryParse(values[2], out int itemId) && !itemIds.Contains(itemId))
                    result.Details.Add($"Board 행 {i + 1}: Item ID '{itemId}'를 찾을 수 없습니다");
            }
        }

        private void ValidateEventConditionReferences(ValidationResult result)
        {
            var eventIds = GetEventIds();
            var conditionPath = Path.Combine(csvFolderPath, DataManager.EventConditionCSVFileName);
            if (!File.Exists(conditionPath) || eventIds.Count == 0) return;

            var lines = File.ReadAllLines(conditionPath);
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCSVLine(lines[i]);
                if (values.Length > 0)
                {
                    var eventId = values[0];
                    if (!string.IsNullOrEmpty(eventId) && !eventIds.Contains(eventId))
                        result.Details.Add($"EventCondition 행 {i + 1}: EventID '{eventId}'를 Event.csv에서 찾을 수 없습니다");
                }
            }
        }

        private void ValidateEventSequenceReferences(ValidationResult result)
        {
            var eventIds = GetEventIds();
            var sequencePath = Path.Combine(csvFolderPath, DataManager.EventSequenceCSVFileName);
            if (!File.Exists(sequencePath) || eventIds.Count == 0) return;

            var lines = File.ReadAllLines(sequencePath);
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCSVLine(lines[i]);
                if (values.Length > 0)
                {
                    var eventId = values[0];
                    if (!string.IsNullOrEmpty(eventId) && !eventIds.Contains(eventId))
                        result.Details.Add($"EventSequence 행 {i + 1}: EventID '{eventId}'를 Event.csv에서 찾을 수 없습니다");
                }
            }
        }
        #endregion

        private HashSet<int> GetScriptIds()
        {
            var scriptIds = new HashSet<int>();
            var scriptPath = Path.Combine(csvFolderPath, DataManager.ScriptCSVFileName);

            if (!File.Exists(scriptPath)) return scriptIds;

            var lines = File.ReadAllLines(scriptPath);
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCSVLine(lines[i]);
                if (values.Length > 0 && int.TryParse(values[0], out int id))
                    scriptIds.Add(id);
            }

            return scriptIds;
        }

        private HashSet<int> GetItemIds()
        {
            var itemIds = new HashSet<int>();
            var itemPath = Path.Combine(csvFolderPath, DataManager.ItemCSVFileName);

            if (!File.Exists(itemPath)) return itemIds;

            var lines = File.ReadAllLines(itemPath);
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCSVLine(lines[i]);
                if (values.Length > 0 && int.TryParse(values[0], out int id))
                    itemIds.Add(id);
            }

            return itemIds;
        }

        private HashSet<string> GetEventIds()
        {
            var eventIds = new HashSet<string>();
            var eventPath = Path.Combine(csvFolderPath, DataManager.EventCSVFileName);

            if (!File.Exists(eventPath)) return eventIds;

            var lines = File.ReadAllLines(eventPath);
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCSVLine(lines[i]);
                if (values.Length > 0 && !string.IsNullOrEmpty(values[0]))
                    eventIds.Add(values[0]);
            }

            return eventIds;
        }

        private HashSet<string> GetEnumValues(string enumName)
        {
            var enumValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var enumPath = Path.Combine(csvFolderPath, DataManager.EnumCSVFileName);

            if (!File.Exists(enumPath)) return enumValues;

            var lines = File.ReadAllLines(enumPath);
            for (int i = 1; i < lines.Length; i++)
            {
                var values = ParseCSVLine(lines[i]);
                if (values.Length < 3 || !string.Equals(values[0], enumName, StringComparison.OrdinalIgnoreCase))
                    continue;

                for (int j = 2; j < values.Length; j++)
                {
                    if (!string.IsNullOrWhiteSpace(values[j]))
                        enumValues.Add(values[j]);
                }
            }

            return enumValues;
        }

        private bool ShouldSkipBoardRow(string[] values)
        {
            if (values == null || values.Length < 5)
                return true;

            return string.IsNullOrWhiteSpace(values[1])
                && string.IsNullOrWhiteSpace(values[2])
                && string.IsNullOrWhiteSpace(values[3])
                && string.IsNullOrWhiteSpace(values[4]);
        }

        private string[] ParseCSVLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            string currentField = "";

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                    inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField);
                    currentField = "";
                }
                else
                    currentField += c;
            }

            result.Add(currentField);
            return result.ToArray();
        }

        private class ValidationResult
        {
            public string FileName { get; set; }
            public bool IsValid { get; set; } = true;
            public string Message { get; set; }
            public List<string> Details { get; set; } = new List<string>();
        }
    }
}
#endif
