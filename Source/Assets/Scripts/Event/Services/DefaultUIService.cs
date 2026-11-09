using System.Collections;
using UnityEngine;
using SeaVillage.UI;

namespace SeaVillage.Event.Services
{
    /// <summary>
    /// 이벤트 UI 표시 서비스. 해양 선택지는 Event Panel로, 마을 단일 확인은 System Message Panel로 표시한다.
    /// </summary>
    public class DefaultUIService : IUIService
    {
        public IEnumerator ShowPanel(string panelName, string content, bool isChoicePrompt)
        {
            if (isChoicePrompt)
            {
                EventPanel panel = UIManager.HasInstance ? UIManager.Instance.OpenPanel<EventPanel>() : null;
                if (panel != null)
                {
                    panel.SetIllustration(ResolveSprite(panelName));
                    panel.ShowBody(content);
                }
                yield break;
            }
        }

        public IEnumerator ShowPopup(string title, string content, string confirmText, System.Action<bool> onCompleted)
        {
            SystemMessagePanel panel = UIManager.HasInstance
                ? UIManager.Instance.OpenPanel<SystemMessagePanel>()
                : null;
            if (panel == null)
            {
                Debug.LogError("[EventUIService] 이벤트 팝업 패널을 열 수 없습니다");
                onCompleted?.Invoke(false);
                yield break;
            }

            bool confirmed = false;
            panel.SetupAlert(content, () => confirmed = true, title, confirmText);
            while (!confirmed && panel != null && panel.State != PanelState.Closed)
                yield return null;

            if (!confirmed)
            {
                onCompleted?.Invoke(false);
                yield break;
            }

            while (panel != null && panel.State != PanelState.Closed)
                yield return null;

            onCompleted?.Invoke(true);
        }

        private static Sprite ResolveSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;
            return UIManager.HasInstance ? UIManager.Instance.LoadItemIcon(spriteName) : null;
        }
    }
}
