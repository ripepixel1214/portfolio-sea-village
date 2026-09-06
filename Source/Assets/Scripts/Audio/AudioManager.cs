using System.Collections;
using System.Collections.Generic;
using SeaVillage.Core;
using SeaVillage.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SeaVillage.Audio
{
    public class AudioManager : Singleton<AudioManager>
    {
        public const string SfxCash = "UI_Cash";
        public const string SfxClear = "UI_Clear";
        public const string SfxClick = "UI_Click";

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("Audio Settings")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Min(0f)] private float defaultFadeDuration = 1f;

        private readonly Dictionary<string, AudioClip> sfxClips = new();
        private GameManager gameManager;
        private SceneBgm sceneBgm;
        private AudioClip requestedBgm;
        private Coroutine bgmFadeCoroutine;
        private float bgmFadeGain;

        public float MasterVolume => masterVolume;
        public float BgmVolume => bgmVolume;
        public float SfxVolume => sfxVolume;

        #region MonoBehaviour
        protected override void Awake()
        {
            base.Awake();
            if (!HasInstance || Instance != this)
                return;

            bgmSource = SetupSource(bgmSource, "BGM Source", true);
            sfxSource = SetupSource(sfxSource, "SFX Source", false);
            foreach (AudioClip clip in Resources.LoadAll<AudioClip>("Audio/SFX"))
                sfxClips[clip.name] = clip;
            ApplyVolumeSettings();
        }

        private void OnEnable()
        {
            if (!HasInstance || Instance != this)
                return;

            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            if (GameManager.HasInstance)
            {
                gameManager = GameManager.Instance;
                gameManager.OnCurrentTownChanged += HandleCurrentTownChanged;
            }
            RefreshSceneBgm(SceneManager.GetActiveScene());
        }

        private void Start()
        {
            if (HasInstance && Instance == this)
                RefreshSceneBgm(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            if (gameManager != null)
                gameManager.OnCurrentTownChanged -= HandleCurrentTownChanged;
            gameManager = null;
            sceneBgm = null;
            CancelBgmFade();
            requestedBgm = null;
            if (bgmSource != null)
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }
            if (sfxSource != null)
                sfxSource.Stop();
            bgmFadeGain = 0f;
        }
        #endregion

        #region Public API
        public void PlayBgm(AudioClip clip, bool fadeIn = true)
        {
            if (clip == null)
            {
                Debug.LogWarning("[AudioManager] BGM 클립이 없습니다", this);
                return;
            }
            RequestBgm(clip, fadeIn);
        }

        public void StopBgm(bool fadeOut = true) => RequestBgm(null, fadeOut);

        public static void TryPlaySfx(string sfxName, float volume = 1f)
        {
            if (HasInstance)
                Instance.PlaySfx(sfxName, volume);
        }

        public static void TryPlayClickSfx() => TryPlaySfx(SfxClick);

        public void PlaySfx(string sfxName, float volume = 1f)
        {
            if (!isActiveAndEnabled || sfxSource == null)
                return;
            if (string.IsNullOrEmpty(sfxName) || !sfxClips.TryGetValue(sfxName, out AudioClip clip))
            {
                Debug.LogWarning($"[AudioManager] SFX 클립을 찾을 수 없습니다: {sfxName}", this);
                return;
            }
            sfxSource.PlayOneShot(clip, ClampVolume(volume));
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = ClampVolume(volume);
            ApplyVolumeSettings();
        }

        public void SetBgmVolume(float volume)
        {
            bgmVolume = ClampVolume(volume);
            ApplyVolumeSettings();
        }

        public void SetSfxVolume(float volume)
        {
            sfxVolume = ClampVolume(volume);
            ApplyVolumeSettings();
        }
        #endregion

        #region Event Handlers
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene == SceneManager.GetActiveScene())
                RefreshSceneBgm(scene);
        }

        private void HandleActiveSceneChanged(Scene previous, Scene next) => RefreshSceneBgm(next);

        private void HandleCurrentTownChanged(TownKey townKey) => ApplySceneBgm(townKey);
        #endregion

        #region Private Helpers
        private AudioSource SetupSource(AudioSource source, string sourceName, bool loop)
        {
            if (source == null)
            {
                source = new GameObject(sourceName).AddComponent<AudioSource>();
                source.transform.SetParent(transform, false);
            }
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        private void RefreshSceneBgm(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            sceneBgm = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SceneBgm candidate in root.GetComponentsInChildren<SceneBgm>())
                {
                    if (!candidate.isActiveAndEnabled)
                        continue;
                    if (sceneBgm != null)
                    {
                        Debug.LogError($"[AudioManager] 씬에 BGM 설정이 중복되어 있습니다: {scene.name}", candidate);
                        sceneBgm = null;
                        StopBgm();
                        return;
                    }
                    sceneBgm = candidate;
                }
            }
            ApplySceneBgm(gameManager != null ? gameManager.CurrentTownKey : TownKey.Unknown);
        }

        private void ApplySceneBgm(TownKey townKey)
        {
            AudioClip clip = sceneBgm != null ? sceneBgm.ResolveClip(townKey) : null;
            if (clip != null)
                PlayBgm(clip);
            else
                StopBgm();
        }

        private void RequestBgm(AudioClip clip, bool fade)
        {
            if (!isActiveAndEnabled || bgmSource == null)
                return;
            if (requestedBgm == clip && (bgmFadeCoroutine != null ? fade
                : (clip == bgmSource.clip && bgmSource.isPlaying)
                    || (clip == null && !bgmSource.isPlaying)))
                return;

            CancelBgmFade();
            requestedBgm = clip;
            if (!fade || defaultFadeDuration <= 0f || float.IsNaN(defaultFadeDuration)
                || float.IsInfinity(defaultFadeDuration))
            {
                bgmSource.Stop();
                bgmSource.clip = clip;
                bgmFadeGain = clip != null ? 1f : 0f;
                ApplyVolumeSettings();
                if (clip != null)
                    bgmSource.Play();
                return;
            }
            bgmFadeCoroutine = StartCoroutine(ChangeBgm(clip));
        }

        private IEnumerator ChangeBgm(AudioClip clip)
        {
            if (bgmSource.clip != clip)
            {
                if (bgmSource.isPlaying)
                    yield return FadeBgm(0f, defaultFadeDuration * 0.5f);
                bgmSource.Stop();
                bgmSource.clip = clip;
                bgmFadeGain = 0f;
                ApplyVolumeSettings();
                if (clip != null)
                    bgmSource.Play();
            }
            if (clip != null)
                yield return FadeBgm(1f, defaultFadeDuration * 0.5f);
            bgmFadeCoroutine = null;
        }

        private IEnumerator FadeBgm(float targetGain, float duration)
        {
            float startGain = bgmFadeGain;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmFadeGain = Mathf.Lerp(startGain, targetGain, elapsed / duration);
                ApplyVolumeSettings();
                yield return null;
            }
            bgmFadeGain = targetGain;
            ApplyVolumeSettings();
        }

        private void CancelBgmFade()
        {
            if (bgmFadeCoroutine != null)
                StopCoroutine(bgmFadeCoroutine);
            bgmFadeCoroutine = null;
        }

        private void ApplyVolumeSettings()
        {
            // 페이드와 사용자 음량을 분리해 전환 중에도 음소거 유지
            if (bgmSource != null)
                bgmSource.volume = ClampVolume(masterVolume) * ClampVolume(bgmVolume) * bgmFadeGain;
            if (sfxSource != null)
                sfxSource.volume = ClampVolume(masterVolume) * ClampVolume(sfxVolume);
        }

        private static float ClampVolume(float value) => float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
        #endregion
    }
}
