using System.Collections;
using UnityEngine;
using SeaVillage.UI;

namespace SeaVillage.Event.Services
{
    /// <summary>
    /// 이벤트 UI 표시 서비스. 선택지 직전 프롬프트는 Event Panel(본문/일러스트)로,
    /// 그 외 내러티브/결과 메시지는 Toast Panel 로 표시한다.
    /// </summary>
    public class DefaultUIService : IUIService
    {
        public IEnumerator ShowDialogue(string npcId, string dialogueText)
        {
            // 대화창 연동은 추후 작업
            yield break;
        }

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

        private static Sprite ResolveSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;
            return UIManager.HasInstance ? UIManager.Instance.LoadItemIcon(spriteName) : null;
        }
    }
}
