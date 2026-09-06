using System.Collections.Generic;
using UnityEngine;

namespace SeaVillage.UI.Tutorial
{
    /// <summary>
    /// 강조 영역 외부 입력을 차단하고 Highlight Frame을 배치하는 View
    /// </summary>
    public sealed class TutorialInputMask : MonoBehaviour
    {
        [SerializeField] private RectTransform overlayRoot;
        [SerializeField] private GameObject blockerRoot;
        [SerializeField] private RectTransform topBlocker;
        [SerializeField] private RectTransform bottomBlocker;
        [SerializeField] private RectTransform leftBlocker;
        [SerializeField] private RectTransform rightBlocker;
        [SerializeField] private RectTransform highlightRoot;
        [SerializeField] private RectTransform highlightFrameTemplate;

        private readonly List<RectTransform> _highlightFrames = new List<RectTransform>();
        private bool _shouldBlockOutsidePrimaryHighlight;

        #region Properties

        public RectTransform OverlayRoot => overlayRoot;

        #endregion

        #region MonoBehaviour

        private void Awake()
        {
            if (highlightFrameTemplate != null)
                highlightFrameTemplate.gameObject.SetActive(false);
        }

        #endregion

        #region Public API

        public bool TryValidateConfiguration(out string failReason)
        {
            if (overlayRoot == null || blockerRoot == null
                || topBlocker == null || bottomBlocker == null
                || leftBlocker == null || rightBlocker == null
                || highlightRoot == null || highlightFrameTemplate == null)
            {
                failReason = "Tutorial Input Mask 참조가 연결되지 않았습니다";
                return false;
            }

            failReason = string.Empty;
            return true;
        }

        public void Show(IReadOnlyList<string> highlightKeys, bool blockOutsidePrimaryHighlight)
        {
            _shouldBlockOutsidePrimaryHighlight = blockOutsidePrimaryHighlight;
            if (blockerRoot != null)
                blockerRoot.SetActive(false);

            Refresh(highlightKeys);
        }

        public void Hide()
        {
            _shouldBlockOutsidePrimaryHighlight = false;
            if (blockerRoot != null)
                blockerRoot.SetActive(false);

            SetFrameCount(0);
        }

        public void Refresh(IReadOnlyList<string> highlightKeys)
        {
            if (overlayRoot == null)
                return;

            int resolvedCount = 0;
            bool hasPrimaryRect = false;
            Rect primaryRect = default;
            int keyCount = highlightKeys?.Count ?? 0;
            for (int i = 0; i < keyCount; i++)
            {
                string key = highlightKeys[i];
                if (!TutorialUIAnchorRegistry.TryGetHighlight(key, out TutorialUIAnchor anchor)
                    || !anchor.TryGetRect(overlayRoot, out Rect rect))
                {
                    continue;
                }

                RectTransform frame = GetOrCreateFrame(resolvedCount);
                if (frame == null)
                    continue;

                ApplyRect(frame, rect);
                frame.gameObject.SetActive(true);
                resolvedCount++;

                if (!hasPrimaryRect)
                {
                    primaryRect = rect;
                    hasPrimaryRect = true;
                }
            }

            SetFrameCount(resolvedCount);
            if (blockerRoot == null || !_shouldBlockOutsidePrimaryHighlight)
            {
                if (blockerRoot != null)
                    blockerRoot.SetActive(false);
                return;
            }

            if (hasPrimaryRect)
            {
                blockerRoot.SetActive(true);
                ApplyBlockerRects(primaryRect);
            }
            else
            {
                blockerRoot.SetActive(true);
                ApplyFullBlockerRect();
            }
        }

        #endregion

        #region Private Helpers

        private RectTransform GetOrCreateFrame(int index)
        {
            if (index < _highlightFrames.Count)
                return _highlightFrames[index];

            if (highlightFrameTemplate == null || highlightRoot == null)
                return null;

            RectTransform frame = Instantiate(highlightFrameTemplate, highlightRoot);
            frame.name = $"Highlight Frame {index + 1}";
            _highlightFrames.Add(frame);
            return frame;
        }

        private void SetFrameCount(int visibleCount)
        {
            for (int i = 0; i < _highlightFrames.Count; i++)
                _highlightFrames[i].gameObject.SetActive(i < visibleCount);
        }

        private void ApplyBlockerRects(Rect hole)
        {
            Rect bounds = overlayRoot.rect;
            float xMin = Mathf.Clamp(hole.xMin, bounds.xMin, bounds.xMax);
            float xMax = Mathf.Clamp(hole.xMax, bounds.xMin, bounds.xMax);
            float yMin = Mathf.Clamp(hole.yMin, bounds.yMin, bounds.yMax);
            float yMax = Mathf.Clamp(hole.yMax, bounds.yMin, bounds.yMax);

            SetRect(topBlocker, bounds.xMin, yMax, bounds.width, bounds.yMax - yMax);
            SetRect(bottomBlocker, bounds.xMin, bounds.yMin, bounds.width, yMin - bounds.yMin);
            SetRect(leftBlocker, bounds.xMin, yMin, xMin - bounds.xMin, yMax - yMin);
            SetRect(rightBlocker, xMax, yMin, bounds.xMax - xMax, yMax - yMin);
        }

        private void ApplyFullBlockerRect()
        {
            Rect bounds = overlayRoot.rect;
            SetRect(topBlocker, bounds.xMin, bounds.yMin, bounds.width, bounds.height);
            SetRect(bottomBlocker, 0f, 0f, 0f, 0f);
            SetRect(leftBlocker, 0f, 0f, 0f, 0f);
            SetRect(rightBlocker, 0f, 0f, 0f, 0f);
        }

        private static void ApplyRect(RectTransform target, Rect rect)
        {
            if (target == null)
                return;

            target.anchorMin = new Vector2(0.5f, 0.5f);
            target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.anchoredPosition = rect.center;
            target.sizeDelta = rect.size;
        }

        private static void SetRect(
            RectTransform target,
            float x,
            float y,
            float width,
            float height)
        {
            if (target == null)
                return;

            float safeWidth = Mathf.Max(0f, width);
            float safeHeight = Mathf.Max(0f, height);
            target.anchorMin = new Vector2(0.5f, 0.5f);
            target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0f, 0f);
            target.anchoredPosition = new Vector2(x, y);
            target.sizeDelta = new Vector2(safeWidth, safeHeight);
            bool shouldBeActive = safeWidth > 0f && safeHeight > 0f;
            if (target.gameObject.activeSelf != shouldBeActive)
                target.gameObject.SetActive(shouldBeActive);
        }

        #endregion
    }
}
