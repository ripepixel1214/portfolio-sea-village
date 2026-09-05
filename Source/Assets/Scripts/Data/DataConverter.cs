#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SeaVillage.Data
{
    public class DataConverter : EditorWindow
    {
        // 기본적으로 DataManager에 정의된 경로를 사용하지만, DataConverter 창에서 직접 변경 가능
        private string csvFolderPath = DataManager.CSV_FOLDER_PATH;
        private string soFolderPath = DataManager.SO_FOLDER_PATH;

        [MenuItem("SeaVillage/Data Converter")]
        public static void ShowWindow()
        {
            GetWindow<DataConverter>("Data Converter");
        }

        private void OnGUI()
        {
            GUILayout.Label("Sea Village Data Converter", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // 경로 설정
            GUILayout.Label("Paths", EditorStyles.boldLabel);
            csvFolderPath = EditorGUILayout.TextField("CSV Folder Path", csvFolderPath);
            soFolderPath = EditorGUILayout.TextField("SO Output Path", soFolderPath);

            GUILayout.Space(10);

            // 변환 버튼들
            GUILayout.Label("Convert Data", EditorStyles.boldLabel);

            if (GUILayout.Button("Convert All CSV Files"))
            {
                ConvertAllCSVFiles();
            }

            GUILayout.Space(5);

            if (GUILayout.Button("Convert Item Data"))
            {
                ConvertItemData();
            }

            if (GUILayout.Button("Convert Item Type Data"))
            {
                ConvertItemTypeData();
            }

            if (GUILayout.Button("Convert Item Price Data"))
            {
                ConvertItemPriceData();
            }

            if (GUILayout.Button("Convert Recipe Data"))
            {
                ConvertRecipeData();
            }

            if (GUILayout.Button("Convert Shop Data"))
            {
                ConvertShopData();
            }

            if (GUILayout.Button("Convert Customer Data"))
            {
                ConvertCustomerData();
            }

            if (GUILayout.Button("Convert Board Data"))
            {
                ConvertBoardData();
            }

            if (GUILayout.Button("Convert Special Effect Data"))
            {
                ConvertSpecialEffectData();
            }

            if (GUILayout.Button("Convert Event Data"))
            {
                ConvertEventData();
            }

            if (GUILayout.Button("Convert Script Data"))
            {
                ConvertScriptData();
            }

            if (GUILayout.Button("Convert Tutorial Data"))
            {
                ConvertTutorialData();
            }

            if (GUILayout.Button("Convert Game Data"))
            {
                ConvertGameData();
            }
        }

        /// <summary>
        /// 외부 코드(GoogleSheetDownloader 등)에서 전체 CSV→SO 변환을 실행할 때 사용.
        /// </summary>
        public static bool ConvertAllFromCode()
        {
            var converter = CreateInstance<DataConverter>();
            converter.csvFolderPath = DataManager.CSV_FOLDER_PATH;
            converter.soFolderPath = DataManager.SO_FOLDER_PATH;
            bool converted = converter.ConvertAllCSVFiles();
            DestroyImmediate(converter);
            return converted;
        }

        public static bool ConvertTutorialFromCode()
        {
            var converter = CreateInstance<DataConverter>();
            converter.csvFolderPath = DataManager.CSV_FOLDER_PATH;
            converter.soFolderPath = DataManager.SO_FOLDER_PATH;
            converter.CreateDirectoryIfNotExists(converter.soFolderPath);
            bool converted = converter.ConvertTutorialData();
            AssetDatabase.Refresh();
            DestroyImmediate(converter);
            return converted;
        }

        private bool ConvertAllCSVFiles()
        {
            CreateDirectoryIfNotExists(soFolderPath);

            if (!TryBuildTutorialDatabase(out TutorialDatabase tutorialDatabase))
            {
                Debug.LogError("Tutorial 데이터 계약 검증 실패로 전체 변환을 시작하지 않았습니다");
                return false;
            }

            ConvertItemData();
            ConvertItemTypeData();
            ConvertItemPriceData();
            ConvertRecipeData();
            ConvertShopData();
            ConvertCustomerData();
            ConvertBoardData();
            ConvertSpecialEffectData();
            ConvertEventData();
            ConvertScriptData();
            bool tutorialConverted = TryReplaceTutorialDatabase(tutorialDatabase);
            if (!tutorialConverted)
            {
                AssetDatabase.Refresh();
                Debug.LogError("TutorialDatabase 교체 실패로 전체 변환을 중단했습니다");
                return false;
            }

            ConvertGameData();

            AssetDatabase.Refresh();
            Debug.Log("모든 CSV 데이터 변환 완료!");

            return true;
        }

        private void ConvertItemData()
        {
            try
            {
                var csvPath = GetCSVFilePath(DataManager.ItemCSVFileName);
                if (csvPath is null) return;

                var lines = File.ReadAllLines(csvPath);
                int propertyNum = GetConvertibleColumnCount(lines[0]);
                var itemDatabase = CreateInstance<ItemDatabase>();
                var items = new List<ItemData>();

                // 헤더 스킵 (첫 번째 줄)
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCSVLine(line);
                    if (values.Length < propertyNum) continue;

                    var item = new ItemData
                    {
                        ID = ParseInt(values[0]),
                        Name = ParseInt(values[1]),
                        Description = ParseInt(values[2]),
                        Town = values[3],
                        Type = ParseToList<string>(values[4]),
                        Weight = Mathf.Round(ParseFloat(values[5]) * 10f) / 10f,
                        OriginPrice = ParseInt(values[6]),
                        PriceListID = ParseInt(values[7]),
                        Unsellable = ParseBool(values[8]),
                        Recipe = ParseInt(values[9]),
                        Usage = values[10],
                        Value = ParseInt(values[11]),
                        Image = values[12],
                    };

                    items.Add(item);
                }

                itemDatabase.SetItems(items);
                SaveScriptableObject(itemDatabase, "ItemDatabase.asset");
                Debug.Log($"Item 데이터 변환 완료: {items.Count}개");
            }
            catch (Exception e)
            {
                Debug.LogError($"Item 데이터 변환 중 오류: {e.Message}");
            }
        }

        private void ConvertItemTypeData()
        {
            try
            {
                var csvPath = GetCSVFilePath(DataManager.ItemTypeCSVFileName);
                if (csvPath is null) return;

                var lines = File.ReadAllLines(csvPath);
                int propertyNum = GetConvertibleColumnCount(lines[0]);
                var itemTypeDatabase = CreateInstance<ItemTypeDatabase>();
                var itemTypes = new List<ItemTypeData>();

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCSVLine(line);
                    if (values.Length < propertyNum) continue;

                    string itemType = values[0]?.Trim();
                    if (string.IsNullOrEmpty(itemType))
                        continue;

                    var itemTypeData = new ItemTypeData
                    {
                        ItemType = itemType,
                        Name = values.Length > 1 ? values[1]?.Trim() : string.Empty,
                        Icon = values.Length > 2 ? values[2]?.Trim() : string.Empty,
                    };

                    itemTypes.Add(itemTypeData);
                }

                itemTypeDatabase.SetItemTypes(itemTypes);
                SaveScriptableObject(itemTypeDatabase, "ItemTypeDatabase.asset");
                Debug.Log($"ItemType 데이터 변환 완료: {itemTypes.Count}개");
            }
            catch (Exception e)
            {
                Debug.LogError($"ItemType 데이터 변환 중 오류: {e.Message}");
            }
        }

        private void ConvertItemPriceData()
        {
            try
            {
                var csvPath = GetCSVFilePath(DataManager.ItemPriceCSVFileName);
                if (csvPath is null) return;

                var lines = File.ReadAllLines(csvPath);
                int propertyNum = GetConvertibleColumnCount(lines[0]);
                var itemPriceDatabase = CreateInstance<ItemPriceDatabase>();
                var itemPrices = new List<ItemPriceData>();

                // 헤더 스킵 (첫 번째 줄)
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCSVLine(line);
                    if (values.Length < propertyNum) continue;

                    var itemPrice = new ItemPriceData
                    {
                        ID = ParseInt(values[0]),
                        Town = values[1],
                        Distance = ParseFloat(values[2]),
                        Preference = ParseFloat(values[3]),
                    };

                    itemPrices.Add(itemPrice);
                }

                itemPriceDatabase.SetItemPrices(itemPrices);
                SaveScriptableObject(itemPriceDatabase, "ItemPriceDatabase.asset");
                Debug.Log($"ItemPrice 데이터 변환 완료: {itemPrices.Count}개");
            }
            catch (Exception e)
            {
                Debug.LogError($"Item 데이터 변환 중 오류: {e.Message}");
            }
        }

        private void ConvertRecipeData()
        {
            try
            {
                var csvPath = GetCSVFilePath(DataManager.RecipeCSVFileName);
                if (csvPath is null) return;

                var lines = File.ReadAllLines(csvPath);
                int propertyNum = GetConvertibleColumnCount(lines[0]);
                var recipeDatabase = CreateInstance<RecipeDatabase>();
                var recipes = new List<RecipeData>();

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCSVLine(line);
                    if (values.Length < propertyNum) continue;

                    var recipe = new RecipeData
                    {
                        ID = ParseInt(values[0]),
                        Stuff = ParseInt(values[1]),
                        Count = ParseInt(values[2]),
                    };

                    recipes.Add(recipe);
                }

                recipeDatabase.SetRecipes(recipes);
                SaveScriptableObject(recipeDatabase, "RecipeDatabase.asset");
                Debug.Log($"Recipe 데이터 변환 완료: {recipes.Count}개");
            }
            catch (Exception e)
            {
                Debug.LogError($"Recipe 데이터 변환 중 오류: {e.Message}");
            }
        }

        private void ConvertShopData()
        {
            try
            {
                var csvPath = GetCSVFilePath(DataManager.ShopCSVFileName);
                if (csvPath is null) return;

                var lines = File.ReadAllLines(csvPath);
                int propertyNum = GetConvertibleColumnCount(lines[0]);
                var shopDatabase = CreateInstance<ShopDatabase>();
                var shops = new List<ShopData>();

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCSVLine(line);
                    if (values.Length < propertyNum) continue;

                    var shop = new ShopData
                    {
                        ID = ParseInt(values[0]),
                        ItemID = ParseInt(values[1]),
                        Count = ParseInt(values[2]),
                        Condition = ParseInt(values[3])
                    };

                    shops.Add(shop);
                }

                shopDatabase.SetShops(shops);
                SaveScriptableObject(shopDatabase, "ShopDatabase.asset");
                Debug.Log($"Shop 데이터 변환 완료: {shops.Count}개");
            }
            catch (Exception e)
            {
                Debug.LogError($"Shop 데이터 변환 중 오류: {e.Message}");
            }
        }

        private void ConvertCustomerData()
        {
            try
            {
                var csvPath = GetCSVFilePath(DataManager.CustomerCSVFileName);
                if (csvPath is null) return;

                var lines = File.ReadAllLines(csvPath);
                int propertyNum = GetConvertibleColumnCount(lines[0]);
                var customerDatabase = CreateInstance<CustomerDatabase>();
                var customers = new List<CustomerData>();
                var customerSpawns = new List<CustomerSpawnData>();
                var customerDialogues = new List<CustomerDialogueData>();

                // Customer.csv
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCSVLine(line);
                    if (values.Length < propertyNum) continue;

                    var customer = new CustomerData
                    {
                        ID = ParseInt(values[0]),
                        Town = values[1],
                        Money = ParseInt(values[2]),
                        ConsumptionType = values[3],
                        Like = values[4],
                        Favorite = ParseInt(values[5]),
                        Image = values[6]
                    };

                    customers.Add(customer);
                }

                // CustomerSpawn.csv
                var spawnPath = GetCSVFilePath(DataManager.CustomerSpawnCSVFileName);
                if (spawnPath != null)
                {
                    var spawnLines = File.ReadAllLines(spawnPath);
                    int spawnPropertyNum = GetConvertibleColumnCount(spawnLines[0]);
                    for (int i = 1; i < spawnLines.Length; i++)
                    {
                        var line = spawnLines[i];
                        if (string.IsNullOrEmpty(line)) continue;
                        var values = ParseCSVLine(line);
                        if (values.Length < spawnPropertyNum) continue;

                        var customerSpawn = new CustomerSpawnData
                        {
                            ID = ParseInt(values[0]),
                            Town = values[1],
                            LoveLv = ParseInt(values[2]),
                            CustomerID = values[3],
                            SpawnProbability = ParseFloat(values[4])
                        };

                        customerSpawns.Add(customerSpawn);
                    }
                }

                // CustomerDialogue.csv
                var dialoguePath = GetCSVFilePath(DataManager.CustomerDialogueCSVFileName);
                if (dialoguePath != null)
                {
                    var dialogueLines = File.ReadAllLines(dialoguePath);
                    int dialoguePropertyNum = GetConvertibleColumnCount(dialogueLines[0]);
                    for (int i = 1; i < dialogueLines.Length; i++)
                    {
                        var line = dialogueLines[i];
                        if (string.IsNullOrEmpty(line)) continue;
                        var values = ParseCSVLine(line);
                        if (values.Length < dialoguePropertyNum) continue;

                        var customerDialogue = new CustomerDialogueData
                        {
                            ID = ParseInt(values[0]),
                            TownEffect = values[1],
                            Town = values[2],
                            Type = values[3],
                            Condition0 = values[4],
                            Condition1 = values[5],
                            ScriptID = values[6]
                        };

                        customerDialogues.Add(customerDialogue);
                    }
                }

                customerDatabase.SetCustomers(customers);
                customerDatabase.SetCustomerSpawns(customerSpawns);
                customerDatabase.SetCustomerDialogues(customerDialogues);
                SaveScriptableObject(customerDatabase, "CustomerDatabase.asset");
                Debug.Log($"Customer 데이터 변환 완료: Customers {customers.Count}개, Spawns {customerSpawns.Count}개, Dialogues {customerDialogues.Count}개");
            }
            catch (Exception e)
            {
                Debug.LogError($"Customer 데이터 변환 중 오류: {e.Message}");
            }
        }

        private void ConvertSpecialEffectData()
        {
            try
            {
                var specialEffectDatabase = CreateInstance<SpecialEffectDatabase>();
                var specialEffects = new List<SpecialEffectData>();
                var specialEffectItemChanges = new List<SpecialEffectItemChangeData>();

                // SpecialEffect.csv
                var specialEffectCSVPath = GetCSVFilePath(DataManager.SpecialEffectCSVFileName);
                if (specialEffectCSVPath is null || !File.Exists(specialEffectCSVPath)) return;
                var lines = File.ReadAllLines(specialEffectCSVPath);
                int propertyNum = GetConvertibleColumnCount(lines[0]);

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCSVLine(line);
                    if (values.Length < propertyNum) continue;

                    var specialEffect = new SpecialEffectData
                    {
                        ID = ParseInt(values[0]),
                        Name = values[1],
                        Town = values[2],
                        ItemChange = ParseInt(values[3]),
                        News = ParseBool(values[4]),
                        Description = values[5],
                        Icon = values[6],
                    };

                    specialEffects.Add(specialEffect);
                }

                // SpecialEffectItemChange.csv
                var specialEffectItemChangeCSVPath = GetCSVFilePath(DataManager.SpecialEffectItemChangeCSVFileName);
                if (specialEffectItemChangeCSVPath is null || !File.Exists(specialEffectItemChangeCSVPath)) return;
                lines = File.ReadAllLines(specialEffectItemChangeCSVPath);
                propertyNum = GetConvertibleColumnCount(lines[0]);

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCSVLine(line);
                    if (values.Length < propertyNum) continue;

                    var specialEffectItemChange = new SpecialEffectItemChangeData
                    {
                        ID = ParseInt(values[0]),
                        ItemID = ParseInt(values[1]),
                        ItemChange = ParseInt(values[2]),
                    };

                    specialEffectItemChanges.Add(specialEffectItemChange);
                }

                specialEffectDatabase.SetData(specialEffects, specialEffectItemChanges);
                SaveScriptableObject(specialEffectDatabase, "SpecialEffectDatabase.asset");
                Debug.Log($"SpecialEffect 데이터 변환 완료: SpecialEffects {specialEffects.Count}개, SpecialEffectItemChanges {specialEffectItemChanges.Count}개");
            }
            catch (Exception e)
            {
                Debug.LogError($"SpecialEffect 데이터 변환 중 오류: {e.Message}");
            }
        }

        private void ConvertBoardData()
        {
            try
            {
                var csvPath = GetCSVFilePath(DataManager.BoardCSVFileName);
                if (csvPath is null) return;

                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1)
                {
                    Debug.LogWarning("Board 데이터가 비어 있습니다.");
                    return;
                }

                int propertyNum = GetConvertibleColumnCount(lines[0]);
                var boardDatabase = CreateInstance<BoardDatabase>();
                var boards = new List<BoardData>();

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCSVLine(line);
                    if (values.Length < propertyNum || ShouldSkipBoardRow(values)) continue;

                    var board = new BoardData
                    {
                        Town = values[0],
                        StartQuest = ParseBool(values[1]),
                        ItemID = ParseInt(values[2]),
                        Description = values[3],
                        Reward = ParseInt(values[4])
                    };

                    boards.Add(board);
                }

                boardDatabase.SetBoards(boards);
                SaveScriptableObject(boardDatabase, "BoardDatabase.asset");
                Debug.Log($"Board 데이터 변환 완료: {boards.Count}개");
            }
            catch (Exception e)
            {
                Debug.LogError($"Board 데이터 변환 중 오류: {e.Message}");
            }
        }

        private void ConvertEventData()
        {
            try
            {
                var eventDatabase = CreateInstance<EventDatabase>();
                var events = new List<EventData>();
                var conditions = new List<EventConditionData>();
                var sequences = new List<EventSequenceData>();
                var dialogues = new List<EventDialogueData>();

                ProcessCsv(DataManager.EventCSVFileName, values =>
                {
                    var eventData = new EventData
                    {
                        EventID = values[0],
                        Name = values[1],
                        SceneName = values[2],
                        TriggerType = values[3],
                        Repeatable = ParseBool(values[4])
                    };
                    events.Add(eventData);
                });

                ProcessCsv(DataManager.EventConditionCSVFileName, values =>
                {
                    var conditionData = new EventConditionData
                    {
                        EventID = values[0],
                        ConditionType = values[1],
                        Value = ParseInt(values[2]),
                        Variable = values[3]
                    };
                    conditions.Add(conditionData);
                });

                ProcessCsv(DataManager.EventSequenceCSVFileName, values =>
                {
                    var sequenceData = new EventSequenceData
                    {
                        EventID = values[0],
                        Step = ParseInt(values[1]),
                        Command = values[2],
                        Target = values[3],
                        Value = values[4],
                        NextStep = string.IsNullOrEmpty(values[5]) ? -1 : ParseInt(values[5]),
                        FailStep = string.IsNullOrEmpty(values[6]) ? -1 : ParseInt(values[6])
                    };
                    sequences.Add(sequenceData);
                });

                ProcessCsv(DataManager.EventDialogueCSVFileName, values =>
                {
                    var dialogueData = new EventDialogueData
                    {
                        ID = ParseInt(values[0]),
                        Speaker = ParseInt(values[1]),
                        Script = values[2],
                        Emotion = values[3]
                    };
                    dialogues.Add(dialogueData);
                });

                eventDatabase.SetData(events, conditions, sequences, dialogues);
                SaveScriptableObject(eventDatabase, "EventDatabase.asset");
                Debug.Log($"Event 데이터 변환 완료: Event {events.Count}개, Condition {conditions.Count}개, Sequence {sequences.Count}개, Dialogue {dialogues.Count}개");
            }
            catch (Exception e)
            {
                Debug.LogError($"Event 데이터 변환 중 오류: {e.Message}\n{e.StackTrace}");
            }
        }

        private void ConvertScriptData()
        {
            try
            {
                var csvPath = GetCSVFilePath(DataManager.ScriptCSVFileName);
                if (csvPath is null) return;

                var lines = File.ReadAllLines(csvPath);
                int propertyNum = GetConvertibleColumnCount(lines[0]);
                var scriptDatabase = CreateInstance<ScriptDatabase>();
                var scripts = new List<ScriptData>();

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    var values = ParseCSVLine(line);
                    if (values.Length < propertyNum) continue;

                    var script = new ScriptData
                    {
                        ID = ParseInt(values[0]),
                        KR = values[1]
                    };

                    scripts.Add(script);
                }

                scriptDatabase.SetScripts(scripts);
                SaveScriptableObject(scriptDatabase, "ScriptDatabase.asset");
                Debug.Log($"Script 데이터 변환 완료: {scripts.Count}개");
            }
            catch (Exception e)
            {
                Debug.LogError($"Script 데이터 변환 중 오류: {e.Message}");
            }
        }

        private bool ConvertTutorialData()
        {
            if (!TryBuildTutorialDatabase(out TutorialDatabase tutorialDatabase))
                return false;

            return TryReplaceTutorialDatabase(tutorialDatabase);
        }

        private bool TryBuildTutorialDatabase(out TutorialDatabase tutorialDatabase)
        {
            tutorialDatabase = null;
            string csvPath = GetCSVFilePath(DataManager.TutorialCSVFileName);
            if (csvPath == null)
                return false;

            if (!TutorialCsvParser.TryParse(
                    csvPath,
                    out List<TutorialData> tutorials,
                    out List<string> errors))
            {
                for (int i = 0; i < errors.Count; i++)
                    Debug.LogError($"[TutorialData] {errors[i]}");

                return false;
            }

            tutorialDatabase = CreateInstance<TutorialDatabase>();
            tutorialDatabase.name = nameof(TutorialDatabase);
            tutorialDatabase.SetTutorials(tutorials);
            return true;
        }

        private bool TryReplaceTutorialDatabase(TutorialDatabase candidate)
        {
            if (candidate == null || candidate.Tutorials == null || candidate.Tutorials.Count == 0)
            {
                Debug.LogError("[TutorialData] 교체할 TutorialDatabase가 비어 있습니다");
                DestroyImmediate(candidate);
                return false;
            }

            string assetPath = Path.Combine(soFolderPath, "TutorialDatabase.asset").Replace('\\', '/');
            TutorialDatabase existing = AssetDatabase.LoadAssetAtPath<TutorialDatabase>(assetPath);
            TutorialDatabase backup = null;
            bool candidateBecameAsset = false;

            try
            {
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(candidate, assetPath);
                    candidateBecameAsset = true;
                    AssetDatabase.SaveAssetIfDirty(candidate);
                }
                else
                {
                    backup = CreateInstance<TutorialDatabase>();
                    EditorUtility.CopySerialized(existing, backup);
                    EditorUtility.CopySerialized(candidate, existing);
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssetIfDirty(existing);
                }

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                TutorialDatabase persisted = AssetDatabase.LoadAssetAtPath<TutorialDatabase>(assetPath);
                if (!HasSameTutorialContract(candidate, persisted))
                    throw new InvalidDataException("저장 후 TutorialDatabase 검증에 실패했습니다");

                Debug.Log($"Tutorial 데이터 변환 완료: {candidate.Tutorials.Count}개 대사");
                return true;
            }
            catch (Exception exception)
            {
                string rollbackError = string.Empty;
                try
                {
                    if (existing != null && backup != null)
                    {
                        EditorUtility.CopySerialized(backup, existing);
                        EditorUtility.SetDirty(existing);
                        AssetDatabase.SaveAssetIfDirty(existing);
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    }
                    else if (candidateBecameAsset
                             && AssetDatabase.LoadAssetAtPath<TutorialDatabase>(assetPath) != null
                             && !AssetDatabase.DeleteAsset(assetPath))
                    {
                        rollbackError = "새로 생성된 에셋 삭제에 실패했습니다";
                    }
                }
                catch (Exception rollbackException)
                {
                    rollbackError = rollbackException.Message;
                }

                if (string.IsNullOrEmpty(rollbackError))
                {
                    Debug.LogError($"[TutorialData] 기존 TutorialDatabase를 유지했습니다: {exception.Message}");
                }
                else
                {
                    Debug.LogError(
                        $"[TutorialData] 교체 실패 후 롤백에도 실패했습니다: {exception.Message} / {rollbackError}");
                }

                return false;
            }
            finally
            {
                if (!candidateBecameAsset)
                    DestroyImmediate(candidate);

                if (backup != null)
                    DestroyImmediate(backup);
            }
        }

        private static bool HasSameTutorialContract(
            TutorialDatabase candidate,
            TutorialDatabase persisted)
        {
            if (candidate == null || persisted == null)
                return false;

            IReadOnlyList<TutorialData> candidateTutorials = candidate.Tutorials;
            IReadOnlyList<TutorialData> persistedTutorials = persisted.Tutorials;
            if (candidateTutorials == null
                || persistedTutorials == null
                || candidateTutorials.Count != persistedTutorials.Count)
            {
                return false;
            }

            for (int i = 0; i < candidateTutorials.Count; i++)
            {
                TutorialData expected = candidateTutorials[i];
                TutorialData actual = persistedTutorials[i];
                if (expected == null || actual == null)
                {
                    if (expected != actual)
                        return false;

                    continue;
                }

                if (!string.Equals(expected.ID, actual.ID, StringComparison.Ordinal)
                    || expected.Type != actual.Type
                    || expected.Sequence != actual.Sequence
                    || !string.Equals(expected.Script, actual.Script, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private void ConvertGameData()
        {
            try
            {
                var gameDatabase = CreateInstance<GameDatabase>();

                // Customer_Spawn 데이터 변환
                ConvertCustomerSpawnData(gameDatabase);

                // Customer_Dialogue 데이터 변환
                ConvertCustomerDialogueData(gameDatabase);

                // Variable 데이터 변환
                ConvertVariableData(gameDatabase);

                // Enum 데이터 변환
                ConvertEnumData(gameDatabase);

                SaveScriptableObject(gameDatabase, "GameDatabase.asset");
                Debug.Log("Game 데이터 변환 완료!");
            }
            catch (Exception e)
            {
                Debug.LogError($"Game 데이터 변환 중 오류: {e.Message}");
            }
        }

        private void ConvertCustomerSpawnData(GameDatabase gameDatabase)
        {
            var csvPath = GetCSVFilePath(DataManager.CustomerSpawnCSVFileName);
            if (csvPath is null) return;

            var lines = File.ReadAllLines(csvPath);
            int propertyNum = GetConvertibleColumnCount(lines[0]);
            var customerSpawns = new List<CustomerSpawnData>();

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;

                var values = ParseCSVLine(line);
                if (values.Length < propertyNum) continue;

                var customerSpawn = new CustomerSpawnData
                {
                    ID = ParseInt(values[0]),
                    Town = values[1],
                    LoveLv = ParseInt(values[2]),
                    CustomerID = values[3],
                    SpawnProbability = ParseFloat(values[4])
                };

                customerSpawns.Add(customerSpawn);
            }

            gameDatabase.SetCustomerSpawns(customerSpawns);
        }

        private void ConvertCustomerDialogueData(GameDatabase gameDatabase)
        {
            var csvPath = GetCSVFilePath(DataManager.CustomerDialogueCSVFileName);
            if (csvPath is null) return;

            var lines = File.ReadAllLines(csvPath);
            int propertyNum = GetConvertibleColumnCount(lines[0]);
            var customerDialogues = new List<CustomerDialogueData>();

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;

                var values = ParseCSVLine(line);
                if (values.Length < propertyNum) continue;

                var customerDialogue = new CustomerDialogueData
                {
                    ID = ParseInt(values[0]),
                    TownEffect = values[1],
                    Town = values[2],
                    Type = values[3],
                    Condition0 = values[4],
                    Condition1 = values[5],
                    ScriptID = values[6]
                };

                customerDialogues.Add(customerDialogue);
            }

            gameDatabase.SetCustomerDialogues(customerDialogues);
        }

        private void ConvertVariableData(GameDatabase gameDatabase)
        {
            var csvPath = GetCSVFilePath(DataManager.VariableCSVFileName);
            if (csvPath is null) return;

            var lines = File.ReadAllLines(csvPath);
            int propertyNum = GetConvertibleColumnCount(lines[0]);
            var variables = new List<VariableData>();

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;

                var values = ParseCSVLine(line);
                if (values.Length < propertyNum) continue;

                var variable = new VariableData
                {
                    Variable = values[0],
                    Type = values[1],
                    Value = values[2],
                    Description = values.Length > 3 ? values[3] : ""
                };

                variables.Add(variable);
            }

            gameDatabase.SetVariables(variables);
        }

        private void ConvertEnumData(GameDatabase gameDatabase)
        {
            var csvPath = GetCSVFilePath(DataManager.EnumCSVFileName);
            if (csvPath is null) return;

            var lines = File.ReadAllLines(csvPath);
            int propertyNum = GetConvertibleColumnCount(lines[0]);
            var enums = new List<EnumData>();

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;

                var values = ParseCSVLine(line);
                if (values.Length < propertyNum) continue;

                var enumData = new EnumData
                {
                    EnumName = values[0],
                    Description = values[1],
                    Value = values[2]
                };

                enums.Add(enumData);
            }

            gameDatabase.SetEnums(enums);
        }

        #region Parsing
        private int GetConvertibleColumnCount(string headerLine)
        {
            if (string.IsNullOrWhiteSpace(headerLine))
                return 0;

            var headers = ParseCSVLine(headerLine);
            int count = 0;

            foreach (var header in headers)
            {
                if (string.IsNullOrWhiteSpace(header))
                    continue;

                if (header.TrimStart().StartsWith("#"))
                    continue;

                count++;
            }

            return count;
        }

        private string[] ParseCSVLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var currentField = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                    inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                    currentField.Append(c);
            }

            result.Add(currentField.ToString());
            return result.ToArray();
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

        private int ParseInt(string value)
        {
            if (int.TryParse(value, out int result))
                return result;
            return 0;
        }

        /// <summary>
        /// 공통 CSV 처리 유틸 (첫 줄 헤더, 빈 행/컬럼 수 미달은 스킵)
        /// </summary>
        private void ProcessCsv(string fileName, Action<string[]> rowHandler)
        {
            var path = GetCSVFilePath(fileName);
            if (path is null || !File.Exists(path)) return;

            var lines = SplitCSVRecords(File.ReadAllText(path));
            if (lines.Count <= 1) return;

            int propertyNum = GetConvertibleColumnCount(lines[0]);
            for (int i = 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;

                var values = ParseCSVLine(line);
                if (values.Length < propertyNum) continue;

                rowHandler(values);
            }
        }

        private List<string> SplitCSVRecords(string text)
        {
            var records = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    current.Append(c);
                }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;

                    if (inQuotes)
                    {
                        current.Append('\n');
                    }
                    else
                    {
                        records.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0) records.Add(current.ToString());
            return records;
        }

        private float ParseFloat(string value)
        {
            if (float.TryParse(value, out float result))
                return result;
            return 0f;
        }

        private bool ParseBool(string value)
        {
            return value.ToLower() == "true" || value == "1";
        }

        private List<T> ParseToList<T>(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            List<T> typeList = new List<T>();

            var types = value.Split(',');
            foreach (var type in types)
            {
                var trimmedType = type.Trim();
                if (string.IsNullOrEmpty(trimmedType))
                    continue;
                try
                {
                    T parsedValue = (T)Convert.ChangeType(trimmedType, typeof(T));
                    typeList.Add(parsedValue);
                }
                catch (Exception e)
                {
                    Debug.LogError($"리스트 변환 오류: '{trimmedType}'를 {typeof(T)}로 변환할 수 없습니다. 오류: {e.Message}");
                }
            }

            return typeList;
        }
        #endregion

        private void CreateDirectoryIfNotExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private string GetCSVFilePath(string filePath)
        {
            var csvPath = Path.Combine(csvFolderPath, filePath);
            if (File.Exists(csvPath))
                return csvPath;
            else
            {
                Debug.LogWarning($"파일을 찾을 수 없습니다: {csvPath}");
                return null;
            }
        }

        private void SaveScriptableObject(ScriptableObject obj, string fileName)
        {
            var fullPath = Path.Combine(soFolderPath, fileName);
            AssetDatabase.CreateAsset(obj, fullPath);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
