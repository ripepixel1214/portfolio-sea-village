using System;
using SeaVillage.Core;
using SeaVillage.Data;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SeaVillage.Editor
{
    [InitializeOnLoad]
    public sealed class TutorialDefinitionValidator : IPreprocessBuildWithReport
    {
        private const string DialogueDatabasePath =
            "Assets/Resources/Data/ScriptableObjects/TutorialDatabase.asset";

        static TutorialDefinitionValidator()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!TryValidate(out string failReason))
                throw new BuildFailedException($"튜토리얼 정의 검증 실패: {failReason}");
        }

        [MenuItem("SeaVillage/Tutorial/Validate Definitions")]
        public static void ValidateFromMenu()
        {
            if (!TryValidate(out string failReason))
                throw new InvalidOperationException($"튜토리얼 정의 검증 실패: {failReason}");

            Debug.Log("[TutorialDefinitionValidator] 튜토리얼 정의 검증 통과");
        }

        public static bool TryValidate(out string failReason)
        {
            failReason = string.Empty;
            TutorialDatabase database = AssetDatabase.LoadAssetAtPath<TutorialDatabase>(DialogueDatabasePath);
            if (database == null)
            {
                failReason = $"TutorialDatabase를 찾을 수 없습니다: {DialogueDatabasePath}";
                return false;
            }

            TutorialRepository repository = TutorialDefinitionCatalog.CreateRepository(database.Tutorials);
            return repository.TryValidate(out failReason);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode || TryValidate(out string failReason))
                return;

            EditorApplication.isPlaying = false;
            Debug.LogError($"[TutorialDefinitionValidator] Play Mode 진입 중단: {failReason}");
        }
    }
}
