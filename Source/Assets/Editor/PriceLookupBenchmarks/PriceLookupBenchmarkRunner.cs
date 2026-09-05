using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using SeaVillage.Data;
using UnityEditor;
using Unity.Profiling;
using UnityEngine;

namespace SeaVillage.Editor.PriceLookupBenchmarks
{
    public static class PriceLookupBenchmarkRunner
    {
        private const int RandomSeed = 20260816;
        private const int CategoriesPerItem = 3;
        private const int PositiveControlAllocationBytes = 4096;
        private const int GcRecorderCapacity = 16_384;
        private const int MaxPriceKeyCount = 1_000_000;
        private const long MaxTimedLookupCount = 2_000_000_000L;
        private const string ResultDirectory = "UserSettings/PriceLookupBenchmarks/Results";

        private static long _consumedChecksum;

        public static void RunSmokeTestFromCommandLine()
        {
            var settings = new PriceLookupBenchmarkSettings
            {
                ItemCount = 20,
                TownCount = 3,
                ActiveRuleCount = 8,
                LookupCount = 1_000,
                WarmupCount = 2,
                MeasurementCount = 5
            };

            PriceLookupBenchmarkResult result = Run(settings);
            if (!result.DataEquivalent || !result.MeasurementValid || result.Samples.Count != settings.MeasurementCount)
                throw new InvalidOperationException($"Price lookup benchmark smoke test failed: {result.FailureReason}");

            UnityEngine.Debug.Log(
                $"Price lookup benchmark smoke test passed: "
                + $"Uncached={result.UncachedMedianMilliseconds:F3} ms, "
                + $"Cached={result.CachedMedianMilliseconds:F3} ms, "
                + $"CSV={result.CsvPath}");
        }

        internal static PriceLookupBenchmarkResult Run(PriceLookupBenchmarkSettings settings)
        {
            ValidateSettings(settings);

            BenchmarkDataset dataset = BenchmarkDataset.Create(settings);
            PriceLookupBenchmarkResult result = new PriceLookupBenchmarkResult(settings, dataset.PriceKeyCount);

            ValidateDataEquivalence(dataset, result);
            if (!result.DataEquivalent)
                return result;

            WarmUp(dataset, settings.WarmupCount);
            MeasureGcAllocations(dataset, result);
            if (!result.MeasurementValid)
                return result;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            try
            {
                for (int iteration = 0; iteration < settings.MeasurementCount; iteration++)
                {
                    if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar(
                            "Price Lookup Performance Lab",
                            $"측정 중 {iteration + 1}/{settings.MeasurementCount}",
                            (float)iteration / settings.MeasurementCount))
                    {
                        result.Cancelled = true;
                        return result;
                    }

                    bool uncachedFirst = (iteration & 1) == 0;
                    double uncachedMilliseconds;
                    double cachedMilliseconds;
                    long uncachedChecksum;
                    long cachedChecksum;

                    if (uncachedFirst)
                    {
                        uncachedMilliseconds = MeasureBatch(dataset.RunUncachedBatch, out uncachedChecksum);
                        cachedMilliseconds = MeasureBatch(dataset.RunCachedBatch, out cachedChecksum);
                    }
                    else
                    {
                        cachedMilliseconds = MeasureBatch(dataset.RunCachedBatch, out cachedChecksum);
                        uncachedMilliseconds = MeasureBatch(dataset.RunUncachedBatch, out uncachedChecksum);
                    }

                    Consume(uncachedChecksum);
                    Consume(cachedChecksum);

                    if (uncachedChecksum != cachedChecksum)
                    {
                        result.DataEquivalent = false;
                        result.FailureReason = $"측정 {iteration + 1}회차 checksum이 일치하지 않습니다";
                        return result;
                    }

                    result.Samples.Add(new PriceLookupBenchmarkSample(
                        iteration + 1,
                        uncachedFirst,
                        uncachedMilliseconds,
                        cachedMilliseconds,
                        uncachedChecksum));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            result.CalculateStatistics();
            result.CsvPath = WriteCsv(result);
            return result;
        }

        internal static string GetResultDirectoryAbsolutePath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, ResultDirectory.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void ValidateSettings(PriceLookupBenchmarkSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            if (settings.ItemCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(settings.ItemCount), "아이템 수는 1 이상이어야 합니다");
            if (settings.TownCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(settings.TownCount), "지역 수는 1 이상이어야 합니다");
            if (settings.ActiveRuleCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(settings.ActiveRuleCount), "활성 변동 규칙 수는 1 이상이어야 합니다");
            if (settings.LookupCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(settings.LookupCount), "조회 횟수는 1 이상이어야 합니다");
            if (settings.WarmupCount < 0)
                throw new ArgumentOutOfRangeException(nameof(settings.WarmupCount), "워밍업 횟수는 0 이상이어야 합니다");
            if (settings.MeasurementCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(settings.MeasurementCount), "측정 횟수는 1 이상이어야 합니다");

            long priceKeyCount = (long)settings.ItemCount * settings.TownCount;
            if (priceKeyCount > MaxPriceKeyCount)
                throw new ArgumentOutOfRangeException(nameof(settings), $"가격 키는 최대 {MaxPriceKeyCount:N0}개까지 허용합니다");

            long timedLookupCount = (long)settings.LookupCount * settings.MeasurementCount * 2L;
            if (timedLookupCount > MaxTimedLookupCount)
                throw new ArgumentOutOfRangeException(nameof(settings), $"전체 측정 조회는 최대 {MaxTimedLookupCount:N0}회까지 허용합니다");
        }

        private static void ValidateDataEquivalence(
            BenchmarkDataset dataset,
            PriceLookupBenchmarkResult result)
        {
            for (int i = 0; i < dataset.AllKeys.Length; i++)
            {
                ItemPriceKey key = dataset.AllKeys[i];
                int uncachedPrice = dataset.GetUncachedPrice(key);
                int cachedPrice = dataset.GetCachedPrice(key);
                if (uncachedPrice == cachedPrice)
                    continue;

                result.DataEquivalent = false;
                result.FailureReason = $"가격 불일치: {key}, Uncached={uncachedPrice}, Cached={cachedPrice}";
                return;
            }

            result.DataEquivalent = true;
        }

        private static void WarmUp(BenchmarkDataset dataset, int warmupCount)
        {
            for (int i = 0; i < warmupCount; i++)
            {
                Consume(dataset.RunUncachedBatch());
                Consume(dataset.RunCachedBatch());
            }
        }

        private static void MeasureGcAllocations(
            BenchmarkDataset dataset,
            PriceLookupBenchmarkResult result)
        {
            result.PositiveControlGcSamples = MeasureGcAllocSamples(RunPositiveControl, out bool positiveControlValid);
            result.UncachedGcSamples = MeasureGcAllocSamples(dataset.RunUncachedBatch, out bool uncachedValid);
            result.CachedGcSamples = MeasureGcAllocSamples(dataset.RunCachedBatch, out bool cachedValid);

            if (!positiveControlValid || !uncachedValid || !cachedValid)
            {
                result.FailureReason = "GC.Alloc Recorder가 유효하지 않거나 수집 버퍼가 초과되어 측정 결과를 폐기했습니다";
                return;
            }

            result.MeasurementValid = result.PositiveControlGcSamples > 0;
            if (!result.MeasurementValid)
                result.FailureReason = "GC.Alloc positive control을 감지하지 못해 측정 결과를 폐기했습니다";
        }

        private static long MeasureGcAllocSamples(Func<long> action, out bool valid)
        {
            const ProfilerRecorderOptions options =
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread
                | ProfilerRecorderOptions.WrapAroundWhenCapacityReached;

            using (var recorder = new ProfilerRecorder(
                       ProfilerCategory.Memory,
                       "GC.Alloc",
                       GcRecorderCapacity,
                       options))
            {
                if (!recorder.Valid)
                {
                    valid = false;
                    return 0L;
                }

                recorder.Start();
                long checksum = action();
                recorder.Stop();
                Consume(checksum);

                if (recorder.WrappedAround)
                {
                    valid = false;
                    return 0L;
                }

                ProfilerRecorderSample[] samples = recorder.ToArray();
                long sampleCount = 0L;
                for (int i = 0; i < samples.Length; i++)
                    sampleCount += samples[i].Count;

                valid = true;
                return sampleCount;
            }
        }

        private static long RunPositiveControl()
        {
            byte[] allocation = new byte[PositiveControlAllocationBytes];
            allocation[0] = 1;
            GC.KeepAlive(allocation);
            return allocation[0];
        }

        private static double MeasureBatch(Func<long> action, out long checksum)
        {
            long startedAt = Stopwatch.GetTimestamp();
            checksum = action();
            long finishedAt = Stopwatch.GetTimestamp();
            return (finishedAt - startedAt) * 1000d / Stopwatch.Frequency;
        }

        private static string WriteCsv(PriceLookupBenchmarkResult result)
        {
            string directory = GetResultDirectoryAbsolutePath();
            Directory.CreateDirectory(directory);

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string path = Path.Combine(directory, $"PriceLookupBenchmark-{timestamp}.csv");
            var csv = new StringBuilder(4096);
            csv.AppendLine("benchmark,price_lookup_cache");
            csv.Append("created_at,").AppendLine(DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
            csv.Append("item_count,").AppendLine(result.Settings.ItemCount.ToString(CultureInfo.InvariantCulture));
            csv.Append("town_count,").AppendLine(result.Settings.TownCount.ToString(CultureInfo.InvariantCulture));
            csv.Append("price_key_count,").AppendLine(result.PriceKeyCount.ToString(CultureInfo.InvariantCulture));
            csv.Append("active_rule_count,").AppendLine(result.Settings.ActiveRuleCount.ToString(CultureInfo.InvariantCulture));
            csv.Append("lookup_count,").AppendLine(result.Settings.LookupCount.ToString(CultureInfo.InvariantCulture));
            csv.Append("warmup_count,").AppendLine(result.Settings.WarmupCount.ToString(CultureInfo.InvariantCulture));
            csv.Append("measurement_count,").AppendLine(result.Settings.MeasurementCount.ToString(CultureInfo.InvariantCulture));
            csv.Append("data_equivalence,").AppendLine(result.DataEquivalent ? "PASS" : "FAIL");
            csv.Append("measurement_validity,").AppendLine(result.MeasurementValid ? "PASS" : "FAIL");
            csv.Append("positive_control_gc_samples,").AppendLine(result.PositiveControlGcSamples.ToString(CultureInfo.InvariantCulture));
            csv.Append("uncached_gc_samples,").AppendLine(result.UncachedGcSamples.ToString(CultureInfo.InvariantCulture));
            csv.Append("cached_gc_samples,").AppendLine(result.CachedGcSamples.ToString(CultureInfo.InvariantCulture));
            csv.Append("uncached_median_ms,").AppendLine(result.UncachedMedianMilliseconds.ToString("F6", CultureInfo.InvariantCulture));
            csv.Append("uncached_p95_ms,").AppendLine(result.UncachedP95Milliseconds.ToString("F6", CultureInfo.InvariantCulture));
            csv.Append("cached_median_ms,").AppendLine(result.CachedMedianMilliseconds.ToString("F6", CultureInfo.InvariantCulture));
            csv.Append("cached_p95_ms,").AppendLine(result.CachedP95Milliseconds.ToString("F6", CultureInfo.InvariantCulture));
            csv.Append("median_reduction_percent,").AppendLine(result.MedianReductionPercent.ToString("F4", CultureInfo.InvariantCulture));
            csv.AppendLine();
            csv.AppendLine("iteration,first_path,uncached_ms,cached_ms,checksum");

            for (int i = 0; i < result.Samples.Count; i++)
            {
                PriceLookupBenchmarkSample sample = result.Samples[i];
                csv.Append(sample.Iteration.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(sample.UncachedFirst ? "uncached" : "cached").Append(',')
                    .Append(sample.UncachedMilliseconds.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                    .Append(sample.CachedMilliseconds.ToString("F6", CultureInfo.InvariantCulture)).Append(',')
                    .AppendLine(sample.Checksum.ToString(CultureInfo.InvariantCulture));
            }

            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));
            return path;
        }

        private static void Consume(long checksum)
        {
            _consumedChecksum ^= checksum;
        }

        private sealed class BenchmarkDataset
        {
            private readonly Dictionary<ItemPriceKey, PriceEntry> _prices;
            private readonly Dictionary<string, float> _activeOffsets;
            private readonly Dictionary<ItemPriceKey, float> _modifierCache;
            private readonly ItemPriceKey[] _queryKeys;

            internal ItemPriceKey[] AllKeys { get; }
            internal int PriceKeyCount => AllKeys.Length;

            private BenchmarkDataset(
                Dictionary<ItemPriceKey, PriceEntry> prices,
                Dictionary<string, float> activeOffsets,
                Dictionary<ItemPriceKey, float> modifierCache,
                ItemPriceKey[] allKeys,
                ItemPriceKey[] queryKeys)
            {
                _prices = prices;
                _activeOffsets = activeOffsets;
                _modifierCache = modifierCache;
                AllKeys = allKeys;
                _queryKeys = queryKeys;
            }

            internal static BenchmarkDataset Create(PriceLookupBenchmarkSettings settings)
            {
                var random = new System.Random(RandomSeed);
                string[] activeCategories = CreateActiveCategories(settings.ActiveRuleCount);
                string[] inactiveCategories = CreateInactiveCategories(settings.ActiveRuleCount);
                var activeOffsets = CreateActiveOffsets(activeCategories);
                string[] towns = CreateTowns(settings.TownCount);
                string[][] categoriesByItem = CreateItemCategories(
                    settings.ItemCount,
                    activeCategories,
                    inactiveCategories,
                    random);

                int priceKeyCount = settings.ItemCount * settings.TownCount;
                var prices = new Dictionary<ItemPriceKey, PriceEntry>(priceKeyCount);
                var allKeys = new ItemPriceKey[priceKeyCount];
                int keyIndex = 0;

                for (int itemIndex = 0; itemIndex < settings.ItemCount; itemIndex++)
                {
                    int originPrice = 20 + itemIndex % 480;
                    for (int townIndex = 0; townIndex < settings.TownCount; townIndex++)
                    {
                        var key = new ItemPriceKey(itemIndex + 1, towns[townIndex]);
                        float distance = 0.75f + townIndex * 0.07f;
                        float preference = ((itemIndex + townIndex) % 7 - 3) * 0.025f;
                        float basePrice = originPrice * (distance + preference);
                        prices.Add(key, new PriceEntry(basePrice, categoriesByItem[itemIndex]));
                        allKeys[keyIndex++] = key;
                    }
                }

                Dictionary<ItemPriceKey, float> modifierCache = BuildModifierCache(prices, activeOffsets);
                var queryKeys = new ItemPriceKey[settings.LookupCount];
                for (int i = 0; i < queryKeys.Length; i++)
                    queryKeys[i] = allKeys[random.Next(allKeys.Length)];

                return new BenchmarkDataset(prices, activeOffsets, modifierCache, allKeys, queryKeys);
            }

            internal int GetUncachedPrice(ItemPriceKey key)
            {
                PriceEntry entry = _prices[key];
                float modifier = CalculateModifier(entry.Categories, _activeOffsets);
                return Mathf.Max(1, Mathf.RoundToInt(entry.BasePrice * modifier));
            }

            internal int GetCachedPrice(ItemPriceKey key)
            {
                PriceEntry entry = _prices[key];
                float modifier = _modifierCache.TryGetValue(key, out float cachedModifier)
                    ? cachedModifier
                    : 1f;
                return Mathf.Max(1, Mathf.RoundToInt(entry.BasePrice * modifier));
            }

            internal long RunUncachedBatch()
            {
                long checksum = 0L;
                for (int i = 0; i < _queryKeys.Length; i++)
                    checksum += GetUncachedPrice(_queryKeys[i]);
                return checksum;
            }

            internal long RunCachedBatch()
            {
                long checksum = 0L;
                for (int i = 0; i < _queryKeys.Length; i++)
                    checksum += GetCachedPrice(_queryKeys[i]);
                return checksum;
            }

            private static string[] CreateActiveCategories(int count)
            {
                var categories = new string[count];
                for (int i = 0; i < count; i++)
                    categories[i] = $"ActiveCategory_{i:D4}";
                return categories;
            }

            private static string[] CreateInactiveCategories(int activeRuleCount)
            {
                int count = Math.Max(4, activeRuleCount);
                var categories = new string[count];
                for (int i = 0; i < count; i++)
                    categories[i] = $"InactiveCategory_{i:D4}";
                return categories;
            }

            private static Dictionary<string, float> CreateActiveOffsets(string[] activeCategories)
            {
                var offsets = new Dictionary<string, float>(activeCategories.Length);
                for (int i = 0; i < activeCategories.Length; i++)
                {
                    int signedStep = i % 4;
                    float offset = signedStep == 0 ? 0.1f
                        : signedStep == 1 ? -0.1f
                        : signedStep == 2 ? 0.2f
                        : -0.2f;
                    offsets.Add(activeCategories[i], offset);
                }
                return offsets;
            }

            private static string[] CreateTowns(int count)
            {
                var towns = new string[count];
                for (int i = 0; i < count; i++)
                    towns[i] = $"Town_{i:D3}";
                return towns;
            }

            private static string[][] CreateItemCategories(
                int itemCount,
                string[] activeCategories,
                string[] inactiveCategories,
                System.Random random)
            {
                var categoriesByItem = new string[itemCount][];
                for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
                {
                    var categories = new string[CategoriesPerItem];
                    categories[0] = activeCategories[random.Next(activeCategories.Length)];
                    categories[1] = activeCategories[random.Next(activeCategories.Length)];
                    categories[2] = inactiveCategories[random.Next(inactiveCategories.Length)];
                    categoriesByItem[itemIndex] = categories;
                }
                return categoriesByItem;
            }

            private static Dictionary<ItemPriceKey, float> BuildModifierCache(
                Dictionary<ItemPriceKey, PriceEntry> prices,
                Dictionary<string, float> activeOffsets)
            {
                var cache = new Dictionary<ItemPriceKey, float>(prices.Count);
                foreach (KeyValuePair<ItemPriceKey, PriceEntry> pair in prices)
                {
                    float modifier = CalculateModifier(pair.Value.Categories, activeOffsets);
                    if (!Mathf.Approximately(modifier, 1f))
                        cache.Add(pair.Key, modifier);
                }
                return cache;
            }

            private static float CalculateModifier(
                string[] categories,
                Dictionary<string, float> activeOffsets)
            {
                float offset = 0f;
                for (int i = 0; i < categories.Length; i++)
                {
                    if (activeOffsets.TryGetValue(categories[i], out float categoryOffset))
                        offset += categoryOffset;
                }
                return 1f + offset;
            }
        }

        private sealed class PriceEntry
        {
            internal float BasePrice { get; }
            internal string[] Categories { get; }

            internal PriceEntry(float basePrice, string[] categories)
            {
                BasePrice = basePrice;
                Categories = categories;
            }
        }
    }

    internal sealed class PriceLookupBenchmarkSettings
    {
        internal int ItemCount { get; set; } = 200;
        internal int TownCount { get; set; } = 5;
        internal int ActiveRuleCount { get; set; } = 20;
        internal int LookupCount { get; set; } = 100_000;
        internal int WarmupCount { get; set; } = 10;
        internal int MeasurementCount { get; set; } = 100;

        internal PriceLookupBenchmarkSettings Clone()
        {
            return (PriceLookupBenchmarkSettings)MemberwiseClone();
        }
    }

    internal sealed class PriceLookupBenchmarkResult
    {
        internal PriceLookupBenchmarkSettings Settings { get; }
        internal int PriceKeyCount { get; }
        internal List<PriceLookupBenchmarkSample> Samples { get; } = new List<PriceLookupBenchmarkSample>();
        internal bool DataEquivalent { get; set; }
        internal bool MeasurementValid { get; set; }
        internal bool Cancelled { get; set; }
        internal long PositiveControlGcSamples { get; set; }
        internal long UncachedGcSamples { get; set; }
        internal long CachedGcSamples { get; set; }
        internal double UncachedMedianMilliseconds { get; private set; }
        internal double UncachedP95Milliseconds { get; private set; }
        internal double CachedMedianMilliseconds { get; private set; }
        internal double CachedP95Milliseconds { get; private set; }
        internal double MedianReductionPercent { get; private set; }
        internal string CsvPath { get; set; } = string.Empty;
        internal string FailureReason { get; set; } = string.Empty;

        internal PriceLookupBenchmarkResult(PriceLookupBenchmarkSettings settings, int priceKeyCount)
        {
            Settings = settings.Clone();
            PriceKeyCount = priceKeyCount;
        }

        internal void CalculateStatistics()
        {
            if (Samples.Count == 0)
                return;

            var uncached = new double[Samples.Count];
            var cached = new double[Samples.Count];
            for (int i = 0; i < Samples.Count; i++)
            {
                uncached[i] = Samples[i].UncachedMilliseconds;
                cached[i] = Samples[i].CachedMilliseconds;
            }

            Array.Sort(uncached);
            Array.Sort(cached);
            UncachedMedianMilliseconds = Percentile(uncached, 0.5d);
            UncachedP95Milliseconds = Percentile(uncached, 0.95d);
            CachedMedianMilliseconds = Percentile(cached, 0.5d);
            CachedP95Milliseconds = Percentile(cached, 0.95d);
            MedianReductionPercent = UncachedMedianMilliseconds > 0d
                ? (UncachedMedianMilliseconds - CachedMedianMilliseconds) / UncachedMedianMilliseconds * 100d
                : 0d;
        }

        private static double Percentile(double[] sortedValues, double percentile)
        {
            if (sortedValues.Length == 1)
                return sortedValues[0];

            double position = percentile * (sortedValues.Length - 1);
            int lowerIndex = (int)Math.Floor(position);
            int upperIndex = (int)Math.Ceiling(position);
            if (lowerIndex == upperIndex)
                return sortedValues[lowerIndex];

            double weight = position - lowerIndex;
            return sortedValues[lowerIndex]
                   + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * weight;
        }
    }

    internal readonly struct PriceLookupBenchmarkSample
    {
        internal int Iteration { get; }
        internal bool UncachedFirst { get; }
        internal double UncachedMilliseconds { get; }
        internal double CachedMilliseconds { get; }
        internal long Checksum { get; }

        internal PriceLookupBenchmarkSample(
            int iteration,
            bool uncachedFirst,
            double uncachedMilliseconds,
            double cachedMilliseconds,
            long checksum)
        {
            Iteration = iteration;
            UncachedFirst = uncachedFirst;
            UncachedMilliseconds = uncachedMilliseconds;
            CachedMilliseconds = cachedMilliseconds;
            Checksum = checksum;
        }
    }
}
