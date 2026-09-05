using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SeaVillage.EditorTools
{
    /// <summary>
    /// Save/Load 직렬화 비교 설정과 핵심 결과를 표시하는 로컬 전용 창
    /// </summary>
    internal sealed class SaveLoadBenchmarkResultWindow : EditorWindow
    {
        private const string MenuPath = "SeaVillage/Benchmarks/Save Load/Performance Lab";
        private const string ResultsRelativeDirectory = "UserSettings/SaveLoadBenchmarks/Results";
        private const string LegacyImplementation = "LegacyJsonUtility";
        private const string CurrentImplementation = "CurrentMemoryPack";
        private const string HarnessImplementation = "Harness";
        private const string SerializePhase = "Serialize";
        private const string PositiveControlPhase = "AllocationPositiveControl";

        [SerializeField] private float _targetSizeMiB = 1f;
        [SerializeField] private int _warmupCount = 10;
        [SerializeField] private int _measurementCount = 100;

        private BenchmarkReport _report;
        private bool _isRunScheduled;
        private bool _runStatusIsError;
        private string _runStatus = string.Empty;

        #region Unity Lifecycle

        private void OnEnable()
        {
            minSize = new Vector2(640f, 390f);
            maxSize = new Vector2(820f, 520f);
            LoadLatestReport();
        }

        private void OnDisable()
        {
            EditorApplication.delayCall -= RunConfiguredBenchmark;
        }

        private void OnGUI()
        {
            GUILayout.Space(8f);
            GUILayout.Label("Serialization Benchmark", CreateLabelStyle(16, FontStyle.Bold));
            GUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            DrawConfigurationPanel();
            GUILayout.Space(8f);
            DrawResultPanel();
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Menu

        [MenuItem(MenuPath)]
        private static void OpenWindow()
        {
            SaveLoadBenchmarkResultWindow window = GetWindow<SaveLoadBenchmarkResultWindow>();
            window.titleContent = new GUIContent("Serialization Benchmark");
            window.position = new Rect(window.position.x, window.position.y, 700f, 430f);
            window.Show();
            window.Focus();
        }

        #endregion

        #region Drawing

        private void DrawConfigurationPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(300f), GUILayout.ExpandHeight(true));
            GUILayout.Label("Configuration", CreateLabelStyle(13, FontStyle.Bold));
            GUILayout.Space(8f);

            _targetSizeMiB = EditorGUILayout.FloatField(
                new GUIContent("Dataset size (MB)", "목표 MemoryPack payload 크기"),
                _targetSizeMiB);
            _warmupCount = EditorGUILayout.IntField("Warm-up runs", _warmupCount);
            _measurementCount = EditorGUILayout.IntField("Measurement runs", _measurementCount);

            GUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "기준 SaveData를 깊은 복사해 목표 크기에 가까운 데이터셋을 구성합니다.",
                MessageType.None);
            GUILayout.FlexibleSpace();

            bool isBusy = _isRunScheduled || SaveLoadBenchmarkRunner.IsRunning;
            using (new EditorGUI.DisabledScope(isBusy))
            {
                if (GUILayout.Button(isBusy ? "Running..." : "Run Benchmark", GUILayout.Height(34f)))
                    ScheduleBenchmark();
            }

            if (!string.IsNullOrEmpty(_runStatus))
                EditorGUILayout.HelpBox(_runStatus, _runStatusIsError ? MessageType.Error : MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        private void DrawResultPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUILayout.Label("Results", CreateLabelStyle(13, FontStyle.Bold));
            GUILayout.Space(8f);

            if (_report == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(
                    "Set the dataset size and run the benchmark.",
                    CreateLabelStyle(12, FontStyle.Normal, TextAnchor.MiddleCenter));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                return;
            }

            MeasurementSummary legacy = _report.GetSummary(LegacyImplementation, SerializePhase);
            MeasurementSummary current = _report.GetSummary(CurrentImplementation, SerializePhase);
            long jsonSize = _report.GetMetadataLong("json_payload_size_bytes");
            long memoryPackSize = _report.GetMetadataLong("memorypack_payload_size_bytes");

            DrawSectionTitle("Dataset");
            DrawValueRow("SaveData copies", _report.GetMetadata("dataset_count", "-"));
            DrawValueRow("Measurements", _report.GetMetadata("measurement_count", "-") + " runs");

            GUILayout.Space(8f);
            DrawSectionTitle("File Size");
            DrawValueRow("JSON", FormatBytes(jsonSize));
            DrawValueRow("MemoryPack", FormatBytes(memoryPackSize));
            DrawValueRow("Reduction", FormatPercent(CalculateReduction(jsonSize, memoryPackSize)));

            GUILayout.Space(8f);
            DrawSectionTitle("Serialization Time (average)");
            DrawValueRow("JsonUtility", $"{legacy.AverageMilliseconds:F3} ms");
            DrawValueRow("MemoryPack", $"{current.AverageMilliseconds:F3} ms");
            DrawValueRow("Speedup", $"{DivideOrZero(legacy.AverageMilliseconds, current.AverageMilliseconds):F2}x");

            GUILayout.Space(8f);
            DrawSectionTitle("GC.Alloc Events (average)");
            DrawValueRow("JsonUtility", legacy.AverageAllocationCount.ToString("F1"));
            DrawValueRow("MemoryPack", current.AverageAllocationCount.ToString("F1"));
            DrawValueRow("Reduction", FormatPercent(CalculateReduction(
                legacy.AverageAllocationCount,
                current.AverageAllocationCount)));

            GUILayout.FlexibleSpace();
            bool isValid = _report.DataEquivalencePassed && _report.PositiveControlPassed;
            GUIStyle validationStyle = CreateLabelStyle(11, FontStyle.Bold, TextAnchor.MiddleCenter);
            Color previousColor = GUI.color;
            GUI.color = isValid ? new Color(0.35f, 0.75f, 0.45f) : new Color(0.9f, 0.35f, 0.35f);
            GUILayout.Label(isValid ? "Validation: PASS" : "Validation: FAIL", validationStyle);
            GUI.color = previousColor;

            EditorGUILayout.EndVertical();
        }

        private static void DrawSectionTitle(string title)
        {
            GUILayout.Label(title, CreateLabelStyle(11, FontStyle.Bold));
        }

        private static void DrawValueRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(130f));
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, CreateLabelStyle(11, FontStyle.Bold, TextAnchor.MiddleRight));
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Benchmark Execution

        private void ScheduleBenchmark()
        {
            _isRunScheduled = true;
            _runStatusIsError = false;
            _runStatus = "Preparing benchmark...";
            Repaint();
            EditorApplication.delayCall -= RunConfiguredBenchmark;
            EditorApplication.delayCall += RunConfiguredBenchmark;
        }

        private void RunConfiguredBenchmark()
        {
            EditorApplication.delayCall -= RunConfiguredBenchmark;
            try
            {
                EditorUtility.DisplayProgressBar(
                    "Serialization Benchmark",
                    "Comparing JsonUtility and MemoryPack...",
                    0.5f);

                if (SaveLoadBenchmarkRunner.TryRunSnapshotPipeline(
                        _targetSizeMiB,
                        _warmupCount,
                        _measurementCount,
                        out string resultPath,
                        out string error))
                {
                    _runStatus = string.Empty;
                    _runStatusIsError = false;
                    LoadReport(resultPath);
                }
                else
                {
                    _runStatus = error;
                    _runStatusIsError = true;
                }
            }
            catch (Exception exception)
            {
                _runStatus = $"Benchmark failed: {exception.Message}";
                _runStatusIsError = true;
                Debug.LogError($"[Save Load Benchmark] 통합 도구 실행 실패: {exception}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isRunScheduled = false;
                Repaint();
            }
        }

        #endregion

        #region Result Loading

        private void LoadLatestReport()
        {
            string resultsDirectory = GetResultsDirectory();
            if (!Directory.Exists(resultsDirectory))
                return;

            string[] paths = Directory.GetFiles(resultsDirectory, "snapshot-pipeline-*.csv");
            Array.Sort(paths, (left, right) =>
                File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left)));

            for (int i = 0; i < paths.Length; i++)
            {
                if (!IsCurrentResult(paths[i]))
                    continue;

                LoadReport(paths[i]);
                return;
            }
        }

        private void LoadReport(string resultPath)
        {
            try
            {
                BenchmarkReport report = BenchmarkReport.Parse(resultPath);
                report.Validate();
                _report = report;
            }
            catch (Exception exception)
            {
                _report = null;
                _runStatus = $"Result load failed: {exception.Message}";
                _runStatusIsError = true;
            }
        }

        private static bool IsCurrentResult(string path)
        {
            string contents = File.ReadAllText(path);
            return contents.Contains("# dataset_count,") &&
                   contents.Contains("# json_utility_compatible,True") &&
                   contents.Contains(LegacyImplementation + "," + SerializePhase + ",") &&
                   contents.Contains(CurrentImplementation + "," + SerializePhase + ",");
        }

        private static string GetResultsDirectory()
        {
            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
                throw new InvalidOperationException("프로젝트 루트 경로를 확인할 수 없습니다");

            return Path.Combine(projectRoot.FullName, ResultsRelativeDirectory);
        }

        #endregion

        #region Helpers

        private static GUIStyle CreateLabelStyle(
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            return new GUIStyle(EditorStyles.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = alignment
            };
        }

        private static double DivideOrZero(double dividend, double divisor)
        {
            return divisor > 0d ? dividend / divisor : 0d;
        }

        private static double CalculateReduction(double before, double after)
        {
            return before > 0d ? (before - after) / before * 100d : 0d;
        }

        private static string FormatPercent(double value)
        {
            return $"{value:F1}%";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L)
                return $"{bytes / (1024d * 1024d):F2} MB";
            if (bytes >= 1024L)
                return $"{bytes / 1024d:F1} KB";
            return $"{bytes} B";
        }

        #endregion

        private sealed class BenchmarkReport
        {
            private readonly Dictionary<string, string> _metadata =
                new Dictionary<string, string>(StringComparer.Ordinal);

            private readonly Dictionary<string, List<BenchmarkSample>> _groups =
                new Dictionary<string, List<BenchmarkSample>>(StringComparer.Ordinal);

            public bool DataEquivalencePassed =>
                string.Equals(GetMetadata("json_utility_compatible", "False"), "True", StringComparison.OrdinalIgnoreCase);

            public bool PositiveControlPassed =>
                GetSummary(HarnessImplementation, PositiveControlPhase).MaximumAllocationCount > 0;

            public static BenchmarkReport Parse(string resultPath)
            {
                if (!File.Exists(resultPath))
                    throw new FileNotFoundException("CSV 파일을 찾을 수 없습니다", resultPath);

                var report = new BenchmarkReport();
                string[] lines = File.ReadAllLines(resultPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("implementation,"))
                        continue;

                    if (line.StartsWith("# "))
                    {
                        int separatorIndex = line.IndexOf(',');
                        if (separatorIndex > 2)
                        {
                            string metadataKey = line.Substring(2, separatorIndex - 2);
                            report._metadata[metadataKey] = line.Substring(separatorIndex + 1);
                        }

                        continue;
                    }

                    string[] columns = line.Split(',');
                    if (columns.Length != 5 ||
                        !double.TryParse(columns[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double milliseconds) ||
                        !long.TryParse(columns[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out long allocationCount))
                    {
                        throw new InvalidDataException($"CSV {i + 1}행 형식이 올바르지 않습니다");
                    }

                    string groupKey = CreateGroupKey(columns[0], columns[1]);
                    if (!report._groups.TryGetValue(groupKey, out List<BenchmarkSample> samples))
                    {
                        samples = new List<BenchmarkSample>();
                        report._groups.Add(groupKey, samples);
                    }

                    samples.Add(new BenchmarkSample(milliseconds, allocationCount));
                }

                return report;
            }

            public string GetMetadata(string key, string fallback)
            {
                return _metadata.TryGetValue(key, out string value) ? value : fallback;
            }

            public long GetMetadataLong(string key)
            {
                return _metadata.TryGetValue(key, out string value) &&
                       long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result)
                    ? result
                    : 0L;
            }

            public MeasurementSummary GetSummary(string implementation, string phase)
            {
                string key = CreateGroupKey(implementation, phase);
                if (!_groups.TryGetValue(key, out List<BenchmarkSample> samples) || samples.Count == 0)
                    return default;

                double totalMilliseconds = 0d;
                long totalAllocationCount = 0L;
                long maximumAllocationCount = 0L;
                for (int i = 0; i < samples.Count; i++)
                {
                    totalMilliseconds += samples[i].Milliseconds;
                    totalAllocationCount += samples[i].AllocationCount;
                    maximumAllocationCount = Math.Max(maximumAllocationCount, samples[i].AllocationCount);
                }

                return new MeasurementSummary(
                    totalMilliseconds / samples.Count,
                    totalAllocationCount / (double)samples.Count,
                    maximumAllocationCount);
            }

            public void Validate()
            {
                if (!DataEquivalencePassed)
                    throw new InvalidDataException("데이터 동일성 검증을 통과하지 못했습니다");

                RequireGroup(LegacyImplementation, SerializePhase);
                RequireGroup(CurrentImplementation, SerializePhase);
                RequireGroup(HarnessImplementation, PositiveControlPhase);
            }

            private void RequireGroup(string implementation, string phase)
            {
                string key = CreateGroupKey(implementation, phase);
                if (!_groups.TryGetValue(key, out List<BenchmarkSample> samples) || samples.Count == 0)
                    throw new InvalidDataException($"필수 측정 그룹이 없습니다: {implementation}/{phase}");
            }

            private static string CreateGroupKey(string implementation, string phase)
            {
                return implementation + "/" + phase;
            }
        }

        private readonly struct BenchmarkSample
        {
            public BenchmarkSample(double milliseconds, long allocationCount)
            {
                Milliseconds = milliseconds;
                AllocationCount = allocationCount;
            }

            public double Milliseconds { get; }
            public long AllocationCount { get; }
        }

        private readonly struct MeasurementSummary
        {
            public MeasurementSummary(
                double averageMilliseconds,
                double averageAllocationCount,
                long maximumAllocationCount)
            {
                AverageMilliseconds = averageMilliseconds;
                AverageAllocationCount = averageAllocationCount;
                MaximumAllocationCount = maximumAllocationCount;
            }

            public double AverageMilliseconds { get; }
            public double AverageAllocationCount { get; }
            public long MaximumAllocationCount { get; }
        }
    }
}
