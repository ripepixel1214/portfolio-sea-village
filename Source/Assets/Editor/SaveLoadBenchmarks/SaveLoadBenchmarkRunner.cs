using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using MemoryPack;
using SeaVillage.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace SeaVillage.EditorTools
{
    /// <summary>
    /// 동일 저장 스냅샷으로 직렬화와 파일 검증 비용을 측정하는 로컬 전용 도구
    /// </summary>
    internal static class SaveLoadBenchmarkRunner
    {
        private const int DefaultWarmupCount = 10;
        private const int DefaultMeasurementCount = 100;
        private const int SerializationBufferInitialCapacity = 128 * 1024;
        private const int PositiveControlAllocationSize = 256 * 1024;
        private const string MenuRoot = "SeaVillage/Benchmarks/Save Load/";
        private const string BaselineRelativePath =
            "UserSettings/SaveLoadBenchmarks/Baseline/SeaVillage_SaveData_0.baseline.mp";
        private const string ResultsRelativeDirectory = "UserSettings/SaveLoadBenchmarks/Results";

        private static readonly List<BenchmarkSample> Samples = new List<BenchmarkSample>(600);
        private static byte[] _positiveControlBuffer;
        private static string _retainedJson;
        private static int _consumedValue;
        private static int _warmupCount = DefaultWarmupCount;
        private static int _measurementCount = DefaultMeasurementCount;
        private static bool _isRunning;

        public static bool IsRunning => _isRunning;

        public static bool TryRunSnapshotPipeline(
            float targetSizeMiB,
            int warmupCount,
            int measurementCount,
            out string resultPath,
            out string error)
        {
            resultPath = string.Empty;
            error = string.Empty;

            if (_isRunning)
            {
                error = "이미 측정 중입니다";
                return false;
            }

            if (targetSizeMiB <= 0f || targetSizeMiB > 50f)
            {
                error = "목표 데이터 크기는 0 MB보다 크고 50 MB 이하여야 합니다";
                return false;
            }

            if (warmupCount < 1 || warmupCount > 100 || measurementCount < 10 || measurementCount > 1000)
            {
                error = "워밍업은 1~100회, 측정은 10~1000회 범위여야 합니다";
                return false;
            }

            _isRunning = true;
            try
            {
                _warmupCount = warmupCount;
                _measurementCount = measurementCount;
                string projectRoot = GetProjectRoot();
                string baselinePath = Path.Combine(projectRoot, BaselineRelativePath);
                if (!File.Exists(baselinePath))
                    throw new FileNotFoundException("기준 세이브 파일을 찾을 수 없습니다", baselinePath);

                Directory.CreateDirectory(Path.Combine(projectRoot, ResultsRelativeDirectory));

                byte[] baselineBytes = File.ReadAllBytes(baselinePath);
                SaveData baselineSnapshot = MemoryPackSerializer.Deserialize<SaveData>(baselineBytes);
                if (baselineSnapshot == null)
                    throw new InvalidDataException("기준 세이브 역직렬화 결과가 null입니다");

                NormalizeSnapshot(baselineSnapshot);
                ValidateJsonDtoCoverage();
                byte[] canonicalSnapshotBytes = MemoryPackSerializer.Serialize(baselineSnapshot);
                long targetSizeBytes = (long)Math.Ceiling(targetSizeMiB * 1024d * 1024d);
                int datasetCount = Math.Max(
                    1,
                    (int)Math.Ceiling(targetSizeBytes / (double)canonicalSnapshotBytes.Length));
                SaveData[] dataset = CreateDataset(canonicalSnapshotBytes, datasetCount);
                byte[] memoryPackPayload = MemoryPackSerializer.Serialize(dataset);

                JsonSaveDatasetDto jsonDataset = JsonSaveDatasetDto.FromSaveData(dataset);
                string jsonPayload = JsonUtility.ToJson(jsonDataset, true);
                ValidateJsonRoundTrip(canonicalSnapshotBytes, datasetCount, jsonPayload);

                Samples.Clear();
                RunAllocationPositiveControl();
                RunSerializationBenchmarks(dataset);

                resultPath = WriteResults(
                    projectRoot,
                    "snapshot-pipeline",
                    memoryPackPayload.Length,
                    Encoding.UTF8.GetByteCount(jsonPayload),
                    targetSizeBytes,
                    datasetCount,
                    jsonUtilityCompatible: true,
                    includesReflectionOverhead: false);

                LogSummary(resultPath, jsonUtilityCompatible: true);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogError($"[Save Load Benchmark] 측정 실패: {exception}");
                return false;
            }
            finally
            {
                _isRunning = false;
            }
        }

        [MenuItem(MenuRoot + "Run Runtime Collection")]
        private static void RunRuntimeCollection()
        {
            if (_isRunning)
            {
                Debug.LogWarning("[Save Load Benchmark] 이미 측정 중입니다");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[Save Load Benchmark] 런타임 데이터 수집 측정은 Play Mode에서 실행해야 합니다");
                return;
            }

            if (!SaveLoadManager.HasInstance)
            {
                Debug.LogError("[Save Load Benchmark] 준비된 SaveLoadManager가 없습니다");
                return;
            }

            MethodInfo collectMethod = typeof(SaveLoadManager).GetMethod(
                "CollectGameData",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (collectMethod == null)
            {
                Debug.LogError("[Save Load Benchmark] CollectGameData 측정 경계를 찾지 못했습니다");
                return;
            }

            _isRunning = true;
            try
            {
                _warmupCount = DefaultWarmupCount;
                _measurementCount = DefaultMeasurementCount;
                Samples.Clear();
                SaveLoadManager manager = SaveLoadManager.Instance;
                Measure(
                    "CurrentMemoryPack",
                    "CollectGameData",
                    () => collectMethod.Invoke(manager, null));

                string projectRoot = GetProjectRoot();
                string resultPath = WriteResults(
                    projectRoot,
                    "runtime-collection",
                    manager.CurrentGameData == null ? 0 : 1,
                    jsonPayloadSize: 0,
                    targetSizeBytes: 0,
                    datasetCount: manager.CurrentGameData == null ? 0 : 1,
                    jsonUtilityCompatible: false,
                    includesReflectionOverhead: true);

                LogSummary(resultPath, jsonUtilityCompatible: false);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Save Load Benchmark] 런타임 수집 측정 실패: {exception}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        private static void RunSerializationBenchmarks(SaveData[] dataset)
        {
            var reusableBuffer = new ArrayBufferWriter<byte>(SerializationBufferInitialCapacity);

            Measure(
                "LegacyJsonUtility",
                "Serialize",
                () =>
                {
                    JsonSaveDatasetDto dto = JsonSaveDatasetDto.FromSaveData(dataset);
                    _retainedJson = JsonUtility.ToJson(dto, true);
                    _consumedValue = Encoding.UTF8.GetByteCount(_retainedJson);
                });

            Measure(
                "CurrentMemoryPack",
                "Serialize",
                () =>
                {
                    reusableBuffer.Clear();
                    MemoryPackSerializer.Serialize(reusableBuffer, dataset);
                    _consumedValue = reusableBuffer.WrittenCount;
                });
        }

        private static void RunAllocationPositiveControl()
        {
            Measure(
                "Harness",
                "AllocationPositiveControl",
                () =>
                {
                    _positiveControlBuffer = new byte[PositiveControlAllocationSize];
                    _positiveControlBuffer[0] = 1;
                    _consumedValue = _positiveControlBuffer.Length;
                });

            for (int i = 0; i < Samples.Count; i++)
            {
                BenchmarkSample sample = Samples[i];
                if (sample.Implementation == "Harness" && sample.AllocationCount > 0)
                    return;
            }

            throw new InvalidOperationException(
                "GC.Alloc positive control이 할당 이벤트를 감지하지 못했습니다");
        }

        private static SaveData[] CreateDataset(byte[] canonicalSnapshotBytes, int datasetCount)
        {
            var dataset = new SaveData[datasetCount];
            for (int i = 0; i < datasetCount; i++)
            {
                dataset[i] = MemoryPackSerializer.Deserialize<SaveData>(canonicalSnapshotBytes);
                if (dataset[i] == null)
                    throw new InvalidDataException($"기준 세이브 {i + 1}번째 깊은 복사 결과가 null입니다");
            }

            return dataset;
        }

        private static void NormalizeSnapshot(SaveData snapshot)
        {
            snapshot.tutorialProgress ??= new TutorialProgressSaveData();
            snapshot.tutorialProgress.activeStepId ??= string.Empty;
            snapshot.tutorialProgress.activeDialogueKey ??= string.Empty;
            snapshot.tutorialProgress.completedStepIds ??= new List<string>();
            snapshot.tutorialProgress.appliedEffectIds ??= new List<string>();
        }

        private static void ValidateJsonDtoCoverage()
        {
            FieldInfo[] saveDataFields = typeof(SaveData).GetFields(BindingFlags.Instance | BindingFlags.Public);
            var missingFields = new List<string>();
            for (int i = 0; i < saveDataFields.Length; i++)
            {
                if (typeof(JsonSaveDataDto).GetField(
                        saveDataFields[i].Name,
                        BindingFlags.Instance | BindingFlags.Public) == null)
                {
                    missingFields.Add(saveDataFields[i].Name);
                }
            }

            if (missingFields.Count > 0)
            {
                throw new InvalidOperationException(
                    $"JsonUtility 비교 DTO에 SaveData 필드가 누락됐습니다: {string.Join(", ", missingFields)}");
            }
        }

        private static void ValidateJsonRoundTrip(
            byte[] canonicalSnapshotBytes,
            int expectedDatasetCount,
            string jsonPayload)
        {
            JsonSaveDatasetDto restoredDto = JsonUtility.FromJson<JsonSaveDatasetDto>(jsonPayload);
            if (restoredDto?.items == null || restoredDto.items.Count != expectedDatasetCount)
                throw new InvalidDataException("JsonUtility 복원 데이터셋 개수가 일치하지 않습니다");

            for (int i = 0; i < restoredDto.items.Count; i++)
            {
                JsonSaveDataDto restoredItem = restoredDto.items[i];
                if (restoredItem == null)
                    throw new InvalidDataException($"JsonUtility 복원 데이터셋 {i + 1}번째 항목이 null입니다");

                byte[] restoredBytes = MemoryPackSerializer.Serialize(restoredItem.ToSaveData());
                if (!BytesEqual(canonicalSnapshotBytes, restoredBytes))
                    throw new InvalidDataException($"JsonUtility 복원 데이터셋 {i + 1}번째 항목이 기준과 일치하지 않습니다");
            }
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null || left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private static void Measure(string implementation, string phase, Action action)
        {
            for (int i = 0; i < _warmupCount; i++)
                action();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Recorder allocationRecorder = Recorder.Get("GC.Alloc");
            if (!allocationRecorder.isValid)
                throw new InvalidOperationException("GC.Alloc Recorder를 사용할 수 없습니다");

            allocationRecorder.FilterToCurrentThread();
            try
            {
                for (int i = 0; i < _measurementCount; i++)
                {
                    allocationRecorder.enabled = false;
                    allocationRecorder.enabled = true;

                    long timestampBefore = Stopwatch.GetTimestamp();
                    action();
                    long timestampAfter = Stopwatch.GetTimestamp();
                    allocationRecorder.enabled = false;

                    double elapsedMilliseconds =
                        (timestampAfter - timestampBefore) * 1000d / Stopwatch.Frequency;

                    Samples.Add(new BenchmarkSample(
                        implementation,
                        phase,
                        i,
                        elapsedMilliseconds,
                        allocationRecorder.sampleBlockCount));
                }
            }
            finally
            {
                allocationRecorder.enabled = false;
                allocationRecorder.CollectFromAllThreads();
            }
        }

        private static string WriteResults(
            string projectRoot,
            string suiteName,
            int inputSize,
            int jsonPayloadSize,
            long targetSizeBytes,
            int datasetCount,
            bool jsonUtilityCompatible,
            bool includesReflectionOverhead)
        {
            string resultsDirectory = Path.Combine(projectRoot, ResultsRelativeDirectory);
            Directory.CreateDirectory(resultsDirectory);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string resultPath = Path.Combine(resultsDirectory, $"{suiteName}-{timestamp}.csv");

            var builder = new StringBuilder(64 * 1024);
            builder.AppendLine($"# unity_version,{Application.unityVersion}");
            builder.AppendLine($"# game_version,{Application.version}");
            builder.AppendLine($"# warmup_count,{_warmupCount}");
            builder.AppendLine($"# measurement_count,{_measurementCount}");
            builder.AppendLine($"# target_size_bytes,{targetSizeBytes}");
            builder.AppendLine($"# dataset_count,{datasetCount}");
            builder.AppendLine($"# input_size_bytes,{inputSize}");
            builder.AppendLine($"# memorypack_payload_size_bytes,{inputSize}");
            builder.AppendLine($"# json_payload_size_bytes,{jsonPayloadSize}");
            builder.AppendLine($"# json_utility_compatible,{jsonUtilityCompatible}");
            builder.AppendLine($"# includes_reflection_overhead,{includesReflectionOverhead}");
            builder.AppendLine("# gc_alloc_source,Unity Profiler GC.Alloc sample block count");
            builder.AppendLine("implementation,phase,iteration,elapsed_ms,gc_alloc_count");

            for (int i = 0; i < Samples.Count; i++)
            {
                BenchmarkSample sample = Samples[i];
                builder.Append(sample.Implementation).Append(',')
                    .Append(sample.Phase).Append(',')
                    .Append(sample.Iteration).Append(',')
                    .Append(sample.ElapsedMilliseconds.ToString("F6", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(sample.AllocationCount)
                    .AppendLine();
            }

            File.WriteAllText(resultPath, builder.ToString(), new UTF8Encoding(false));
            return resultPath;
        }

        private static void LogSummary(string resultPath, bool jsonUtilityCompatible)
        {
            var groups = new Dictionary<string, List<BenchmarkSample>>();
            for (int i = 0; i < Samples.Count; i++)
            {
                BenchmarkSample sample = Samples[i];
                string key = sample.Implementation + "/" + sample.Phase;
                if (!groups.TryGetValue(key, out List<BenchmarkSample> group))
                {
                    group = new List<BenchmarkSample>(_measurementCount);
                    groups.Add(key, group);
                }

                group.Add(sample);
            }

            var message = new StringBuilder(1024);
            message.AppendLine($"[Save Load Benchmark] 완료: {resultPath}");
            message.AppendLine($"JsonUtility 데이터 동일성: {(jsonUtilityCompatible ? "통과" : "실패 - 비교 대상 제외")}");

            foreach (KeyValuePair<string, List<BenchmarkSample>> pair in groups)
            {
                pair.Value.Sort((left, right) => left.ElapsedMilliseconds.CompareTo(right.ElapsedMilliseconds));
                double totalMilliseconds = 0d;
                long totalAllocatedBytes = 0L;
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    totalMilliseconds += pair.Value[i].ElapsedMilliseconds;
                    totalAllocatedBytes += pair.Value[i].AllocationCount;
                }

                int medianIndex = pair.Value.Count / 2;
                int p95Index = Math.Min(pair.Value.Count - 1, (int)Math.Ceiling(pair.Value.Count * 0.95d) - 1);
                BenchmarkSample maximum = pair.Value[pair.Value.Count - 1];

                message.Append(pair.Key)
                    .Append(" | avg ").Append((totalMilliseconds / pair.Value.Count).ToString("F4"))
                    .Append(" ms | median ").Append(pair.Value[medianIndex].ElapsedMilliseconds.ToString("F4"))
                    .Append(" ms | P95 ").Append(pair.Value[p95Index].ElapsedMilliseconds.ToString("F4"))
                    .Append(" ms | max ").Append(maximum.ElapsedMilliseconds.ToString("F4"))
                    .Append(" ms | avg alloc count ").Append(totalAllocatedBytes / pair.Value.Count)
                    .AppendLine();
            }

            Debug.Log(message.ToString());
        }

        private static string GetProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            if (parent == null)
                throw new InvalidOperationException("프로젝트 루트 경로를 확인할 수 없습니다");

            return parent.FullName;
        }

        [Serializable]
        private sealed class JsonSaveDatasetDto
        {
            public List<JsonSaveDataDto> items = new List<JsonSaveDataDto>();

            public static JsonSaveDatasetDto FromSaveData(SaveData[] source)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));

                var result = new JsonSaveDatasetDto
                {
                    items = new List<JsonSaveDataDto>(source.Length)
                };

                for (int i = 0; i < source.Length; i++)
                    result.items.Add(JsonSaveDataDto.FromSaveData(source[i]));

                return result;
            }
        }

        [Serializable]
        private sealed class JsonSaveDataDto
        {
            public string saveDate = string.Empty;
            public string gameVersion = string.Empty;
            public int gameDate = 1;
            public float dayProgress;
            public string gameState = string.Empty;
            public long gold;
            public PlayerStatSaveData playerStats = new PlayerStatSaveData();
            public TownProgressionSaveData townProgression = new TownProgressionSaveData();
            public List<JsonInventoryItemDto> playerInventoryItems = new List<JsonInventoryItemDto>();
            public List<JsonInventoryItemDto> shipInventoryItems = new List<JsonInventoryItemDto>();
            public float shipFoodStorage;
            public int shipLevel;
            public float shipBonusCapacity;
            public List<ItemPriceData> itemPriceData = new List<ItemPriceData>();
            public List<ShopStockSaveData> shopStock = new List<ShopStockSaveData>();
            public List<PlayerShopData> playerShops = new List<PlayerShopData>();
            public List<JsonStringIntEntry> eventLastTriggerDays = new List<JsonStringIntEntry>();
            public List<string> consumedEventIds = new List<string>();
            public List<string> completedQuestIds = new List<string>();
            public List<JsonStringListEntry> boardAssignedSlots = new List<JsonStringListEntry>();
            public List<StaffData> hiredStaff = new List<StaffData>();
            public OceanFogData oceanFog = new OceanFogData();
            public string oceanLastVisitTown = "StartTown";
            public string currentSceneName = "StartTown";
            public List<ActiveSpecialEffectSaveData> activeSpecialEffects =
                new List<ActiveSpecialEffectSaveData>();
            public List<NormalEffectSaveData> activeNormalEffects = new List<NormalEffectSaveData>();
            public float playerPosX;
            public float playerPosY;
            public bool hasPlayerPosition;
            public List<string> completedTutorialIds = new List<string>();
            public int tutorialForcedFoodPriceTargetDay;
            public bool tutorialRewardGranted;
            public TutorialProgressSaveData tutorialProgress = new TutorialProgressSaveData();

            public static JsonSaveDataDto FromSaveData(SaveData source)
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));

                return new JsonSaveDataDto
                {
                    saveDate = source.saveDate,
                    gameVersion = source.gameVersion,
                    gameDate = source.gameDate,
                    dayProgress = source.dayProgress,
                    gameState = source.gameState,
                    gold = source.gold,
                    playerStats = source.playerStats,
                    townProgression = source.townProgression,
                    playerInventoryItems = CopyInventoryItems(source.playerInventoryItems),
                    shipInventoryItems = CopyInventoryItems(source.shipInventoryItems),
                    shipFoodStorage = source.shipFoodStorage,
                    shipLevel = source.shipLevel,
                    shipBonusCapacity = source.shipBonusCapacity,
                    itemPriceData = source.itemPriceData,
                    shopStock = source.shopStock,
                    playerShops = source.playerShops,
                    eventLastTriggerDays = CopyStringIntEntries(source.eventLastTriggerDays),
                    consumedEventIds = source.consumedEventIds,
                    completedQuestIds = source.completedQuestIds,
                    boardAssignedSlots = CopyStringListEntries(source.boardAssignedSlots),
                    hiredStaff = source.hiredStaff,
                    oceanFog = source.oceanFog,
                    oceanLastVisitTown = source.oceanLastVisitTown,
                    currentSceneName = source.currentSceneName,
                    activeSpecialEffects = source.activeSpecialEffects,
                    activeNormalEffects = source.activeNormalEffects,
                    playerPosX = source.playerPosX,
                    playerPosY = source.playerPosY,
                    hasPlayerPosition = source.hasPlayerPosition,
                    completedTutorialIds = source.completedTutorialIds,
                    tutorialForcedFoodPriceTargetDay = source.tutorialForcedFoodPriceTargetDay,
                    tutorialRewardGranted = source.tutorialRewardGranted,
                    tutorialProgress = source.tutorialProgress,
                };
            }

            public SaveData ToSaveData()
            {
                return new SaveData
                {
                    saveDate = saveDate ?? string.Empty,
                    gameVersion = gameVersion ?? string.Empty,
                    gameDate = gameDate,
                    dayProgress = dayProgress,
                    gameState = gameState ?? string.Empty,
                    gold = gold,
                    playerStats = playerStats ?? new PlayerStatSaveData(),
                    townProgression = townProgression ?? new TownProgressionSaveData(),
                    playerInventoryItems = RestoreInventoryItems(playerInventoryItems),
                    shipInventoryItems = RestoreInventoryItems(shipInventoryItems),
                    shipFoodStorage = shipFoodStorage,
                    shipLevel = shipLevel,
                    shipBonusCapacity = shipBonusCapacity,
                    itemPriceData = itemPriceData ?? new List<ItemPriceData>(),
                    shopStock = shopStock ?? new List<ShopStockSaveData>(),
                    playerShops = playerShops ?? new List<PlayerShopData>(),
                    eventLastTriggerDays = RestoreStringIntDictionary(eventLastTriggerDays),
                    consumedEventIds = consumedEventIds ?? new List<string>(),
                    completedQuestIds = completedQuestIds ?? new List<string>(),
                    boardAssignedSlots = RestoreStringListDictionary(boardAssignedSlots),
                    hiredStaff = hiredStaff ?? new List<StaffData>(),
                    oceanFog = oceanFog ?? new OceanFogData(),
                    oceanLastVisitTown = oceanLastVisitTown ?? string.Empty,
                    currentSceneName = currentSceneName ?? string.Empty,
                    activeSpecialEffects = activeSpecialEffects ?? new List<ActiveSpecialEffectSaveData>(),
                    activeNormalEffects = activeNormalEffects ?? new List<NormalEffectSaveData>(),
                    playerPosX = playerPosX,
                    playerPosY = playerPosY,
                    hasPlayerPosition = hasPlayerPosition,
                    completedTutorialIds = completedTutorialIds ?? new List<string>(),
                    tutorialForcedFoodPriceTargetDay = tutorialForcedFoodPriceTargetDay,
                    tutorialRewardGranted = tutorialRewardGranted,
                    tutorialProgress = tutorialProgress ?? new TutorialProgressSaveData(),
                };
            }

            private static List<JsonInventoryItemDto> CopyInventoryItems(
                List<SeaVillage.Core.InventoryItem> source)
            {
                int count = source?.Count ?? 0;
                var result = new List<JsonInventoryItemDto>(count);
                for (int i = 0; i < count; i++)
                {
                    SeaVillage.Core.InventoryItem item = source[i];
                    result.Add(new JsonInventoryItemDto
                    {
                        id = item.id,
                        quantity = item.quantity,
                        unitWeight = item.unitWeight,
                        totalPurchasePrice = item.totalPurchasePrice,
                        averagePurchasePrice = item.averagePurchasePrice,
                    });
                }

                return result;
            }

            private static List<SeaVillage.Core.InventoryItem> RestoreInventoryItems(
                List<JsonInventoryItemDto> source)
            {
                int count = source?.Count ?? 0;
                var result = new List<SeaVillage.Core.InventoryItem>(count);
                for (int i = 0; i < count; i++)
                {
                    JsonInventoryItemDto item = source[i];
                    if (item == null)
                        continue;

                    result.Add(new SeaVillage.Core.InventoryItem
                    {
                        id = item.id,
                        quantity = item.quantity,
                        unitWeight = item.unitWeight,
                        totalPurchasePrice = item.totalPurchasePrice,
                        averagePurchasePrice = item.averagePurchasePrice,
                    });
                }

                return result;
            }

            private static List<JsonStringIntEntry> CopyStringIntEntries(
                Dictionary<string, int> source)
            {
                int count = source?.Count ?? 0;
                var result = new List<JsonStringIntEntry>(count);
                if (source == null)
                    return result;

                foreach (KeyValuePair<string, int> pair in source)
                {
                    result.Add(new JsonStringIntEntry
                    {
                        key = pair.Key,
                        value = pair.Value,
                    });
                }

                return result;
            }

            private static Dictionary<string, int> RestoreStringIntDictionary(
                List<JsonStringIntEntry> source)
            {
                int count = source?.Count ?? 0;
                var result = new Dictionary<string, int>(count);
                for (int i = 0; i < count; i++)
                {
                    JsonStringIntEntry entry = source[i];
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                        continue;

                    result[entry.key] = entry.value;
                }

                return result;
            }

            private static List<JsonStringListEntry> CopyStringListEntries(
                Dictionary<string, List<string>> source)
            {
                int count = source?.Count ?? 0;
                var result = new List<JsonStringListEntry>(count);
                if (source == null)
                    return result;

                foreach (KeyValuePair<string, List<string>> pair in source)
                {
                    result.Add(new JsonStringListEntry
                    {
                        key = pair.Key,
                        values = pair.Value ?? new List<string>(),
                    });
                }

                return result;
            }

            private static Dictionary<string, List<string>> RestoreStringListDictionary(
                List<JsonStringListEntry> source)
            {
                int count = source?.Count ?? 0;
                var result = new Dictionary<string, List<string>>(count);
                for (int i = 0; i < count; i++)
                {
                    JsonStringListEntry entry = source[i];
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                        continue;

                    result[entry.key] = entry.values ?? new List<string>();
                }

                return result;
            }
        }

        [Serializable]
        private sealed class JsonInventoryItemDto
        {
            public int id;
            public int quantity;
            public float unitWeight;
            public int totalPurchasePrice;
            public int averagePurchasePrice;
        }

        [Serializable]
        private sealed class JsonStringIntEntry
        {
            public string key = string.Empty;
            public int value;
        }

        [Serializable]
        private sealed class JsonStringListEntry
        {
            public string key = string.Empty;
            public List<string> values = new List<string>();
        }

        private readonly struct BenchmarkSample
        {
            public BenchmarkSample(
                string implementation,
                string phase,
                int iteration,
                double elapsedMilliseconds,
                long allocationCount)
            {
                Implementation = implementation;
                Phase = phase;
                Iteration = iteration;
                ElapsedMilliseconds = elapsedMilliseconds;
                AllocationCount = allocationCount;
            }

            public string Implementation { get; }
            public string Phase { get; }
            public int Iteration { get; }
            public double ElapsedMilliseconds { get; }
            public long AllocationCount { get; }
        }
    }
}
