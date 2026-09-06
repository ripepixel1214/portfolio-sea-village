using UnityEngine;

namespace SeaVillage.UI
{
    /// <summary>
    /// UI 요소를 기준 위치에서 위아래로 반복 이동
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIFloatingEffect : MonoBehaviour
    {
        private const float TwoPi = Mathf.PI * 2f;
        private const float MinimumCycleDuration = 0.01f;

        [Header("Floating")]
        [SerializeField, Min(0f)] private float distance = 8f;
        [SerializeField, Min(MinimumCycleDuration)] private float cycleDuration = 2f;
        [SerializeField, Range(0f, 1f)] private float startPhase;

        private RectTransform _rectTransform;
        private Vector2 _baseAnchoredPosition;
        private float _elapsedTime;
        private bool _hasBasePosition;

        private void Awake()
        {
            TryGetComponent(out _rectTransform);
        }

        private void OnEnable()
        {
            if (_rectTransform == null && !TryGetComponent(out _rectTransform))
            {
                enabled = false;
                return;
            }

            _baseAnchoredPosition = _rectTransform.anchoredPosition;
            _elapsedTime = startPhase * Mathf.Max(cycleDuration, MinimumCycleDuration);
            _hasBasePosition = true;
        }

        private void LateUpdate()
        {
            if (_rectTransform == null)
                return;

            float duration = Mathf.Max(cycleDuration, MinimumCycleDuration);
            _elapsedTime = Mathf.Repeat(_elapsedTime + Time.unscaledDeltaTime, duration);

            float offsetY = Mathf.Sin(_elapsedTime / duration * TwoPi) * distance;
            Vector2 anchoredPosition = _baseAnchoredPosition;
            anchoredPosition.y += offsetY;
            _rectTransform.anchoredPosition = anchoredPosition;
        }

        private void OnDisable()
        {
            if (!_hasBasePosition || _rectTransform == null)
                return;

            _rectTransform.anchoredPosition = _baseAnchoredPosition;
            _hasBasePosition = false;
        }
    }
}
