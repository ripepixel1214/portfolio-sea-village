#nullable enable
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using SeaVillage.Core;
using SeaVillage.UI;
using SeaVillage.Utilities;

namespace SeaVillage.Ocean
{
    // 항해 씬 배 입력/이동/경계 컴포넌트 (Rigidbody2D 속도 이동 + X wrap / Y clamp).
    // 입력은 PlayerInput(Player 맵)에서 주입 — PlayerController와 동일 규약. 배의 이동과 경계는 전부 배 자신의 책임.
    [RequireComponent(typeof(Rigidbody2D))]
    public class ShipController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private Transform? spriteTransform;

        private Rigidbody2D rb = null!;
        private Vector2 moveInput;
        private bool wasMoving;
        private VoyageLifecycle? voyage;

        // 경계 (ConfigureBounds 주입 전까지 비활성)
        private bool hasBounds;
        private float minX;
        private float maxX;
        private float minY;
        private float maxY;
        private float wrapInsetMargin;
        private bool skipNextBoundsCheck;

        // 상호작용 입력(Player/Interact) 발생 알림 — 실제 처리는 OceanManager
        public event Action? Interacted;

        // X 경계 wrap 워프 시 이동 델타 알림 (OceanManager가 카메라 스냅에 사용)
        public event Action<Vector2>? Warped;

        public Vector2 WorldPosition => rb != null ? rb.position : (Vector2)transform.position;
        public bool CanControl => voyage != null && voyage.CanControl
            && !TutorialCommandInputPolicy.IsBlocked
            && !(SceneChanger.HasInstance && SceneChanger.Instance.IsTransitioning)
            && !(GameManager.HasInstance && (GameManager.Instance.IsPaused
                || GameManager.Instance.CurrentGameState == GameState.Loading))
            && !(TimeManager.HasInstance && TimeManager.Instance.IsPaused)
            && !(Event.EventManager.HasInstance && Event.EventManager.Instance.IsEventRunning)
            && !(UIManager.HasInstance && UIManager.Instance.IsAnyPanelOpened);
        public float EffectiveMoveSpeed => moveSpeed
            * (PlayerStatManager.HasInstance ? PlayerStatManager.Instance.MovementSpeedMultiplier : 1f)
            * DollEffectPolicy.ShipSpeedMultiplier;

        // 배에서 섬 콜라이더 표면까지의 최단 거리. 섬-배 상호작용 거리 계산은 배의 역할
        public float DistanceTo(Island island)
            => Vector2.Distance(WorldPosition, island.ClosestPoint(WorldPosition));

        // 이동 속도 (런타임 디버그 조절용). 음수 차단, 인스펙터 직렬화 기본값 유지
        public float MoveSpeed
        {
            get => moveSpeed;
            set => moveSpeed = Mathf.Max(0f, value);
        }

        public void BindVoyage(VoyageLifecycle lifecycle)
        {
            if (voyage != null)
                voyage.Changed -= HandleVoyageChanged;

            voyage = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            voyage.Changed += HandleVoyageChanged;
            HandleVoyageChanged();
        }

        private void HandleVoyageChanged()
        {
            if (!CanControl && rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                wasMoving = false;
            }
        }

        private void OnDisable()
        {
            moveInput = Vector2.zero;
            wasMoving = false;
            if (rb != null)
                rb.linearVelocity = Vector2.zero;
        }

        private void OnDestroy()
        {
            if (voyage != null)
                voyage.Changed -= HandleVoyageChanged;
        }

        // 맵 영역 경계를 주입받아 이동 제한(X wrap / Y clamp)을 활성화한다 (OceanManager가 Initialize 에서 호출)
        public void ConfigureBounds(Vector2 areaCenter, Vector2 areaSize, float wrapInsetMargin)
        {
            float halfWidth = Mathf.Abs(areaSize.x) * 0.5f;
            float halfHeight = Mathf.Abs(areaSize.y) * 0.5f;
            minX = areaCenter.x - halfWidth;
            maxX = areaCenter.x + halfWidth;
            minY = areaCenter.y - halfHeight;
            maxY = areaCenter.y + halfHeight;
            this.wrapInsetMargin = wrapInsetMargin;
            hasBounds = true;
        }

        // 컴포넌트 캐시 및 스프라이트 Transform 폴백 설정
        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            if (spriteTransform == null)
            {
                SpriteRenderer? sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null)
                {
                    spriteTransform = sr.transform;
                }
            }
        }

        // 이동 입력 처리 (PlayerInput의 Player/Move)
        public void OnMove(InputAction.CallbackContext context)
        {
            // 패널 동안에도 터치의 최신 값과 해제를 기억해 닫힌 뒤 올바르게 재개한다.
            moveInput = context.ReadValue<Vector2>();
        }

        // 상호작용 입력 처리 (PlayerInput의 Player/Interact, Keyboard 'E')
        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed)
                TryInteract();
        }

        // 키보드와 HUD 터치 모두 동일한 입력 제한과 항해 상호작용 경로를 사용한다.
        public void TryInteract()
        {
            if (!CanControl)
                return;

            Interacted?.Invoke();
        }

        // 지정 위치로 즉시 이동시키고 속도를 초기화한다
        public void WarpTo(Vector2 position)
        {
            if (rb != null)
            {
                rb.position = position;
                rb.linearVelocity = Vector2.zero;
            }
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        // 입력을 속도 이동으로 적용한다 (패널 열림 시 정지)
        private void FixedUpdate()
        {
            if (!CanControl)
            {
                rb.linearVelocity = Vector2.zero;
                wasMoving = false;
                return;
            }

            rb.linearVelocity = moveInput.sqrMagnitude > 0.0001f
                ? moveInput.normalized * EffectiveMoveSpeed
                : Vector2.zero;

            bool moving = rb.linearVelocity.sqrMagnitude > 0.0001f;
            if (moving && !wasMoving)
                TutorialEventReporter.Report(TutorialEventType.PlayerMoved, TutorialTargetIds.OceanPlayer, source: TutorialEventSource.World);
            wasMoving = moving;

            UpdateFacing(moveInput.x);
        }

        // 이동이 끝난 뒤 경계 검사 (X wrap / Y clamp)
        private void LateUpdate()
        {
            TickBounds();
        }

        // 경계 검사: X는 반대편으로 wrap(+카메라 스냅 통지), Y는 clamp
        private void TickBounds()
        {
            if (!hasBounds || !CanControl)
            {
                return;
            }
            if (skipNextBoundsCheck)
            {
                skipNextBoundsCheck = false;
                return;
            }

            Vector2 pos = WorldPosition;

            if (pos.x < minX)
            {
                WrapTo(new Vector2(maxX - wrapInsetMargin, pos.y));
                return;
            }
            if (pos.x > maxX)
            {
                WrapTo(new Vector2(minX + wrapInsetMargin, pos.y));
                return;
            }

            float clampedY = Mathf.Clamp(pos.y, minY, maxY);
            if (!Mathf.Approximately(clampedY, pos.y))
            {
                WarpTo(new Vector2(pos.x, clampedY));
            }
        }

        // X wrap 워프 실행 후 다음 프레임 검사 스킵 + 델타 통지 (카메라 스냅용)
        private void WrapTo(Vector2 destination)
        {
            Vector2 delta = destination - WorldPosition;
            WarpTo(destination);
            skipNextBoundsCheck = true;
            Warped?.Invoke(delta);
        }

        // 좌우 이동에 따라 스프라이트를 Y축으로 뒤집는다
        private void UpdateFacing(float horizontal)
        {
            if (spriteTransform == null || Mathf.Approximately(horizontal, 0f))
            {
                return;
            }

            float yaw = horizontal < 0f ? -180f : 0f;
            spriteTransform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
