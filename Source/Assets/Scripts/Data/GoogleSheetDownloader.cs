#if UNITY_EDITOR
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace SeaVillage.Data
{
    /// <summary>
    /// Google Spreadsheet에서 CSV를 자동 다운로드하고, DataConverter와 연계되는 툴
    /// <para>
    /// 사용법: <br/>
    ///   Unity 메뉴 → SeaVillage → Google Sheet Downloader
    /// </para>
    /// </summary>
    public class GoogleSheetDownloader : EditorWindow
    {
        private GoogleSheetSettings settings;
        private Vector2 scrollPos;
        private bool isDownloading;
        private string statusMessage = "";
        private int downloadedCount;
        private int totalCount;
        private bool autoConvertAfterDownload = true;

        private static readonly HttpClient httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        [MenuItem("SeaVillage/Google Sheet Downloader")]
        public static void ShowWindow()
        {
            var window = GetWindow<GoogleSheetDownloader>("Google Sheet Downloader");
            window.minSize = new Vector2(450, 400);
        }

        private void OnEnable()
        {
            FindSettings();
            EnsureDefaultSheetMappings();
        }

        private void FindSettings()
        {
            // 프로젝트 전역에서 GoogleSheetSettings 에셋 자동 탐색
            var guids = AssetDatabase.FindAssets("t:GoogleSheetSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                settings = AssetDatabase.LoadAssetAtPath<GoogleSheetSettings>(path);
            }
        }

        private void EnsureDefaultSheetMappings()
        {
            if (settings == null)
                return;

            if (!settings.EnsureDefaultMappings())
                return;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private void OnGUI()
        {
            GUILayout.Label("Google Sheet Downloader", EditorStyles.boldLabel);
            GUILayout.Space(5);

            // 설정 참조
            EditorGUI.BeginChangeCheck();
            settings = (GoogleSheetSettings)EditorGUILayout.ObjectField(
                "Settings", settings, typeof(GoogleSheetSettings), false);
            if (EditorGUI.EndChangeCheck() && settings == null)
                FindSettings();

            EnsureDefaultSheetMappings();

            if (settings == null)
            {
                EditorGUILayout.HelpBox(
                    "GoogleSheetSettings 에셋이 필요합니다.\n" +
                    "Project 창 우클릭 -> Create -> SeaVillage -> Google Sheet Settings",
                    MessageType.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.spreadsheetId))
            {
                EditorGUILayout.HelpBox(
                    "Google Spreadsheet ID가 비어 있습니다.\n" +
                    "GoogleSheetSettings 에셋의 Spreadsheet Id 필드를 채워주세요.\n" +
                    "URL 예: https://docs.google.com/spreadsheets/d/여기가_ID/edit",
                    MessageType.Error);
            }

            GUILayout.Space(10);

            autoConvertAfterDownload = EditorGUILayout.Toggle(
                "다운로드 후 자동 변환 (CSV -> SO)", autoConvertAfterDownload);

            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(isDownloading || string.IsNullOrWhiteSpace(settings.spreadsheetId));

            if (GUILayout.Button("전체 다운로드", GUILayout.Height(30)))
            {
                DownloadAllAsync();
            }

            GUILayout.Space(5);

            GUILayout.Label("개별 시트 다운로드", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            for (int i = 0; i < settings.sheetMappings.Count; i++)
            {
                var mapping = settings.sheetMappings[i];
                EditorGUILayout.BeginHorizontal();

                mapping.enabled = EditorGUILayout.Toggle(mapping.enabled, GUILayout.Width(20));

                EditorGUILayout.LabelField(mapping.sheetName, GUILayout.Width(180));
                EditorGUILayout.LabelField("-> " + mapping.csvFileName, GUILayout.MinWidth(140));

                if (GUILayout.Button("다운로드", GUILayout.Width(70)))
                {
                    DownloadSingleAsync(mapping);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUI.EndDisabledGroup();

            if (isDownloading)
            {
                EditorGUILayout.Space(5);
                var rect = EditorGUILayout.GetControlRect(false, 20);
                float progress = totalCount > 0 ? (float)downloadedCount / totalCount : 0f;
                EditorGUI.ProgressBar(rect, progress, $"{downloadedCount} / {totalCount}");
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
            }
        }

        #region Download
        private async void DownloadAllAsync()
        {
            isDownloading = true;
            downloadedCount = 0;
            statusMessage = "다운로드 시작...";

            var enabledMappings = settings.sheetMappings.FindAll(m => m.enabled);
            totalCount = enabledMappings.Count;

            int successCount = 0;
            int failCount = 0;

            foreach (var mapping in enabledMappings)
            {
                statusMessage = $"다운로드 중: {mapping.sheetName}...";
                Repaint();

                bool ok = await DownloadSheet(mapping);
                if (ok) successCount++;
                else failCount++;

                downloadedCount++;
                Repaint();
            }

            statusMessage = $"완료 — 성공: {successCount}, 실패: {failCount}";
            isDownloading = false;
            Repaint();

            AssetDatabase.Refresh();

            if (autoConvertAfterDownload && successCount > 0)
            {
                bool converted = DataConverter.ConvertAllFromCode();
                statusMessage += converted
                    ? " | CSV -> SO 변환 완료"
                    : " | CSV 검증 실패: 기존 TutorialDatabase 유지";
                Repaint();
            }
        }

        private async void DownloadSingleAsync(SheetMapping mapping)
        {
            isDownloading = true;
            totalCount = 1;
            downloadedCount = 0;
            statusMessage = $"다운로드 중: {mapping.sheetName}...";
            Repaint();

            bool ok = await DownloadSheet(mapping);
            downloadedCount = 1;

            statusMessage = ok
                ? $"'{mapping.sheetName}' 다운로드 완료"
                : $"'{mapping.sheetName}' 다운로드 실패";

            isDownloading = false;
            Repaint();

            AssetDatabase.Refresh();

            if (autoConvertAfterDownload && ok)
            {
                bool converted = DataConverter.ConvertAllFromCode();
                statusMessage += converted
                    ? " | CSV -> SO 변환 완료"
                    : " | CSV 검증 실패: 기존 TutorialDatabase 유지";
                Repaint();
            }
        }
        #endregion

        /// <summary>
        /// Sheets의 직접 CSV 내보내기 URL 생성
        /// </summary>
        private string BuildDownloadUrl(string sheetName)
        {
            return $"https://docs.google.com/spreadsheets/d/{settings.spreadsheetId}" +
                   $"/gviz/tq?tqx=out:csv&sheet={Uri.EscapeDataString(sheetName)}";
        }

        /// <summary>
        /// 단일 시트를 다운로드하여 UTF-8 CSV로 저장
        /// </summary>
        private async Task<bool> DownloadSheet(SheetMapping mapping)
        {
            string tempPath = null;
            try
            {
                string url = BuildDownloadUrl(mapping.sheetName);

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                byte[] rawBytes = await response.Content.ReadAsByteArrayAsync();
                string csvContent = Encoding.UTF8.GetString(rawBytes);

                // HTML 응답 감지 (로그인 리다이렉트 방어)
                if (csvContent.TrimStart().StartsWith("<", StringComparison.Ordinal))
                {
                    Debug.LogError(
                        $"[GoogleSheetDownloader] '{mapping.sheetName}' 다운로드 실패: CSV 대신 HTML이 반환되었습니다.\n" +
                        "스프레드시트 공유 설정을 확인하세요:\n" +
                        "  → '링크가 있는 모든 사용자에게 뷰어 권한' 으로 설정 필요");
                    return false;
                }

                // UTF-8로 저장 (한글 깨짐 방지)
                string savePath = Path.Combine(DataManager.CSV_FOLDER_PATH, mapping.csvFileName);
                string directory = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                tempPath = savePath + ".tmp";
                File.WriteAllText(tempPath, csvContent, new UTF8Encoding(true));

                if (File.Exists(savePath))
                    File.Replace(tempPath, savePath, null);
                else
                    File.Move(tempPath, savePath);

                Debug.Log($"[GoogleSheetDownloader] 다운로드 완료: {mapping.sheetName} -> {savePath}");
                return true;
            }
            catch (Exception e)
            {
                if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }

                Debug.LogError($"[GoogleSheetDownloader] 다운로드 실패: {mapping.sheetName}\n{e.Message}");
                return false;
            }
        }
    }
}
#endif
