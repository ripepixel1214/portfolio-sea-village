using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SeaVillage.Utilities
{
    /// <summary>
    /// Fade Effect를 담당하는 컨트롤러
    /// </summary>
    public class FadeController : Singleton<FadeController>
    {
        [Header("Fade Settings")]
        [SerializeField] private float defaultFadeDuration = 0.5f;
        [SerializeField] private Ease fadeEase = Ease.InOutQuad;
        [SerializeField] private bool autoCreateCanvas = true;
        
        [Header("UI References")]
        [SerializeField] private CanvasGroup fadeCanvasGroup;
        [SerializeField] private Image fadeImage;
        [SerializeField] private Canvas fadeCanvas;

        // Properties
        public bool IsFading { get; private set; } = false;
        public float CurrentAlpha => fadeCanvasGroup != null ? fadeCanvasGroup.alpha : 0f;
        
        // Events
        public event Action<bool> OnFadeStarted; // bool: true면 FadeOut, false면 FadeIn
        public event Action<bool> OnFadeCompleted;

        // Tween reference for better control
        private Tween currentFadeTween;

        protected override void Awake()
        {
            base.Awake();
            InitializeFadeSystem();
        }

        /// <summary>
        /// 페이드 시스템 초기화
        /// </summary>
        private void InitializeFadeSystem()
        {
            if (autoCreateCanvas && fadeCanvasGroup == null)
            {
                CreateFadeCanvas();
            }
            
            // 초기 상태: 완전 투명 (화면이 보이는 상태)
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = 0f;
                fadeCanvasGroup.blocksRaycasts = false;
            }
        }

        /// <summary>
        /// 페이드용 Canvas와 UI 요소들을 자동 생성
        /// </summary>
        private void CreateFadeCanvas()
        {
            // Fade Canvas 생성
            GameObject canvasObject = new GameObject("Fade Canvas");
            canvasObject.transform.SetParent(transform);
            
            fadeCanvas = canvasObject.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 9999; // 최상위 레이어
            
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();
            
            // CanvasGroup 추가
            fadeCanvasGroup = canvasObject.AddComponent<CanvasGroup>();
            
            // Fade Image 생성
            GameObject imageObject = new GameObject("Fade Image");
            imageObject.transform.SetParent(canvasObject.transform, false);
            
            fadeImage = imageObject.AddComponent<Image>();
            fadeImage.color = Color.black;
            fadeImage.rectTransform.anchorMin = Vector2.zero;
            fadeImage.rectTransform.anchorMax = Vector2.one;
            fadeImage.rectTransform.offsetMin = Vector2.zero;
            fadeImage.rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 페이드 인
        /// </summary>
        /// <param name="duration">페이드 지속 시간 (기본값: defaultFadeDuration)</param>
        /// <param name="onComplete">완료 시 호출될 콜백</param>
        public Tween FadeIn(float duration = -1f, TweenCallback onComplete = null)
        {
            if (duration < 0) duration = defaultFadeDuration;
            
            return StartFade(0f, duration, false, onComplete);
        }

        /// <summary>
        /// 페이드 아웃
        /// </summary>
        /// <param name="duration">페이드 지속 시간 (기본값: defaultFadeDuration)</param>
        /// <param name="onComplete">완료 시 호출될 콜백</param>
        public Tween FadeOut(float duration = -1f, TweenCallback onComplete = null)
        {
            if (duration < 0) duration = defaultFadeDuration;
            
            return StartFade(1f, duration, true, onComplete);
        }

        /// <summary>
        /// 특정 알파값으로 페이드
        /// </summary>
        /// <param name="targetAlpha">목표 알파값 (0~1)</param>
        /// <param name="duration">페이드 지속 시간</param>
        /// <param name="onComplete">완료 시 호출될 콜백</param>
        public Tween FadeTo(float targetAlpha, float duration = -1f, TweenCallback onComplete = null)
        {
            if (duration < 0) duration = defaultFadeDuration;
            
            targetAlpha = Mathf.Clamp01(targetAlpha);
            bool isFadingOut = targetAlpha > CurrentAlpha;
            
            return StartFade(targetAlpha, duration, isFadingOut, onComplete);
        }

        /// <summary>
        /// 실제 페이드 실행 함수
        /// </summary>
        private Tween StartFade(float targetAlpha, float duration, bool isFadingOut, TweenCallback onComplete)
        {
            if (fadeCanvasGroup == null)
            {
                Debug.LogError("FadeController: CanvasGroup이 설정되지 않았습니다!");
                onComplete?.Invoke();
                return null;
            }

            StopCurrentFade();

            IsFading = true;
            OnFadeStarted?.Invoke(isFadingOut);

            // Raycast 차단 여부 결정
            fadeCanvasGroup.blocksRaycasts = isFadingOut || targetAlpha > 0f;

            currentFadeTween = fadeCanvasGroup.DOFade(targetAlpha, duration)
                .SetEase(fadeEase)
                .OnComplete(() =>
                {
                    IsFading = false;

                    if (targetAlpha <= 0f)
                        fadeCanvasGroup.blocksRaycasts = false;
                    
                    OnFadeCompleted?.Invoke(isFadingOut);
                    onComplete?.Invoke();
                });

            return currentFadeTween;
        }

        /// <summary>
        /// 현재 실행 중인 페이드 중단
        /// </summary>
        public void StopCurrentFade()
        {
            if (currentFadeTween != null && currentFadeTween.IsActive())
            {
                currentFadeTween.Kill();
                IsFading = false;
            }
        }

        /// <summary>
        /// 애니메이션 없이 즉시 특정 알파값으로 페이드 상태 설정
        /// </summary>
        /// <param name="alpha">설정할 알파값</param>
        public void SetFadeImmediate(float alpha)
        {
            if (fadeCanvasGroup == null) return;
            
            StopCurrentFade();
            
            alpha = Mathf.Clamp01(alpha);
            fadeCanvasGroup.alpha = alpha;
            fadeCanvasGroup.blocksRaycasts = alpha > 0f;
        }

        /// <summary>
        /// 페이드 패널 기본 색상 변경
        /// </summary>
        public void SetFadeColor(Color color)
        {
            if (fadeImage != null)
                fadeImage.color = color;
        }

        /// <summary>
        /// 컴포넌트 파괴 시 정리
        /// </summary>
        protected override void OnDestroy()
        {
            StopCurrentFade();
            base.OnDestroy();
        }

        #region Editor Support
        #if UNITY_EDITOR
        [ContextMenu("Create Fade Canvas")]
        private void CreateFadeCanvasInEditor()
        {
            if (fadeCanvasGroup == null)
            {
                CreateFadeCanvas();
                EditorUtility.SetDirty(this);
            }
        }

        [ContextMenu("Test Fade Out")]
        private void TestFadeOutInEditor()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("FadeController: 페이드 테스트는 플레이 모드에서만 가능합니다.");
                return;
            }
            
            FadeOut();
        }

        [ContextMenu("Test Fade In")]
        private void TestFadeInInEditor()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("FadeController: 페이드 테스트는 플레이 모드에서만 가능합니다.");
                return;
            }
            
            FadeIn();
        }

        [ContextMenu("Reset Fade")]
        private void ResetFadeInEditor()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("FadeController: 페이드 리셋은 플레이 모드에서만 가능합니다.");
                return;
            }
            
            SetFadeImmediate(0f);
        }
        #endif
        #endregion
    }
}