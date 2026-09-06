using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    /// <summary>
    /// 메뉴 패널
    /// </summary>
    public class MenuPanel : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backToMainButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button settingsButton;

        #region Event Listeners
        protected override void AddListeners()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);

            if (backToMainButton != null)
                backToMainButton.onClick.AddListener(BackToMainMenu);

            if (saveButton != null)
                saveButton.onClick.AddListener(OpenSavePanel);

            if (loadButton != null)
                loadButton.onClick.AddListener(OpenLoadPanel);

            if (settingsButton != null)
                settingsButton.onClick.AddListener(OpenSettingsPanel);
        }

        protected override void RemoveListeners()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);

            if (backToMainButton != null)
                backToMainButton.onClick.RemoveListener(BackToMainMenu);

            if (saveButton != null)
                saveButton.onClick.RemoveListener(OpenSavePanel);

            if (loadButton != null)
                loadButton.onClick.RemoveListener(OpenLoadPanel);

            if (settingsButton != null)
                settingsButton.onClick.RemoveListener(OpenSettingsPanel);
        }
        #endregion

        #region Button Handlers
        private void BackToMainMenu()
        {
            Utilities.SceneChanger.Instance.ChangeScene("MainMenu");
        }

        private void OpenSavePanel() => OpenSaveLoadPanel(true);

        private void OpenLoadPanel() => OpenSaveLoadPanel(false);

        private void OpenSaveLoadPanel(bool saveMode)
        {
            GameSaveLoadPanel panel = UIManager.Instance.OpenPanel<GameSaveLoadPanel>();
            panel?.Initialize(saveMode);
        }

        private void OpenSettingsPanel()
        {
            UIManager.Instance.OpenPanel<GameSettingsPanel>();
        }
        #endregion
    }
}
