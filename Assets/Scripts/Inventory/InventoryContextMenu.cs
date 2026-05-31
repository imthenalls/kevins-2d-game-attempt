using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Right-click context menu shown when the player right-clicks an occupied inventory slot.
/// Dynamically shows or hides Use / Drop / Split / Inspect buttons based on the item's
/// type and flags (e.g. Use only for Consumables; Drop hidden for Quest/Key items).
///
/// Unity setup:
///   1. Place one instance on a GameObject inside the same Canvas as InventoryUI.
///   2. Assign panel (RectTransform of the popup), and each Button reference
///      (useButton, dropButton, splitButton, inspectButton, closeButton).
///   3. The panel starts hidden; InventorySlotUI calls Show() automatically on right-click.
///   Only one instance should exist in the scene — uses a static singleton pattern.
/// </summary> : MonoBehaviour
{
    private static InventoryContextMenu instance;

    [SerializeField] private RectTransform panel;
    [SerializeField] private Button useButton;
    [SerializeField] private Button dropButton;
    [SerializeField] private Button splitButton;
    [SerializeField] private Button inspectButton;
    [SerializeField] private Button closeButton;

    private InventoryModel model;
    private int targetSlotIndex;
    private Canvas parentCanvas;
    private RectTransform canvasRect;

    private void Awake()
    {
        instance = this;
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            canvasRect = parentCanvas.GetComponent<RectTransform>();

        if (panel != null)
            panel.gameObject.SetActive(false);

        if (useButton     != null) useButton.onClick.AddListener(OnUse);
        if (dropButton    != null) dropButton.onClick.AddListener(OnDrop);
        if (splitButton   != null) splitButton.onClick.AddListener(OnSplit);
        if (inspectButton != null) inspectButton.onClick.AddListener(OnInspect);
        if (closeButton   != null) closeButton.onClick.AddListener(Hide);
    }

    public static void Show(InventoryModel inventoryModel, int slotIndex, Vector2 screenPosition)
    {
        if (instance == null) return;

        instance.model            = inventoryModel;
        instance.targetSlotIndex  = slotIndex;

        var slot = inventoryModel.GetSlot(slotIndex);
        if (slot.IsEmpty) return;

        // Configure visible buttons based on item properties
        if (instance.useButton != null)
            instance.useButton.gameObject.SetActive(slot.item.type == ItemType.Consumable);

        if (instance.splitButton != null)
            instance.splitButton.gameObject.SetActive(slot.item.IsStackable && slot.quantity > 1);

        bool canDrop = (slot.item.flags & (ItemFlags.QuestItem | ItemFlags.KeyItem)) == 0;
        if (instance.dropButton != null)
            instance.dropButton.gameObject.SetActive(canDrop);

        // Position near the click
        if (instance.canvasRect != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                instance.canvasRect,
                screenPosition,
                instance.parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
                out Vector2 localPoint);
            instance.panel.anchoredPosition = localPoint;
        }

        instance.panel.gameObject.SetActive(true);
        instance.panel.SetAsLastSibling();
    }

    public static void Hide()
    {
        if (instance != null && instance.panel != null)
            instance.panel.gameObject.SetActive(false);
    }

    private void OnUse()
    {
        // TODO: forward to an item-use system when one exists
        Hide();
    }

    private void OnDrop()
    {
        if (model == null) return;
        var slot = model.GetSlot(targetSlotIndex);
        if (!slot.IsEmpty)
            model.RemoveItem(slot.item, slot.quantity);
        Hide();
    }

    private void OnSplit()
    {
        model?.SplitStack(targetSlotIndex);
        Hide();
    }

    private void OnInspect()
    {
        // TODO: open an item detail panel when one exists
        Hide();
    }
}
