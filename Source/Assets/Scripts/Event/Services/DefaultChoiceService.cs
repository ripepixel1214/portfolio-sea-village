using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SeaVillage.Data;
using SeaVillage.UI;

namespace SeaVillage.Event.Services
{
    /// <summary>
    /// 이벤트 선택지 처리 서비스. Event Panel 에 선택지(2~3개)를 얹어 표시하고,
    /// 플레이어가 고른 선택지를 이벤트 시퀀스에 반영한다.
    /// </summary>
    public class DefaultChoiceService : IChoiceService
    {
        public IEnumerator Choose(List<EventSequenceData> options, Action<EventSequenceData> onChosen)
        {
            if (options == null || options.Count == 0)
            {
                onChosen?.Invoke(null);
                yield break;
            }

            EventPanel panel = UIManager.HasInstance ? UIManager.Instance.OpenPanel<EventPanel>() : null;
            if (panel == null)
            {
                // UI 가 없으면 첫 선택지로 폴백
                onChosen?.Invoke(options[0]);
                yield break;
            }

            var labels = new List<string>(options.Count);
            foreach (EventSequenceData option in options)
                labels.Add(option.Value);

            bool picked = false;
            int pickedIndex = 0;
            // message=null → 직전 프롬프트(ShowBody) 본문 유지하고 선택지만 얹는다.
            panel.ShowStep(null, labels, index => { pickedIndex = index; picked = true; });

            while (!picked)
                yield return null;

            int safeIndex = Mathf.Clamp(pickedIndex, 0, options.Count - 1);
            EventSequenceData chosen = options[safeIndex];

            // 이벤트가 이 선택으로 끝날 때만 패널을 닫는다. 중간 선택이면 결과 레이아웃이 같은 패널을 재사용하도록 열어둔다.
            if (chosen.NextStep == -1 && UIManager.HasInstance)
                UIManager.Instance.ClosePanel<EventPanel>();

            onChosen?.Invoke(chosen);
        }
    }
}
