using System.Collections;
using System.Collections.Generic;
using SeaVillage.NPC.Base;
using SeaVillage.NPC.Components;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeaVillage.Event
{
    /// <summary>상점 내부 NPC를 이벤트 위치로 이동시키고 원래 상태를 복원</summary>
    internal sealed class EventNpcShopVisual
    {
        private const float RevealDurationSeconds = 1f;
        private const float SpeechBubbleHeadPadding = 0.15f;
        private const string NpcRenderLayerName = "NPC";

        private readonly Transform _visual;
        private readonly Transform _originalParent;
        private readonly Vector3 _originalLocalPosition;
        private readonly Quaternion _originalLocalRotation;
        private readonly Vector3 _originalLocalScale;
        private readonly int _originalSiblingIndex;
        private readonly bool _originalActiveSelf;
        private readonly List<SkeletonRenderer> _skeletons = new();
        private readonly List<float> _skeletonAlphas = new();
        private readonly List<SpriteMaskInteraction> _skeletonMaskInteractions = new();
        private readonly List<SpriteRenderer> _spriteRenderers = new();
        private readonly List<float> _spriteAlphas = new();
        private readonly List<SpriteMaskInteraction> _spriteMaskInteractions = new();
        private readonly List<MeshRenderer> _meshRenderers = new();
        private readonly List<SpriteMaskInteraction> _meshMaskInteractions = new();
        private readonly List<SkinnedMeshRenderer> _skinnedMeshRenderers = new();
        private readonly List<SpriteMaskInteraction> _skinnedMeshMaskInteractions = new();
        private readonly List<CanvasGroup> _canvasGroups = new();
        private readonly List<float> _canvasAlphas = new();
        private readonly List<Rigidbody2D> _rigidbodies = new();
        private readonly List<RigidbodyType2D> _bodyTypes = new();
        private readonly List<bool> _bodySimulated = new();
        private readonly List<float> _bodyGravityScales = new();
        private readonly List<RigidbodyConstraints2D> _bodyConstraints = new();
        private readonly List<Vector2> _bodyLinearVelocities = new();
        private readonly List<float> _bodyAngularVelocities = new();
        private readonly List<Collider2D> _colliders = new();
        private readonly List<bool> _colliderEnabled = new();
        private readonly List<NPCMove> _movementComponents = new();
        private readonly List<bool> _movementEnabled = new();
        private readonly List<Renderer> _renderers = new();
        private readonly List<int> _sortingLayerIds = new();
        private readonly List<int> _sortingOrders = new();
        private bool _isRevealing;

        public EventNpcShopVisual(Transform visual)
        {
            _visual = visual;
            _originalParent = visual.parent;
            _originalLocalPosition = visual.localPosition;
            _originalLocalRotation = visual.localRotation;
            _originalLocalScale = visual.localScale;
            _originalSiblingIndex = visual.GetSiblingIndex();
            _originalActiveSelf = visual.gameObject.activeSelf;

            CaptureAlphaTargets();
            CapturePhysicsAndMovement();
        }

        public Transform Transform => _visual;
        public bool IsPrepared { get; private set; }
        public bool HasAlphaTarget => _skeletons.Count > 0
            || _spriteRenderers.Count > 0
            || _canvasGroups.Count > 0;

        public Vector2 GetSpeechBubbleOffset()
        {
            if (_visual == null)
                return Vector2.up * 1.7f;

            bool hasBounds = false;
            Bounds bounds = default;
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
                return Vector2.up * 1.7f;

            float yOffset = bounds.max.y - _visual.position.y + SpeechBubbleHeadPadding;
            float xOffset = bounds.center.x - _visual.position.x;
            return new Vector2(xOffset, Mathf.Max(0.5f, yOffset));
        }

        public IEnumerator Reveal(Transform destination)
        {
            if (IsPrepared || _visual == null || destination == null)
                yield break;

            if (_isRevealing)
            {
                while (_isRevealing)
                    yield return null;

                yield break;
            }

            _isRevealing = true;
            try
            {
                FreezePhysicsAndMovement();
                _visual.SetParent(destination, true);
                _visual.position = destination.position;
                _visual.rotation = destination.rotation;
                _visual.gameObject.SetActive(true);
                ApplyNpcRenderOrder();
                SetMaskInteractionNone();
                SetAlpha(0f);

                float elapsed = 0f;
                while (elapsed < RevealDurationSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    SetAlpha(Mathf.Clamp01(elapsed / RevealDurationSeconds));
                    yield return null;
                }

                SetAlpha(1f);
                IsPrepared = true;
            }
            finally
            {
                _isRevealing = false;
            }
        }

        public void Restore()
        {
            if (_visual == null)
                return;

            _isRevealing = false;
            IsPrepared = false;

            _visual.SetParent(_originalParent, false);
            _visual.localPosition = _originalLocalPosition;
            _visual.localRotation = _originalLocalRotation;
            _visual.localScale = _originalLocalScale;
            if (_originalParent != null)
            {
                int siblingIndex = Mathf.Clamp(
                    _originalSiblingIndex,
                    0,
                    Mathf.Max(0, _originalParent.childCount - 1));
                _visual.SetSiblingIndex(siblingIndex);
            }

            SetOriginalAlpha();
            RestoreMaskInteractions();
            RestoreRenderOrder();
            RestorePhysicsAndMovement();
            _visual.gameObject.SetActive(_originalActiveSelf);
        }

        private void CaptureAlphaTargets()
        {
            if (_visual == null)
                return;

            SkeletonRenderer[] skeletons = _visual.GetComponentsInChildren<SkeletonRenderer>(true);
            for (int i = 0; i < skeletons.Length; i++)
            {
                SkeletonRenderer skeleton = skeletons[i];
                if (skeleton == null)
                    continue;

                _skeletons.Add(skeleton);
                _skeletonAlphas.Add(skeleton.Skeleton != null ? skeleton.Skeleton.A : 1f);
                _skeletonMaskInteractions.Add(skeleton.maskInteraction);
            }

            SpriteRenderer[] spriteRenderers = _visual.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                SpriteRenderer spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null)
                    continue;

                _spriteRenderers.Add(spriteRenderer);
                _spriteAlphas.Add(spriteRenderer.color.a);
                _spriteMaskInteractions.Add(spriteRenderer.maskInteraction);
            }

            MeshRenderer[] meshRenderers = _visual.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                MeshRenderer meshRenderer = meshRenderers[i];
                if (meshRenderer == null)
                    continue;

                _meshRenderers.Add(meshRenderer);
                _meshMaskInteractions.Add(meshRenderer.GetSpriteMaskInteraction());
            }

            SkinnedMeshRenderer[] skinnedMeshRenderers = _visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedMeshRenderers.Length; i++)
            {
                SkinnedMeshRenderer skinnedMeshRenderer = skinnedMeshRenderers[i];
                if (skinnedMeshRenderer == null)
                    continue;

                _skinnedMeshRenderers.Add(skinnedMeshRenderer);
                _skinnedMeshMaskInteractions.Add(skinnedMeshRenderer.GetSpriteMaskInteraction());
            }

            CanvasGroup[] canvasGroups = _visual.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < canvasGroups.Length; i++)
            {
                CanvasGroup canvasGroup = canvasGroups[i];
                if (canvasGroup == null)
                    continue;

                _canvasGroups.Add(canvasGroup);
                _canvasAlphas.Add(canvasGroup.alpha);
            }

            Renderer[] renderers = _visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                _renderers.Add(renderer);
                _sortingLayerIds.Add(renderer.sortingLayerID);
                _sortingOrders.Add(renderer.sortingOrder);
            }

        }

        private void CapturePhysicsAndMovement()
        {
            if (_visual == null)
                return;

            Rigidbody2D[] rigidbodies = _visual.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                Rigidbody2D body = rigidbodies[i];
                if (body == null)
                    continue;

                _rigidbodies.Add(body);
                _bodyTypes.Add(body.bodyType);
                _bodySimulated.Add(body.simulated);
                _bodyGravityScales.Add(body.gravityScale);
                _bodyConstraints.Add(body.constraints);
                _bodyLinearVelocities.Add(body.linearVelocity);
                _bodyAngularVelocities.Add(body.angularVelocity);
            }

            Collider2D[] colliders = _visual.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null)
                    continue;

                _colliders.Add(collider);
                _colliderEnabled.Add(collider.enabled);
            }

            NPCMove[] movementComponents = _visual.GetComponentsInChildren<NPCMove>(true);
            for (int i = 0; i < movementComponents.Length; i++)
            {
                NPCMove movement = movementComponents[i];
                if (movement == null)
                    continue;

                _movementComponents.Add(movement);
                _movementEnabled.Add(movement.enabled);
            }
        }

        private void FreezePhysicsAndMovement()
        {
            for (int i = 0; i < _movementComponents.Count; i++)
            {
                NPCMove movement = _movementComponents[i];
                if (movement == null)
                    continue;

                movement.Stop();
                movement.enabled = false;
            }

            for (int i = 0; i < _colliders.Count; i++)
            {
                Collider2D collider = _colliders[i];
                if (collider != null)
                    collider.enabled = false;
            }

            for (int i = 0; i < _rigidbodies.Count; i++)
            {
                Rigidbody2D body = _rigidbodies[i];
                if (body == null)
                    continue;

                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }
        }

        private void SetAlpha(float alpha)
        {
            for (int i = 0; i < _skeletons.Count; i++)
            {
                SkeletonRenderer skeleton = _skeletons[i];
                if (skeleton != null && skeleton.Skeleton != null)
                    skeleton.Skeleton.A = alpha;
            }

            for (int i = 0; i < _spriteRenderers.Count; i++)
            {
                SpriteRenderer spriteRenderer = _spriteRenderers[i];
                if (spriteRenderer == null)
                    continue;

                Color color = spriteRenderer.color;
                color.a = alpha;
                spriteRenderer.color = color;
            }

            for (int i = 0; i < _canvasGroups.Count; i++)
            {
                CanvasGroup canvasGroup = _canvasGroups[i];
                if (canvasGroup != null)
                    canvasGroup.alpha = alpha;
            }
        }

        private void SetOriginalAlpha()
        {
            for (int i = 0; i < _skeletons.Count; i++)
            {
                SkeletonRenderer skeleton = _skeletons[i];
                if (skeleton != null && skeleton.Skeleton != null)
                    skeleton.Skeleton.A = _skeletonAlphas[i];
            }

            for (int i = 0; i < _spriteRenderers.Count; i++)
            {
                SpriteRenderer spriteRenderer = _spriteRenderers[i];
                if (spriteRenderer == null)
                    continue;

                Color color = spriteRenderer.color;
                color.a = _spriteAlphas[i];
                spriteRenderer.color = color;
                spriteRenderer.maskInteraction = _spriteMaskInteractions[i];
            }

            for (int i = 0; i < _canvasGroups.Count; i++)
            {
                CanvasGroup canvasGroup = _canvasGroups[i];
                if (canvasGroup != null)
                    canvasGroup.alpha = _canvasAlphas[i];
            }
        }

        private void SetMaskInteractionNone()
        {
            for (int i = 0; i < _skeletons.Count; i++)
            {
                SkeletonRenderer skeleton = _skeletons[i];
                if (skeleton != null)
                    skeleton.maskInteraction = SpriteMaskInteraction.None;
            }

            for (int i = 0; i < _spriteRenderers.Count; i++)
            {
                SpriteRenderer spriteRenderer = _spriteRenderers[i];
                if (spriteRenderer != null)
                    spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
            }

            for (int i = 0; i < _meshRenderers.Count; i++)
            {
                MeshRenderer meshRenderer = _meshRenderers[i];
                if (meshRenderer != null)
                    meshRenderer.SetSpriteMaskInteraction(SpriteMaskInteraction.None);
            }

            for (int i = 0; i < _skinnedMeshRenderers.Count; i++)
            {
                SkinnedMeshRenderer skinnedMeshRenderer = _skinnedMeshRenderers[i];
                if (skinnedMeshRenderer != null)
                    skinnedMeshRenderer.SetSpriteMaskInteraction(SpriteMaskInteraction.None);
            }

        }

        private void RestoreMaskInteractions()
        {
            for (int i = 0; i < _skeletons.Count; i++)
            {
                SkeletonRenderer skeleton = _skeletons[i];
                if (skeleton != null)
                    skeleton.maskInteraction = _skeletonMaskInteractions[i];
            }

            for (int i = 0; i < _meshRenderers.Count; i++)
            {
                MeshRenderer meshRenderer = _meshRenderers[i];
                if (meshRenderer != null)
                    meshRenderer.SetSpriteMaskInteraction(_meshMaskInteractions[i]);
            }

            for (int i = 0; i < _skinnedMeshRenderers.Count; i++)
            {
                SkinnedMeshRenderer skinnedMeshRenderer = _skinnedMeshRenderers[i];
                if (skinnedMeshRenderer != null)
                    skinnedMeshRenderer.SetSpriteMaskInteraction(_skinnedMeshMaskInteractions[i]);
            }

        }

        private void ApplyNpcRenderOrder()
        {
            int sortingOrder = NPCBase.NextRenderOrder();
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null)
                    continue;

                renderer.sortingLayerName = NpcRenderLayerName;
                renderer.sortingOrder = sortingOrder;
            }
        }

        private void RestoreRenderOrder()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null)
                    continue;

                renderer.sortingLayerID = _sortingLayerIds[i];
                renderer.sortingOrder = _sortingOrders[i];
            }
        }

        private void RestorePhysicsAndMovement()
        {
            for (int i = 0; i < _rigidbodies.Count; i++)
            {
                Rigidbody2D body = _rigidbodies[i];
                if (body == null)
                    continue;

                body.bodyType = _bodyTypes[i];
                body.gravityScale = _bodyGravityScales[i];
                body.constraints = _bodyConstraints[i];
                body.linearVelocity = _bodyLinearVelocities[i];
                body.angularVelocity = _bodyAngularVelocities[i];
                body.simulated = _bodySimulated[i];
            }

            for (int i = 0; i < _colliders.Count; i++)
            {
                Collider2D collider = _colliders[i];
                if (collider != null)
                    collider.enabled = _colliderEnabled[i];
            }

            for (int i = 0; i < _movementComponents.Count; i++)
            {
                NPCMove movement = _movementComponents[i];
                if (movement != null)
                    movement.enabled = _movementEnabled[i];
            }
        }
    }
}
