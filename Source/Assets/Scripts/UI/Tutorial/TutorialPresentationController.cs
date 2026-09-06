using System.Collections;
using System.Collections.Generic;
using SeaVillage.Core;
using SeaVillage.Data;
using SeaVillage.UI.NPC;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeaVillage.UI.Tutorial
{
    /// <summary>
    /// 튜토리얼 대사 타입을 실제 UI로 표시하는 Presenter
    /// </summary>
    public sealed class TutorialPresentationController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Canvas presentationCanvas;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private GameObject inputCatcher;
        [SerializeField] private RectTransform boxRoot;
        [SerializeField] private TMP_Text boxText;
        [SerializeField] private TutorialInputMask inputMask;
        [SerializeField, Min(0f)] private float autoAdvanceDelay = 0.35f;
        [SerializeField] private Vector2 defaultBoxPosition = new Vector2(0f, -260f);
        [SerializeField] private Vector2 boxPadding = new Vector2(56f, 38f);
        [SerializeField] private Vector2 boxMaxSize = new Vector2(760f, 300f);

        private Coroutine _presentationRoutine;
        private Coroutine _managerBindingRoutine;
        private readonly string[] _singleHighlightKey = new string[1];
        private TutorialManager _boundManager;
        private SpeechBubblePool _speechBubblePool;
        private UISpeechBubbleController _guideSpeechBubble;
        private Transform _guideSpeechTarget;
        private TutorialDialogueContext _currentContext;
        private bool _hasCurrentContext;
        private bool _isGuideSpeechTyping;

        #region Properties

        public static TutorialPresentationController Current { get; private set; }
        public bool IsConfigured { get; private set; }

        #endregion

        #region MonoBehaviour

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }

            Current = this;
            DontDestroyOnLoad(gameObject);

            if (!TryValidateViewReferences(out string failReason))
            {
                Debug.LogError($"[TutorialPresentationController] {failReason}", this);
                Current = null;
                enabled = false;
                Destroy(gameObject);
                return;
            }

            IsConfigured = true;
            HidePresentation();
        }

        private void OnEnable()
        {
            TutorialUIAnchorRegistry.OnRegistryChanged += HandleAnchorRegistryChanged;
            Canvas.willRenderCanvases += HandleCanvasWillRender;

            if (!TryBindTutorialManager())
                _managerBindingRoutine = StartCoroutine(BindTutorialManagerWhenReady());
        }

        private void OnDisable()
        {
            TutorialUIAnchorRegistry.OnRegistryChanged -= HandleAnchorRegistryChanged;
            Canvas.willRenderCanvases -= HandleCanvasWillRender;

            if (_managerBindingRoutine != null)
            {
                StopCoroutine(_managerBindingRoutine);
                _managerBindingRoutine = null;
            }

            UnbindTutorialManager();
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        #endregion

        #region Public API

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_hasCurrentContext || _currentContext.Type != TutorialDialogueType.Stop)
                return;

            if (_guideSpeechBubble != null && _guideSpeechBubble.CompleteTyping())
                return;

            if (_boundManager == null
                || _boundManager.PlaybackState != TutorialPlaybackState.WaitingForInput)
            {
                return;
            }

            if (!_boundManager.TryAdvanceFromInput(out string failReason))
                Debug.LogWarning($"[TutorialPresentationController] {failReason}");
        }

        public bool TryValidateSceneDependencies(out string failReason)
        {
            if (TryResolveSpeechBubblePool())
            {
                if (_speechBubblePool.TryValidateConfiguration(out failReason))
                    return true;

                failReason = $"현재 씬의 SpeechBubblePool 설정이 올바르지 않습니다: {failReason}";
                return false;
            }

            failReason = "현재 씬에 SpeechBubblePool이 없습니다";
            return false;
        }

        #endregion

        #region Event Handlers

        private void HandleDialogueChanged(TutorialDialogueContext context)
        {
            Present(context);
        }

        private void HandleTutorialEnded(string _)
        {
            HidePresentation();
        }

        private void HandleTutorialFailed(string _, string __)
        {
            HidePresentation();
        }

        private void HandleConditionProgressChanged(TutorialConditionProgressContext _)
        {
            if (_boundManager == null
                || !_boundManager.TryGetCurrentDialogue(out TutorialDialogueContext context))
            {
                return;
            }

            _currentContext = context;
            RefreshLayout();
        }

        private void HandleAnchorRegistryChanged()
        {
            RefreshLayout();
        }

        private void HandleCanvasWillRender()
        {
            if (_hasCurrentContext)
                RefreshLayout();
        }

        private void HandleGuideSpeechTypingCompleted()
        {
            _isGuideSpeechTyping = false;
        }

        #endregion

        #region Private Helpers

        private IEnumerator BindTutorialManagerWhenReady()
        {
            while (isActiveAndEnabled && !TryBindTutorialManager())
                yield return null;

            _managerBindingRoutine = null;
        }

        private bool TryBindTutorialManager()
        {
            if (_boundManager != null)
                return true;

            if (!TutorialManager.HasInstance || !TutorialManager.Instance.IsInitialized)
                return false;

            _boundManager = TutorialManager.Instance;
            _boundManager.OnDialogueChanged += HandleDialogueChanged;
            _boundManager.OnConditionProgressChanged += HandleConditionProgressChanged;
            _boundManager.OnTutorialCompleted += HandleTutorialEnded;
            _boundManager.OnTutorialCancelled += HandleTutorialEnded;
            _boundManager.OnTutorialFailed += HandleTutorialFailed;

            if (_boundManager.TryGetCurrentDialogue(out TutorialDialogueContext context))
                Present(context);

            return true;
        }

        private void UnbindTutorialManager()
        {
            if (_boundManager == null)
                return;

            _boundManager.OnDialogueChanged -= HandleDialogueChanged;
            _boundManager.OnConditionProgressChanged -= HandleConditionProgressChanged;
            _boundManager.OnTutorialCompleted -= HandleTutorialEnded;
            _boundManager.OnTutorialCancelled -= HandleTutorialEnded;
            _boundManager.OnTutorialFailed -= HandleTutorialFailed;
            _boundManager = null;
        }

        private bool TryValidateViewReferences(out string failReason)
        {
            if (presentationCanvas == null)
            {
                failReason = "Presentation Canvas 참조가 없습니다";
                return false;
            }

            if (GetComponent<CanvasScaler>() == null || GetComponent<GraphicRaycaster>() == null)
            {
                failReason = "CanvasScaler 또는 GraphicRaycaster가 없습니다";
                return false;
            }

            if (contentRoot == null || inputCatcher == null
                || boxRoot == null || boxText == null || inputMask == null)
            {
                failReason = "필수 Tutorial UI 참조가 연결되지 않았습니다";
                return false;
            }

            if (!inputMask.TryValidateConfiguration(out failReason))
                return false;

            failReason = string.Empty;
            return true;
        }

        private void Present(TutorialDialogueContext context)
        {
            StopPresentationRoutine();
            _currentContext = context;
            _hasCurrentContext = true;

            if (contentRoot != null)
                contentRoot.gameObject.SetActive(true);

            bool isBox = context.Type == TutorialDialogueType.Box;
            if (!isBox && !TryPresentGuideSpeech(context.Script, out string failReason))
            {
                FailCurrentPresentation(failReason);
                return;
            }

            if (boxRoot != null)
                boxRoot.gameObject.SetActive(isBox);
            if (inputCatcher != null)
                inputCatcher.SetActive(!isBox);

            if (isBox)
            {
                bool blockOutside = context.InputPolicy == TutorialInputPolicy.BlockOutsidePrimaryHighlight;
                inputMask?.Show(GetVisibleHighlightKeys(context), blockOutside);
            }
            else
                inputMask?.Hide();

            if (isBox)
                ConfigureText(boxText, boxRoot, context.Script, boxPadding, boxMaxSize);

            RefreshLayout();
            _presentationRoutine = StartCoroutine(PresentationRoutine(context));
        }

        private IEnumerator PresentationRoutine(TutorialDialogueContext context)
        {
            yield return null;

            if (context.Type == TutorialDialogueType.Box)
            {
                boxText.maxVisibleCharacters = int.MaxValue;
            }
            else
            {
                while (_isGuideSpeechTyping)
                    yield return null;
            }

            if (_boundManager == null)
            {
                _presentationRoutine = null;
                yield break;
            }

            if (!_boundManager.NotifyCurrentDialoguePresented(out string failReason))
            {
                _presentationRoutine = null;
                FailCurrentPresentation(failReason);
                yield break;
            }

            if (context.Type != TutorialDialogueType.Auto)
            {
                _presentationRoutine = null;
                yield break;
            }

            if (autoAdvanceDelay > 0f)
                yield return new WaitForSecondsRealtime(autoAdvanceDelay);

            _presentationRoutine = null;
            if (_boundManager != null
                && !_boundManager.TryAdvanceAutomatically(out failReason))
            {
                Debug.LogWarning($"[TutorialPresentationController] {failReason}");
            }
        }

        private void ConfigureText(
            TMP_Text targetText,
            RectTransform targetRoot,
            string content,
            Vector2 padding,
            Vector2 maxSize)
        {
            if (targetText == null || targetRoot == null)
                return;

            targetText.text = content ?? string.Empty;
            targetText.maxVisibleCharacters = int.MaxValue;
            float textWidth = Mathf.Max(1f, maxSize.x - padding.x);
            Vector2 preferred = targetText.GetPreferredValues(targetText.text, textWidth, 0f);
            targetRoot.sizeDelta = new Vector2(
                Mathf.Min(maxSize.x, preferred.x + padding.x),
                Mathf.Min(maxSize.y, preferred.y + padding.y));
        }

        private void RefreshLayout()
        {
            if (!_hasCurrentContext || inputMask == null || inputMask.OverlayRoot == null)
                return;

            if (_currentContext.Type != TutorialDialogueType.Box)
                return;

            RectTransform targetRoot = boxRoot;
            if (targetRoot != null)
            {
                Vector2 position = _currentContext.BoxPosition ?? defaultBoxPosition;
                if (!_currentContext.BoxPosition.HasValue
                    && TutorialUIAnchorRegistry.TryGetPlacement(
                        _currentContext.PlacementKey,
                        out TutorialUIAnchor anchor)
                    && anchor.TryGetRect(inputMask.OverlayRoot, out Rect anchorRect))
                {
                    position = anchorRect.center + anchor.PlacementOffset;
                }

                targetRoot.anchoredPosition = ClampToOverlay(targetRoot, position, inputMask.OverlayRoot.rect);
            }

            inputMask.Refresh(GetVisibleHighlightKeys(_currentContext));
        }

        private IReadOnlyList<string> GetVisibleHighlightKeys(TutorialDialogueContext context)
        {
            if (!context.UsesSequentialHighlights)
                return context.HighlightKeys;

            int index = context.ConditionProgress;
            if (index < 0 || index >= context.HighlightKeys.Count)
                return System.Array.Empty<string>();

            _singleHighlightKey[0] = context.HighlightKeys[index];
            return _singleHighlightKey;
        }

        private static Vector2 ClampToOverlay(RectTransform target, Vector2 position, Rect bounds)
        {
            Vector2 halfSize = target.rect.size * 0.5f;
            position.x = Mathf.Clamp(position.x, bounds.xMin + halfSize.x, bounds.xMax - halfSize.x);
            position.y = Mathf.Clamp(position.y, bounds.yMin + halfSize.y, bounds.yMax - halfSize.y);
            return position;
        }

        private void HidePresentation()
        {
            StopPresentationRoutine();
            _hasCurrentContext = false;
            inputMask?.Hide();

            if (contentRoot != null)
                contentRoot.gameObject.SetActive(false);
        }

        private void StopPresentationRoutine()
        {
            if (_presentationRoutine != null)
            {
                StopCoroutine(_presentationRoutine);
                _presentationRoutine = null;
            }

            StopGuideSpeech();
        }

        private bool TryPresentGuideSpeech(string content, out string failReason)
        {
            GuideMove guide = FindFirstObjectByType<GuideMove>();
            if (guide == null)
            {
                failReason = "현재 씬에서 가이드 NPC를 찾을 수 없습니다";
                return false;
            }

            if (!TryResolveSpeechBubblePool())
            {
                failReason = "현재 씬에서 SpeechBubblePool을 찾을 수 없습니다";
                return false;
            }

            if (!_speechBubblePool.TryValidateConfiguration(out failReason))
            {
                failReason = $"SpeechBubblePool 설정이 올바르지 않습니다: {failReason}";
                return false;
            }

            _guideSpeechTarget = guide.transform;
            _guideSpeechBubble = _speechBubblePool.RequestBubble();
            if (_guideSpeechBubble == null)
            {
                failReason = "SpeechBubblePool에서 말풍선을 가져올 수 없습니다";
                return false;
            }

            _isGuideSpeechTyping = true;
            _guideSpeechBubble.PlayPersistentText(
                _guideSpeechTarget,
                content,
                HandleGuideSpeechTypingCompleted);
            failReason = string.Empty;
            return true;
        }

        private void FailCurrentPresentation(string failReason)
        {
            string normalizedReason = string.IsNullOrWhiteSpace(failReason)
                ? "가이드 NPC 말풍선을 표시할 수 없습니다"
                : failReason.Trim();
            Debug.LogError($"[TutorialPresentationController] {normalizedReason}");
            HidePresentation();

            if (_boundManager == null || !_boundManager.FailActiveTutorial(normalizedReason))
            {
                Debug.LogError("[TutorialPresentationController] 표시 실패 상태를 TutorialManager에 전달하지 못했습니다");
            }
        }

        private bool TryResolveSpeechBubblePool()
        {
            if (_speechBubblePool == null)
                _speechBubblePool = FindFirstObjectByType<SpeechBubblePool>();

            return _speechBubblePool != null;
        }

        private void StopGuideSpeech()
        {
            _isGuideSpeechTyping = false;

            if (_guideSpeechBubble != null
                && _guideSpeechBubble.IsBoundTo(_guideSpeechTarget))
            {
                _guideSpeechBubble.Stop();
            }

            _guideSpeechBubble = null;
            _guideSpeechTarget = null;
        }

        #endregion
    }
}
