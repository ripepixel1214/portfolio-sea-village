using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Audio;

namespace SeaVillage.UI
{
    /// <summary>
    /// 설정 패널
    /// </summary>
    public class MainSettingsPanel : UIPanel
    {
        [SerializeField] private Button closeButton;

        [Header("Audio Sliders")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider bgmVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Audio Values")]
        [SerializeField] private TMP_Text masterVolumeText;
        [SerializeField] private TMP_Text bgmVolumeText;
        [SerializeField] private TMP_Text sfxVolumeText;

        private AudioManager _audioManager;

        #region MonoBehaviour
        private void OnEnable()
        {
            AddListeners();
            if (State == PanelState.Open)
                RefreshAudioSettings();
        }

        private void OnDisable()
        {
            RemoveListeners();
            _audioManager = null;
        }
        #endregion

        #region Public API
        public override void OnOpen()
        {
            base.OnOpen();
            RefreshAudioSettings();
            if (masterVolumeSlider != null && masterVolumeSlider.interactable)
                masterVolumeSlider.Select();
        }

        public override void OnFocusRestored()
        {
            base.OnFocusRestored();
            RefreshAudioSettings();
            if (masterVolumeSlider != null && masterVolumeSlider.interactable)
                masterVolumeSlider.Select();
        }

        public override void OnCloseRequested()
        {
            base.OnCloseRequested();
            SetAudioInteractable(false);
        }
        #endregion

        #region Event Handlers
        protected override void AddListeners()
        {
            RemoveListeners();
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.AddListener(HandleMasterVolumeChanged);
            if (bgmVolumeSlider != null)
                bgmVolumeSlider.onValueChanged.AddListener(HandleBgmVolumeChanged);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);
        }

        protected override void RemoveListeners()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
            if (masterVolumeSlider != null)
                masterVolumeSlider.onValueChanged.RemoveListener(HandleMasterVolumeChanged);
            if (bgmVolumeSlider != null)
                bgmVolumeSlider.onValueChanged.RemoveListener(HandleBgmVolumeChanged);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);
        }

        private void HandleMasterVolumeChanged(float value)
        {
            if (_audioManager == null || State != PanelState.Open) return;
            _audioManager.SetMasterVolume(value);
            UpdateVolumeDisplay(masterVolumeSlider, masterVolumeText, _audioManager.MasterVolume);
        }

        private void HandleBgmVolumeChanged(float value)
        {
            if (_audioManager == null || State != PanelState.Open) return;
            _audioManager.SetBgmVolume(value);
            UpdateVolumeDisplay(bgmVolumeSlider, bgmVolumeText, _audioManager.BgmVolume);
        }

        private void HandleSfxVolumeChanged(float value)
        {
            if (_audioManager == null || State != PanelState.Open) return;
            _audioManager.SetSfxVolume(value);
            UpdateVolumeDisplay(sfxVolumeSlider, sfxVolumeText, _audioManager.SfxVolume);
        }

        #endregion

        #region Private Helpers
        private void RefreshAudioSettings()
        {
            bool hasReferences = masterVolumeSlider != null && bgmVolumeSlider != null
                && sfxVolumeSlider != null
                && masterVolumeText != null && bgmVolumeText != null
                && sfxVolumeText != null;

            _audioManager = AudioManager.HasInstance ? AudioManager.Instance : null;
            SetAudioInteractable(hasReferences && _audioManager != null);
            if (!hasReferences)
            {
                Debug.LogError("[MainSettingsPanel] 소리 설정 UI 참조가 누락되었습니다", this);
                return;
            }

            if (_audioManager == null)
            {
                Debug.LogWarning("[MainSettingsPanel] AudioManager가 없어 소리 설정을 비활성화합니다", this);
                return;
            }

            UpdateVolumeDisplay(masterVolumeSlider, masterVolumeText, _audioManager.MasterVolume);
            UpdateVolumeDisplay(bgmVolumeSlider, bgmVolumeText, _audioManager.BgmVolume);
            UpdateVolumeDisplay(sfxVolumeSlider, sfxVolumeText, _audioManager.SfxVolume);
        }

        private void SetAudioInteractable(bool value)
        {
            if (masterVolumeSlider != null) masterVolumeSlider.interactable = value;
            if (bgmVolumeSlider != null) bgmVolumeSlider.interactable = value;
            if (sfxVolumeSlider != null) sfxVolumeSlider.interactable = value;
        }

        private static void UpdateVolumeDisplay(Slider slider, TMP_Text text, float value)
        {
            slider.SetValueWithoutNotify(value);
            text.SetText("{0}%", Mathf.RoundToInt(value * 100f));
        }
        #endregion
    }
}
