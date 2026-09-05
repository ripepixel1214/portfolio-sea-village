using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SeaVillage.Editor.PriceLookupBenchmarks
{
    internal sealed class PriceLookupPerformanceWindow : EditorWindow
    {
        private const float LabelWidth = 190f;
        private const float ValueWidth = 150f;

        private readonly PriceLookupBenchmarkSettings _settings = new PriceLookupBenchmarkSettings();
        private PriceLookupBenchmarkResult _result;
        private Vector2 _scrollPosition;
        private string _runError = string.Empty;

        [MenuItem("SeaVillage/Performance/Price Lookup Performance Lab")]
        private static void Open()
        {
            var window = GetWindow<PriceLookupPerformanceWindow>();
            window.titleContent = new GUIContent("Price Lookup Lab");
            window.minSize = new Vector2(560f, 620f);
            window.Show();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            DrawHeader();
            EditorGUILayout.Space(8f);
            DrawSettings();
            EditorGUILayout.Space(10f);
            DrawRunButton();
            EditorGUILayout.Space(12f);
            DrawResults();
            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("PRICE LOOKUP PERFORMANCE LAB", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "동일한 합성 데이터에서 카테고리 변동을 매번 계산하는 조회와 "
                + "가격 키별 파생 배율 캐시 조회를 비교합니다. 캐시 생성 비용은 측정 범위에 포함하지 않습니다.",
                MessageType.Info);
        }

        private void DrawSettings()
        {
            EditorGUILayout.LabelField("SETTINGS", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _settings.ItemCount = DrawIntField("Item Count", _settings.ItemCount, 1, 10_000);
                _settings.TownCount = DrawIntField("Town Count", _settings.TownCount, 1, 100);
                _settings.ActiveRuleCount = DrawIntField("Active Rule Count", _settings.ActiveRuleCount, 1, 10_000);
                _settings.LookupCount = DrawIntField("Lookups per Measurement", _settings.LookupCount, 1, 10_000_000);
                _settings.WarmupCount = DrawIntField("Warmup Count", _settings.WarmupCount, 0, 100);
                _settings.MeasurementCount = DrawIntField("Measurement Count", _settings.MeasurementCount, 1, 1_000);

                long priceKeyCount = (long)_settings.ItemCount * _settings.TownCount;
                DrawReadOnlyRow("Price Key Count", priceKeyCount.ToString("N0"));
            }
        }

        private void DrawRunButton()
        {
            if (GUILayout.Button("RUN BENCHMARK", GUILayout.Height(34f)))
                RunBenchmark();

            if (!string.IsNullOrEmpty(_runError))
                EditorGUILayout.HelpBox(_runError, MessageType.Error);
        }

        private void DrawResults()
        {
            EditorGUILayout.LabelField("RESULT", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (_result == null)
                {
                    EditorGUILayout.LabelField("No benchmark result");
                    return;
                }

                DrawStatusRow("DATA EQUIVALENCE", _result.DataEquivalent);
                DrawStatusRow("MEASUREMENT VALIDITY", _result.MeasurementValid);

                if (_result.Cancelled)
                {
                    EditorGUILayout.HelpBox("측정이 취소되었습니다", MessageType.Warning);
                    return;
                }

                if (!string.IsNullOrEmpty(_result.FailureReason))
                {
                    EditorGUILayout.HelpBox(_result.FailureReason, MessageType.Error);
                    return;
                }

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("WITHOUT CACHE", EditorStyles.boldLabel);
                DrawReadOnlyRow("Median", FormatMilliseconds(_result.UncachedMedianMilliseconds));
                DrawReadOnlyRow("P95", FormatMilliseconds(_result.UncachedP95Milliseconds));
                DrawReadOnlyRow("GC.Alloc", $"{_result.UncachedGcSamples:N0} samples");

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("WITH CACHE", EditorStyles.boldLabel);
                DrawReadOnlyRow("Median", FormatMilliseconds(_result.CachedMedianMilliseconds));
                DrawReadOnlyRow("P95", FormatMilliseconds(_result.CachedP95Milliseconds));
                DrawReadOnlyRow("GC.Alloc", $"{_result.CachedGcSamples:N0} samples");

                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("LOOKUP TIME REDUCTION", EditorStyles.boldLabel);
                DrawReadOnlyRow("Median Reduction", $"{_result.MedianReductionPercent:F1}%");

                EditorGUILayout.Space(6f);
                DrawReadOnlyRow("Positive Control", $"{_result.PositiveControlGcSamples:N0} GC.Alloc samples");
                DrawReadOnlyRow(
                    "Synthetic Workload",
                    $"{_result.PriceKeyCount:N0} keys × {_result.Settings.LookupCount:N0} lookups");

                EditorGUILayout.Space(6f);
                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_result.CsvPath)))
                {
                    if (GUILayout.Button("SHOW RESULT CSV"))
                        EditorUtility.RevealInFinder(_result.CsvPath);
                }
            }
        }

        private void RunBenchmark()
        {
            _runError = string.Empty;
            _result = null;

            try
            {
                _result = PriceLookupBenchmarkRunner.Run(_settings);
            }
            catch (Exception exception)
            {
                _runError = exception.Message;
                Debug.LogException(exception);
            }

            Repaint();
        }

        private static int DrawIntField(string label, int value, int minimum, int maximum)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(LabelWidth));
                int nextValue = EditorGUILayout.IntField(value, GUILayout.Width(ValueWidth));
                return Mathf.Clamp(nextValue, minimum, maximum);
            }
        }

        private static void DrawStatusRow(string label, bool passed)
        {
            Color previousColor = GUI.contentColor;
            GUI.contentColor = passed ? new Color(0.3f, 0.8f, 0.4f) : new Color(1f, 0.4f, 0.35f);
            DrawReadOnlyRow(label, passed ? "PASS" : "FAIL");
            GUI.contentColor = previousColor;
        }

        private static void DrawReadOnlyRow(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(LabelWidth));
                EditorGUILayout.SelectableLabel(value, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private static string FormatMilliseconds(double milliseconds)
        {
            return $"{milliseconds:F3} ms";
        }
    }
}
