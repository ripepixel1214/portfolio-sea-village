using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SeaVillage.Utilities;
using SeaVillage.Core;
using SeaVillage.UI.Tutorial;
using Sirenix.OdinInspector;

namespace SeaVillage.UI
{
    /// <summary>
    /// UI 중앙 관리 클래스
    /// </summary>
    public class UIManager : Singleton<UIManager>
    {
        public const string MenuButtonName = "Menu Button";
        public const string InteractionButtonName = "Interaction Button";

        [Title("UI Registry")]
        [SerializeField] private UIRegistry _uiRegistry;
        [SerializeField] private Transform panelRoot;

        [Title("UI Paths")]
        [SerializeField] private string _uiRegistryPrimaryResourcePath = "Data/ScriptableObjects/UI/UIRegistry";
        [SerializeField] private string _uiRegistryFallbackResourcePath = "Data/ScriptableObjects/UIRegistry";
        [SerializeField] private string _iconSpriteResourcePath = "Sprites/Sample/SampleIcons";
        [SerializeField, Tooltip("개별 아이콘 폴더. 같은 이름이면 위 베이스 시트보다 우선한다")]
        private string _iconOverrideResourcePath = "Sprites/UI/Items";
        [SerializeField] private string _panelCanvasObjectName = "Panel Canvas";
        [SerializeField] private string _tutorialPresentationResourcePath = "Prefabs/UI/Tutorial/Tutorial Presentation";

        private readonly List<UIPanel> _openedPanels = new List<UIPanel>();
        private UIPanel _currentActivePanel = null;
        private readonly Dictionary<Type, UIPanel> _registeredPanels = new Dictionary<Type, UIPanel>();
        private readonly Dictionary<UIPanel, UIPanel> _panelOwners = new Dictionary<UIPanel, UIPanel>();
        private readonly Dictionary<UIPanel, List<UIPanel>> _ownedPanels = new Dictionary<UIPanel, List<UIPanel>>();
        private readonly HashSet<UIPanel> _runtimeContextualPanels = new HashSet<UIPanel>();

        private readonly Dictionary<string, Sprite> _iconCache = new Dictionary<string, Sprite>();
        private TutorialPresentationController _tutorialPresentation;

        // 아이콘 이름이 비었을 때 쓰는 대체 아이콘
        private const string DefaultIconName = "qGold";
        private const string EventIllustrationResourcePath = "Sprites/UI/Event";

        // Properties
        public UIPanel CurrentActivePanel => _currentActivePanel;

        public bool IsAnyPanelOpened => _currentActivePanel != null;

        public Action OnPanelOpened;
        public Action OnPanelClosed;
        public Action OnAllPanelsClosed;

        #region MonoBehaviour
        protected override void Awake()
        {
            base.Awake();
            InitializePanelRoot();
            InitializeRegistry();
            InitializeIconCache();
            RegisterPanelsFromPanelRoot();
            EnsureTutorialPresentation();
        }
        #endregion

        #region Scene Hooks
        /// <summary>
        /// 씬 로드 시 GameManager로부터 호출됨
        /// </summary>
        public void OnSceneLoaded()
        {
            InitializePanelRoot();
            RegisterPanelsFromPanelRoot();
            EnsureTutorialPresentation();
        }

        /// <summary>
        /// 씬 언로드 시 GameManager로부터 호출됨
        /// </summary>
        public void OnSceneUnLoaded()
        {
            ClearAllPanels();
        }
        #endregion

        #region Public Panel API
        public bool EnsureTutorialPresentationReady()
        {
            EnsureTutorialPresentation();
            if (_tutorialPresentation == null || !_tutorialPresentation.IsConfigured)
                return false;

            if (_tutorialPresentation.TryValidateSceneDependencies(out string failReason))
                return true;

            Debug.LogError($"UI Manager: Tutorial Presentation 씬 의존성 검증 실패: {failReason}");
            return false;
        }

        /// <summary>
        /// 현재 씬의 UI Controller를 통해 패널을 등록
        /// </summary>
        public void RegisterPanel<T>(T panel) where T : UIPanel
        {
            if (panel == null)
            {
                Debug.LogWarning($"UI Manager: Cannot register NULL panel ({typeof(T).Name})");
                return;
            }

            RegisterPanelDirect(typeof(T), panel);
        }

        /// <summary>
        /// 타입 정보를 직접 사용하여 패널 등록 (제네릭 우회) <br/>
        /// UI Controller의 루프에서 패널을 자동 등록할 때 사용됨
        /// </summary>
        public void RegisterPanelDirect(Type type, UIPanel panel)
        {
            if (panel == null) return;

            if (_registeredPanels.TryGetValue(type, out UIPanel existingPanel))
            {
                if (existingPanel == panel) return;
                Debug.Log($"UI Manager: Overwriting registered panel({type.Name})");
            }

            _registeredPanels[type] = panel;
        }

        /// <summary>
        /// 현재 panelRoot 하위의 모든 UIPanel을 선탐색해 등록
        /// </summary>
        public int RegisterPanelsFromPanelRoot()
        {
            return RegisterPanelsInHierarchy(panelRoot);
        }

        /// <summary>
        /// 지정된 루트 하위의 UIPanel을 등록 (IContextualPanel 제외)
        /// </summary>
        public int RegisterPanelsInHierarchy(Transform root)
        {
            if (root == null)
                return 0;

            UIPanel[] panels = root.GetComponentsInChildren<UIPanel>(true);
            if (panels == null || panels.Length == 0)
                return 0;

            int registeredCount = 0;

            for (int i = 0; i < panels.Length; i++)
            {
                UIPanel panel = panels[i];
                if (panel == null)
                    continue;

                Type panelType = panel.GetType();
                bool isContextualPanelType = typeof(IContextualPanel).IsAssignableFrom(panelType);
                if (isContextualPanelType)
                    continue;

                if (TryGetRegisteredPanel(panelType, out UIPanel existingPanel))
                {
                    if (existingPanel == panel)
                        continue;

                    Debug.LogWarning($"UI Manager: Duplicate panel type detected while scanning scene ({panelType.Name}). Keeping existing registration: {existingPanel.name}, skipped: {panel.name}");
                    continue;
                }

                RegisterPanelDirect(panelType, panel);
                registeredCount++;
            }

            if (registeredCount > 0)
                Debug.Log($"UI Manager: Pre-registered {registeredCount} panel(s) from '{root.name}'.");

            return registeredCount;
        }

        public T OpenPanel<T>() where T : UIPanel
        {
            Type panelType = typeof(T);
            bool useContextParenting = typeof(IContextualPanel).IsAssignableFrom(panelType);
            UIPanel ownerPanel = _currentActivePanel;
            bool hasRegisteredPanel = TryGetRegisteredPanel(panelType, out UIPanel registeredPanel);

            if (!hasRegisteredPanel)
            {
                GameObject prefab = GetPanelPrefab<T>();
                if (prefab == null)
                {
                    Debug.LogError($"UI Manager: Prefab not found for panel type: {typeof(T).Name}");
                    return null;
                }

                Transform parent = useContextParenting ? ResolveSpawnParent(ownerPanel) : panelRoot;
                GameObject instance = Instantiate(prefab, parent);
                instance.name = prefab.name;
                T panelComponent = instance.GetComponent<T>();

                if (panelComponent == null)
                {
                    Debug.LogError($"UI Manager: Panel component {typeof(T).Name} not found on prefab");
                    Destroy(instance);
                    return null;
                }

                RegisterPanel(panelComponent);
                registeredPanel = panelComponent;
                if (useContextParenting)
                {
                    _runtimeContextualPanels.Add(panelComponent);
                    UpdatePanelOwnership(panelComponent, ownerPanel);
                }
                panelComponent.gameObject.SetActive(false);
            }

            T panel = registeredPanel as T;
            if (panel == null)
            {
                Debug.LogError($"UI Manager: Registered panel is not of type: {typeof(T).Name}");
                return null;
            }

            if (useContextParenting)
            {
                if (ownerPanel == panel)
                    ownerPanel = GetOwnerPanel(panel);

                Transform targetParent = ResolveSpawnParent(ownerPanel);
                if (targetParent != null && panel.transform.parent != targetParent)
                    panel.transform.SetParent(targetParent, false);

                UpdatePanelOwnership(panel, ownerPanel);
            }
            else
            {
                if (panelRoot != null && panel.transform.parent != panelRoot)
                    panel.transform.SetParent(panelRoot, false);

                RemovePanelOwnership(panel);
            }

            OpenPanelInternal(panel);
            return panel;
        }

        public void ClosePanel<T>() where T : UIPanel
        {
            Type panelType = typeof(T);
            if (_registeredPanels.TryGetValue(panelType, out UIPanel panel))
                ClosePanelInternal(panel);
        }

        /// <summary>
        /// 현재 활성 패널 닫기
        /// </summary>
        public void CloseCurrentPanel()
        {
            SyncPanelRuntimeState();

            if (_currentActivePanel != null)
                ClosePanelInternal(_currentActivePanel);
            else
                Debug.LogWarning("[UI Manager] No active panel to close.");
        }

        /// <summary>
        /// 모든 패널 닫기 (비동기 close 루틴 기반)
        /// </summary>
        public void CloseAllPanels()
        {
            var panels = new List<UIPanel>(_registeredPanels.Values);
            for (int i = 0; i < panels.Count; i++)
            {
                UIPanel panel = panels[i];
                if (panel != null && panel.gameObject.activeInHierarchy)
                    ClosePanelInternal(panel);
            }
        }

        public void ClearAllPanels()
        {
            CloseAllPanels();
            _registeredPanels.Clear();
            _openedPanels.Clear();
            _panelOwners.Clear();
            _ownedPanels.Clear();
            _runtimeContextualPanels.Clear();
            _currentActivePanel = null;

            TimeManager.Instance.ResumeTimeProgress();

            Debug.Log("UI Manager: Cleared all panel references");
        }

        public T GetPanel<T>() where T : UIPanel
        {
            Type panelType = typeof(T);
            return _registeredPanels.TryGetValue(panelType, out UIPanel panel) ? panel as T : null;
        }

        /// <summary>
        /// 알림 메시지 표시 (확인 버튼만)
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="onConfirm">확인 버튼 클릭 시 콜백 (선택)</param>
        /// <param name="title">타이틀 텍스트 (선택, 기본값: "알림")</param>
        public void ShowAlertMessage(string message, Action onConfirm = null, string title = "")
        {
            var panel = OpenPanel<SystemMessagePanel>();
            if (panel != null)
                panel.SetupAlert(message, onConfirm, title);
        }

        /// <summary>
        /// 확인 대화상자 표시 (확인 + 취소 버튼)
        /// </summary>
        /// <param name="message">표시할 메시지</param>
        /// <param name="onConfirm">확인 버튼 클릭 시 콜백</param>
        /// <param name="onCancel">취소 버튼 클릭 시 콜백 (선택)</param>
        /// <param name="title">타이틀 텍스트 (선택, 기본값: "확인")</param>
        public void ShowConfirmMessage(string message, Action onConfirm, Action onCancel = null, string title = "확인")
        {
            var panel = OpenPanel<SystemMessagePanel>();
            if (panel != null)
                panel.SetupConfirm(message, onConfirm, onCancel, title);
        }

        /// <summary>
        /// 아이콘 이름으로 스프라이트를 로드 (캐시 사용)
        /// </summary>
        public Sprite LoadItemIcon(string imageName)
        {
            if (string.IsNullOrEmpty(imageName))
            {
                Debug.LogWarning("[UI Manager] LoadItemIcon called with null or empty imageName");
                return _iconCache.TryGetValue(DefaultIconName, out Sprite defaultIcon) ? defaultIcon : null;
            }

            if (_iconCache.TryGetValue(imageName, out Sprite cachedIcon))
                return cachedIcon;

            Debug.LogWarning($"[UI Manager] Item icon '{imageName}' not found in resources.");
            return null;
        }

        /// <summary>
        /// 데이터베이스의 아이템들과 Icon sprites를 연결
        /// </summary>
        public void LinkIconsToDatabase()
        {
            var itemDB = Data.DataManager.Instance?.ItemDatabase;
            if (itemDB == null)
            {
                Debug.LogWarning("UI Manager: ItemDatabase not found. Skipping icon linking.");
                return;
            }

            foreach (var item in itemDB.Items)
                if (!string.IsNullOrEmpty(item.Image) && _iconCache.TryGetValue(item.Image, out Sprite icon))
                    item.Icon = icon;
        }
        #endregion

        #region Initialization
        private void InitializeRegistry()
        {
            if (_uiRegistry == null)
            {
                _uiRegistry = Resources.Load<UIRegistry>(_uiRegistryPrimaryResourcePath);
                if (_uiRegistry == null)
                    _uiRegistry = Resources.Load<UIRegistry>(_uiRegistryFallbackResourcePath);

                if (_uiRegistry == null)
                    Debug.LogWarning($"UI Manager: UIRegistry not found. Checked Resources/{_uiRegistryPrimaryResourcePath} and Resources/{_uiRegistryFallbackResourcePath}");
            }
        }

        private void InitializeIconCache()
        {
            _iconCache.Clear();

            // 베이스 → 오버라이드 순으로 쌓아 같은 이름은 오버라이드가 이긴다
            CacheIconsFrom(_iconSpriteResourcePath);
            CacheIconsFrom(_iconOverrideResourcePath);
            CacheEventIllustrations();
        }

        private void CacheIconsFrom(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                return;

            var sprites = Resources.LoadAll<Sprite>(resourcePath);
            foreach (var sprite in sprites)
                _iconCache[sprite.name] = sprite;
        }

        private void CacheEventIllustrations()
        {
            var sprites = Resources.LoadAll<Sprite>(EventIllustrationResourcePath);
            foreach (var sprite in sprites)
            {
                if (sprite == null)
                    continue;

                _iconCache[sprite.name] = sprite;

                if (sprite.name.EndsWith("_0", StringComparison.Ordinal))
                    _iconCache[sprite.name[..^2]] = sprite;
            }
        }

        private void InitializePanelRoot()
        {
            BaseUIController baseController = FindFirstObjectByType<BaseUIController>();
            if (baseController != null)
            {
                panelRoot = FindPanelCanvas(baseController.transform);

                if (panelRoot == null)
                    Debug.LogError("UI Manager: BaseUIController의 자식 중 Panel Canvas를 찾지 못했습니다.");
            }
            else
            {
                Debug.LogWarning("UI Manager: BaseUIController가 씬에 존재하지 않습니다. MainMenuUIController를 찾기를 시도합니다.");
                MainMenuUIController mmUIController = FindFirstObjectByType<MainMenuUIController>();
                if (mmUIController != null)
                {
                    panelRoot = FindPanelCanvas(mmUIController.transform);
                    if (panelRoot == null)
                        panelRoot = mmUIController.transform;
                }
            }

            if (panelRoot == null)
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                    panelRoot = canvas.transform;
            }

            if (panelRoot == null)
                Debug.LogWarning("UI Manager: Panel root transform not found in the scene.");
            else
                Debug.Log($"UI Manager: Panel root set to '{panelRoot.name}'.");
        }

        private void EnsureTutorialPresentation()
        {
            if (TutorialPresentationController.Current != null
                && TutorialPresentationController.Current.IsConfigured)
            {
                _tutorialPresentation = TutorialPresentationController.Current;
                return;
            }

            TutorialPresentationController prefab =
                Resources.Load<TutorialPresentationController>(_tutorialPresentationResourcePath);
            if (prefab == null)
            {
                Debug.LogError(
                    $"UI Manager: Tutorial Presentation 프리팹을 찾을 수 없습니다: {_tutorialPresentationResourcePath}");
                return;
            }

            _tutorialPresentation = Instantiate(prefab);
            _tutorialPresentation.name = prefab.name;
        }

        private Transform FindPanelCanvas(Transform controllerRoot)
        {
            if (controllerRoot == null)
                return null;

            Transform panelCanvas = controllerRoot.Find(_panelCanvasObjectName);
            if (panelCanvas != null && panelCanvas.GetComponent<Canvas>() != null)
                return panelCanvas;

            Canvas[] canvases = controllerRoot.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null)
                    continue;

                if (canvas.gameObject.name.Equals(_panelCanvasObjectName, StringComparison.OrdinalIgnoreCase))
                    return canvas.transform;
            }

            return null;
        }
        #endregion

        #region Registration & Lookup Helpers
        private bool TryGetRegisteredPanel(Type panelType, out UIPanel panel)
        {
            if (_registeredPanels.TryGetValue(panelType, out panel))
            {
                if (panel != null)
                    return true;

                _registeredPanels.Remove(panelType);
            }

            panel = null;
            return false;
        }

        private GameObject GetPanelPrefab<T>() where T : UIPanel
        {
            if (_uiRegistry == null)
            {
                Debug.LogError("UI Manager: UI Registry is not assigned!");
                return null;
            }

            return _uiRegistry.GetPrefab<T>();
        }

        private bool IsRuntimeContextualPanel(UIPanel panel)
        {
            return panel != null && panel is IContextualPanel && _runtimeContextualPanels.Contains(panel);
        }

        private void UnregisterPanel(UIPanel panel)
        {
            if (panel == null)
                return;

            Type panelType = panel.GetType();
            if (_registeredPanels.TryGetValue(panelType, out UIPanel registeredPanel) && registeredPanel == panel)
                _registeredPanels.Remove(panelType);
        }
        #endregion

        #region Ownership Helpers
        private Transform ResolveSpawnParent(UIPanel ownerPanel)
        {
            if (ownerPanel != null)
                return ownerPanel.transform;

            return panelRoot;
        }

        private UIPanel GetOwnerPanel(UIPanel panel)
        {
            if (panel == null)
                return null;

            return _panelOwners.TryGetValue(panel, out UIPanel owner) ? owner : null;
        }

        private void UpdatePanelOwnership(UIPanel panel, UIPanel owner)
        {
            if (panel == null)
                return;

            if (owner == panel)
                owner = null;

            RemovePanelOwnership(panel);

            if (owner == null)
                return;

            _panelOwners[panel] = owner;
            if (!_ownedPanels.TryGetValue(owner, out List<UIPanel> children))
            {
                children = new List<UIPanel>();
                _ownedPanels[owner] = children;
            }

            if (!children.Contains(panel))
                children.Add(panel);
        }

        private void RemovePanelOwnership(UIPanel panel)
        {
            if (panel == null)
                return;

            if (_panelOwners.TryGetValue(panel, out UIPanel owner))
            {
                _panelOwners.Remove(panel);

                if (owner != null && _ownedPanels.TryGetValue(owner, out List<UIPanel> children))
                {
                    children.Remove(panel);
                    if (children.Count == 0)
                        _ownedPanels.Remove(owner);
                }
            }

            _ownedPanels.Remove(panel);
        }

        // 특정 패널에 종속되는 패널이 있다면 모두 강제로 닫는다
        private void CloseOwnedPanels(UIPanel ownerPanel)
        {
            if (ownerPanel == null)
                return;

            if (!_ownedPanels.TryGetValue(ownerPanel, out List<UIPanel> children) || children.Count == 0)
                return;

            var snapshot = new List<UIPanel>(children);
            for (int i = snapshot.Count - 1; i >= 0; i--)
            {
                UIPanel child = snapshot[i];
                if (child == null)
                    continue;

                ForceClosePanelImmediate(child);
            }

            _ownedPanels.Remove(ownerPanel);
        }
        #endregion

        #region Runtime State Helpers
        private void FinalizePanelClose(UIPanel panel, bool destroyRuntimeContextual)
        {
            if (panel == null)
                return;

            bool destroyAfterClose = destroyRuntimeContextual && IsRuntimeContextualPanel(panel);

            _openedPanels.Remove(panel);
            RemovePanelOwnership(panel);

            if (destroyAfterClose)
            {
                _runtimeContextualPanels.Remove(panel);
                UnregisterPanel(panel);

                if (panel.gameObject != null)
                    Destroy(panel.gameObject);
            }
            else if (panel.gameObject != null)
            {
                panel.gameObject.SetActive(false);
            }
        }

        private void UpdateCurrentActivePanelFromStack()
        {
            if (_openedPanels.Count > 0)
            {
                UIPanel topPanel = _openedPanels[_openedPanels.Count - 1];
                if (_currentActivePanel != topPanel)
                    _currentActivePanel = topPanel;
            }
            else
            {
                _currentActivePanel = null;
            }
        }

        private void SyncPanelRuntimeState()
        {
            var snapshot = new List<UIPanel>(_openedPanels);
            for (int i = 0; i < snapshot.Count; i++)
            {
                UIPanel openedPanel = snapshot[i];
                if (openedPanel == null)
                {
                    _openedPanels.RemoveAll(p => p == null);
                    continue;
                }

                if (openedPanel.gameObject.activeInHierarchy)
                    continue;

                bool destroyBecauseOwnerInactive = false;
                if (IsRuntimeContextualPanel(openedPanel) && _panelOwners.TryGetValue(openedPanel, out UIPanel owner))
                {
                    bool ownerInactive = owner == null || owner.gameObject == null || !owner.gameObject.activeInHierarchy;
                    bool ownerNotOpened = owner == null || !_openedPanels.Contains(owner);
                    destroyBecauseOwnerInactive = ownerInactive || ownerNotOpened;
                }

                // 외부 요인으로 먼저 비활성화된 경우에도 상태를 Closed로 정리해 Closing 고착을 방지
                if (openedPanel.State != PanelState.Closed)
                    openedPanel.OnClose();

                FinalizePanelClose(openedPanel, destroyBecauseOwnerInactive);
            }

            if (_currentActivePanel != null && !_openedPanels.Contains(_currentActivePanel))
                _currentActivePanel = null;

            UpdateCurrentActivePanelFromStack();
        }
        #endregion

        #region Internal Open/Close Pipeline
        private void OpenPanelInternal(UIPanel panel)
        {
            if (panel == null) return;

            SyncPanelRuntimeState();

            if (panel.IsClosing)
            {
                Debug.LogWarning($"UI Manager: Panel {panel.GetType().Name} is closing and cannot be reopened yet");
                return;
            }

            if (_openedPanels.Contains(panel) && panel.gameObject.activeInHierarchy)
            {
                panel.transform.SetAsLastSibling();
                _currentActivePanel = panel;
                return;
            }

            panel.gameObject.SetActive(true);
            panel.transform.SetAsLastSibling();
            panel.OnOpen();

            _openedPanels.Add(panel);
            _currentActivePanel = panel;

            OnPanelOpened?.Invoke();

            // 인게임 시간 정지
            TimeManager.Instance.PauseTimeProgress();
        }

        private void ClosePanelInternal(UIPanel panel)
        {
            if (panel == null) return;

            SyncPanelRuntimeState();

            if (!_openedPanels.Contains(panel))
            {
                Debug.LogWarning($"UI Manager: Panel {panel.GetType().Name} is not in opened panels list");
                return;
            }

            if (panel.IsClosing)
                return;

            CloseOwnedPanels(panel);

            panel.OnCloseRequested();
            StartCoroutine(ClosePanelRoutine(panel));
        }

        private IEnumerator ClosePanelRoutine(UIPanel panel)
        {
            yield return panel.PlayCloseAnimation();

            if (!_openedPanels.Contains(panel))
            {
                // 이미 스택에서 제거된 경우에도 상태가 Closing에 고착되지 않도록 마무리
                if (panel != null && panel.State != PanelState.Closed)
                    panel.OnClose();

                yield break;
            }

            panel.OnClose();
            FinalizePanelClose(panel, false);
            UpdateCurrentActivePanelFromStack();

            OnPanelClosed?.Invoke();

            if (_currentActivePanel != null)
            {
                _currentActivePanel.OnFocusRestored();
            }
            else
            {
                OnAllPanelsClosed?.Invoke();

                // 모든 패널이 닫히면 인게임 시간 재개
                TimeManager.Instance.ResumeTimeProgress();
            }
        }

        private void ForceClosePanelImmediate(UIPanel panel)
        {
            if (panel == null)
                return;

            CloseOwnedPanels(panel);

            if (_openedPanels.Contains(panel))
            {
                if (!panel.IsClosing)
                    panel.OnCloseRequested();

                panel.OnClose();
                FinalizePanelClose(panel, true);
            }

            SyncPanelRuntimeState();
            OnPanelClosed?.Invoke();
        }
        #endregion
    }
}
