#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SeaVillage.Player;
using SeaVillage.Data;
using SeaVillage.Event;
using SeaVillage.NPC.Components;
using SeaVillage.Utilities;

namespace SeaVillage.Core
{
    /// <summary>
    /// UGUI based developer panel. Press F1 to toggle.
    /// </summary>
    public class DeveloperUGUIController : MonoBehaviour
    {
        private const KeyCode ToggleKey = KeyCode.F1;

        private static readonly string[] DebugSceneNames =
        {
            "StartTown",
            "ForestTown",
            "MineTown",
            "SeaTown",
            "DessertTown",
            "Ocean"
        };

        private static readonly string[] TownAffinityOrder =
        {
            "Start",
            "Forest",
            "Mine",
            "Cave",
            "Sea",
            "Dessert",
        };

        private static Font _defaultFont;

        private bool _isVisible;
        private bool _shouldPersistPlayerMoveSpeed;
        private bool _shouldPersistTimeSettings;
        private bool _shouldPersistTownLoveLevels;
        private bool _shouldPersistShipMoveSpeed;
        private bool _targetShipInventory;

        private float _persistedDayDuration = 30f;
        private float _persistedMoveSpeedMultiplier = 1f;
        private float _persistedShipMoveSpeed = 5f;
        private bool _persistedAutoProgress = true;

        private TimeManager _timeManager;
        private CurrencyManager _currencyManager;
        private InventoryManager _inventoryManager;
        private PlayerController _playerController;
        private PlayerStatManager _playerStatManager;
        private EventManager _eventManager;
        private SeaVillage.Ocean.ShipController _shipController;

        private GameObject _panelRoot;

        private Text _dayText;
        private Text _dayProgressText;
        private Slider _dayDurationSlider;
        private Text _dayDurationValueText;
        private Button _autoProgressButton;
        private Text _autoProgressButtonText;

        private Slider _moveSpeedSlider;
        private Text _moveSpeedValueText;

        private Slider _npcMoveSpeedSlider;
        private Text _npcMoveSpeedValueText;

        private Slider _shipMoveSpeedSlider;
        private Text _shipMoveSpeedValueText;

        private Text _goldText;
        private InputField _goldInput;

        private Text _inventoryWeightText;
        private Text _shipWeightText;
        private Text _shipCapacityText;

        private Transform _townLoveLevelRoot;
        private readonly Dictionary<string, int> _persistedTownLoveLevelValues = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Slider> _townLoveLevelSliders = new Dictionary<string, Slider>();
        private readonly Dictionary<string, Text> _townLoveLevelValueTexts = new Dictionary<string, Text>();

        private Text _itemInfoText;
        private InputField _itemIdInput;
        private InputField _itemQuantityInput;
        private InputField _itemPriceInput;
        private Button _targetInventoryButton;
        private Text _targetInventoryButtonText;

        private Text _statsText;
        private InputField _statDeltaInput;

        private Text _messageText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var existing = FindFirstObjectByType<DeveloperUGUIController>();
            if (existing != null)
                return;

            var go = new GameObject("DeveloperUGUIController");
            DontDestroyOnLoad(go);
            go.AddComponent<DeveloperUGUIController>();
        }

        private void Awake()
        {
            EnsureDefaultFont();
            EnsureEventSystem();
            BuildUI();
            SetVisible(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
                SetVisible(!_isVisible);

            RefreshReferences();
            CapturePersistentValues();

            if (!_isVisible)
                return;

            UpdateDeveloperPanelSelection();
            ApplyInputBlockState();
            RefreshRuntimeTexts();
        }

        private void OnDisable()
        {
            if (_playerController == null)
                _playerController = FindFirstObjectByType<PlayerController>();

            _playerController?.SetInputBlocked(false);
        }

        private void EnsureDefaultFont()
        {
            if (_defaultFont == null)
                _defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
                return;

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventSystem);
        }

        private void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_panelRoot != null)
                _panelRoot.SetActive(_isVisible);

            if (!_isVisible)
                ClearDeveloperPanelSelection();

            RefreshReferences();
            ApplyInputBlockState();

            if (_isVisible)
                RefreshRuntimeTexts();
        }

        private void RefreshReferences()
        {
            TimeManager newTimeManager = TimeManager.Instance;
            if (_timeManager != newTimeManager)
            {
                _timeManager = newTimeManager;
                ApplyPersistedTimeSettings();
            }

            _currencyManager = CurrencyManager.Instance;

            _inventoryManager = InventoryManager.Instance;

            PlayerController newPlayerController = FindFirstObjectByType<PlayerController>();
            if (_playerController != newPlayerController)
            {
                _playerController = newPlayerController;
                ApplyPersistedPlayerMoveSpeed();
            }

            _playerStatManager = PlayerStatManager.HasInstance ? PlayerStatManager.Instance : null;

            EventManager newEventManager = EventManager.Instance;
            if (_eventManager != newEventManager)
            {
                _eventManager = newEventManager;
                ApplyPersistedTownLoveLevels();
            }

            // 배는 Ocean 씬에만 존재 — PlayerController 와 동일하게 씬마다 재탐색
            SeaVillage.Ocean.ShipController newShipController = FindFirstObjectByType<SeaVillage.Ocean.ShipController>();
            if (_shipController != newShipController)
            {
                _shipController = newShipController;
                ApplyPersistedShipMoveSpeed();
            }
        }

        private void CapturePersistentValues()
        {
            if (_shouldPersistTimeSettings && _timeManager != null)
            {
                _persistedDayDuration = _timeManager.DayDuration;
                _persistedAutoProgress = _timeManager.AutoProgress;
            }

            if (_shouldPersistPlayerMoveSpeed && _playerController != null)
                _persistedMoveSpeedMultiplier = _playerController.DebugMoveSpeedMultiplier;

            if (_shouldPersistShipMoveSpeed && _shipController != null)
                _persistedShipMoveSpeed = _shipController.MoveSpeed;

            if (_shouldPersistTownLoveLevels && _eventManager != null)
            {
                for (int i = 0; i < TownAffinityOrder.Length; i++)
                {
                    string townName = TownAffinityOrder[i];
                    _persistedTownLoveLevelValues[townName] = GetTownLoveLevel(townName);
                }
            }
        }

        private void ApplyPersistedTimeSettings()
        {
            if (!_shouldPersistTimeSettings || _timeManager == null)
                return;

            if (!Mathf.Approximately(_timeManager.DayDuration, _persistedDayDuration))
                _timeManager.SetDayDuration(_persistedDayDuration);

            if (_timeManager.AutoProgress != _persistedAutoProgress)
                _timeManager.SetAutoProgress(_persistedAutoProgress);
        }

        private void ApplyPersistedPlayerMoveSpeed()
        {
            if (!_shouldPersistPlayerMoveSpeed || _playerController == null)
                return;

            if (!Mathf.Approximately(_playerController.DebugMoveSpeedMultiplier, _persistedMoveSpeedMultiplier))
                _playerController.SetDebugMoveSpeedMultiplier(_persistedMoveSpeedMultiplier);
        }

        private void ApplyPersistedShipMoveSpeed()
        {
            if (!_shouldPersistShipMoveSpeed || _shipController == null)
                return;

            if (!Mathf.Approximately(_shipController.MoveSpeed, _persistedShipMoveSpeed))
                _shipController.MoveSpeed = _persistedShipMoveSpeed;
        }

        private void ApplyPersistedTownLoveLevels()
        {
            if (!_shouldPersistTownLoveLevels || _eventManager == null)
                return;

            foreach (KeyValuePair<string, int> pair in _persistedTownLoveLevelValues)
            {
                int currentValue = GetTownLoveLevel(pair.Key);
                int delta = pair.Value - currentValue;
                if (delta != 0)
                    _eventManager.ChangeStat(TownKeyUtility.ToLoveLevelKey(pair.Key), delta);
            }
        }

        private void ApplyInputBlockState()
        {
            if (_playerController == null)
                return;

            bool shouldBlock = _isVisible && IsDeveloperPanelFocused();
            _playerController.SetInputBlocked(shouldBlock);
        }

        private bool IsDeveloperPanelFocused()
        {
            if (_panelRoot == null || !_panelRoot.activeInHierarchy)
                return false;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return false;

            GameObject selected = eventSystem.currentSelectedGameObject;
            return selected != null && selected.transform.IsChildOf(_panelRoot.transform);
        }

        private void UpdateDeveloperPanelSelection()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || _panelRoot == null)
                return;

            if (!WasOutsideClick(eventSystem))
                return;

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(_panelRoot.transform))
                eventSystem.SetSelectedGameObject(null);
        }

        private void ClearDeveloperPanelSelection()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || _panelRoot == null)
                return;

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(_panelRoot.transform))
                eventSystem.SetSelectedGameObject(null);
        }

        private bool WasOutsideClick(EventSystem eventSystem)
        {
            if (Input.GetMouseButtonDown(0))
                return !IsScreenPositionInsideDeveloperPanel(eventSystem, Input.mousePosition);

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began
                    && !IsScreenPositionInsideDeveloperPanel(eventSystem, touch.position))
                    return true;
            }

            return false;
        }

        private bool IsScreenPositionInsideDeveloperPanel(EventSystem eventSystem, Vector2 screenPosition)
        {
            if (eventSystem == null || _panelRoot == null)
                return false;

            PointerEventData eventData = new PointerEventData(eventSystem)
            {
                position = screenPosition
            };

            List<RaycastResult> results = new List<RaycastResult>(8);
            eventSystem.RaycastAll(eventData, results);

            foreach (RaycastResult result in results)
            {
                if (result.gameObject != null && result.gameObject.transform.IsChildOf(_panelRoot.transform))
                    return true;
            }

            return false;
        }

        private void BuildUI()
        {
            GameObject canvasGO = CreateUIObject("DeveloperCanvas", transform);
            DontDestroyOnLoad(canvasGO);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            _panelRoot = CreateUIObject("DeveloperPanel", canvasGO.transform);
            RectTransform panelRect = _panelRoot.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -24f);
            panelRect.sizeDelta = new Vector2(560f, 820f);

            Image panelImage = _panelRoot.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.85f);
            var panelFocusHandler = _panelRoot.AddComponent<UGUIPanelFocusHandler>();
            panelFocusHandler.Initialize(_panelRoot);

            Text title = CreateText("Title", _panelRoot.transform, 24, TextAnchor.MiddleLeft);
            title.text = "Developer Tools";
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(16f, -48f);
            titleRect.offsetMax = new Vector2(-16f, -8f);

            // Drag handle: allow moving panel by dragging header area.
            var dragHandle = CreateUIObject("DragHandle", _panelRoot.transform);
            RectTransform dragHandleRect = dragHandle.AddComponent<RectTransform>();
            dragHandleRect.anchorMin = new Vector2(0f, 1f);
            dragHandleRect.anchorMax = new Vector2(1f, 1f);
            dragHandleRect.pivot = new Vector2(0.5f, 1f);
            dragHandleRect.offsetMin = new Vector2(0f, -52f);
            dragHandleRect.offsetMax = new Vector2(0f, 0f);
            var dragImage = dragHandle.AddComponent<Image>();
            dragImage.color = new Color(1f, 1f, 1f, 0.01f);
            var dragComponent = dragHandle.AddComponent<UGUIPanelDragHandle>();
            dragComponent.Initialize(panelRect, canvas);

            var scrollRoot = CreateUIObject("ScrollRoot", _panelRoot.transform);
            RectTransform scrollRectTransform = scrollRoot.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0f, 0f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.offsetMin = new Vector2(12f, 12f);
            scrollRectTransform.offsetMax = new Vector2(-12f, -56f);

            var scrollRect = scrollRoot.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            var viewport = CreateUIObject("Viewport", scrollRoot.transform);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.1f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = CreateUIObject("Content", viewport.transform);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(12, 12, 12, 12);
            contentLayout.spacing = 8f;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;

            var contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            BuildContent(content.transform);
        }

        private void BuildContent(Transform parent)
        {
            CreateSectionTitle(parent, "Time");
            _dayText = CreateText("DayText", parent, 20, TextAnchor.MiddleLeft);
            _dayProgressText = CreateText("DayProgressText", parent, 20, TextAnchor.MiddleLeft);

            var dayDurationRow = CreateRow(parent);
            CreateText("DayDurationLabel", dayDurationRow, 18, TextAnchor.MiddleLeft).text = "Day Duration";
            _dayDurationValueText = CreateText("DayDurationValue", dayDurationRow, 18, TextAnchor.MiddleRight);
            _dayDurationValueText.text = "30.0";

            _dayDurationSlider = CreateSlider(parent, 1f, 30f, 30f);
            _dayDurationSlider.onValueChanged.AddListener(OnDayDurationChanged);

            var timeButtonRow = CreateRow(parent);
            CreateButton(timeButtonRow, "Advance 1 Day", () => _timeManager?.AdvanceDay(1));
            _autoProgressButton = CreateButton(timeButtonRow, "Auto: ON", ToggleAutoProgress);
            _autoProgressButtonText = _autoProgressButton.GetComponentInChildren<Text>();

            CreateSectionTitle(parent, "Tutorial");
            CreateButton(parent, "Skip All Tutorials", SkipAllTutorials);

            CreateSectionTitle(parent, "Player Move Speed");
            _moveSpeedValueText = CreateText("MoveSpeedValue", parent, 18, TextAnchor.MiddleLeft);
            _moveSpeedValueText.text = "1.00";
            _moveSpeedSlider = CreateSlider(parent, 0.25f, 4f, 1f);
            _moveSpeedSlider.onValueChanged.AddListener(OnMoveSpeedMultiplierChanged);

            CreateSectionTitle(parent, "NPC Move Speed");
            _npcMoveSpeedValueText = CreateText("NpcMoveSpeedValue", parent, 18, TextAnchor.MiddleLeft);
            _npcMoveSpeedValueText.text = $"Multiplier: {NPCMove.DebugMoveSpeedMultiplier:F2}";
            _npcMoveSpeedSlider = CreateSlider(parent, 0.25f, 4f, NPCMove.DebugMoveSpeedMultiplier);
            _npcMoveSpeedSlider.onValueChanged.AddListener(OnNpcMoveSpeedMultiplierChanged);

            CreateSectionTitle(parent, "Ship Move Speed");
            _shipMoveSpeedValueText = CreateText("ShipMoveSpeedValue", parent, 18, TextAnchor.MiddleLeft);
            _shipMoveSpeedValueText.text = "Speed: N/A";
            _shipMoveSpeedSlider = CreateSlider(parent, 0f, 30f, 5f);
            _shipMoveSpeedSlider.onValueChanged.AddListener(OnShipMoveSpeedChanged);

            CreateSectionTitle(parent, "Scene Travel");
            Transform sceneButtonRow = null;
            for (int i = 0; i < DebugSceneNames.Length; i++)
            {
                if (i % 3 == 0)
                    sceneButtonRow = CreateRow(parent);

                string sceneName = DebugSceneNames[i];
                CreateButton(sceneButtonRow, sceneName, () => RequestSceneChange(sceneName));
            }

            CreateSectionTitle(parent, "Gold");
            _goldText = CreateText("GoldText", parent, 20, TextAnchor.MiddleLeft);
            _goldInput = CreateInputField(parent, "10000");
            var goldButtonRow = CreateRow(parent);
            CreateButton(goldButtonRow, "Set", SetGold);
            CreateButton(goldButtonRow, "Add", AddGold);
            CreateButton(goldButtonRow, "Spend", SpendGold);

            CreateSectionTitle(parent, "Inventory");
            _inventoryWeightText = CreateText("InventoryWeightText", parent, 18, TextAnchor.MiddleLeft);
            _shipWeightText = CreateText("ShipWeightText", parent, 18, TextAnchor.MiddleLeft);
            _shipCapacityText = CreateText("ShipCapacityText", parent, 18, TextAnchor.MiddleLeft);

            CreateSectionTitle(parent, "Town Affinity");
            _townLoveLevelRoot = CreateVerticalGroup("TownAffinityRoot", parent, 8f);

            CreateSectionTitle(parent, "Item Control");
            _targetInventoryButton = CreateButton(parent, "Target: Player", ToggleTargetInventory);
            _targetInventoryButtonText = _targetInventoryButton.GetComponentInChildren<Text>();
            CreateText("ItemIdLabel", parent, 16, TextAnchor.MiddleLeft).text = "Item ID";
            _itemIdInput = CreateInputField(parent, "1");
            CreateText("ItemQuantityLabel", parent, 16, TextAnchor.MiddleLeft).text = "Quantity";
            _itemQuantityInput = CreateInputField(parent, "1");
            CreateText("ItemPriceLabel", parent, 16, TextAnchor.MiddleLeft).text = "Unit Price";
            _itemPriceInput = CreateInputField(parent, "0");
            _itemInfoText = CreateText("ItemInfoText", parent, 16, TextAnchor.MiddleLeft);
            var itemButtonRow = CreateRow(parent);
            CreateButton(itemButtonRow, "Add Item", AddItemToInventory);
            CreateButton(itemButtonRow, "Remove Item", RemoveItemFromInventory);

            CreateSectionTitle(parent, "Player Stats");
            _statsText = CreateText("StatsText", parent, 18, TextAnchor.UpperLeft);
            _statDeltaInput = CreateInputField(parent, "1");
            var statRow1 = CreateRow(parent);
            CreateButton(statRow1, "STR +", () => ChangeStat(PlayerStatType.Strength, true));
            CreateButton(statRow1, "STR -", () => ChangeStat(PlayerStatType.Strength, false));
            CreateButton(statRow1, "AGI +", () => ChangeStat(PlayerStatType.Agility, true));
            CreateButton(statRow1, "AGI -", () => ChangeStat(PlayerStatType.Agility, false));

            _messageText = CreateText("MessageText", parent, 16, TextAnchor.MiddleLeft);
            _messageText.text = string.Empty;
        }

        private void RefreshRuntimeTexts()
        {
            if (_timeManager != null)
            {
                _dayText.text = $"Day: {_timeManager.CurrentDay}";
                _dayProgressText.text = $"Progress: {_timeManager.DayProgress:P0}";
                _dayDurationSlider.SetValueWithoutNotify(_timeManager.DayDuration);
                _dayDurationValueText.text = _timeManager.DayDuration.ToString("F1", CultureInfo.InvariantCulture);
                if (_autoProgressButtonText != null)
                    _autoProgressButtonText.text = _timeManager.AutoProgress ? "Auto: ON" : "Auto: OFF";
            }
            else
            {
                _dayText.text = "Day: N/A";
                _dayProgressText.text = "Progress: N/A";
            }

            if (_playerController != null)
            {
                float moveMult = _playerController.DebugMoveSpeedMultiplier;
                _moveSpeedSlider.SetValueWithoutNotify(moveMult);
                _moveSpeedValueText.text = $"Multiplier: {moveMult:F2}";
            }
            else
            {
                _moveSpeedValueText.text = "Multiplier: N/A";
            }

            float npcMoveMult = NPCMove.DebugMoveSpeedMultiplier;
            _npcMoveSpeedSlider.SetValueWithoutNotify(npcMoveMult);
            _npcMoveSpeedValueText.text = $"Multiplier: {npcMoveMult:F2}";

            if (_shipController != null)
            {
                float shipSpeed = _shipController.MoveSpeed;
                _shipMoveSpeedSlider.SetValueWithoutNotify(shipSpeed);
                _shipMoveSpeedValueText.text = $"Speed: {shipSpeed:F2}";
            }
            else
            {
                _shipMoveSpeedValueText.text = "Speed: N/A";
            }

            if (_currencyManager != null)
                _goldText.text = $"Gold: {_currencyManager.GetPlayerBalance(CurrencyType.Gold)}";
            else
                _goldText.text = "Gold: N/A";

            if (_inventoryManager != null)
            {
                var playerInventory = _inventoryManager.PlayerInventory;
                var shipInventory = _inventoryManager.ShipInventory;

                _inventoryWeightText.text = playerInventory != null
                    ? $"Player Weight: {playerInventory.CurrentWeight:F1}/{playerInventory.MaxWeight:F1}"
                    : "Player Weight: N/A";

                _shipWeightText.text = shipInventory != null
                    ? $"Ship Weight: {shipInventory.CurrentWeight:F1}/{shipInventory.MaxWeight:F1}"
                    : "Ship Weight: N/A";

                _shipCapacityText.text = $"Ship Max Capacity: {_inventoryManager.ShipMaxCapacity:F1}";
            }
            else
            {
                _inventoryWeightText.text = "Player Weight: N/A";
                _shipWeightText.text = "Ship Weight: N/A";
                _shipCapacityText.text = "Ship Max Capacity: N/A";
            }

            RefreshTownLoveLevelControls();

            if (_playerStatManager != null)
            {
                _statsText.text =
                    $"STR {_playerStatManager.Strength}  AGI {_playerStatManager.Agility}\n" +
                    $"Carry {_playerStatManager.CarryCapacity:F1}  Move {_playerStatManager.MovementSpeedMultiplier:F2}";
            }
            else
            {
                _statsText.text = "Player Stats: N/A";
            }

            UpdateTargetInventoryButtonText();
            RefreshItemInfoText();
        }

        private void OnDayDurationChanged(float value)
        {
            if (_timeManager == null)
                return;

            _shouldPersistTimeSettings = true;
            _persistedDayDuration = value;
            _timeManager.SetDayDuration(value);
            _dayDurationValueText.text = value.ToString("F1", CultureInfo.InvariantCulture);
        }

        private void OnMoveSpeedMultiplierChanged(float value)
        {
            if (_playerController == null)
                return;

            _shouldPersistPlayerMoveSpeed = true;
            _persistedMoveSpeedMultiplier = value;
            _playerController.SetDebugMoveSpeedMultiplier(value);
            _moveSpeedValueText.text = $"Multiplier: {value:F2}";
        }

        private void OnNpcMoveSpeedMultiplierChanged(float value)
        {
            NPCMove.SetDebugMoveSpeedMultiplier(value);
            _npcMoveSpeedValueText.text = $"Multiplier: {NPCMove.DebugMoveSpeedMultiplier:F2}";
        }

        private void OnShipMoveSpeedChanged(float value)
        {
            if (_shipController == null)
                return;

            _shouldPersistShipMoveSpeed = true;
            _persistedShipMoveSpeed = value;
            _shipController.MoveSpeed = value;
            _shipMoveSpeedValueText.text = $"Speed: {_shipController.MoveSpeed:F2}";
        }

        private void RequestSceneChange(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                SetMessage("Invalid scene name.");
                return;
            }

            SceneChanger sceneChanger = SceneChanger.Instance;
            if (sceneChanger == null)
            {
                SetMessage("SceneChanger not ready.");
                return;
            }

            if (sceneChanger.IsTransitioning)
            {
                SetMessage("Scene transition already in progress.");
                return;
            }

            if (!sceneChanger.IsSceneInBuildSettings(sceneName))
            {
                SetMessage($"Scene not in Build Settings: {sceneName}");
                return;
            }

            SetVisible(false);
            SetMessage($"Loading scene: {sceneName}");
            sceneChanger.ChangeScene(sceneName);
        }

        private void ToggleAutoProgress()
        {
            if (_timeManager == null)
                return;

            _timeManager.SetAutoProgress(!_timeManager.AutoProgress);
            _shouldPersistTimeSettings = true;
            _persistedAutoProgress = _timeManager.AutoProgress;
            RefreshRuntimeTexts();
        }

        private void SkipAllTutorials()
        {
            if (!TutorialManager.HasInstance)
            {
                SetMessage("Tutorial manager not ready.");
                return;
            }

            if (!TutorialManager.Instance.TrySkipAllTutorials(out string failReason))
            {
                SetMessage(failReason);
                return;
            }

            SetMessage("Skipped all tutorials.");
        }

        private void SetGold()
        {
            if (_currencyManager == null || !TryParseLong(_goldInput.text, out long amount) || amount < 0)
            {
                SetMessage("Invalid gold value.");
                return;
            }

            bool ok = _currencyManager.SetPlayerBalance(CurrencyType.Gold, amount);
            SetMessage(ok ? $"Set Gold = {amount}" : "Set Gold failed");
            RefreshRuntimeTexts();
        }

        private void AddGold()
        {
            if (_currencyManager == null || !TryParseLong(_goldInput.text, out long amount) || amount < 0)
            {
                SetMessage("Invalid gold value.");
                return;
            }

            bool ok = _currencyManager.TryAddPlayer(CurrencyType.Gold, amount);
            SetMessage(ok ? $"Added Gold +{amount}" : "Add Gold failed");
            RefreshRuntimeTexts();
        }

        private void SpendGold()
        {
            if (_currencyManager == null || !TryParseLong(_goldInput.text, out long amount) || amount < 0)
            {
                SetMessage("Invalid gold value.");
                return;
            }

            bool ok = _currencyManager.TrySpendPlayer(CurrencyType.Gold, amount);
            SetMessage(ok ? $"Spent Gold -{amount}" : "Spend Gold failed");
            RefreshRuntimeTexts();
        }

        private void ToggleTargetInventory()
        {
            _targetShipInventory = !_targetShipInventory;
            UpdateTargetInventoryButtonText();
        }

        private void RefreshTownLoveLevelControls()
        {
            if (_townLoveLevelRoot == null)
                return;

            for (int i = 0; i < TownAffinityOrder.Length; i++)
            {
                string townName = TownAffinityOrder[i];
                EnsureTownLoveLevelControl(townName);
                RefreshTownLoveLevelControl(townName);
            }
        }

        private void EnsureTownLoveLevelControl(string townName)
        {
            if (_townLoveLevelRoot == null || _townLoveLevelSliders.ContainsKey(townName))
                return;

            Transform headerRow = CreateRow(_townLoveLevelRoot);
            CreateText(townName + "LoveLevelLabel", headerRow, 18, TextAnchor.MiddleLeft).text = townName;
            Text valueText = CreateText(townName + "LoveLevelValue", headerRow, 18, TextAnchor.MiddleRight);
            valueText.text = "0";

            Slider slider = CreateSlider(
                _townLoveLevelRoot,
                TownAffinityRules.MinAffinity,
                TownAffinityRules.MaxAffinity,
                TownAffinityRules.MinAffinity);
            slider.wholeNumbers = true;

            string capturedTownName = townName;
            slider.onValueChanged.AddListener(value => OnTownLoveLevelChanged(capturedTownName, value));

            _townLoveLevelSliders[townName] = slider;
            _townLoveLevelValueTexts[townName] = valueText;
        }

        private void RefreshTownLoveLevelControl(string townName)
        {
            if (!_townLoveLevelSliders.TryGetValue(townName, out Slider slider))
                return;

            int currentLoveLevel = TownAffinityRules.Clamp(GetTownLoveLevel(townName));
            slider.SetValueWithoutNotify(currentLoveLevel);

            if (_townLoveLevelValueTexts.TryGetValue(townName, out Text valueText))
                valueText.text = currentLoveLevel.ToString(CultureInfo.InvariantCulture);
        }

        private int GetTownLoveLevel(string townName)
        {
            if (_eventManager == null || string.IsNullOrWhiteSpace(townName))
                return 0;

            return _eventManager.GetStat(TownKeyUtility.ToLoveLevelKey(townName));
        }

        private void OnTownLoveLevelChanged(string townName, float value)
        {
            if (_eventManager == null || string.IsNullOrWhiteSpace(townName))
                return;

            int targetLoveLevel = TownAffinityRules.Clamp(Mathf.RoundToInt(value));
            int currentLoveLevel = GetTownLoveLevel(townName);
            int delta = targetLoveLevel - currentLoveLevel;

            _shouldPersistTownLoveLevels = true;
            _persistedTownLoveLevelValues[townName] = targetLoveLevel;

            if (delta != 0)
                _eventManager.ChangeStat(TownKeyUtility.ToLoveLevelKey(townName), delta);

            if (_townLoveLevelValueTexts.TryGetValue(townName, out Text valueText))
                valueText.text = targetLoveLevel.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateTargetInventoryButtonText()
        {
            if (_targetInventoryButtonText != null)
                _targetInventoryButtonText.text = _targetShipInventory ? "Target: Ship" : "Target: Player";
        }

        private void RefreshItemInfoText()
        {
            if (_itemInfoText == null)
                return;

            if (!TryParseInt(_itemIdInput.text, out int itemId) || itemId <= 0)
            {
                _itemInfoText.text = "Item: invalid ID";
                return;
            }

            ItemData item = DataManager.Instance?.GetItem(itemId);
            if (item == null)
            {
                _itemInfoText.text = "Item: not found";
                return;
            }

            _itemInfoText.text = $"Item ID={item.ID}, NameKey={item.Name}, Weight={item.Weight:F1}";
        }

        private void AddItemToInventory()
        {
            if (_inventoryManager == null)
            {
                SetMessage("Inventory manager not ready.");
                return;
            }

            if (!TryParseInt(_itemIdInput.text, out int itemId) || itemId <= 0
                || !TryParseInt(_itemQuantityInput.text, out int quantity) || quantity <= 0
                || !TryParseInt(_itemPriceInput.text, out int purchasePrice) || purchasePrice < 0)
            {
                SetMessage("Invalid item input.");
                return;
            }

            InventoryData target = _targetShipInventory ? _inventoryManager.ShipInventory : _inventoryManager.PlayerInventory;
            if (target == null)
            {
                SetMessage("Target inventory not initialized.");
                return;
            }

            bool ok = target.AddItem(itemId, quantity, purchasePrice);
            SetMessage(ok
                ? $"Added item {itemId} x{quantity} to {(_targetShipInventory ? "Ship" : "Player")}"
                : "Add item failed");

            RefreshRuntimeTexts();
        }

        private void RemoveItemFromInventory()
        {
            if (_inventoryManager == null)
            {
                SetMessage("Inventory manager not ready.");
                return;
            }

            if (!TryParseInt(_itemIdInput.text, out int itemId) || itemId <= 0
                || !TryParseInt(_itemQuantityInput.text, out int quantity) || quantity <= 0)
            {
                SetMessage("Invalid item input.");
                return;
            }

            InventoryData target = _targetShipInventory ? _inventoryManager.ShipInventory : _inventoryManager.PlayerInventory;
            if (target == null)
            {
                SetMessage("Target inventory not initialized.");
                return;
            }

            bool ok = target.RemoveItem(itemId, quantity);
            SetMessage(ok
                ? $"Removed item {itemId} x{quantity} from {(_targetShipInventory ? "Ship" : "Player")}"
                : "Remove item failed");

            RefreshRuntimeTexts();
        }

        private void ChangeStat(PlayerStatType statType, bool increase)
        {
            if (_playerStatManager == null)
            {
                SetMessage("Player status not ready.");
                return;
            }

            if (!TryParseInt(_statDeltaInput.text, out int delta) || delta <= 0)
            {
                SetMessage("Invalid stat delta.");
                return;
            }

            int signedDelta = increase ? delta : -delta;
            _playerStatManager.ChangeStat(statType, signedDelta);
            SetMessage($"{(increase ? "Increased" : "Decreased")} {statType} by {delta}");

            RefreshRuntimeTexts();
        }

        private void SetMessage(string message)
        {
            if (_messageText != null)
                _messageText.text = message;
        }

        private static bool TryParseInt(string input, out int parsed)
        {
            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return true;

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed))
                return true;

            parsed = 0;
            return false;
        }

        private static bool TryParseLong(string input, out long parsed)
        {
            if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return true;

            if (long.TryParse(input, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed))
                return true;

            parsed = 0;
            return false;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            return go;
        }

        private static Transform CreateRow(Transform parent)
        {
            var row = CreateUIObject("Row", parent);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return row.transform;
        }

        private static Transform CreateVerticalGroup(string name, Transform parent, float spacing)
        {
            var group = CreateUIObject(name, parent);
            var layout = group.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = group.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            return group.transform;
        }

        private static void CreateSectionTitle(Transform parent, string text)
        {
            var label = CreateText("SectionTitle", parent, 22, TextAnchor.MiddleLeft);
            label.text = $"== {text} ==";
            label.color = new Color(0.95f, 0.9f, 0.5f, 1f);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 34f;
        }

        private static Text CreateText(string name, Transform parent, int fontSize, TextAnchor anchor)
        {
            var go = CreateUIObject(name, parent);
            var text = go.AddComponent<Text>();
            text.font = _defaultFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = Mathf.Max(26f, fontSize + 10f);

            return text;
        }

        private static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction callback)
        {
            var go = CreateUIObject("Button_" + label, parent);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(callback);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 36f;
            le.preferredHeight = 36f;
            le.preferredWidth = 120f;

            var text = CreateText("Text", go.transform, 17, TextAnchor.MiddleCenter);
            text.text = label;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            return button;
        }

        private static InputField CreateInputField(Transform parent, string defaultValue)
        {
            var go = CreateUIObject("InputField", parent);
            var image = go.AddComponent<Image>();
            image.color = Color.white;

            var input = go.AddComponent<InputField>();

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 34f;
            le.preferredHeight = 34f;

            var textGo = CreateUIObject("Text", go.transform);
            var text = textGo.AddComponent<Text>();
            text.font = _defaultFont;
            text.fontSize = 18;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;

            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 6f);
            textRect.offsetMax = new Vector2(-8f, -6f);

            var placeholderGo = CreateUIObject("Placeholder", go.transform);
            var placeholder = placeholderGo.AddComponent<Text>();
            placeholder.font = _defaultFont;
            placeholder.fontSize = 18;
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 0.75f);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.text = "input";

            RectTransform placeholderRect = placeholder.rectTransform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(8f, 6f);
            placeholderRect.offsetMax = new Vector2(-8f, -6f);

            input.textComponent = text;
            input.placeholder = placeholder;
            input.text = defaultValue;

            return input;
        }

        private static Slider CreateSlider(Transform parent, float min, float max, float value)
        {
            var root = CreateUIObject("Slider", parent);
            var slider = root.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            var le = root.AddComponent<LayoutElement>();
            le.minHeight = 24f;
            le.preferredHeight = 24f;

            var background = CreateUIObject("Background", root.transform);
            var bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            RectTransform bgRect = background.GetComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0f, 0.25f);
            bgRect.anchorMax = new Vector2(1f, 0.75f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var fillArea = CreateUIObject("Fill Area", root.transform);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = new Vector2(8f, 0f);
            fillAreaRect.offsetMax = new Vector2(-8f, 0f);

            var fill = CreateUIObject("Fill", fillArea.transform);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.8f, 0.3f, 1f);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var handleArea = CreateUIObject("Handle Slide Area", root.transform);
            RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(8f, 0f);
            handleAreaRect.offsetMax = new Vector2(-8f, 0f);

            var handle = CreateUIObject("Handle", handleArea.transform);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.offsetMin = new Vector2(-4f, 3f);
            handleRect.offsetMax = new Vector2(4f, -3f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;

            return slider;
        }
    }

    public class UGUIPanelDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private RectTransform _target;
        private Canvas _canvas;

        public void Initialize(RectTransform target, Canvas canvas)
        {
            _target = target;
            _canvas = canvas;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_target == null)
                return;

            float scaleFactor = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            _target.anchoredPosition += eventData.delta / scaleFactor;
        }
    }

    public class UGUIPanelFocusHandler : MonoBehaviour, IPointerDownHandler
    {
        private GameObject _selectionTarget;

        public void Initialize(GameObject selectionTarget)
        {
            _selectionTarget = selectionTarget;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectionTarget == null || EventSystem.current == null)
                return;

            EventSystem.current.SetSelectedGameObject(_selectionTarget);
        }
    }
}
#endif
