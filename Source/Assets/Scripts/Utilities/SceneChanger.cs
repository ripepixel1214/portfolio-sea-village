using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SeaVillage.Core;
using DG.Tweening;

namespace SeaVillage.Utilities
{
    /// <summary>
    /// Scene 전환과 페이드 효과를 통합 관리하는 클래스
    /// 비동기 로딩과 부드러운 페이드 전환을 제공
    /// </summary>
    public class SceneChanger : Singleton<SceneChanger>
    {
        [Header("Scene Transition Settings")]
        [SerializeField] private float fadeOutDuration = 0.5f;
        [SerializeField] private float fadeInDuration = 0.5f;
        [SerializeField] private float minimumLoadingTime = 1f; // 최소 로딩 시간 (너무 빠른 전환 방지를 위해)
        [SerializeField] private bool useAsyncLoading = true;
        
        [Header("Loading UI")]
        [Tooltip("비어 있으면 Resources/Prefabs/UI/Loading Screen을 자동 생성합니다.")]
        [SerializeField] private GameObject loadingUI; 
        [SerializeField] private UnityEngine.UI.Slider loadingProgressBar;
        [SerializeField] private TMPro.TextMeshProUGUI loadingText;

        // Events
        public event Action<string> OnSceneLoadStarted;
        public event Action<string, float> OnSceneLoadProgress; // sceneName, progress
        public event Action<string> OnSceneLoadCompleted;
        public event Action<string> OnSceneTransitionCompleted;

        // Properties
        public bool IsTransitioning { get; private set; } = false;
        public int ActiveSceneIndex { get; private set; } = 0;
        public string CurrentSceneName => SceneManager.GetActiveScene().name;
        public float LoadingProgress { get; private set; } = 0f;

        // References
        private FadeController fadeController;
        private Coroutine currentTransitionCoroutine;
        private LoadingScreenView loadingScreenView;
        private AsyncOperation activeSceneLoad;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this)
                return;

            fadeController = FadeController.Instance;
            EnsureLoadingUI();
        }

        private void EnsureLoadingUI()
        {
            if (loadingUI == null)
            {
                GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/Loading Screen");
                if (prefab == null)
                {
                    Debug.LogError("SceneChanger: Loading Screen 프리팹을 찾을 수 없습니다.");
                    return;
                }

                loadingUI = Instantiate(prefab, transform, false);
                loadingUI.name = "Loading Screen";
                loadingProgressBar = loadingUI.GetComponentInChildren<UnityEngine.UI.Slider>(true);
                loadingText = loadingUI.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            }

            loadingScreenView = loadingUI.GetComponent<LoadingScreenView>();
            loadingUI.SetActive(false);
        }

        /// <summary>
        /// Scene 전환 함수 (By Name)
        /// </summary>
        /// <param name="sceneName">로드할 Scene 이름</param>
        /// <param name="showLoadingUI">로딩 UI 표시 여부</param>
        /// <param name="onComplete">전환 완료 시 호출될 콜백</param>
        public void ChangeScene(string sceneName, bool showLoadingUI = true, Action onComplete = null)
        {
            if (IsTransitioning)
            {
                Debug.LogWarning($"SceneChanger: 이미 Scene 전환 중입니다.");
                return;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("SceneChanger: Scene 이름이 비어있습니다.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"SceneChanger: Scene을 로드할 수 없습니다. ({sceneName})");
                return;
            }

            // 만약 진행 중이던 전환이 있다면 즉시 중단
            if (currentTransitionCoroutine != null)
            {
                StopCoroutine(currentTransitionCoroutine);
            }

            currentTransitionCoroutine = StartCoroutine(ChangeSceneCoroutine(sceneName, showLoadingUI, onComplete));
        }

        /// <summary>
        /// Scene 전환 함수 (By Index)
        /// </summary>
        /// <param name="sceneIndex">Scene 빌드 인덱스</param>
        /// <param name="showLoadingUI">로딩 UI 표시 여부</param>
        /// <param name="onComplete">전환 완료 시 호출될 콜백</param>
        public void ChangeScene(int sceneIndex, bool showLoadingUI = true, Action onComplete = null)
        {
            if (sceneIndex < 0 || sceneIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"SceneChanger: 잘못된 Scene 인덱스입니다. ({sceneIndex})");
                return;
            }

            string sceneName = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(sceneIndex));
            ChangeScene(sceneName, showLoadingUI, onComplete);
        }

        /// <summary>
        /// Scene 전환 코루틴
        /// </summary>
        private IEnumerator ChangeSceneCoroutine(string sceneName, bool showLoadingUI, Action onComplete)
        {
            IsTransitioning = true;
            LoadingProgress = 0f;
            
            OnSceneLoadStarted?.Invoke(sceneName);

            // 1. Fade Out
            if (fadeController != null)
            {
                fadeController.FadeOut(fadeOutDuration)?.SetUpdate(true);
                yield return new WaitForSecondsRealtime(fadeOutDuration);
            }

            // 1-1. 데이터 Load 단계 — 화면이 가려진 이 시점에 새 씬에 필요한 영속 데이터를 로드한다.
            if (TimeManager.HasInstance)
                TimeManager.Instance.OnChangeSceneToTime(sceneName);

            // 2. Loading UI 표시
            if (showLoadingUI && loadingUI != null)
            {
                loadingUI.SetActive(true);
                UpdateLoadingUI(0f);
            }

            // 실제 로딩이 메인 스레드를 사용하기 전에 로딩창을 한 프레임 그린다.
            yield return null;

            // 로딩 시간 계산에는 안전하게 Time.unscaledTime 사용
            float startTime = Time.unscaledTime;

            // 3. Scene 비동기 로딩
            AsyncOperation asyncLoad = null;
            if (useAsyncLoading)
            {
                asyncLoad = SceneManager.LoadSceneAsync(sceneName);
                activeSceneLoad = asyncLoad;
                asyncLoad.allowSceneActivation = false;

                while (!asyncLoad.isDone)
                {
                    // allowSceneActivation을 false로 설정했으므로 asyncLoad.progress는 0.9까지만 올라감 => progress로 정규화해서 표시 (마지막 0.1는 Scene 활성화)
                    float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                    LoadingProgress = progress;
                    OnSceneLoadProgress?.Invoke(sceneName, progress);

                    if (showLoadingUI)
                    {
                        float elapsed = Time.unscaledTime - startTime;
                        float displayProgress = minimumLoadingTime > 0f
                            ? Mathf.Min(progress, elapsed / minimumLoadingTime)
                            : progress;
                        UpdateLoadingUI(displayProgress);
                    }

                    // 로딩이 90% 완료되면 최소 로딩 시간 체크
                    if (asyncLoad.progress >= 0.9f)
                    {
                        float elapsedTime = Time.unscaledTime - startTime;
                        if (elapsedTime >= minimumLoadingTime)
                            break;
                    }

                    yield return null;
                }

                // Scene 활성화
                asyncLoad.allowSceneActivation = true;
                yield return asyncLoad;
                activeSceneLoad = null;
            }
            else
            {
                // 동기 로딩
                SceneManager.LoadScene(sceneName);
                // LoadScene은 다음 프레임에 완료되므로 이전 Scene에서 UI를 숨기지 않는다.
                yield return null;
                float remainingTime = minimumLoadingTime - (Time.unscaledTime - startTime);
                if (remainingTime > 0f)
                    yield return new WaitForSecondsRealtime(remainingTime);
            }

            LoadingProgress = 1f;
            OnSceneLoadProgress?.Invoke(sceneName, 1f);
            OnSceneLoadCompleted?.Invoke(sceneName);

            if (showLoadingUI && loadingUI != null)
            {
                UpdateLoadingUI(1f);
                loadingScreenView?.Complete();
                yield return new WaitForSecondsRealtime(0.1f);
            }

            // 4. Loading UI 숨기기
            if (showLoadingUI && loadingUI != null)
            {
                loadingUI.SetActive(false);
            }

            // 5. Fade In
            if (fadeController != null)
            {
                fadeController.FadeIn(fadeInDuration)?.SetUpdate(true);
                yield return new WaitForSecondsRealtime(fadeInDuration);
            }

            IsTransitioning = false;
            currentTransitionCoroutine = null;
            ActiveSceneIndex = SceneManager.GetActiveScene().buildIndex;
            OnSceneTransitionCompleted?.Invoke(sceneName);

            // 전환 애니메이션 종료 — 현재 씬의 총괄 매니저를 시동한다.
            // 주의: ISceneRoot.Initialize() 안에서 또 다른 ChangeScene을 호출하면 안 된다(이 전환 코루틴은 이미 종료된 상태).
            GameManager.Instance.CurrentSceneRoot?.Initialize();

            onComplete?.Invoke();
        }

        /// <summary>
        /// Loading UI 업데이트
        /// </summary>
        private void UpdateLoadingUI(float progress)
        {
            if (loadingText != null)
                loadingText.text = "LOADING...";
            
            if (loadingScreenView != null)
                loadingScreenView.SetProgress(progress);
            else if (loadingProgressBar != null)
                loadingProgressBar.SetValueWithoutNotify(progress);
        }

        /// <summary>
        /// 현재 Scene 새로고침 (재로딩)
        /// </summary>
        /// <param name="showLoadingUI">로딩 UI 표시 여부</param>
        /// <param name="onComplete">완료 시 호출될 콜백</param>
        public void ReloadCurrentScene(bool showLoadingUI = true, Action onComplete = null)
        {
            string currentScene = CurrentSceneName;
            ChangeScene(currentScene, showLoadingUI, onComplete);
        }

        /// <summary>
        /// 페이드 없이 즉시 Scene 전환
        /// </summary>
        /// <param name="sceneName">로드할 Scene 이름</param>
        public void ChangeSceneImmediate(string sceneName)
        {
            if (activeSceneLoad != null && !activeSceneLoad.isDone)
            {
                Debug.LogWarning("SceneChanger: 진행 중인 비동기 로딩이 완료된 후 이동할 수 있습니다.");
                return;
            }

            if (IsTransitioning)
            {
                Debug.LogWarning("SceneChanger: 현재 전환 중인 작업을 중단하고 즉시 전환합니다.");
                if (currentTransitionCoroutine != null)
                {
                    StopCoroutine(currentTransitionCoroutine);
                }
            }

            if (loadingUI != null)
                loadingUI.SetActive(false);
            fadeController?.SetFadeImmediate(0f);
            currentTransitionCoroutine = null;
            SceneManager.LoadScene(sceneName);
            IsTransitioning = false;
            ActiveSceneIndex = SceneManager.GetSceneByName(sceneName).buildIndex;
        }

        /// <summary>
        /// 특정 Scene이 Build Settings에 포함되어 있는지 확인
        /// </summary>
        /// <param name="sceneName">확인할 Scene 이름</param>
        /// <returns>포함 여부</returns>
        public bool IsSceneInBuildSettings(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneNameInBuild = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                if (sceneNameInBuild.Equals(sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Scene 전환 취소 (가능한 경우)
        /// </summary>
        public void CancelTransition()
        {
            // Unity의 Scene 비동기 로딩은 취소할 수 없다. 활성화 대기를 남기면 로딩 큐가 멈춘다.
            if (activeSceneLoad != null && !activeSceneLoad.isDone)
            {
                Debug.LogWarning("SceneChanger: Scene 로딩이 시작되어 전환을 취소할 수 없습니다.");
                return;
            }

            if (IsTransitioning && currentTransitionCoroutine != null)
            {
                StopCoroutine(currentTransitionCoroutine);
                currentTransitionCoroutine = null;
                
                if (loadingUI != null)
                    loadingUI.SetActive(false);
                
                if (fadeController != null)
                    fadeController.FadeIn(fadeInDuration)?.SetUpdate(true);
                
                IsTransitioning = false;
                Debug.Log("SceneChanger: Scene 전환이 취소되었습니다.");
            }
        }

        #region Quick Scene Change Methods
        /// <summary>
        /// 메인 메뉴로 이동
        /// </summary>
        public void GoToMainMenu(Action onComplete = null)
        {
            ChangeScene("MainMenu", true, onComplete);
        }

        /// <summary>
        /// 마을 Scene으로 이동
        /// </summary>
        public void GoToTown(Action onComplete = null)
        {
            ChangeScene("StartTown", true, onComplete);
        }

        /// <summary>
        /// 항해 Scene으로 이동
        /// </summary>
        public void GoToSailing(Action onComplete = null)
        {
            ChangeScene("Ocean", true, onComplete);
        }

        /// <summary>
        /// 설정 Scene으로 이동
        /// </summary>
        public void GoToSettings(Action onComplete = null)
        {
            ChangeScene("Settings", true, onComplete);
        }

        /// <summary>
        /// GameState를 기반으로 Scene 이름 반환
        /// </summary>
        /// <param name="gameState">게임 상태</param>
        /// <returns>해당하는 Scene 이름</returns>
        private string GetSceneNameFromGameState(GameState gameState)
        {
            return gameState switch
            {
                GameState.MainMenu => "MainMenu",
                GameState.Town => "StartTown",
                GameState.Sailing => "Ocean",
                GameState.Loading => null, // Loading은 별도 Scene이 없음
                _ => null
            };
        }
        #endregion

        #region Editor Support
        #if UNITY_EDITOR
        [ContextMenu("Reload Current Scene")]
        private void ReloadCurrentSceneInEditor()
        {
            ReloadCurrentScene();
        }

        [ContextMenu("Go To Main Menu")]
        private void GoToMainMenuInEditor()
        {
            GoToMainMenu();
        }
        #endif
        #endregion

        /// <summary>
        /// 컴포넌트 파괴 시 정리
        /// </summary>
        protected override void OnDestroy()
        {
            if (activeSceneLoad != null && !activeSceneLoad.isDone)
                activeSceneLoad.allowSceneActivation = true;

            if (currentTransitionCoroutine != null)
            {
                StopCoroutine(currentTransitionCoroutine);
            }
            base.OnDestroy();
        }
    }
}
