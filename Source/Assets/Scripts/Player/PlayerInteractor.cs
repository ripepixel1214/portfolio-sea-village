using UnityEngine;
using System.Collections.Generic;
using SeaVillage.UI;
using SeaVillage.Core;

namespace SeaVillage.Player
{
    /// <summary>
    /// 플레이어 상호작용 클래스. 플레이어의 자식 오브젝트에 부착되어야 함
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("상호작용 설정")]
        [SerializeField] private float interactionRange = 3f;

        // 현재 주변의 상호작용 가능한 객체 리스트
        private List<IInteractable> nearbyInteractables = new List<IInteractable>();
        // 현재 가장 가까운 상호작용 타겟
        private IInteractable currentTarget;

        // UI 이벤트(하이라이트 표시, 해제 등)
        public System.Action<IInteractable> OnInteractableEnter;
        public System.Action<IInteractable> OnInteractableExit;
        public System.Action<IInteractable> OnInteract;

        public bool HasInteractableTarget => currentTarget != null;

        private BoxCollider2D _interactionCollider;

        [SerializeField, Header("디버그 설정")]
        public TMPro.TextMeshProUGUI interactableDebugText;

        private void Awake()
        {
            InitializeInteractionCollider();
        }

        private void InitializeInteractionCollider()
        {
            if (_interactionCollider == null)
                _interactionCollider = GetComponent<BoxCollider2D>();

            _interactionCollider.isTrigger = true;
            _interactionCollider.gameObject.transform.localScale = new Vector3(interactionRange, 1f, 1f);
        }

        /// <summary>
        /// 상호작용 객체가 범위에 들어왔을 때
        /// </summary>
        public void RegisterInteractable(IInteractable interactable)
        {
            if (!nearbyInteractables.Contains(interactable))
            {
                nearbyInteractables.Add(interactable);
            }

            UpdateCurrentTarget();
        }

        /// <summary>
        /// 이미 범위 안에 겹쳐 있는 객체를 등록한다. 활성화 직후처럼 트리거 enter 가 발생하지 않는 경우 대비.
        /// </summary>
        public void RegisterIfOverlapping(IInteractable interactable)
        {
            if (interactable?.GameObject == null || _interactionCollider == null)
                return;

            if (interactable.GameObject.TryGetComponent(out Collider2D targetCollider)
                && _interactionCollider.IsTouching(targetCollider))
            {
                RegisterInteractable(interactable);
            }
        }

        /// <summary>
        /// 상호작용 객체가 범위에서 나갔을 때
        /// </summary>
        public void UnregisterInteractable(IInteractable interactable)
        {
            nearbyInteractables.Remove(interactable);

            if (currentTarget == interactable)
            {
                OnInteractableExit?.Invoke(currentTarget);
                currentTarget = null;
            }

            UpdateCurrentTarget();
        }

        /// <summary>
        /// 현재 타겟 업데이트 (가장 가까운 객체 선택)
        /// </summary>
        private void UpdateCurrentTarget()
        {
            IInteractable closest = null;
            float closestDistance = float.MaxValue;

            foreach (var interactable in nearbyInteractables)
            {
                if (interactable?.GameObject == null || !TutorialInteractionPolicy.IsAllowed(interactable))
                    continue;

                float distance = Vector3.Distance(transform.position, interactable.GameObject.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = interactable;
                }
            }

            // 가장 가까운 객체가 현재 타겟과 다르다면 변경(이벤트 호출)
            if (currentTarget != closest)
            {
                if (currentTarget != null)
                    OnInteractableExit?.Invoke(currentTarget);

                currentTarget = closest;
                if (currentTarget != null)
                    OnInteractableEnter?.Invoke(currentTarget);
            }
        }

        /// <summary>
        /// 상호작용 시도
        /// </summary>
        public void TryInteract()
        {
            UpdateCurrentTarget();
            if (currentTarget != null)
            {
                // 상호작용 이벤트 발생
                OnInteract?.Invoke(currentTarget);
                currentTarget.Interact();
                Debug.Log($"상호작용: {currentTarget.GameObject.name}");
            }
        }

        public void RefreshCurrentTarget()
        {
            UpdateCurrentTarget();
        }

        /// <summary>
        /// 현재 상호작용 타겟 가져오기
        /// </summary>
        public IInteractable GetCurrentTarget()
        {
            return currentTarget;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.TryGetComponent<IInteractable>(out var interactable))
                RegisterInteractable(interactable);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.TryGetComponent<IInteractable>(out var interactable))
                UnregisterInteractable(interactable);
        }
    }
}
