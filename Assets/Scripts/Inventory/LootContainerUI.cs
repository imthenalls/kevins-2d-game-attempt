using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Loot / container panel. Shows items from any InventoryModel (chest, enemy corpse, etc.)
/// as a grid of LootSlotUI cells. Click a slot to take that stack; Take All grabs everything.
/// Opening the panel automatically opens the player's inventory alongside it.
/// Pressing Escape or clicking Close hides the panel (inventory stays open).
///
/// Unity setup:
///   1. Create a child GameObject inside the same Canvas as InventoryUI.
///   2. Add a Panel hierarchy:
///        Panel Root         ← assign to panel
///          Title Text       ← assign to titleText  (TextMeshProUGUI)
///          Grid Container   ← assign to gridContainer (needs no layout component — added automatically)
///          Take All Button  ← assign to takeAllButton
///          Close Button     ← assign to closeButton
///   3. Assign the LootSlotUI prefab to slotPrefab.
///   4. Set columns, slotSize, slotSpacing to match your InventoryUI look.
///   Only one instance should exist in the scene — uses a static singleton pattern.
/// </summary>
public class LootContainerUI : MonoBehaviour
{
    private static LootContainerUI instance;

    [SerializeField] private RectTransform    panel;
    [SerializeField] private RectTransform    gridContainer;
    [SerializeField] private LootSlotUI       slotPrefab;
    [SerializeField] private TextMeshProUGUI  titleText;
    [SerializeField] private Button           takeAllButton;
    [SerializeField] private Button           closeButton;

    [Header("Grid Layout")]
    [SerializeField, Min(1)] private int     columns     = 4;
    [SerializeField]         private Vector2 slotSize    = new Vector2(64f, 64f);
    [SerializeField]         private Vector2 slotSpacing = new Vector2(4f, 4f);

    private InventoryModel sourceModel;
    private LootSlotUI[]   slotUIs;

    // ── Static API ────────────────────────────────────────────────────────────

    /// <summary>True while the loot panel is visible.</summary>
    public static bool IsOpen =>
        instance != null && instance.panel != null && instance.panel.gameObject.activeSelf;

    /// <summary>
    /// Opens the loot panel and shows all items from <paramref name="source"/>.
    /// Also opens the player's inventory panel so both are visible side by side.
    /// </summary>
    public static void Show(InventoryModel source, string containerName)
    {
        if (instance == null || source == null) return;

        instance.Bind(source, containerName);
        instance.panel.gameObject.SetActive(true);
        instance.panel.SetAsLastSibling();

        // Open the player inventory alongside the loot panel
        InventoryUI.Instance?.Open();
    }

    /// <summary>Hides the loot panel and cleans up subscriptions.</summary>
    public static void Hide()
    {
        if (instance == null || instance.panel == null) return;
        instance.Unbind();
        instance.panel.gameObject.SetActive(false);
        InventoryTooltip.Hide();
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        instance = this;
        SetupGridLayout();

        if (panel != null) panel.gameObject.SetActive(false);
        if (takeAllButton != null) takeAllButton.onClick.AddListener(OnTakeAll);
        if (closeButton   != null) closeButton.onClick.AddListener(Hide);
    }

    private void Update()
    {
        if (IsOpen && WasEscapePressed())
            Hide();
    }

    // ── Grid management ───────────────────────────────────────────────────────

    private void Bind(InventoryModel source, string containerName)
    {
        Unbind();

        sourceModel = source;
        sourceModel.OnChanged += RefreshSlots;

        if (titleText != null)
            titleText.text = containerName;

        RebuildGrid();
    }

    private void Unbind()
    {
        if (sourceModel != null)
        {
            sourceModel.OnChanged -= RefreshSlots;
            sourceModel = null;
        }

        if (gridContainer != null)
        {
            foreach (Transform child in gridContainer)
                Destroy(child.gameObject);
        }

        slotUIs = null;
    }

    private void RebuildGrid()
    {
        if (slotPrefab == null || sourceModel == null) return;

        slotUIs = new LootSlotUI[sourceModel.SlotCount];
        for (int i = 0; i < sourceModel.SlotCount; i++)
        {
            var ui = Instantiate(slotPrefab, gridContainer);
            ui.Setup(i, sourceModel.GetSlot(i));
            ui.Clicked += OnSlotClicked;
            slotUIs[i] = ui;
        }
    }

    private void RefreshSlots()
    {
        if (slotUIs == null) return;
        foreach (var ui in slotUIs)
            ui.Refresh();
    }

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

    // ── Slot interaction ──────────────────────────────────────────────────────

    /// <summary>Takes the entire stack from one slot and gives it to the player's inventory.</summary>
    private void OnSlotClicked(int slotIndex)
    {
        if (sourceModel == null || InventoryUI.Model == null) return;

        var slot = sourceModel.GetSlot(slotIndex);
        if (slot.IsEmpty) return;

        var item = slot.item;
        int qty  = slot.quantity;

        sourceModel.RemoveItem(item, qty);
        int leftover = InventoryUI.Model.AddItem(item, qty);

        if (leftover > 0)
            sourceModel.AddItem(item, leftover); // put overflow back into the container

        int taken = qty - leftover;
        if (taken > 0)
            QuestEventBus.Raise("ItemCollected", item.itemId, taken);
    }

    /// <summary>Transfers all items from the container into the player's inventory.</summary>
    private void OnTakeAll()
    {
        if (sourceModel == null || InventoryUI.Model == null) return;

        for (int i = 0; i < sourceModel.SlotCount; i++)
        {
            var slot = sourceModel.GetSlot(i);
            if (slot.IsEmpty) continue;

            var item     = slot.item;
            int qty      = slot.quantity;
            int leftover = InventoryUI.Model.AddItem(item, qty);
            int taken    = qty - leftover;

            if (taken > 0)
            {
                sourceModel.RemoveItem(item, taken);
                QuestEventBus.Raise("ItemCollected", item.itemId, taken);
            }

            if (leftover > 0) break; // inventory full — stop trying
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private static bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
