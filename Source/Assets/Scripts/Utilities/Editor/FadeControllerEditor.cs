#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace SeaVillage.Utilities.Editor
{
    /// <summary>
    /// FadeController용 에디터 유틸리티
    /// </summary>
    public static class FadeControllerEditor
    {
        [MenuItem("SeaVillage/Fade Controller/Create Fade Controller")]
        public static void CreateFadeController()
        {
            // 이미 존재하는지 확인
            FadeController existingController = Object.FindFirstObjectByType<FadeController>();
            if (existingController != null)
            {
                Debug.LogWarning("FadeController가 이미 Scene에 존재합니다.");
                Selection.activeGameObject = existingController.gameObject;
                return;
            }

            // 새로운 FadeController 생성
            GameObject controllerObj = new GameObject("FadeController");
            controllerObj.AddComponent<FadeController>();
            
            // Undo 등록
            Undo.RegisterCreatedObjectUndo(controllerObj, "Create FadeController");
            
            // 선택 및 포커스
            Selection.activeGameObject = controllerObj;
            EditorGUIUtility.PingObject(controllerObj);
            
            Debug.Log("FadeController가 생성되었습니다.");
        }

        [MenuItem("SeaVillage/Fade Controller/Find Fade Controller")]
        public static void FindFadeController()
        {
            FadeController controller = Object.FindFirstObjectByType<FadeController>();
            if (controller != null)
            {
                Selection.activeGameObject = controller.gameObject;
                EditorGUIUtility.PingObject(controller.gameObject);
                Debug.Log($"FadeController를 찾았습니다: {controller.name}");
            }
            else
            {
                Debug.LogWarning("Scene에서 FadeController를 찾을 수 없습니다.");
            }
        }

        [MenuItem("SeaVillage/Fade Controller/Test Fade Out", false, 100)]
        public static void TestFadeOut()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("페이드 테스트는 플레이 모드에서만 가능합니다.");
                return;
            }

            FadeController controller = FadeController.Instance;
            if (controller != null)
            {
                controller.FadeOut();
                Debug.Log("Fade Out 테스트 실행");
            }
            else
            {
                Debug.LogError("FadeController 인스턴스를 찾을 수 없습니다.");
            }
        }

        [MenuItem("SeaVillage/Fade Controller/Test Fade In", false, 101)]
        public static void TestFadeIn()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("페이드 테스트는 플레이 모드에서만 가능합니다.");
                return;
            }

            FadeController controller = FadeController.Instance;
            if (controller != null)
            {
                controller.FadeIn();
                Debug.Log("Fade In 테스트 실행");
            }
            else
            {
                Debug.LogError("FadeController 인스턴스를 찾을 수 없습니다.");
            }
        }

        [MenuItem("SeaVillage/Fade Controller/Reset Fade", false, 102)]
        public static void ResetFade()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("페이드 리셋은 플레이 모드에서만 가능합니다.");
                return;
            }

            FadeController controller = FadeController.Instance;
            if (controller != null)
            {
                controller.SetFadeImmediate(0f);
                Debug.Log("페이드 상태가 리셋되었습니다.");
            }
            else
            {
                Debug.LogError("FadeController 인스턴스를 찾을 수 없습니다.");
            }
        }

        // 메뉴 항목 활성화 조건
        [MenuItem("SeaVillage/Fade Controller/Test Fade Out", true)]
        [MenuItem("SeaVillage/Fade Controller/Test Fade In", true)]
        [MenuItem("SeaVillage/Fade Controller/Reset Fade", true)]
        public static bool ValidatePlayModeTests()
        {
            return Application.isPlaying;
        }
    }

    /// <summary>
    /// FadeController 커스텀 인스펙터
    /// </summary>
    [CustomEditor(typeof(FadeController))]
    public class FadeControllerInspector : UnityEditor.Editor
    {
        private FadeController fadeController;
        
        private void OnEnable()
        {
            fadeController = (FadeController)target;
        }

        public override void OnInspectorGUI()
        {
            // 기본 인스펙터 표시
            DrawDefaultInspector();
            
            EditorGUILayout.Space(20);
            
            // 현재 상태 표시
            EditorGUILayout.LabelField("Runtime Status", EditorStyles.boldLabel);
            
            GUI.enabled = false;
            EditorGUILayout.Toggle("Is Fading", Application.isPlaying ? fadeController.IsFading : false);
            EditorGUILayout.Slider("Current Alpha", Application.isPlaying ? fadeController.CurrentAlpha : 0f, 0f, 1f);
            GUI.enabled = true;
            
            EditorGUILayout.Space(10);
            
            // 에디터 버튼들
            EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            // Canvas 생성 버튼
            if (GUILayout.Button("Create Fade Canvas"))
            {
                // private 메서드 접근을 위한 리플렉션 사용. 코드가 난해해서 그냥 public으로 바꾸는 게 더 나을 수도 있음
                var method = typeof(FadeController).GetMethod("CreateFadeCanvas", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(fadeController, null);
                
                EditorUtility.SetDirty(fadeController);
                Debug.Log("Fade Canvas가 생성되었습니다.");
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 플레이 모드 전용 버튼들
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Runtime Tests", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Fade Out"))
                {
                    fadeController.FadeOut();
                }
                
                if (GUILayout.Button("Fade In"))
                {
                    fadeController.FadeIn();
                }
                
                if (GUILayout.Button("Fade 50%"))
                {
                    fadeController.FadeTo(0.5f);
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                
                if (GUILayout.Button("Reset"))
                {
                    fadeController.SetFadeImmediate(0f);
                }
                
                if (GUILayout.Button("Stop Current"))
                {
                    fadeController.StopCurrentFade();
                }
                
                EditorGUILayout.EndHorizontal();
                
                // 색상 변경 테스트
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Test Colors:", GUILayout.Width(80));
                
                if (GUILayout.Button("Black", GUILayout.Width(50)))
                {
                    fadeController.SetFadeColor(Color.black);
                }
                
                if (GUILayout.Button("White", GUILayout.Width(50)))
                {
                    fadeController.SetFadeColor(Color.white);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("런타임 테스트 기능은 플레이 모드에서만 사용 가능합니다.", MessageType.Info);
            }
            
            // // 경고 메시지
            // if (Application.isPlaying && fadeController.fadeCanvasGroup == null)
            // {
            //     EditorGUILayout.Space(5);
            //     EditorGUILayout.HelpBox("Fade Canvas가 생성되지 않았습니다. 'Create Fade Canvas' 버튼을 클릭하세요.", MessageType.Warning);
            // }
        }
    }
}
#endif