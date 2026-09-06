using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaVillage.UI.Tutorial
{
    /// <summary>
    /// 튜토리얼 UI 배치와 강조 대상을 키로 노출하는 Anchor
    /// </summary>
    public sealed class TutorialUIAnchor : MonoBehaviour
    {
        [SerializeField] private string placementKey = string.Empty;
        [SerializeField] private string highlightKey = string.Empty;
        [SerializeField] private RectTransform target;
        [SerializeField] private RectTransform additionalTarget;
        [SerializeField] private Vector2 placementOffset;
        [SerializeField] private Vector2 highlightPadding = new Vector2(12f, 12f);

        private readonly Vector3[] _worldCorners = new Vector3[4];

        #region Properties

        public string PlacementKey => placementKey?.Trim() ?? string.Empty;
        public string HighlightKey => highlightKey?.Trim() ?? string.Empty;
        public Vector2 PlacementOffset => placementOffset;

        #endregion

        #region MonoBehaviour

        private void Reset()
        {
            target = transform as RectTransform;
        }

        private void Awake()
        {
            if (target == null)
                target = transform as RectTransform;
        }

        private void OnEnable()
        {
            TutorialUIAnchorRegistry.Register(this);
        }

        private void OnDisable()
        {
            TutorialUIAnchorRegistry.Unregister(this);
        }

        #endregion

        #region Public API

        public void Configure(
            string newPlacementKey,
            string newHighlightKey,
            RectTransform newTarget = null,
            RectTransform newAdditionalTarget = null)
        {
            bool wasActive = isActiveAndEnabled;
            if (wasActive)
                TutorialUIAnchorRegistry.Unregister(this);

            placementKey = newPlacementKey?.Trim() ?? string.Empty;
            highlightKey = newHighlightKey?.Trim() ?? string.Empty;
            if (newTarget != null)
                target = newTarget;
            else if (target == null)
                target = transform as RectTransform;
            additionalTarget = newAdditionalTarget;

            if (wasActive)
                TutorialUIAnchorRegistry.Register(this);
        }

        public bool TryGetRect(RectTransform overlayRoot, out Rect rect)
        {
            rect = default;
            if (target == null || overlayRoot == null)
                return false;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            if (!TryEncapsulate(target, overlayRoot, ref min, ref max)
                || (additionalTarget != null
                    && !TryEncapsulate(additionalTarget, overlayRoot, ref min, ref max)))
            {
                return false;
            }

            Vector2 padding = new Vector2(Mathf.Max(0f, highlightPadding.x), Mathf.Max(0f, highlightPadding.y));
            rect = Rect.MinMaxRect(
                min.x - padding.x,
                min.y - padding.y,
                max.x + padding.x,
                max.y + padding.y);
            return true;
        }

        #endregion

        #region Private Helpers

        private bool TryEncapsulate(
            RectTransform rectTransform,
            RectTransform overlayRoot,
            ref Vector2 min,
            ref Vector2 max)
        {
            if (rectTransform == null || !rectTransform.gameObject.activeInHierarchy)
                return false;

            rectTransform.GetWorldCorners(_worldCorners);
            Canvas sourceCanvas = rectTransform.GetComponentInParent<Canvas>();
            Camera sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? sourceCanvas.worldCamera
                : null;

            for (int i = 0; i < _worldCorners.Length; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(sourceCamera, _worldCorners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        overlayRoot,
                        screenPoint,
                        null,
                        out Vector2 localPoint))
                {
                    return false;
                }

                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
            }
            return true;
        }

        #endregion
    }

    /// <summary>
    /// 활성 UI Anchor만 보관하는 비생성형 Registry
    /// </summary>
    public static class TutorialUIAnchorRegistry
    {
        private static readonly Dictionary<string, TutorialUIAnchor> PlacementAnchors =
            new Dictionary<string, TutorialUIAnchor>(StringComparer.Ordinal);
        private static readonly Dictionary<string, TutorialUIAnchor> HighlightAnchors =
            new Dictionary<string, TutorialUIAnchor>(StringComparer.Ordinal);

        #region Events

        public static event Action OnRegistryChanged;

        #endregion

        #region Public API

        public static void Register(TutorialUIAnchor anchor)
        {
            if (anchor == null)
                return;

            bool changed = RegisterKey(PlacementAnchors, anchor.PlacementKey, anchor, "PlacementKey");
            changed |= RegisterKey(HighlightAnchors, anchor.HighlightKey, anchor, "HighlightKey");
            if (changed)
                OnRegistryChanged?.Invoke();
        }

        public static void Unregister(TutorialUIAnchor anchor)
        {
            if (anchor == null)
                return;

            bool changed = UnregisterKey(PlacementAnchors, anchor.PlacementKey, anchor);
            changed |= UnregisterKey(HighlightAnchors, anchor.HighlightKey, anchor);
            if (changed)
                OnRegistryChanged?.Invoke();
        }

        public static bool TryGetPlacement(string key, out TutorialUIAnchor anchor)
        {
            return TryGet(PlacementAnchors, key, out anchor);
        }

        public static bool TryGetHighlight(string key, out TutorialUIAnchor anchor)
        {
            return TryGet(HighlightAnchors, key, out anchor);
        }

        #endregion

        #region Private Helpers

        private static bool RegisterKey(
            IDictionary<string, TutorialUIAnchor> anchors,
            string key,
            TutorialUIAnchor anchor,
            string keyType)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (anchors.TryGetValue(key, out TutorialUIAnchor existing) && existing != null && existing != anchor)
            {
                Debug.LogError($"[TutorialUIAnchorRegistry] 중복 {keyType}를 등록할 수 없습니다: {key}", anchor);
                return false;
            }

            anchors[key] = anchor;
            return true;
        }

        private static bool UnregisterKey(
            IDictionary<string, TutorialUIAnchor> anchors,
            string key,
            TutorialUIAnchor anchor)
        {
            if (string.IsNullOrWhiteSpace(key)
                || !anchors.TryGetValue(key, out TutorialUIAnchor existing)
                || existing != anchor)
            {
                return false;
            }

            anchors.Remove(key);
            return true;
        }

        private static bool TryGet(
            IDictionary<string, TutorialUIAnchor> anchors,
            string key,
            out TutorialUIAnchor anchor)
        {
            anchor = null;
            if (string.IsNullOrWhiteSpace(key) || !anchors.TryGetValue(key.Trim(), out TutorialUIAnchor candidate))
                return false;

            if (candidate != null && candidate.isActiveAndEnabled)
            {
                anchor = candidate;
                return true;
            }

            anchors.Remove(key.Trim());
            return false;
        }

        #endregion
    }
}
