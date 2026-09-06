using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SeaVillage.UI
{
    /// <summary>
    /// UI 패널 기본 클래스
    /// </summary>
    public class UIPanel : MonoBehaviour
    {
        [Header("Panel Settings")]
        [SerializeField] protected bool canClose = true;
        [SerializeField] protected bool blockInput = false;

        [Header("Button Settings")]
        [SerializeField, Tooltip("버튼 navigation을 위한 버튼 List, 자식 클래스에서 Navigate 함수를 오버라이딩하지 않을 경우 선형적으로 순회한다")]
        protected List<Button> navigableButtons = new List<Button>();

        [Tooltip("패널 활성화시 가장 먼저 선택될 버튼의 인덱스")]
        [SerializeField] protected int defaultSelectedButtonIndex = 1; // Close 버튼 제외한 첫 번째 버튼
        
        // Navigation state
        [SerializeField] protected int currentSelectedButtonIndex = 0;
        protected bool isNavigationEnabled = true;
        private PanelState _state = PanelState.Closed;

        [Header("Animation")]
        [SerializeField] protected Animator panelAnimator;

        // Properties
        public bool CanClose => canClose;
        public bool BlockInput => blockInput;
        public int CurrentSelectedButtonIndex => currentSelectedButtonIndex;
        public bool IsNavigationEnabled => isNavigationEnabled;
        public bool IsClosing => _state == PanelState.Closing;
        public PanelState State => _state;

        protected virtual void AddListeners() { }
        protected virtual void RemoveListeners() { }

        private void Awake()
        {
            // 창 오브젝트에 Animator 를 둔 패널은 인스펙터 지정을 그대로 쓴다.
            if (panelAnimator == null)
                panelAnimator = GetComponent<Animator>();
        }

        /// <summary>
        /// 패널이 열릴 때 호출
        /// </summary>
        public virtual void OnOpen()
        {
            _state = PanelState.Open;
            isNavigationEnabled = true;

            // 기본 선택 버튼 설정
            SelectButton(GetValidDefaultButtonIndex());

            AttachClickSfx();
            AddListeners();
        }

        /// <summary>
        /// 패널 닫기 요청 시 호출. OnClose()보다 먼저 호출되며, 입력을 차단
        /// </summary>
        public virtual void OnCloseRequested()
        {
            _state = PanelState.Closing;
            isNavigationEnabled = false;
        }

        public virtual IEnumerator PlayCloseAnimation()
        {
            if (panelAnimator == null)
                yield break;

            AnimatorUpdateMode previousUpdateMode = panelAnimator.updateMode;
            panelAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            panelAnimator.SetTrigger("Close");

            yield return null;

            float closeDuration = panelAnimator.GetCurrentAnimatorStateInfo(0).length;
            if (closeDuration > 0f)
                yield return new WaitForSecondsRealtime(closeDuration);

            if (panelAnimator != null)
                panelAnimator.updateMode = previousUpdateMode;
        }

        /// <summary>
        /// 패널이 닫힐 때 호출
        /// </summary>
        public virtual void OnClose()
        {
            RemoveListeners();
            _state = PanelState.Closed;
        }

        /// <summary>
        /// 패널 하위 모든 버튼에 클릭 효과음을 연결. OnOpen 이후 생성되는 슬롯은 BaseSlotUI가 스스로 연결한다
        /// </summary>
        private void AttachClickSfx()
        {
            // 중복 등록을 막기 위해 해제 후 등록한다
            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                button.onClick.RemoveListener(Audio.AudioManager.TryPlayClickSfx);
                button.onClick.AddListener(Audio.AudioManager.TryPlayClickSfx);
            }
        }

        /// <summary>
        /// 포커스가 복원될 때 호출
        /// </summary>
        public virtual void OnFocusRestored()
        {
            currentSelectedButtonIndex = GetValidDefaultButtonIndex();
            SelectButton(currentSelectedButtonIndex);
        }

        /// <summary>
        /// 현재 선택된 버튼 클릭 (외부에서 호출 가능)
        /// </summary>
        public virtual void ClickSelectedButton()
        {
            if (!isNavigationEnabled || navigableButtons.Count == 0) return;

            if (currentSelectedButtonIndex >= 0 && currentSelectedButtonIndex < navigableButtons.Count)
            {
                Button selectedButton = navigableButtons[currentSelectedButtonIndex];
                if (selectedButton != null && selectedButton.interactable)
                    selectedButton.onClick.Invoke();
            }
        }

        /// <summary>
        /// 이전 버튼으로 이동 (Up Button). 선택 불가한 버튼은 건너뛴다
        /// </summary>
        public virtual void NavigateToUpperButton()
        {
            int index = FindSelectableButtonIndex(currentSelectedButtonIndex, -1);
            if (index >= 0)
                SelectButton(index);
        }

        /// <summary>
        /// 다음 버튼으로 이동 (Down Button). 선택 불가한 버튼은 건너뛴다
        /// </summary>
        public virtual void NavigateToLowerButton()
        {
            int index = FindSelectableButtonIndex(currentSelectedButtonIndex, 1);
            if (index >= 0)
                SelectButton(index);
        }

        /// <summary>
        /// 해당 인덱스의 버튼을 키보드로 선택할 수 있는지 여부
        /// </summary>
        protected bool IsButtonSelectable(int index)
        {
            if (index < 0 || index >= navigableButtons.Count)
                return false;

            Button button = navigableButtons[index];
            return button != null && button.interactable && button.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// from에서 step 방향으로 순회하며 선택 가능한 첫 버튼의 인덱스를 반환. 없으면 -1
        /// </summary>
        protected int FindSelectableButtonIndex(int from, int step)
        {
            int count = navigableButtons.Count;
            if (count == 0 || step == 0)
                return -1;

            for (int i = 1; i <= count; i++)
            {
                int index = ((from + step * i) % count + count) % count;
                if (IsButtonSelectable(index))
                    return index;
            }

            return -1;
        }

        /// <summary>
        /// 현재 선택이 불가능해졌으면 선택 가능한 버튼으로 옮긴다. 버튼 활성 상태를 갱신한 뒤 호출한다
        /// </summary>
        protected void EnsureValidSelection()
        {
            if (IsButtonSelectable(currentSelectedButtonIndex))
                return;

            SelectButton(GetValidDefaultButtonIndex());
        }

        public virtual void NavigateToLeftButton() { }

        public virtual void NavigateToRightButton() { }

        /// <summary>
        /// 특정 버튼을 선택 상태로 설정. 음수 인덱스는 선택 가능한 버튼이 없음을 뜻한다
        /// </summary>
        public virtual void SelectButton(int index)
        {
            if (navigableButtons.Count == 0 || index < 0) return;

            if (index >= navigableButtons.Count)
            {
                Debug.LogWarning($"[UI Panel] {gameObject.name} 패널: 잘못된 버튼 인덱스 {index} 설정 시도");
                return;
            }

            currentSelectedButtonIndex = index;

            // 새 버튼을 선택하여 하이라이트 활성화
            Button button = navigableButtons[index];
            if (button == null) return;

            button.Select();
        }

        /// <summary>
        /// 유효한 기본 선택 버튼 인덱스 반환. 선택 가능한 버튼이 없으면 -1
        /// </summary>
        protected virtual int GetValidDefaultButtonIndex()
        {
            if (navigableButtons.Count == 0) return -1;

            if (IsButtonSelectable(defaultSelectedButtonIndex))
                return defaultSelectedButtonIndex;

            // 기본 버튼을 쓸 수 없으면 뒤쪽으로 순회하며 선택 가능한 버튼을 찾는다
            return FindSelectableButtonIndex(defaultSelectedButtonIndex, 1);
        }

        /// <summary>
        /// 버튼의 활성 상태를 설정하고 ColorBlock의 색을 대상 Graphic에 반영
        /// </summary>
        /// <remarks>
        /// Animation transition 버튼은 Unity가 ColorBlock을 적용하지 않으므로 직접 반영해야 한다
        /// </remarks>
        protected static void SetButtonEnabled(Button button, bool enabled)
        {
            if (button == null)
                return;

            button.interactable = enabled;

            if (button.targetGraphic == null)
                return;

            ColorBlock colors = button.colors;
            Color tint = enabled ? colors.normalColor : colors.disabledColor;
            button.targetGraphic.color = tint * colors.colorMultiplier;
        }

        /// <summary>
        /// 패널 닫기 (외부에서 호출 가능)
        /// </summary>
        public virtual void Close()
        {
            if (UIManager.HasInstance && canClose)
                if (UIManager.Instance.CurrentActivePanel == this)
                    UIManager.Instance.CloseCurrentPanel();
        }
    }
}
