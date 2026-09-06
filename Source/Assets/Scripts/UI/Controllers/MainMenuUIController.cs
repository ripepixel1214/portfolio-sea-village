using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;
using SeaVillage.Utilities;

namespace SeaVillage.UI
{
    /// <summary>
    /// 메인 메뉴 씬 전용 UI Controller
    /// 게임플레이 씬과 별개로 취급하며 BaseUIController를 상속하지 않음
    /// </summary>
    public class MainMenuUIController : MonoBehaviour
    {
        [Header("Main Menu Buttons")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button exitButton;

        [Header("Panels")]
        [SerializeField] private MainSaveSlotPanel saveSlotPanel;
        [SerializeField] private MainSettingsPanel settingsPanel;

        [Header("Scene Settings")]
        [SerializeField] private string gameSceneName = "UITest";

        // Reference
        private UIManager _uiManager;

        #region MonoBehaviour
        private void Awake()
        {
            _uiManager = UIManager.Instance;
        }

        private void Start()
        {
            RegisterPanels();
            RegisterButtonEvents();
        }

        private void OnDestroy()
        {
            UnregisterButtonEvents();
        }
        #endregion

        #region Initialization
        private void RegisterPanels()
        {
            if (saveSlotPanel != null)
                _uiManager.RegisterPanel<MainSaveSlotPanel>(saveSlotPanel);

            if (settingsPanel != null)
                _uiManager.RegisterPanel<MainSettingsPanel>(settingsPanel);
        }

        private void RegisterButtonEvents()
        {
            if (playButton != null)
            {
                playButton.onClick.AddListener(OnPlayButtonClicked);
                playButton.onClick.AddListener(Audio.AudioManager.TryPlayClickSfx);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(OnSettingsButtonClicked);
                settingsButton.onClick.AddListener(Audio.AudioManager.TryPlayClickSfx);
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(OnExitButtonClicked);
                exitButton.onClick.AddListener(Audio.AudioManager.TryPlayClickSfx);
            }
        }

        private void UnregisterButtonEvents()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayButtonClicked);
                playButton.onClick.RemoveListener(Audio.AudioManager.TryPlayClickSfx);
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
                settingsButton.onClick.RemoveListener(Audio.AudioManager.TryPlayClickSfx);
            }
        }
        #endregion

        #region Button Event Handlers
        /// <summary>
        /// 세이브 슬롯 선택 패널 열기
        /// </summary>
        private void OnPlayButtonClicked()
        {
            _uiManager.OpenPanel<MainSaveSlotPanel>();
        }

        /// <summary>
        /// 설정 패널 열기
        /// </summary>
        private void OnSettingsButtonClicked()
        {
            Debug.Log("Settings button clicked");
            _uiManager.OpenPanel<MainSettingsPanel>();
        }

        /// <summary>
        /// 게임 종료
        /// </summary>
        private void OnExitButtonClicked()
        {
            Debug.Log("Exit button clicked");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        #endregion

        #region Scene Management
        /// <summary>
        /// 게임 씬으로 전환
        /// </summary>
        public void LoadGameScene()
        {
            Debug.Log($"Loading game scene: {gameSceneName}");
            SceneChanger.Instance.ChangeScene(gameSceneName);
        }


        public async System.Threading.Tasks.Task ContinueGameAsync(int slotIndex)
        {
            Debug.Log($"Continuing game from slot {slotIndex}");

            if (!Data.SaveLoadManager.HasInstance)
            {
                Debug.LogError("SaveLoadManager not found!");
                return;
            }

            bool loadSuccess = await Data.SaveLoadManager.Instance.LoadGameAsync(slotIndex);
            if (!loadSuccess)
            {
                Debug.LogWarning($"Failed to load slot {slotIndex}. Aborting scene change.");
                return;
            }

            // 저장 시점의 마을로 복귀
            SceneChanger.Instance.ChangeScene(Data.SaveLoadManager.Instance.GetResumeSceneName());
        }

        public void ContinueGame(int slotIndex)
        {
            _ = ContinueGameAsync(slotIndex);
        }

        /// <summary>
        /// 새 게임 시작
        /// </summary>
        public async System.Threading.Tasks.Task<bool> StartNewGameAsync(
            int slotIndex,
            PlayerGender playerGender = PlayerGender.Male)
        {
            Debug.Log($"Starting new game in slot {slotIndex}");

            if (!Data.SaveLoadManager.HasInstance)
            {
                Debug.LogError("SaveLoadManager not found!");
                return false;
            }

            bool created = await Data.SaveLoadManager.Instance.CreateNewGameAsync(slotIndex, playerGender);
            if (!created)
            {
                Debug.LogError($"Failed to create a new game in slot {slotIndex}. Aborting scene change.");
                return false;
            }

            SceneChanger.Instance.ChangeScene(gameSceneName, true, HandleNewGameSceneReady);
            return true;
        }

        public void StartNewGame(int slotIndex)
        {
            _ = StartNewGameAsync(slotIndex);
        }

        public bool OpenGenderSelection(int slotIndex)
        {
            GenderSelectionPanel panel = _uiManager.OpenPanel<GenderSelectionPanel>();
            if (panel == null)
                return false;

            panel.Initialize(slotIndex, this);
            return true;
        }

        private static void HandleNewGameSceneReady()
        {
            if (!TutorialManager.HasInstance || !TutorialManager.Instance.IsInitialized)
            {
                Debug.LogError("[NewGame] TutorialManager가 준비되지 않아 튜토리얼을 시작할 수 없습니다");
                return;
            }

            if (!TutorialManager.Instance.TryStartNewGameTutorial(out string failReason))
                Debug.LogError($"[NewGame] 튜토리얼 시작 실패: {failReason}");
        }
        #endregion
    }
}
