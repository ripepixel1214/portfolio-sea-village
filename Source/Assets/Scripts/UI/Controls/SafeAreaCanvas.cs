using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeviceScreen = UnityEngine.Device.Screen;

namespace SeaVillage.UI
{
    /// <summary>
    /// UI 경로를 유지하면서 HUD와 패널 콘텐츠를 기기의 안전 영역 안에 배치한다.
    /// 전체 화면 컨테이너와 배경, 입력 차단 영역은 화면 전체를 유지한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class SafeAreaCanvas : MonoBehaviour
    {
        private struct Target
        {
            public RectTransform Rect;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
        }

        private readonly List<Target> _targets = new List<Target>();
        private Canvas _canvas;
        private Rect _lastSafeArea;
        private Rect _lastViewport;
        private bool _hasApplied;

        private void OnEnable()
        {
            RefreshTargets();
        }

        private void LateUpdate()
        {
            ApplyCurrentSafeArea();
        }

        private void OnDisable()
        {
            RestoreAnchors();
            _targets.Clear();
            _hasApplied = false;
        }

        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled)
                RefreshTargets();
        }

        /// <summary>
        /// 기존 전체 화면 패널 아래에 콘텐츠를 추가한 뒤 보정 대상을 갱신한다.
        /// </summary>
        public void RefreshTargets()
        {
            RestoreAnchors();
            _targets.Clear();
            _canvas = GetComponent<Canvas>();
            _hasApplied = false;

            if (_canvas.renderMode == RenderMode.WorldSpace || !_canvas.isRootCanvas)
                return;

            CollectTargets(transform);
            ApplyCurrentSafeArea();
        }

        private void CollectTargets(Transform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (!(parent.GetChild(i) is RectTransform child))
                    continue;

                // 전체 화면 패널의 배경과 입력 차단 영역은 유지하고 내부 콘텐츠만 보정한다.
                // 비활성화된 팝업도 포함해 열릴 때부터 안전 영역에 배치되도록 한다.
                bool fullScreen = child.anchorMin == Vector2.zero
                    && child.anchorMax == Vector2.one
                    && child.offsetMin == Vector2.zero
                    && child.offsetMax == Vector2.zero;
                if (fullScreen && child.GetComponent<LayoutGroup>() == null)
                {
                    CollectTargets(child);
                    continue;
                }

                _targets.Add(new Target
                {
                    Rect = child,
                    AnchorMin = child.anchorMin,
                    AnchorMax = child.anchorMax
                });
            }
        }

        private void ApplyCurrentSafeArea()
        {
            if (_canvas == null || !isActiveAndEnabled)
                return;

            // 에디터에서는 Device.Screen을 통해 Device Simulator의 화면 정보도 반영한다.
            ApplySafeArea(DeviceScreen.safeArea, _canvas.pixelRect);
        }

        /// <summary>
        /// 화면 픽셀 기준의 안전 영역을 Canvas 뷰포트 기준 앵커로 변환한다.
        /// </summary>
        public void ApplySafeArea(Rect safeArea, Rect viewport)
        {
            if (viewport.width <= 0f || viewport.height <= 0f
                || safeArea.width <= 0f || safeArea.height <= 0f)
                return; // 화면 회전이나 초기화 중에는 화면 정보가 일시적으로 비어 있을 수 있다.

            if (_hasApplied && safeArea == _lastSafeArea && viewport == _lastViewport)
                return;

            Vector2 minimum = new Vector2(
                Mathf.Clamp01((safeArea.xMin - viewport.xMin) / viewport.width),
                Mathf.Clamp01((safeArea.yMin - viewport.yMin) / viewport.height));
            Vector2 maximum = new Vector2(
                Mathf.Clamp01((safeArea.xMax - viewport.xMin) / viewport.width),
                Mathf.Clamp01((safeArea.yMax - viewport.yMin) / viewport.height));
            if (maximum.x <= minimum.x || maximum.y <= minimum.y)
                return;

            Vector2 span = maximum - minimum;
            foreach (Target target in _targets)
            {
                if (target.Rect == null)
                    continue;

                // 크기, 위치 오프셋, 애니메이션과 직렬화된 오브젝트 참조는 유지한다.
                // 회전할 때 여백이 누적되지 않도록 항상 원래 앵커를 기준으로 계산한다.
                target.Rect.anchorMin = minimum + Vector2.Scale(target.AnchorMin, span);
                target.Rect.anchorMax = minimum + Vector2.Scale(target.AnchorMax, span);
            }

            _lastSafeArea = safeArea;
            _lastViewport = viewport;
            _hasApplied = true;
        }

        private void RestoreAnchors()
        {
            foreach (Target target in _targets)
            {
                if (target.Rect == null)
                    continue;

                target.Rect.anchorMin = target.AnchorMin;
                target.Rect.anchorMax = target.AnchorMax;
            }
        }
    }
}
