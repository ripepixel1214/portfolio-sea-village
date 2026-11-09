using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.Utilities
{
    /// <summary>Scene 전환 중 표시하는 로딩창. 일시정지와 무관하게 애니메이션을 재생한다.</summary>
    public sealed class LoadingScreenView : MonoBehaviour
    {
        [SerializeField] private Slider progressBar;
        [SerializeField] private RectTransform boat;
        [SerializeField] private RectTransform waves;
        [SerializeField, Min(0f)] private float progressSpeed = 2f;
        [SerializeField, Min(0f)] private float bobHeight = 6f;
        [SerializeField, Min(0f)] private float rockAngle = 2f;
        [SerializeField, Min(0f)] private float waveTravel = 35f;

        private Vector2 boatPosition;
        private Vector2 wavesPosition;
        private Quaternion boatRotation;
        private float targetProgress;
        private float animationTime;

        private void Awake()
        {
            if (boat != null)
            {
                boatPosition = boat.anchoredPosition;
                boatRotation = boat.localRotation;
            }
            if (waves != null)
                wavesPosition = waves.anchoredPosition;
        }

        private void OnEnable()
        {
            animationTime = 0f;
            targetProgress = 0f;
            if (progressBar != null)
                progressBar.SetValueWithoutNotify(0f);
            ResetArtwork();
        }

        public void SetProgress(float progress)
        {
            targetProgress = Mathf.Max(targetProgress, Mathf.Clamp01(progress));
        }

        public void Complete()
        {
            targetProgress = 1f;
            if (progressBar != null)
                progressBar.SetValueWithoutNotify(1f);
        }

        private void Update()
        {
            animationTime += Time.unscaledDeltaTime;
            if (progressBar != null)
                progressBar.SetValueWithoutNotify(Mathf.MoveTowards(
                    progressBar.value, targetProgress, progressSpeed * Time.unscaledDeltaTime));

            float phase = animationTime * 2f;
            if (boat != null)
            {
                boat.anchoredPosition = boatPosition + Vector2.up * (Mathf.Sin(phase) * bobHeight);
                boat.localRotation = boatRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.8f) * rockAngle);
            }
            if (waves != null)
                waves.anchoredPosition = wavesPosition + Vector2.right * (Mathf.Sin(phase * 0.6f) * waveTravel);
        }

        private void OnDisable()
        {
            ResetArtwork();
        }

        private void ResetArtwork()
        {
            if (boat != null)
            {
                boat.anchoredPosition = boatPosition;
                boat.localRotation = boatRotation;
            }
            if (waves != null)
                waves.anchoredPosition = wavesPosition;
        }
    }
}
