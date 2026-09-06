using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Spine.Unity;
using SeaVillage.UI;
using SeaVillage.Core;
using SeaVillage.Town;

namespace SeaVillage.Player
{
    /// <summary>
    /// 플레이어 입력 처리 클래스
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        private const string PlayerActionMapName = "Player";
        private const string UIActionMapName = "UI";
        private const string IdleAnimation = "Player_Idle";
        private const string WalkAnimation = "Player_Walk";

        /// <summary>
        /// 플레이어 이동 속도는 PlayerStats의 Dex 스탯에 의해 결정
        /// </summary>
        [SerializeField] private float playerMoveSpeed = 3f;
        [SerializeField] private float debugMoveSpeedMultiplier = 1f;

        private Vector2 _moveInput;
        private bool _isInputBlocked;
        private TutorialMovementConstraint _tutorialMovementConstraint;
        private Rigidbody2D _rigidbody;
        private SkeletonAnimation _skeletonAnimation;
        private InputActionAsset inputActionAsset;
        private PlayerInput _playerInput;

        private Player _player;
        private PlayerInteractor _playerInteractor;
        private bool _playerStatEventsBound;
        private bool _uiEventsBound;

        public float BaseMoveSpeed => playerMoveSpeed;
        public float DebugMoveSpeedMultiplier => debugMoveSpeedMultiplier;
        public float EffectiveMoveSpeed => playerMoveSpeed
            * ResolveAgilitySpeedMultiplier()
            * debugMoveSpeedMultiplier
            * DollEffectPolicy.PlayerSpeedMultiplier;
        public bool IsInputBlocked => _isInputBlocked;
        public bool IsTutorialCommandInputBlocked => TutorialCommandInputPolicy.IsBlocked;
        public TutorialMovementConstraint TutorialMovementConstraint => _tutorialMovementConstraint;

        private bool IsCommandInputBlocked => _isInputBlocked || TutorialCommandInputPolicy.IsBlocked;

        private void Awake()
        {
            StartCoroutine(InitializePlayerController());
        }

        private void OnEnable()
        {
            BindPlayerStatEvents();
            SubscribeToUIEvents();
        }

        private void OnDisable()
        {
            UnbindPlayerStatEvents();
            UnsubscribeFromUIEvents();
        }

        private void FixedUpdate()
        {
            if (IsCommandInputBlocked || _moveInput == Vector2.zero || UIManager.Instance.IsAnyPanelOpened)
            {
                PlayLocomotionAnimation(IdleAnimation);
                return;
            }

            SetFacing(_moveInput.x > 0);
            PlayLocomotionAnimation(WalkAnimation);
            float finalMoveSpeed = EffectiveMoveSpeed;
            Vector2 next = _rigidbody.position + _moveInput * finalMoveSpeed * Time.fixedDeltaTime;

            // 이동 가능 영역 밖이면 이번 프레임 이동을 취소(벽에서 정지). 영역 미설정 시 자유 이동.
            ITownContext town = GameManager.HasInstance
                ? GameManager.Instance.CurrentSceneRoot as ITownContext
                : null;
            if (town == null || town.IsInMovableArea(next))
            {
                _rigidbody.MovePosition(next);
            }
        }

        #region Animation
        /// <summary>
        /// 스켈레톤 X 스케일 부호를 바꿔 좌우 방향 표현. (NPCMove와 동일 규약: faceRight = 음수 스케일)
        /// </summary>
        private void SetFacing(bool faceRight)
        {
            if (_skeletonAnimation == null) return;
            _skeletonAnimation.Skeleton.ScaleX = faceRight ? -1f : 1f;
        }

        /// <summary>
        /// 이동 상태 애니메이션 루프 재생. 동일 애니메이션이 이미 재생 중이면 무시.
        /// </summary>
        private void PlayLocomotionAnimation(string animationName)
        {
            if (_skeletonAnimation == null) return;
            if (_skeletonAnimation.AnimationName == animationName) return;
            _skeletonAnimation.AnimationState.SetAnimation(0, animationName, true);
        }
        #endregion

        #region Initialization
        /// <summary>
        /// 플레이어 컨트롤러 초기화 코루틴. Player 컴포넌트 초기화과 완료될 때까지 대기 후 실행
        /// </summary>
        /// <returns></returns>
        private IEnumerator InitializePlayerController()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _skeletonAnimation = GetComponent<SkeletonAnimation>();
            _playerInput = GetComponent<PlayerInput>();
            inputActionAsset = _playerInput?.actions;

            // GetComponent 대신 Player 컴포넌트를 통해 참조 받기
            _player = GetComponent<Player>();
            if (_player == null)
            {
                Debug.LogError("PlayerController: Player 컴포넌트를 찾을 수 없습니다.");
                yield break;
            }

            yield return new WaitUntil(() => _player != null && _player.IsComponentInitialized);
            _playerInteractor = _player.Interactor;
            if (_playerInteractor == null)
            {
                Debug.LogError("PlayerController: PlayerInteractor 컴포넌트를 찾을 수 없습니다.");
                yield break;
            }

            BindPlayerStatEvents();
            SubscribeToUIEvents();
        }

        private void SubscribeToUIEvents()
        {
            if (_uiEventsBound || !UIManager.HasInstance)
                return;

            UIManager.Instance.OnPanelOpened += SwitchToUIActionMap;
            UIManager.Instance.OnAllPanelsClosed += SwitchToPlayerActionMap;
            _uiEventsBound = true;
        }

        private void UnsubscribeFromUIEvents()
        {
            if (!_uiEventsBound || !UIManager.HasInstance)
                return;

            UIManager.Instance.OnPanelOpened -= SwitchToUIActionMap;
            UIManager.Instance.OnAllPanelsClosed -= SwitchToPlayerActionMap;
            _uiEventsBound = false;
        }
        #endregion

        #region Player Input Methods
        /// <summary>
        /// 이동 입력 처리
        /// </summary>
        public void OnMove(InputAction.CallbackContext context)
        {
            if (IsCommandInputBlocked)
            {
                _moveInput = Vector2.zero;
                return;
            }

            _moveInput = ApplyTutorialMovementConstraint(context.ReadValue<Vector2>());
        }

        /// <summary>
        /// 상호작용 입력 처리 (Keyboard 'E' 키)
        /// </summary>
        public void OnInteract(InputAction.CallbackContext context)
        {
            if (IsCommandInputBlocked) return;

            if (context.performed && _playerInteractor != null)
                _playerInteractor.TryInteract();
        }

        /// <summary>
        /// 인벤토리 창 열기 입력 처리
        /// </summary>
        public void OnInventory(InputAction.CallbackContext context)
        {
            if (IsCommandInputBlocked) return;

            if (context.performed)
            {
                UIManager.Instance?.OpenPanel<PlayerInventoryPanel>();
            }
        }

        /// <summary>
        /// 플레이어 상태창 열기 입력 처리
        /// </summary>
        public void OnPlayerInfo(InputAction.CallbackContext context)
        {
            if (IsCommandInputBlocked) return;

            if (context.performed)
            {
                UIManager.Instance?.OpenPanel<PlayerInfoPanel>();
            }
        }
        #endregion

        #region UI Input Methods
        /// <summary>
        /// UI 네비게이션 입력 처리
        /// </summary>
        public void OnUINavigate(InputAction.CallbackContext context)
        {
            if (IsCommandInputBlocked) return;

            if (context.performed && UIManager.HasInstance && UIManager.Instance.IsAnyPanelOpened)
            {
                Vector2 navigationInput = context.ReadValue<Vector2>();

                UIPanel currentPanel = UIManager.Instance.CurrentActivePanel;
                if (currentPanel != null)
                {
                    if (navigationInput.x < 0)
                        currentPanel.NavigateToLeftButton();
                    else if (navigationInput.x > 0)
                        currentPanel.NavigateToRightButton();
                    else if (navigationInput.y > 0)
                        currentPanel.NavigateToUpperButton();
                    else if (navigationInput.y < 0)
                        currentPanel.NavigateToLowerButton();
                }
            }
        }

        /// <summary>
        /// UI Submit 입력 처리
        /// </summary>
        public void OnUISubmit(InputAction.CallbackContext context)
        {
            if (IsCommandInputBlocked) return;

            if (context.performed && UIManager.HasInstance && UIManager.Instance.IsAnyPanelOpened)
            {
                var currentPanel = UIManager.Instance.CurrentActivePanel;
                if (currentPanel != null && currentPanel.IsNavigationEnabled)
                    currentPanel.ClickSelectedButton();
            }
        }

        /// <summary>
        /// UI Cancel 입력 처리 (ESC)
        /// </summary>
        public void OnUICancel(InputAction.CallbackContext context)
        {
            if (IsCommandInputBlocked) return;

            if (context.performed)
                UIManager.Instance?.CloseCurrentPanel();
        }
        #endregion

        private void HandlePlayerStatChanged(PlayerStatType statType, int value)
        {
            if (statType == PlayerStatType.Agility)
                Debug.Log($"민첩 스탯 변경: {value}, 이동 속도 배율: {ResolveAgilitySpeedMultiplier():F1}");
        }

        public void SetDebugMoveSpeedMultiplier(float multiplier)
        {
            if (multiplier <= 0f)
            {
                Debug.LogWarning($"PlayerController: invalid debug speed multiplier {multiplier}. Must be > 0.");
                return;
            }

            debugMoveSpeedMultiplier = multiplier;
        }

        // 지정 월드 위치로 즉시 이동(세이브 로드 복원 등). z는 현재 값 유지
        public void SetPosition(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            if (_rigidbody != null)
                _rigidbody.position = position;
        }

        public void SetInputBlocked(bool isBlocked)
        {
            if (_isInputBlocked == isBlocked)
                return;

            _isInputBlocked = isBlocked;

            if (_isInputBlocked)
                _moveInput = Vector2.zero;

            if (_playerInput != null)
            {
                if (_isInputBlocked)
                    _playerInput.DeactivateInput();
                else
                    _playerInput.ActivateInput();
            }
        }

        public void SetTutorialMovementConstraint(TutorialMovementConstraint constraint)
        {
            _tutorialMovementConstraint = constraint;
            _moveInput = ApplyTutorialMovementConstraint(_moveInput);
        }

        public void LookAt(Vector2 target)
        {
            SetFacing(target.x > transform.position.x);
        }

        private void BindPlayerStatEvents()
        {
            if (_playerStatEventsBound || !PlayerStatManager.HasInstance)
                return;

            PlayerStatManager.Instance.OnStatChanged += HandlePlayerStatChanged;
            _playerStatEventsBound = true;
        }

        private void UnbindPlayerStatEvents()
        {
            if (_playerStatEventsBound && PlayerStatManager.HasInstance)
                PlayerStatManager.Instance.OnStatChanged -= HandlePlayerStatChanged;

            _playerStatEventsBound = false;
        }

        private static float ResolveAgilitySpeedMultiplier()
        {
            return PlayerStatManager.HasInstance
                ? PlayerStatManager.Instance.MovementSpeedMultiplier
                : 1f;
        }

        /// <summary>
        /// UI Action Map으로 전환
        /// </summary>
        private void SwitchToUIActionMap()
        {
            if (inputActionAsset == null) return;

            var playerActionMap = inputActionAsset.FindActionMap(PlayerActionMapName);
            var uiActionMap = inputActionAsset.FindActionMap(UIActionMapName);

            if (playerActionMap != null && playerActionMap.enabled)
                playerActionMap.Disable();

            if (uiActionMap != null && !uiActionMap.enabled)
                uiActionMap.Enable();
        }

        /// <summary>
        /// Player Action Map으로 전환
        /// </summary>
        private void SwitchToPlayerActionMap()
        {
            if (inputActionAsset == null) return;

            var playerActionMap = inputActionAsset.FindActionMap(PlayerActionMapName);
            var uiActionMap = inputActionAsset.FindActionMap(UIActionMapName);

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            if (uiActionMap != null && uiActionMap.enabled)
                uiActionMap.Disable();
#endif

            if (playerActionMap != null && !playerActionMap.enabled)
                playerActionMap.Enable();
        }

        private Vector2 ApplyTutorialMovementConstraint(Vector2 input)
        {
            input.y = 0f;
            input.x = _tutorialMovementConstraint switch
            {
                TutorialMovementConstraint.Blocked => 0f,
                TutorialMovementConstraint.RightOnly => Mathf.Max(0f, input.x),
                TutorialMovementConstraint.LeftOnly => Mathf.Min(0f, input.x),
                _ => input.x
            };
            return input;
        }
    }
}
