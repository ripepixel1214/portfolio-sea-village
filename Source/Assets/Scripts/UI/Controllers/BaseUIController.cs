using System;
using UnityEngine;
using UnityEngine.UI;
using SeaVillage.Core;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    /// <summary>
    /// 모든 인게임 UI Controller의 기본 클래스
    /// </summary>
    public abstract class BaseUIController : MonoBehaviour
    {
        [Header("Base UI Buttons")]
        [SerializeField] protected Button menuButton;
        [SerializeField] protected Button leftButton;
        [SerializeField] protected Button rightButton;
        [SerializeField] protected Button upButton;
        [SerializeField] protected Button downButton;
        [SerializeField] protected Button interactionButton;
        
        protected MenuPanel menuPanel => uiManager.GetPanel<MenuPanel>();
        
        // Reference
        protected UIManager uiManager;

        #region MonoBehaviour
        protected virtual void Awake()
        {
            uiManager = UIManager.Instance;
        }

        protected virtual void Start()
        {
            FindBaseUIElements();
            BindTutorialAnchors();
            RegisterAllPanels();
            RegisterCommonEvents();
            RegisterSceneSpecificEvents();
            AttachClickSfxToButtons();
            InitializeUIElements();
        }
        #endregion

        protected virtual void InitializeUIElements() {}

        protected virtual void BindTutorialAnchors()
        {
            TutorialUIBinding.Bind(interactionButton, TutorialAnchorKeys.InteractionButton);
        }

        /// <summary>
        /// 클릭 효과음을 붙일 버튼을 지정. RegisterButton이 RemoveAllListeners를 호출하므로 이벤트 등록 이후에 실행된다
        /// </summary>
        protected virtual void AttachClickSfxToButtons()
        {
            AttachClickSfx(interactionButton);
        }

        // 중복 등록을 막기 위해 해제 후 등록한다
        protected static void AttachClickSfx(params Button[] buttons)
        {
            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;

                button.onClick.RemoveListener(Audio.AudioManager.TryPlayClickSfx);
                button.onClick.AddListener(Audio.AudioManager.TryPlayClickSfx);
            }
        }

        protected virtual void FindBaseUIElements()
        {
            if (menuButton == null)
                menuButton = GameObject.Find("Main Canvas/Menu Button")?.GetComponent<Button>();

            if (leftButton == null)
                leftButton = GameObject.Find("Main Canvas/Move Button/Left Button")?.GetComponent<Button>();
            
            if (rightButton == null)
                rightButton = GameObject.Find("Main Canvas/Move Button/Right Button")?.GetComponent<Button>();

            if (upButton == null)
                upButton = GameObject.Find("Main Canvas/Move Button/Up Button")?.GetComponent<Button>();

            if (downButton == null)
                downButton = GameObject.Find("Main Canvas/Move Button/Down Button")?.GetComponent<Button>();

            if (interactionButton == null)
                interactionButton = GameObject.Find("Main Canvas").transform.Find("Interaction Button")?.GetComponent<Button>();
        }

        protected virtual void RegisterAllPanels()
        {
            // panelRoot 기준으로 비컨텍스트 패널을 선등록
            uiManager.RegisterPanelsFromPanelRoot();
        }

        /// <summary>
        /// 버튼 이벤트 연결
        /// </summary>
        protected void RegisterButton(Button button, Action action, string buttonName = "")
        {
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => action());
            }
        }

        #region Events
        /// <summary>
        /// 공통 이벤트 등록
        /// </summary>
        protected virtual void RegisterCommonEvents()
        {
            // const 문자열 참조는 UIManager에 남아있으므로 유지
            if (menuButton != null)
                RegisterButton(menuButton, () => OnMenuButtonClicked(), UIManager.MenuButtonName);

            if (interactionButton != null)
                RegisterButton(interactionButton, () => OnInteractionButtonClicked(), UIManager.InteractionButtonName);
        }

        /// <summary>
        /// 씬별 특수 이벤트 등록 (하위 클래스에서 구현)
        /// </summary>
        protected abstract void RegisterSceneSpecificEvents();

        #endregion

        #region Onclick methods
        protected virtual void OnMenuButtonClicked()
        {
            if (menuPanel != null)
            {
                if (menuPanel.gameObject.activeInHierarchy)
                    menuPanel.Close();
                else
                    OpenPanel<MenuPanel>();
            }
            else
                OpenPanel<MenuPanel>();
        }

        protected virtual void OnInteractionButtonClicked()
        {
            Player.Player player = Core.GameManager.Instance.Player;

            if (player == null)
            {
                Debug.LogWarning("GameManager의 Player를 참조할 수 없습니다.");
                return;
            }
            else if (player.Interactor == null)
            {
                Debug.LogWarning("GameManager의 Player Interactor를 참조할 수 없습니다.");
                return;
            }

            if (uiManager.IsAnyPanelOpened)
                uiManager.CurrentActivePanel.ClickSelectedButton();
            else
                player.Interactor.TryInteract();
        }
        #endregion

        #region Utility Methods
        protected void OpenPanel<T>() where T : UIPanel
        {
            uiManager.OpenPanel<T>();
        }

        protected void ClosePanel<T>() where T : UIPanel
        {
            uiManager.ClosePanel<T>();
        }

        public void CloseCurrentPanel()
        {
            uiManager.CloseCurrentPanel();
        }

        #endregion
    }
}
