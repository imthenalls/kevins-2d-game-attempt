using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Main inventory panel controller. Owns the InventoryModel and drives the slot grid UI.
/// Handles drag-and-drop reordering, the I-key / gamepad toggle, and player movement lock
/// while the inventory is open. Exposes InventoryUI.Model for global read/write access.
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
    [SerializeField] private PlayerController2D playerController;

    [Header("Slot Size")]
    [SerializeField] private Vector2 slotSize    = new Vector2(64f, 64f);
    [SerializeField] private Vector2 slotSpacing = new Vector2(4f,  4f);

    [Header("Legacy Input Fallback")]
    [SerializeField] private KeyCode legacyToggleKey = KeyCode.I;

    private InventoryModel model;
    private InventorySlotUI[] slotUIs;
    private int dragFromIndex = -1;

    /// <summary>
    /// The inventory slot index currently being dragged, or -1 when no drag is active.
    /// Read by HotbarSlotUI.OnDrop to identify which item is being assigned.
    /// </summary>
    public int DragFromIndex => dragFromIndex;

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
            playerController = FindFirstObjectByType<PlayerController2D>();

        model = new InventoryModel(rows, columns);
        model.OnChanged += RefreshAllSlots;

        SetupGridLayout();
        BuildGrid();
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

        if (IsOpen && dragFromIndex >= 0)
            UpdateDragGhostPosition();
    }

    private void OnDisable()
    {
        SetPlayerMovementLocked(false);
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
        dragGhostImage.sprite = slot.item != null ? slot.item.icon : null;
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
            slotUIs[i].Refresh();
    }

    // -------------------------------------------------------------------------
    // Open / close
    // -------------------------------------------------------------------------

    public void Toggle() { if (!InputLocked) SetPanelVisible(!IsOpen); }
    public void Open()   => SetPanelVisible(true);
    public void Close()  => SetPanelVisible(false);

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null)
            panelRoot.gameObject.SetActive(visible);

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
        if (playerController != null)
            playerController.SetMovementEnabled(!locked);
    }

    // -------------------------------------------------------------------------
    // Input helpers
    // -------------------------------------------------------------------------

    private bool WasTogglePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(legacyToggleKey);
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
