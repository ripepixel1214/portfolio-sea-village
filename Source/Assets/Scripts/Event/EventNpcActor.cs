using SeaVillage.NPC.Base;
using SeaVillage.NPC.Components;
using UnityEngine;

namespace SeaVillage.Event
{
    /// <summary>일반 손님 NPC를 이벤트 전용 고정 상호작용 대상으로 전환</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class EventNpcActor : BaseInteractable
    {
        private const string NpcRenderLayerName = "NPC";
        private const string InteractableLayerName = "Interactable";

        private bool _isEventMode;
        private int _interactionLeaseCount;
        private CustomerNPC _customer;

        public override InteractionType InteractionType => global::InteractionType.Dialogue;
        public Vector2 SpeechBubbleOffset => _customer != null ? _customer.SpeechBubbleOffset : Vector2.up * 1.7f;
        public bool IsEventMode => _isEventMode;
        public bool IsEventInteractionActive => _isEventMode && _interactionLeaseCount > 0;

        public bool EnterEventMode()
        {
            if (_isEventMode)
                return true;

            _customer = GetComponent<CustomerNPC>();
            NPCMove movement = GetComponent<NPCMove>();
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            Collider2D interactionCollider = GetComponent<Collider2D>();
            int interactableLayer = LayerMask.NameToLayer(InteractableLayerName);

            if (body == null || interactionCollider == null)
            {
                Debug.LogError($"[{nameof(EventNpcActor)}] 필수 NPC 컴포넌트가 없습니다: {name}", this);
                return false;
            }

            if (interactableLayer < 0)
            {
                Debug.LogError($"[{nameof(EventNpcActor)}] {InteractableLayerName} 레이어를 찾을 수 없습니다", this);
                return false;
            }

            movement?.Stop();
            if (_customer != null)
                _customer.enabled = false;
            if (movement != null)
                movement.enabled = false;

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.constraints = RigidbodyConstraints2D.FreezeAll;

            interactionCollider.isTrigger = true;
            interactionCollider.gameObject.layer = interactableLayer;

            _isEventMode = true;
            _interactionLeaseCount = 0;
            enabled = true;
            return true;
        }

        public void SetRenderOrder(int order)
        {
            _customer ??= GetComponent<CustomerNPC>();
            if (_customer != null)
            {
                _customer.SetRenderOrder(order);
                return;
            }

            Renderer renderer = GetComponentInChildren<Renderer>(true);
            if (renderer == null)
                return;

            renderer.sortingLayerName = NpcRenderLayerName;
            renderer.sortingOrder = order;
        }

        public bool BeginInteraction()
        {
            if (!_isEventMode)
                return false;

            _interactionLeaseCount++;
            return true;
        }

        public void EndInteraction()
        {
            if (_interactionLeaseCount > 0)
                _interactionLeaseCount--;
        }
    }
}
