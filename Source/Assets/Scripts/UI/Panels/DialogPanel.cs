using UnityEngine;

namespace SeaVillage.UI
{
    /// <summary>
    /// 대화 패널
    /// </summary>
    public class DialogPanel : UIPanel
    {
        #region UIPanel Overrides
        public override void OnOpen()
        {
            base.OnOpen();
            Debug.Log("Dialog Panel opened");
        }
        #endregion
    }
}