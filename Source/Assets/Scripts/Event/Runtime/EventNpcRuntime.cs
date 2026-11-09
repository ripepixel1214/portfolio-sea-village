using System;
using System.Collections;
using System.Collections.Generic;
using SeaVillage.Core;
using SeaVillage.NPC.Base;
using SeaVillage.Player;
using SeaVillage.UI.NPC;
using UnityEngine;

namespace SeaVillage.Event
{
    public sealed class EventNpcRuntime : IEventNpcRuntime
    {
        private sealed class EventNpcBinding
        {
            public EventNpcDefinition Definition;
            public EventNpcActor VisualActor;
            public EventNpcShopVisual ShopVisual;
            public Transform ShopSpawnAnchor;
            public Transform VisualAnchor;
            public IInteractable InteractionTarget;
            public bool HasDynamicVisual;

            public Vector2 SpeechBubbleOffset => VisualActor != null
                ? VisualActor.SpeechBubbleOffset
                : ShopVisual != null
                    ? ShopVisual.GetSpeechBubbleOffset()
                    : Vector2.up * 1.7f;
        }

        private readonly EventNpcCatalog _catalog;
        private readonly Dictionary<string, EventNpcBinding> _bindingsByNpcId =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, EventNpcSpawnPoint> _spawnPointsByNpcId =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _busyNpcIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<IInteractable> _busyInteractionTargets = new();

        private SpeechBubblePool _speechBubblePool;
        private UISpeechBubbleController _activeDialogueBubble;
        private IDisposable _dialogueInputScope;
        private PlayerInteractor _dialogueInteractor;

        public EventNpcRuntime(EventNpcCatalog catalog)
        {
            _catalog = catalog;
        }

        public bool TrySpawn(string npcId)
        {
            return TryGetOrSpawn(npcId, out _);
        }

        public IEnumerator WaitForInteraction(string npcId, Action<bool> onCompleted)
        {
            if (!TryGetOrSpawn(npcId, out EventNpcBinding binding))
            {
                onCompleted?.Invoke(false);
                yield break;
            }

            while (IsAlive(binding)
                && (!GameManager.HasInstance || !GameManager.Instance.HasPlayer))
            {
                yield return null;
            }

            if (!IsAlive(binding))
            {
                onCompleted?.Invoke(false);
                yield break;
            }

            PlayerInteractor interactor = GameManager.Instance.Player?.Interactor;
            IInteractable interactionTarget = binding.InteractionTarget;
            if (interactor == null || !IsAlive(interactionTarget))
            {
                Debug.LogWarning($"[EventNpcRuntime] 상호작용 대상을 찾을 수 없습니다: {npcId}");
                onCompleted?.Invoke(false);
                yield break;
            }

            while (IsAlive(binding)
                && (_busyNpcIds.Contains(npcId) || _busyInteractionTargets.Contains(interactionTarget)))
                yield return null;

            if (!IsAlive(binding)
                || !_busyNpcIds.Add(npcId)
                || !_busyInteractionTargets.Add(interactionTarget))
            {
                _busyNpcIds.Remove(npcId);
                onCompleted?.Invoke(false);
                yield break;
            }

            bool interacted = false;
            void HandleInteract(IInteractable candidate)
            {
                if (!ReferenceEquals(candidate, interactionTarget))
                    return;

                interacted = true;
                interactor.ConsumeCurrentInteraction();
            }

            interactor.OnInteract += HandleInteract;
            try
            {
                if (!BeginEventInteraction(binding))
                {
                    onCompleted?.Invoke(false);
                    yield break;
                }

                // 활성화 시점에 이미 상호작용 범위 안에 있으면 TriggerEnter가 생략될 수 있음
                interactor.RegisterIfOverlapping(interactionTarget);

                while (!interacted && IsAlive(binding))
                    yield return null;
            }
            finally
            {
                if (interactor != null)
                    interactor.OnInteract -= HandleInteract;

                EndEventInteraction(binding);
                if (interactor != null)
                    interactor.RefreshCurrentTarget();
                _busyNpcIds.Remove(npcId);
                _busyInteractionTargets.Remove(interactionTarget);
            }

            onCompleted?.Invoke(interacted);
        }

        public IEnumerator ShowDialogue(string npcId, string dialogueText, Action<bool> onCompleted)
        {
            if (string.IsNullOrWhiteSpace(dialogueText))
            {
                onCompleted?.Invoke(true);
                yield break;
            }

            if (!TryGetOrSpawn(npcId, out EventNpcBinding binding))
            {
                onCompleted?.Invoke(false);
                yield break;
            }

            if (!TryResolveSpeechBubblePool(out SpeechBubblePool pool))
            {
                onCompleted?.Invoke(false);
                yield break;
            }

            PlayerInteractor interactor = GameManager.HasInstance
                ? GameManager.Instance.Player?.Interactor
                : null;
            if (interactor == null)
            {
                Debug.LogError($"[EventNpcRuntime] 대사 입력을 받을 PlayerInteractor가 없습니다: {npcId}");
                onCompleted?.Invoke(false);
                yield break;
            }

            IInteractable interactionTarget = binding.InteractionTarget;
            while (IsAlive(binding)
                && (_busyNpcIds.Contains(npcId)
                    || _busyInteractionTargets.Contains(interactionTarget)
                    || _activeDialogueBubble != null
                    || _dialogueInputScope != null
                    || DialogueInputLock.IsActive))
            {
                yield return null;
            }

            if (!IsAlive(binding)
                || !IsAlive(interactionTarget)
                || !_busyNpcIds.Add(npcId)
                || !_busyInteractionTargets.Add(interactionTarget))
            {
                _busyNpcIds.Remove(npcId);
                onCompleted?.Invoke(false);
                yield break;
            }

            bool typing = true;
            bool advanced = false;
            void HandleInteract(IInteractable candidate)
            {
                if (!ReferenceEquals(candidate, interactionTarget))
                    return;

                interactor.ConsumeCurrentInteraction();
                if (typing && _activeDialogueBubble != null && _activeDialogueBubble.CompleteTyping())
                {
                    typing = false;
                    return;
                }

                advanced = true;
            }

            try
            {
                _activeDialogueBubble = pool.RequestBubble();
                if (_activeDialogueBubble == null)
                {
                    Debug.LogError($"[EventNpcRuntime] 말풍선을 가져올 수 없습니다: {npcId}");
                    yield break;
                }

                if (!DialogueInputLock.TryAcquire(interactionTarget, out _dialogueInputScope))
                {
                    Debug.LogError($"[EventNpcRuntime] 대사 입력 잠금을 획득할 수 없습니다: {npcId}");
                    yield break;
                }

                yield return PrepareShopVisual(binding);
                if (!IsShopVisualReady(binding))
                {
                    Debug.LogError($"[EventNpcRuntime] 상점 NPC를 이벤트 위치로 이동할 수 없습니다: {npcId}");
                    yield break;
                }

                if (!BeginEventInteraction(binding))
                {
                    Debug.LogError($"[EventNpcRuntime] 이벤트 상호작용 상태를 시작할 수 없습니다: {npcId}");
                    yield break;
                }

                _dialogueInteractor = interactor;
                interactor.RegisterIfOverlapping(interactionTarget);
                interactor.RefreshCurrentTarget();

                Transform dialogueAnchor = ResolveDialogueAnchor(binding);
                if (dialogueAnchor == null)
                {
                    Debug.LogError($"[EventNpcRuntime] 이벤트 NPC의 말풍선 기준점을 찾을 수 없습니다: {npcId}");
                    yield break;
                }

                _activeDialogueBubble.PlayPersistentText(
                    dialogueAnchor,
                    dialogueText,
                    () => typing = false,
                    binding.SpeechBubbleOffset);

                interactor.OnInteract += HandleInteract;
                while (!advanced && IsAlive(binding))
                    yield return null;

                onCompleted?.Invoke(advanced);
            }
            finally
            {
                if (interactor != null)
                    interactor.OnInteract -= HandleInteract;
                EndEventInteraction(binding);
                StopActiveDialogue();
                _busyNpcIds.Remove(npcId);
                _busyInteractionTargets.Remove(interactionTarget);
            }
        }

        public bool TryOpenShopAfterEvent(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId)
                || !_bindingsByNpcId.TryGetValue(npcId, out EventNpcBinding binding)
                || binding == null
                || !binding.Definition.UsesShopInteraction
                || !IsAlive(binding.InteractionTarget))
            {
                return false;
            }

            binding.InteractionTarget.Interact();
            return true;
        }

        public void BeginSceneRelease()
        {
            StopActiveDialogue();

            PlayerInteractor interactor = GameManager.HasInstance
                ? GameManager.Instance.Player?.Interactor
                : null;
            foreach (EventNpcBinding binding in _bindingsByNpcId.Values)
            {
                if (binding?.VisualActor != null && binding.HasDynamicVisual)
                    interactor?.UnregisterInteractable(binding.VisualActor);
            }

            interactor?.RefreshCurrentTarget();
            _busyNpcIds.Clear();
            _busyInteractionTargets.Clear();
        }

        public void ReleaseSceneInstances(bool destroySceneObjects = false)
        {
            StopActiveDialogue();

            List<KeyValuePair<string, EventNpcBinding>> bindings =
                new(_bindingsByNpcId);
            for (int i = 0; i < bindings.Count; i++)
                ReleaseBinding(bindings[i].Key, bindings[i].Value, destroySceneObjects);

            _bindingsByNpcId.Clear();
            _spawnPointsByNpcId.Clear();
            _busyNpcIds.Clear();
            _busyInteractionTargets.Clear();
            _speechBubblePool = null;
        }

        private bool TryGetOrSpawn(string npcId, out EventNpcBinding binding)
        {
            binding = null;
            if (string.IsNullOrWhiteSpace(npcId))
            {
                Debug.LogError("[EventNpcRuntime] NPC ID가 비어 있습니다");
                return false;
            }

            if (_bindingsByNpcId.TryGetValue(npcId, out binding))
            {
                if (IsAlive(binding))
                    return true;

                ReleaseBinding(npcId, binding, true);
                binding = null;
            }

            if (_catalog == null || !_catalog.TryGet(npcId, out EventNpcDefinition definition))
            {
                Debug.LogError($"[EventNpcRuntime] Catalog에서 이벤트 NPC를 찾을 수 없습니다: {npcId}");
                return false;
            }

            TownKey currentTown = GameManager.HasInstance ? GameManager.Instance.CurrentTownKey : TownKey.Unknown;
            if (currentTown != definition.Town)
            {
                Debug.LogError($"[EventNpcRuntime] 이벤트 NPC의 마을이 일치하지 않습니다: {npcId}, expected={definition.Town}, current={currentTown}");
                return false;
            }

            EventNpcSpawnPoint spawnPoint = null;
            if (!definition.UsesShopInteraction
                && !EventNpcSpawnPoint.TryGetActive(definition.SpawnPointId, out spawnPoint))
            {
                Debug.LogError($"[EventNpcRuntime] 활성 Spawn Point를 찾을 수 없습니다: {definition.SpawnPointId}");
                return false;
            }

            IInteractable interactionTarget = null;
            if (definition.UsesShopInteraction
                && !TryResolveShopInteractionTarget(definition, out interactionTarget))
            {
                Debug.LogError($"[EventNpcRuntime] 이벤트 상점 상호작용 대상을 찾을 수 없습니다: {npcId}");
                return false;
            }

            Transform shopSpawnAnchor = null;
            if (definition.UsesShopVisual
                && !TryResolveShopSpawnAnchor(definition, interactionTarget, out shopSpawnAnchor))
            {
                return false;
            }

            EventNpcShopVisual shopVisual = null;
            if (definition.UsesShopVisual
                && !TryCreateShopVisual(definition, interactionTarget, shopSpawnAnchor, out shopVisual))
            {
                return false;
            }

            EventNpcActor visualActor = null;
            bool hasDynamicVisual = !definition.UsesShopInteraction
                && definition.SpawnsDynamicVisual;
            if (hasDynamicVisual)
            {
                if (definition.Prefab == null)
                {
                    Debug.LogError($"[EventNpcRuntime] 동적 이벤트 NPC 프리팹이 없습니다: {npcId}");
                    return false;
                }

                if (!definition.UsesShopInteraction)
                    spawnPoint.ApplyReplacementTarget();

                GameObject instance = UnityEngine.Object.Instantiate(
                    definition.Prefab,
                    spawnPoint.transform.position,
                    spawnPoint.transform.rotation,
                    spawnPoint.transform);
                instance.name = $"EventNpc_{npcId}";
                instance.SetActive(false);

                visualActor = instance.GetComponent<EventNpcActor>();
                if (visualActor == null)
                {
                    Debug.LogError($"[EventNpcRuntime] 이벤트 NPC 컴포넌트가 없습니다: {npcId}", instance);
                    UnityEngine.Object.Destroy(instance);
                    spawnPoint.RestoreReplacementTarget();
                    return false;
                }

                if (!visualActor.EnterEventMode())
                {
                    UnityEngine.Object.Destroy(instance);
                    spawnPoint.RestoreReplacementTarget();
                    return false;
                }

                instance.SetActive(true);
                // 일반 NPC 스포너가 설정하는 NPC 정렬 레이어를 동적 이벤트 NPC에도 적용
                visualActor.SetRenderOrder(NPCBase.NextRenderOrder());
                _spawnPointsByNpcId[npcId] = spawnPoint;
            }

            if (!definition.UsesShopInteraction)
            {
                interactionTarget = visualActor;
                if (!IsAlive(interactionTarget))
                {
                    ReleaseBinding(npcId, new EventNpcBinding
                    {
                        Definition = definition,
                        VisualActor = visualActor,
                        VisualAnchor = visualActor != null
                            ? visualActor.transform
                            : spawnPoint.VisualAnchor != null ? spawnPoint.VisualAnchor : spawnPoint.transform,
                        InteractionTarget = visualActor,
                        HasDynamicVisual = hasDynamicVisual
                    }, true);
                    return false;
                }
            }

            binding = new EventNpcBinding
            {
                Definition = definition,
                VisualActor = visualActor,
                ShopVisual = shopVisual,
                ShopSpawnAnchor = shopSpawnAnchor,
                VisualAnchor = visualActor != null
                    ? visualActor.transform
                    : shopVisual != null
                        ? shopVisual.Transform
                    : definition.UsesShopInteraction
                        ? interactionTarget?.GameObject?.transform
                        : spawnPoint?.VisualAnchor != null ? spawnPoint.VisualAnchor : spawnPoint?.transform,
                InteractionTarget = interactionTarget,
                HasDynamicVisual = hasDynamicVisual
            };
            _bindingsByNpcId[npcId] = binding;
            return true;
        }

        private bool TryResolveShopInteractionTarget(
            EventNpcDefinition definition,
            out IInteractable interactionTarget)
        {
            interactionTarget = null;
            if (definition.InteractionMode == EventNpcInteractionMode.RegularShop)
            {
                ShopInteractable[] shops = UnityEngine.Object.FindObjectsByType<ShopInteractable>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int i = 0; i < shops.Length; i++)
                {
                    ShopInteractable shop = shops[i];
                    if (shop != null
                        && shop.ShopId == definition.ShopId
                        && IsInActiveScene(shop))
                    {
                        interactionTarget = shop;
                        return true;
                    }
                }

                return false;
            }

            if (definition.InteractionMode == EventNpcInteractionMode.SpecialShop)
            {
                SpecialShopInteractable[] shops = UnityEngine.Object.FindObjectsByType<SpecialShopInteractable>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int i = 0; i < shops.Length; i++)
                {
                    SpecialShopInteractable shop = shops[i];
                    if (shop != null
                        && shop.ShopType == definition.SpecialShopType
                        && IsInActiveScene(shop))
                    {
                        interactionTarget = shop;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryCreateShopVisual(
            EventNpcDefinition definition,
            IInteractable interactionTarget,
            Transform shopSpawnAnchor,
            out EventNpcShopVisual shopVisual)
        {
            shopVisual = null;
            Transform shopRoot = interactionTarget?.GameObject?.transform;
            Transform visual = null;
            EventNpcShopSpawnPoint spawnPoint = shopSpawnAnchor != null
                ? shopSpawnAnchor.GetComponent<EventNpcShopSpawnPoint>()
                : null;

            if (spawnPoint?.Visual != null)
            {
                visual = spawnPoint.Visual;
                if (shopRoot == null || (!ReferenceEquals(visual, shopRoot) && !visual.IsChildOf(shopRoot)))
                {
                    Debug.LogError($"[EventNpcRuntime] 이벤트 NPC 시각 참조가 상점 프리팹 외부를 가리킵니다: {definition.NpcId}", visual);
                    return false;
                }

                if (!HasVisualRenderer(visual))
                {
                    Debug.LogError($"[EventNpcRuntime] 이벤트 NPC 시각 참조에 렌더러가 없습니다: {definition.NpcId}", visual);
                    return false;
                }
            }
            else
            {
                visual = FindShopNpcVisual(shopRoot);
                if (visual != null)
                {
                    Debug.LogWarning($"[EventNpcRuntime] EventNpcShopSpawnPoint Visual 참조가 비어 있어 InteriorMask의 단일 후보를 임시 사용합니다: {definition.NpcId}", shopRoot);
                }
            }

            if (visual == null)
            {
                Debug.LogError($"[EventNpcRuntime] 상점 InteriorMask 안에서 이벤트 NPC를 찾을 수 없습니다: {definition.NpcId}");
                return false;
            }

            shopVisual = new EventNpcShopVisual(visual);
            if (!shopVisual.HasAlphaTarget)
            {
                Debug.LogError($"[EventNpcRuntime] 상점 이벤트 NPC의 알파 대상(SkeletonRenderer/SpriteRenderer/CanvasGroup)이 없습니다: {definition.NpcId}", visual);
                shopVisual = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveShopSpawnAnchor(
            EventNpcDefinition definition,
            IInteractable interactionTarget,
            out Transform spawnAnchor)
        {
            spawnAnchor = null;
            Transform shopRoot = interactionTarget?.GameObject?.transform;
            if (shopRoot == null)
                return false;

            EventNpcShopSpawnPoint[] spawnPoints = shopRoot.GetComponentsInChildren<EventNpcShopSpawnPoint>(true);
            int matchCount = 0;
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                EventNpcShopSpawnPoint candidate = spawnPoints[i];
                if (candidate == null || !candidate.Matches(definition.NpcId))
                    continue;

                spawnAnchor = candidate.transform;
                matchCount++;
            }

            if (matchCount == 1)
                return true;

            if (matchCount > 1)
            {
                Debug.LogError($"[EventNpcRuntime] 상점 프리팹에 이벤트 NPC 위치가 중복됩니다: {definition.NpcId}", shopRoot);
            }
            else
            {
                Debug.LogError($"[EventNpcRuntime] 상점 프리팹에서 이벤트 NPC 위치를 찾을 수 없습니다: {definition.NpcId}. EventNpcShopSpawnPoint 자식과 NPC ID를 확인하세요", shopRoot);
            }

            spawnAnchor = null;
            return false;
        }

        private static Transform FindShopNpcVisual(Transform shopRoot)
        {
            if (shopRoot == null)
                return null;

            Transform interiorMask = FindDescendantByName(shopRoot, "InteriorMask");
            if (interiorMask == null)
                return null;

            Transform singleVisual = null;
            int visualCount = 0;
            for (int i = 0; i < interiorMask.childCount; i++)
            {
                Transform child = interiorMask.GetChild(i);
                if (!HasVisualRenderer(child))
                    continue;

                singleVisual = child;
                visualCount++;
            }

            if (visualCount == 1)
                return singleVisual;

            if (visualCount > 1)
            {
                Debug.LogError($"[EventNpcRuntime] InteriorMask 아래 이벤트 NPC 후보가 여러 개입니다: {shopRoot.name}", interiorMask);
            }

            return null;
        }

        private static Transform FindDescendantByName(Transform root, string expectedName)
        {
            if (root == null || string.IsNullOrWhiteSpace(expectedName))
                return null;

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform candidate = descendants[i];
                if (candidate != root
                    && string.Equals(candidate.name, expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool HasVisualRenderer(Transform target)
        {
            return target != null
                && (target.GetComponentInChildren<Spine.Unity.SkeletonRenderer>(true) != null
                    || target.GetComponentInChildren<SpriteRenderer>(true) != null
                    || target.GetComponentInChildren<MeshRenderer>(true) != null);
        }

        private IEnumerator PrepareShopVisual(EventNpcBinding binding)
        {
            if (binding?.ShopVisual == null)
                yield break;

            if (!binding.ShopVisual.IsPrepared)
                yield return binding.ShopVisual.Reveal(binding.ShopSpawnAnchor);
        }

        private static bool IsShopVisualReady(EventNpcBinding binding)
        {
            return binding?.ShopVisual == null
                || binding.ShopVisual.IsPrepared;
        }

        private static Transform ResolveDialogueAnchor(EventNpcBinding binding)
        {
            if (binding?.VisualActor != null)
                return binding.VisualActor.transform;

            if (binding?.ShopVisual != null)
                return binding.ShopVisual.Transform;

            return binding?.VisualAnchor != null
                ? binding.VisualAnchor
                : binding?.InteractionTarget?.GameObject?.transform;
        }

        private bool BeginEventInteraction(EventNpcBinding binding)
        {
            if (binding == null || !IsAlive(binding.InteractionTarget))
                return false;

            if (binding.VisualActor != null
                && ReferenceEquals(binding.InteractionTarget, binding.VisualActor))
            {
                return binding.VisualActor.BeginInteraction();
            }

            return binding.InteractionTarget.CanInteract;
        }

        private static void EndEventInteraction(EventNpcBinding binding)
        {
            if (binding?.VisualActor == null
                || !ReferenceEquals(binding.InteractionTarget, binding.VisualActor))
            {
                return;
            }

            binding.VisualActor.EndInteraction();
        }

        private bool TryResolveSpeechBubblePool(out SpeechBubblePool pool)
        {
            if (_speechBubblePool == null)
                _speechBubblePool = UnityEngine.Object.FindFirstObjectByType<SpeechBubblePool>();

            pool = _speechBubblePool;
            if (pool == null)
            {
                Debug.LogError("[EventNpcRuntime] 현재 씬에서 SpeechBubblePool을 찾을 수 없습니다");
                return false;
            }

            if (!pool.TryValidateConfiguration(out string failReason))
            {
                Debug.LogError($"[EventNpcRuntime] SpeechBubblePool 설정이 올바르지 않습니다: {failReason}", pool);
                return false;
            }

            return true;
        }

        private void StopActiveDialogue()
        {
            if (_activeDialogueBubble != null)
                _activeDialogueBubble.Stop();

            _activeDialogueBubble = null;

            _dialogueInputScope?.Dispose();
            _dialogueInputScope = null;

            PlayerInteractor interactor = _dialogueInteractor;
            _dialogueInteractor = null;
            if (interactor != null)
                interactor.RefreshCurrentTarget();
        }

        private static bool IsAlive(EventNpcBinding binding)
        {
            return binding != null
                && IsAlive(binding.InteractionTarget)
                && (!binding.HasDynamicVisual || IsAlive(binding.VisualActor))
                && (binding.ShopVisual == null
                    || (binding.ShopVisual.Transform != null
                        && binding.ShopVisual.Transform.gameObject.scene.IsValid()))
                && (binding.ShopSpawnAnchor == null || binding.ShopSpawnAnchor.gameObject.scene.IsValid())
                && (binding.VisualAnchor == null || binding.VisualAnchor.gameObject.scene.IsValid());
        }

        private static bool IsAlive(EventNpcActor target)
        {
            return target != null && target.gameObject.scene.IsValid();
        }

        private static bool IsAlive(IInteractable target)
        {
            if (target == null || target.GameObject == null || !target.GameObject.scene.IsValid())
                return false;

            return target is not Behaviour behaviour || behaviour.isActiveAndEnabled;
        }

        private static bool IsInActiveScene(Component component)
        {
            return component != null
                && component.gameObject.scene.IsValid()
                && component.gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                && component.gameObject.activeInHierarchy;
        }

        private void ReleaseBinding(string npcId, EventNpcBinding binding, bool destroySceneObject)
        {
            RestoreSpawnPoint(npcId);
            binding?.ShopVisual?.Restore();

            if (binding?.VisualActor != null && binding.HasDynamicVisual)
            {
                PlayerInteractor interactor = GameManager.HasInstance
                    ? GameManager.Instance.Player?.Interactor
                    : null;
                if (interactor != null)
                    interactor.UnregisterInteractable(binding.VisualActor);

                if (destroySceneObject)
                    UnityEngine.Object.Destroy(binding.VisualActor.gameObject);
            }

            _bindingsByNpcId.Remove(npcId);
        }

        private void RestoreSpawnPoint(string npcId)
        {
            if (string.IsNullOrWhiteSpace(npcId))
                return;

            if (_spawnPointsByNpcId.TryGetValue(npcId, out EventNpcSpawnPoint spawnPoint))
                spawnPoint?.RestoreReplacementTarget();

            _spawnPointsByNpcId.Remove(npcId);
        }
    }
}
