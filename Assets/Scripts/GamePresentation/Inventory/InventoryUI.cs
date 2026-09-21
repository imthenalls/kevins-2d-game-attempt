using Game.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Main inventory panel controller. Owns the InventoryModel and drives the slot grid UI.
/// Handles drag-and-drop reordering, the I-key / gamepad toggle, and player movement lock
/// while the inventory is open. Exposes InventoryUI.Model for global read/write access.
/// Automatically creates the player keyring and its inventory-side viewer.
///
/// Unity setup (recommended Canvas hierarchy):
///   Canvas (Screen Space \u2013 Overlay)
///     InventoryUI (this component + DontDestroyOnLoad)
///       Panel Root        \u2014 assign to panelRoot
///       Grid Container    \u2014 assign to gridContainer; GridLayoutGroup added automatically
///       Drag Ghost Image  \u2014 assign to dragGhostImage (Image, raycastTarget = false)
///       Sort Button       \u2014 assign to sortButton
///       Close Button      \u2014 assign to closeButton
///       InventoryTooltip  \u2014 sibling component on its own child GameObject
///       InventoryContextMenu \u2014 sibling component on its own child GameObject
///
///   Inspector fields:
///     Slot Prefab       \u2014 prefab with InventorySlotUI + background/icon Images + TMP quantity text
///     Rows / Columns    \u2014 grid dimensions (default 5 \u00d7 6)
///     Slot Size / Spacing \u2014 cell pixel size and gap fed into GridLayoutGroup
///     Player Controller \u2014 optional; found automatically if left blank
///     Legacy Toggle Key \u2014 fallback key when the new Input System is disabled (default I)
/// </summary>
[DisallowMultipleComponent]
public class InventoryUI : MonoBehaviour
{
    private static InventoryUI instance;

    /// <summary>Global access to the single InventoryUI instance.</summary>
    public static InventoryUI Instance => instance;

    /// <summary>Global access to the inventory data model.</summary>
    public static InventoryModel Model => instance != null ? instance.model : null;

    /// <summary>Raised after world travel changes which inventory model the UI exposes.</summary>
    public static event Action<InventoryModel> OnModelChanged;

    [Header("Grid Layout")]
    [SerializeField] private int rows    = 5;
    [SerializeField] private int columns = 6;

    [Header("References")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private RectTransform gridContainer;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private Image dragGhostImage;
    [SerializeField] private Button sortButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private PlayerControllerBase playerController;

    [Header("Slot Size")]
    [SerializeField] private Vector2 slotSize    = new Vector2(64f, 64f);
    [SerializeField] private Vector2 slotSpacing = new Vector2(4f,  4f);

#if !ENABLE_INPUT_SYSTEM
    [Header("Legacy Input Fallback")]
    [SerializeField] private KeyCode legacyToggleKey = KeyCode.I;
#endif

    private InventoryModel model;
    private InventoryModel worldAModel;
    private InventoryModel worldBModel;
    private InventorySlotUI[] slotUIs;
    private int dragFromIndex = -1;
    private int selectedSlotIndex = -1;
    private int inventoryOpenedFrame = -1;

    /// <summary>
    /// The inventory slot index currently being dragged, or -1 when no drag is active.
    /// Read by HotbarSlotUI.OnDrop to identify which item is being assigned.
    /// </summary>
    public int DragFromIndex => dragFromIndex;

    /// <summary>Root inventory panel used to position the companion equipment panel.</summary>
    public RectTransform PanelRoot => panelRoot;

    private Canvas parentCanvas;
    private RectTransform canvasRect;

    public bool IsOpen => panelRoot != null && panelRoot.gameObject.activeSelf;

    /// <summary>
    /// When true, player input cannot open or toggle the inventory.
    /// Set by SceneRulesManager for scenes that disable inventory access.
    /// Programmatic calls to Open() / Close() still work.
    /// </summary>
    public bool InputLocked { get; set; }

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            canvasRect = parentCanvas.GetComponent<RectTransform>();

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerControllerBase>();

        worldAModel = new InventoryModel(rows, columns, ItemScope.WorldA);
        worldBModel = new InventoryModel(rows, columns, ItemScope.WorldB);
        model = GetModelForWorld(CurrentWorld);
        model.OnChanged += RefreshAllSlots;
        if (WorldTravelState.Instance != null)
            WorldTravelState.Instance.OnWorldChanged += HandleWorldChanged;
        PlayerKeyring.GetOrCreate(gameObject);

        SetupGridLayout();
        BuildGrid();
        EquipmentUI.GetOrCreate(panelRoot);
        KeyringUI.GetOrCreate(panelRoot);
        SetPanelVisible(false);

        if (dragGhostImage != null)
        {
            dragGhostImage.raycastTarget = false;
            dragGhostImage.gameObject.SetActive(false);
        }

        if (sortButton  != null) sortButton.onClick.AddListener(() => model.Sort());
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    private void Update()
    {
        if (!InputLocked && WasTogglePressedThisFrame())
            Toggle();

        if (!IsOpen) return;

        if (WasEscapePressedThisFrame())
        {
            // Loot, equipment, context, tooltip, and split UI all belong to the
            // inventory menu stack. Close the stack together on Escape.
            if (LootContainerUI.IsOpen)
                LootContainerUI.Hide();
            Close();
            return;
        }

        HandleKeyboardSelection();

        // Do not reuse the key press that caused another system to open the inventory.
        if (Time.frameCount > inventoryOpenedFrame && WasEquipPressedThisFrame())
            TryEquipSelectedItem();

        if (dragFromIndex >= 0)
            UpdateDragGhostPosition();
    }

    private void OnDisable()
    {
        SetPlayerMovementLocked(false);
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        if (model != null)
            model.OnChanged -= RefreshAllSlots;
        if (WorldTravelState.Instance != null)
            WorldTravelState.Instance.OnWorldChanged -= HandleWorldChanged;
        instance = null;
    }

    public InventoryModel GetInventoryForWorld(WorldLayer world) => GetModelForWorld(world);

    private WorldLayer CurrentWorld => WorldTravelState.Instance != null
        ? WorldTravelState.Instance.CurrentWorld
        : WorldLayer.WorldA;

    private InventoryModel GetModelForWorld(WorldLayer world) =>
        world == WorldLayer.WorldB ? worldBModel : worldAModel;

    private void HandleWorldChanged(WorldLayer world)
    {
        InventoryModel nextModel = GetModelForWorld(world);
        if (nextModel == null || nextModel == model) return;

        MoveSharedItems(model, nextModel);
        model.OnChanged -= RefreshAllSlots;
        model = nextModel;
        model.OnChanged += RefreshAllSlots;
        playerController = FindAnyObjectByType<PlayerControllerBase>();
        SetPlayerMovementLocked(IsOpen);

        selectedSlotIndex = -1;
        RebindSlotViews();
        RefreshAllSlots();
        OnModelChanged?.Invoke(model);
    }

    private static void MoveSharedItems(InventoryModel source, InventoryModel destination)
    {
        if (source == null || destination == null) return;

        var sharedItems = new List<ItemData>();
        for (int i = 0; i < source.SlotCount; i++)
        {
            InventorySlot slot = source.GetSlot(i);
            if (!slot.IsEmpty && slot.item.Scope == ItemScope.Shared &&
                !sharedItems.Contains(slot.item.AsItemData()))
                sharedItems.Add(slot.item.AsItemData());
        }

        bool movedAny = false;
        for (int i = 0; i < sharedItems.Count; i++)
        {
            ItemData item = sharedItems[i];
            int quantity = source.CountItem(item);
            if (source.TryTransferItemTo(destination, item, quantity, out _, out _))
            {
                movedAny = true;
            }
            else
            {
                Debug.LogWarning(
                    $"[InventoryUI] Shared item '{item.itemId}' could not move because the " +
                    "destination world's inventory is full.");
            }
        }

        if (movedAny)
        {
            source.NotifyChanged();
            destination.NotifyChanged();
        }
    }

    private void RebindSlotViews()
    {
        if (slotUIs == null || model == null) return;
        for (int i = 0; i < slotUIs.Length && i < model.SlotCount; i++)
            slotUIs[i].Setup(i, model.GetSlot(i));
    }

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private void SetupGridLayout()
    {
        if (gridContainer == null) return;

        var layout = gridContainer.GetComponent<GridLayoutGroup>();
        if (layout == null)
            layout = gridContainer.gameObject.AddComponent<GridLayoutGroup>();

        layout.cellSize        = slotSize;
        layout.spacing         = slotSpacing;
        layout.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = columns;
        layout.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        layout.startAxis       = GridLayoutGroup.Axis.Horizontal;
    }

    private void BuildGrid()
    {
        if (slotPrefab == null)
        {
            Debug.LogWarning("[InventoryUI] slotPrefab is not assigned — grid will be empty.");
            return;
        }

        slotUIs = new InventorySlotUI[model.SlotCount];

        for (int i = 0; i < model.SlotCount; i++)
        {
            var slotGo = Instantiate(slotPrefab, gridContainer);
            slotGo.Setup(i, model.GetSlot(i));
            slotGo.DragStarted   += OnSlotDragStarted;
            slotGo.DragEnded     += OnSlotDragEnded;
            slotGo.Dropped       += OnSlotDropped;
            slotGo.RightClicked  += OnSlotRightClicked;
            slotGo.ShiftClicked  += OnSlotShiftClicked;
            slotGo.LeftClicked   += SelectSlot;
            slotUIs[i] = slotGo;
        }
    }

    // -------------------------------------------------------------------------
    // Drag and drop
    // -------------------------------------------------------------------------

    private void OnSlotDragStarted(int fromIndex)
    {
        dragFromIndex = fromIndex;
        InventoryContextMenu.Hide();
        InventoryTooltip.Hide();

        if (dragGhostImage == null) return;
        var slot = model.GetSlot(fromIndex);
        dragGhostImage.sprite = slot.item != null ? slot.item.IconOf() : null;
        dragGhostImage.gameObject.SetActive(slot.item != null);
        dragGhostImage.transform.SetAsLastSibling();
    }

    private void OnSlotDropped(int toIndex)
    {
        if (dragFromIndex >= 0 && dragFromIndex != toIndex)
            model.MoveSlot(dragFromIndex, toIndex);

        // DragEnded fires after this and will clean up
    }

    private void OnSlotDragEnded(int _)
    {
        dragFromIndex = -1;
        if (dragGhostImage != null)
            dragGhostImage.gameObject.SetActive(false);
    }

    private void UpdateDragGhostPosition()
    {
        if (dragGhostImage == null || canvasRect == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            GetMousePosition(),
            parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? Camera.main
                : null,
            out Vector2 localPoint);

        ((RectTransform)dragGhostImage.transform).anchoredPosition = localPoint;
    }

    // -------------------------------------------------------------------------
    // Right-click context menu
    // -------------------------------------------------------------------------

    private void OnSlotRightClicked(int slotIndex, Vector2 screenPos)
    {
        InventoryContextMenu.Show(model, slotIndex, screenPos);
    }

    private void OnSlotShiftClicked(int slotIndex, Vector2 screenPos)
    {
        InventoryContextMenu.Hide();
        InventorySplitDialog.Show(model, slotIndex, screenPos);
    }

    // -------------------------------------------------------------------------
    // Refresh
    // -------------------------------------------------------------------------

    private void RefreshAllSlots()
    {
        if (slotUIs == null) return;
        for (int i = 0; i < slotUIs.Length; i++)
        {
            slotUIs[i].Refresh();
            slotUIs[i].SetSelected(i == selectedSlotIndex);
        }
    }

    // -------------------------------------------------------------------------
    // Open / close
    // -------------------------------------------------------------------------

    public void Toggle() { if (!InputLocked) SetPanelVisible(!IsOpen); }
    public void Open()   => SetPanelVisible(true);
    public void Close()  => SetPanelVisible(false);

    private void SetPanelVisible(bool visible)
    {
        bool wasVisible = IsOpen;

        if (panelRoot != null)
            panelRoot.gameObject.SetActive(visible);

        EquipmentUI.Instance?.SetVisible(visible);
        KeyringUI.Instance?.SetInventoryVisible(visible);

        if (visible)
        {
            if (!wasVisible)
                inventoryOpenedFrame = Time.frameCount;
            EnsureSelection();
        }

        if (!visible)
        {
            dragFromIndex = -1;
            if (dragGhostImage != null) dragGhostImage.gameObject.SetActive(false);
            InventoryContextMenu.Hide();
            InventoryTooltip.Hide();
            InventorySplitDialog.Hide();
        }

        SetPlayerMovementLocked(visible);
    }

    private void SetPlayerMovementLocked(bool locked)
    {
        if (playerController == null || !playerController.gameObject.scene.IsValid())
            playerController = FindAnyObjectByType<PlayerControllerBase>();
        if (playerController != null)
            playerController.SetMovementEnabled(!locked);
    }

    // -------------------------------------------------------------------------
    // Input helpers
    // -------------------------------------------------------------------------

    private void HandleKeyboardSelection()
    {
        if (WasLeftPressedThisFrame())       MoveSelection(-1, 0);
        else if (WasRightPressedThisFrame()) MoveSelection(1, 0);
        else if (WasUpPressedThisFrame())    MoveSelection(0, -1);
        else if (WasDownPressedThisFrame())  MoveSelection(0, 1);
    }

    private void EnsureSelection()
    {
        if (selectedSlotIndex >= 0 && selectedSlotIndex < model.SlotCount)
        {
            SelectSlot(selectedSlotIndex);
            return;
        }

        int firstOccupied = -1;
        for (int i = 0; i < model.SlotCount; i++)
        {
            if (!model.GetSlot(i).IsEmpty)
            {
                firstOccupied = i;
                break;
            }
        }

        SelectSlot(firstOccupied >= 0 ? firstOccupied : 0);
    }

    private void MoveSelection(int columnDelta, int rowDelta)
    {
        EnsureSelection();
        if (selectedSlotIndex < 0 || columns <= 0) return;

        int rowCount = Mathf.CeilToInt(model.SlotCount / (float)columns);
        int currentRow = selectedSlotIndex / columns;
        int currentColumn = selectedSlotIndex % columns;
        int nextRow = Mathf.Clamp(currentRow + rowDelta, 0, rowCount - 1);
        int nextColumn = Mathf.Clamp(currentColumn + columnDelta, 0, columns - 1);
        int nextIndex = Mathf.Min(nextRow * columns + nextColumn, model.SlotCount - 1);
        SelectSlot(nextIndex);
    }

    private void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= model.SlotCount) return;

        selectedSlotIndex = slotIndex;
        InventoryContextMenu.Hide();
        InventoryTooltip.Hide();

        if (slotUIs == null) return;
        for (int i = 0; i < slotUIs.Length; i++)
            slotUIs[i].SetSelected(i == selectedSlotIndex);
    }

    private void TryEquipSelectedItem()
    {
        if (selectedSlotIndex < 0 || selectedSlotIndex >= model.SlotCount) return;

        InventorySlot slot = model.GetSlot(selectedSlotIndex);
        if (slot == null || slot.IsEmpty || !slot.item.IsEquip) return;

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerControllerBase>();

        EquipmentManager equipment = playerController != null
            ? playerController.GetComponent<EquipmentManager>()
            : null;
        if (equipment == null)
        {
            Debug.LogWarning("[InventoryUI] Player has no EquipmentManager; selected item was not equipped.", this);
            return;
        }

        ItemData item = slot.item.AsItemData();
        if (!equipment.TryEquipFromInventory(model, item, out ItemData displaced))
        {
            Debug.LogWarning($"[InventoryUI] Could not equip '{item.itemName}'.", this);
            return;
        }

        QuestEventBus.Raise("ItemEquipped", item.itemId, 1);
        if (displaced != null)
            QuestEventBus.Raise("ItemUnequipped", displaced.itemId, 1);
    }

    private bool WasTogglePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(legacyToggleKey);
#endif
    }

    private bool WasEquipPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

    private bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private bool WasLeftPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.LeftArrow);
#endif
    }

    private bool WasRightPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.rightArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.RightArrow);
#endif
    }

    private bool WasUpPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.upArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.UpArrow);
#endif
    }

    private bool WasDownPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.downArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.DownArrow);
#endif
    }

    private Vector2 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }
}
