#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace SeaVillage.Utilities.Editor
{
    /// <summary>
    /// SceneChanger용 에디터 유틸리티
    /// </summary>
    public static class SceneChangerEditor
    {
        [MenuItem("SeaVillage/Scene Changer/Create Scene Changer")]
        public static void CreateSceneChanger()
        {
            // 이미 존재하는지 확인
            SceneChanger existingChanger = Object.FindFirstObjectByType<SceneChanger>();
            if (existingChanger != null)
            {
                Debug.LogWarning("SceneChanger가 이미 Scene에 존재합니다.");
                Selection.activeGameObject = existingChanger.gameObject;
                return;
            }

            // 새로운 SceneChanger 생성
            GameObject changerObj = new GameObject("SceneChanger");
            SceneChanger changer = changerObj.AddComponent<SceneChanger>();
            
            // Undo 등록
            Undo.RegisterCreatedObjectUndo(changerObj, "Create SceneChanger");
            
            // 선택 및 포커스
            Selection.activeGameObject = changerObj;
            EditorGUIUtility.PingObject(changerObj);
            
            Debug.Log("SceneChanger가 생성되었습니다.");
        }

        [MenuItem("SeaVillage/Scene Changer/Find Scene Changer")]
        public static void FindSceneChanger()
        {
            SceneChanger changer = Object.FindFirstObjectByType<SceneChanger>();
            if (changer != null)
            {
                Selection.activeGameObject = changer.gameObject;
                EditorGUIUtility.PingObject(changer.gameObject);
                Debug.Log($"SceneChanger를 찾았습니다: {changer.name}");
            }
            else
            {
                Debug.LogWarning("Scene에서 SceneChanger를 찾을 수 없습니다.");
            }
        }

        [MenuItem("SeaVillage/Scene Changer/Reload Current Scene", false, 100)]
        public static void ReloadCurrentScene()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Scene 재로딩은 플레이 모드에서만 가능합니다.");
                return;
            }

            SceneChanger changer = SceneChanger.Instance;
            if (changer != null)
            {
                changer.ReloadCurrentScene();
                Debug.Log("현재 Scene 재로딩 시작");
            }
            else
            {
                Debug.LogError("SceneChanger 인스턴스를 찾을 수 없습니다.");
            }
        }

        [MenuItem("SeaVillage/Scene Changer/List Build Scenes", false, 200)]
        public static void ListBuildScenes()
        {
            Debug.Log("=== Build Settings에 등록된 Scene 목록 ===");
            
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                bool isEnabled = EditorBuildSettings.scenes[i].enabled;
                
                Debug.Log($"[{i}] {sceneName} ({scenePath}) - {(isEnabled ? "Enabled" : "Disabled")}");
            }
            
            if (SceneManager.sceneCountInBuildSettings == 0)
            {
                Debug.LogWarning("Build Settings에 Scene이 등록되지 않았습니다.");
            }
        }

        [MenuItem("SeaVillage/Scene Changer/Open Build Settings")]
        public static void OpenBuildSettings()
        {
            EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
        }

        // 메뉴 항목 활성화 조건
        [MenuItem("SeaVillage/Scene Changer/Reload Current Scene", true)]
        public static bool ValidateReloadScene()
        {
            return Application.isPlaying;
        }
    }

    /// <summary>
    /// SceneChanger 커스텀 인스펙터
    /// </summary>
    [CustomEditor(typeof(SceneChanger))]
    public class SceneChangerInspector : UnityEditor.Editor
    {
        private SceneChanger sceneChanger;
        private string testSceneName = "";
        
        private void OnEnable()
        {
            sceneChanger = (SceneChanger)target;
        }

        public override void OnInspectorGUI()
        {
            // 기본 인스펙터 표시
            DrawDefaultInspector();
            
            EditorGUILayout.Space(10);

            // 현재 상태 표시
            EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
            
            GUI.enabled = false;
            EditorGUILayout.Toggle("Is Transitioning", Application.isPlaying ? sceneChanger.IsTransitioning : false);
            EditorGUILayout.TextField("Current Scene", Application.isPlaying ? sceneChanger.CurrentSceneName : SceneManager.GetActiveScene().name);
            EditorGUILayout.Slider("Loading Progress", Application.isPlaying ? sceneChanger.LoadingProgress : 0f, 0f, 1f);
            GUI.enabled = true;
            
            EditorGUILayout.Space(10);
            
            // Build Settings 정보
            EditorGUILayout.LabelField("Build Settings Info", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Total Scenes: {SceneManager.sceneCountInBuildSettings}");
            
            if (GUILayout.Button("List All Build Scenes"))
            {
                SceneChangerEditor.ListBuildScenes();
            }
            
            if (GUILayout.Button("Open Build Settings"))
            {
                SceneChangerEditor.OpenBuildSettings();
            }
            
            EditorGUILayout.Space(10);
            
            // 플레이 모드 전용 기능
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Runtime Tests", EditorStyles.boldLabel);
                
                // Scene 이름 입력
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Scene Name:", GUILayout.Width(80));
                testSceneName = EditorGUILayout.TextField(testSceneName);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Change Scene"))
                {
                    if (!string.IsNullOrEmpty(testSceneName))
                    {
                        if (sceneChanger.IsSceneInBuildSettings(testSceneName))
                        {
                            sceneChanger.ChangeScene(testSceneName);
                        }
                        else
                        {
                            Debug.LogError($"Scene '{testSceneName}'이 Build Settings에 없습니다.");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("Scene 이름을 입력하세요.");
                    }
                }
                
                if (GUILayout.Button("Validate Scene"))
                {
                    if (!string.IsNullOrEmpty(testSceneName))
                    {
                        bool exists = sceneChanger.IsSceneInBuildSettings(testSceneName);
                        Debug.Log($"Scene '{testSceneName}' 존재 여부: {exists}");
                    }
                    else
                    {
                        Debug.LogWarning("Scene 이름을 입력하세요.");
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                // 빠른 전환 버튼들
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Reload Current"))
                {
                    sceneChanger.ReloadCurrentScene();
                }
                
                if (GUILayout.Button("Cancel Transition") && sceneChanger.IsTransitioning)
                {
                    sceneChanger.CancelTransition();
                }
                
                EditorGUILayout.EndHorizontal();
                
                // 인덱스 기반 빠른 Scene 전환
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Quick Scene Changes", EditorStyles.miniLabel);
                
                EditorGUILayout.BeginHorizontal();
                
                // 이후 씬 개수가 5개 이상이 된다면 수정 필요
                for (int i = 0; i < Mathf.Min(4, SceneManager.sceneCountInBuildSettings); i++)
                {
                    string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                    if (GUILayout.Button($"[{i}] {sceneName}", GUILayout.MaxWidth(100)))
                    {
                        sceneChanger.ChangeScene(i);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                if (SceneManager.sceneCountInBuildSettings > 4)
                {
                    EditorGUILayout.HelpBox($"총 {SceneManager.sceneCountInBuildSettings}개 Scene이 있습니다. 'List All Build Scenes' 버튼으로 전체 목록을 확인하세요.", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("런타임 테스트 기능은 플레이 모드에서만 사용 가능합니다.", MessageType.Info);
            }
            
            // 경고 메시지들
            if (SceneManager.sceneCountInBuildSettings == 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Build Settings에 Scene이 등록되지 않았습니다.", MessageType.Warning);
            }
            
            if (Application.isPlaying && sceneChanger.IsTransitioning)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox($"현재 Scene 전환 중입니다... (진행률: {sceneChanger.LoadingProgress * 100:F0}%)", MessageType.Info);
            }
        }
    }
}
#endif