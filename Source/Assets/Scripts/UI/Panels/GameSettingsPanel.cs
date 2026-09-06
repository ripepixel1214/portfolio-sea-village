using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    /// <summary>
    /// 인게임 설정 패널
    /// </summary>
    public class GameSettingsPanel : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        #region Event Listeners
        protected override void AddListeners()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
        }

        protected override void RemoveListeners()
        {
            if (closeButton != null)
                closeButton.onClick.RemoveListener(Close);
        }
        #endregion
    }
}