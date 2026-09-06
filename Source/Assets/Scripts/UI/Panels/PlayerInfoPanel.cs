using UnityEngine;

namespace SeaVillage.UI
{
    /// <summary>
    /// 플레이어 정보 패널
    /// </summary>
    public class PlayerInfoPanel : UIPanel
    {
        #region UIPanel Overrides
        public override void OnOpen()
        {
            base.OnOpen();
            Debug.Log("Player Info Panel opened");
        }
        #endregion
    }
}