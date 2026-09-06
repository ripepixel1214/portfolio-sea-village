using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SeaVillage.Core;
using SeaVillage.UI.Tutorial;

namespace SeaVillage.UI
{
    /// <summary>
    /// 시스템 메시지 모달 패널
    /// </summary>
    public class SystemMessagePanel : UIPanel
    {
        [Header("Message Content")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI confirmButtonText;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI cancelButtonText;

        private Action _onConfirm;
        private Action _onCancel;
        private MessageType _currentMessageType;

        /// <summary>
        /// 메시지 타입 (버튼 구성 결정)
        /// </summary>
        public enum MessageType
        {
            Alert,      // 확인 버튼만
            Confirm     // 확인 + 취소 버튼
        }

        #region Message Setup
        /// <summary>
        /// 알림 메시지 설정 (확인 버튼만)
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="onConfirm">확인 버튼 클릭 시 콜백 (선택)</param>
        /// <param name="title">타이틀 텍스트 (선택, 기본값: "알림")</param>
        /// <param name="confirmText">확인 버튼 텍스트 (선택, 기본값: "확인")</param>
        public void SetupAlert(string message, Action onConfirm = null, string title = "", string confirmText = "확인")
        {
            SetupInternal(message, MessageType.Alert, onConfirm, null, title, confirmText, null);
        }

        /// <summary>
        /// 확인 대화상자 설정 (확인 + 취소 버튼)
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="onConfirm">확인 버튼 클릭 시 콜백</param>
        /// <param name="onCancel">취소 버튼 클릭 시 콜백 (선택)</param>
        /// <param name="title">타이틀 텍스트 (선택, 기본값: "확인")</param>
        /// <param name="confirmText">확인 버튼 텍스트 (선택, 기본값: "확인")</param>
        /// <param name="cancelText">취소 버튼 텍스트 (선택, 기본값: "취소")</param>
        public void SetupConfirm(string message, Action onConfirm, Action onCancel = null, string title = "확인",
            string confirmText = "확인", string cancelText = "취소")
        {
            SetupInternal(message, MessageType.Confirm, onConfirm, onCancel, title, confirmText, cancelText);
        }

        /// <summary>
        /// 내부 설정 메서드
        /// </summary>
        private void SetupInternal(string message, MessageType messageType, Action onConfirm, Action onCancel,
            string title, string confirmText, string cancelText)
        {
            // 이전 상태 정리
            ClearCallbacks();

            // 메시지 설정
            if (titleText != null)
                titleText.text = title;

            if (messageText != null)
                messageText.text = message;

            // 콜백 저장
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _currentMessageType = messageType;

            // 버튼 텍스트 설정
            if (confirmButtonText != null && !string.IsNullOrEmpty(confirmText))
                confirmButtonText.text = confirmText;

            if (cancelButtonText != null && !string.IsNullOrEmpty(cancelText))
                cancelButtonText.text = cancelText;

            // 버튼 표시/숨김
            ConfigureButtons(messageType);
        }

        /// <summary>
        /// 메시지 타입에 따라 버튼 구성
        /// </summary>
        private void ConfigureButtons(MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Alert:
                    // 확인 버튼만 표시
                    if (confirmButton != null)
                        confirmButton.gameObject.SetActive(true);
                    if (cancelButton != null)
                        cancelButton.gameObject.SetActive(false);

                    // 네비게이션 리스트 업데이트 (확인 버튼만)
                    navigableButtons.Clear();
                    if (confirmButton != null)
                        navigableButtons.Add(confirmButton);
                    break;

                case MessageType.Confirm:
                    // 확인, 취소 버튼 모두 표시
                    if (confirmButton != null)
                        confirmButton.gameObject.SetActive(true);
                    if (cancelButton != null)
                        cancelButton.gameObject.SetActive(true);

                    // 네비게이션 리스트 업데이트 (취소 → 확인 순서)
                    navigableButtons.Clear();
                    if (cancelButton != null)
                        navigableButtons.Add(cancelButton);
                    if (confirmButton != null)
                        navigableButtons.Add(confirmButton);
                    break;
            }

            // 기본 선택 버튼 인덱스 설정
            defaultSelectedButtonIndex = _currentMessageType == MessageType.Alert ? 0 : 1; // Alert: 확인, Confirm: 확인(두번째)
        }
        #endregion

        #region UIPanel Overrides
        public override void OnOpen()
        {
            base.OnOpen();
            TutorialUIBinding.Bind(confirmButton, TutorialAnchorKeys.SystemMessageConfirmButton);
            AddListeners();
        }

        public override void OnClose()
        {
            RemoveListeners();
            ClearCallbacks();
            base.OnClose();
        }
        #endregion

        #region Event Listeners
        protected override void AddListeners()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);

            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelClicked);
        }

        protected override void RemoveListeners()
        {
            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(OnConfirmClicked);

            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(OnCancelClicked);
        }
        #endregion

        #region Button Handlers
        private void OnConfirmClicked()
        {
            // 이중 입력(마우스+Submit 등)으로 콜백이 두 번 실행되지 않도록 먼저 비운다
            Action callback = _onConfirm;
            _onConfirm = null;
            callback?.Invoke();
            ClosePanel();
        }

        private void OnCancelClicked()
        {
            Action callback = _onCancel;
            _onCancel = null;
            callback?.Invoke();
            ClosePanel();
        }

        private void ClosePanel()
        {
            UIManager.Instance?.ClosePanel<SystemMessagePanel>();
        }

        private void ClearCallbacks()
        {
            _onConfirm = null;
            _onCancel = null;
        }
        #endregion

        #region Validation
#if UNITY_EDITOR
        private void OnValidate()
        {
            // Inspector에서 필수 필드 검증
            if (messageText == null)
                Debug.LogWarning($"{nameof(SystemMessagePanel)}: Message Text is not assigned!", this);

            if (confirmButton == null)
                Debug.LogWarning($"{nameof(SystemMessagePanel)}: Confirm Button is not assigned!", this);
        }
#endif
        #endregion
    }
}
