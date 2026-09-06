using SeaVillage.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SeaVillage.UI.Tutorial
{
    /// <summary>
    /// 런타임 생성 UI를 Tutorial Anchor에 연결하는 보조 API
    /// </summary>
    public static class TutorialUIBinding
    {
        public static TutorialUIAnchor Bind(
            Component target,
            string highlightKey,
            string placementKey = "")
        {
            if (target == null || target.transform is not RectTransform rectTransform)
                return null;

            TutorialUIAnchor anchor = target.GetComponent<TutorialUIAnchor>();
            if (anchor == null)
                anchor = target.gameObject.AddComponent<TutorialUIAnchor>();

            anchor.Configure(placementKey, highlightKey, rectTransform);
            return anchor;
        }

        public static TutorialUIAnchor BindComposite(
            Component primaryTarget,
            Component additionalTarget,
            string highlightKey,
            string placementKey = "")
        {
            if (primaryTarget == null
                || additionalTarget == null
                || primaryTarget.transform is not RectTransform primaryRect
                || additionalTarget.transform is not RectTransform additionalRect)
            {
                return null;
            }

            TutorialUIAnchor anchor = primaryTarget.GetComponent<TutorialUIAnchor>();
            if (anchor == null)
                anchor = primaryTarget.gameObject.AddComponent<TutorialUIAnchor>();

            anchor.Configure(placementKey, highlightKey, primaryRect, additionalRect);
            return anchor;
        }
    }

    /// <summary>
    /// 별도 Button이 없는 UI 영역의 Pointer 입력을 Signal로 변환하는 Adapter
    /// </summary>
    public sealed class TutorialSignalPointer : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TutorialEventType eventType;
        [SerializeField] private string targetId = string.Empty;

        #region Public API

        public void Configure(TutorialEventType newEventType, string newTargetId, string highlightKey, string placementKey = "")
        {
            ConfigureEvent(newEventType, newTargetId);
            TutorialUIBinding.Bind(this, highlightKey, placementKey);
        }

        public void ConfigureEvent(TutorialEventType newEventType, string newTargetId)
        {
            eventType = newEventType;
            targetId = newTargetId?.Trim() ?? string.Empty;

            Graphic graphic = GetComponent<Graphic>();
            if (graphic != null)
                graphic.raycastTarget = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            TutorialEventReporter.Report(eventType, targetId, source: TutorialEventSource.UserInterface);
        }

        #endregion
    }
}
