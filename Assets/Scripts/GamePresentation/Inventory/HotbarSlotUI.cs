using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Visual cell for one hotbar slot. Shows the assigned item icon, the live inventory
/// count, and the 1-6 hotkey label. Accepts drag-drops from InventorySlotUI to assign
/// items; right-click clears the slot.
///
/// Unity setup (as a prefab):
///   1. Create a prefab with this component.
///   2. Assign backgroundImage, iconImage, quantityText, and hotkeyLabel in the Inspector.
///   3. The parent Canvas must have a GraphicRaycaster for drop-target detection to work.
///   4. Assign the prefab to HotbarUI.slotPrefab — HotbarUI instantiates these at runtime.
/// </summary>
public class HotbarSlotUI : MonoBehaviour,
    IDropHandler,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Image           backgroundImage;
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private TextMeshProUGUI hotkeyLabel;

    [Header("Colors")]
    [SerializeField] private Color normalColor  = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    [SerializeField] private Color hoveredColor = new Color(0.30f, 0.30f, 0.30f, 0.90f);

    private ItemData assignedItem;

    public int SlotIndex { get; private set; }

    /// <summary>Fires when the player right-clicks to clear this slot.</summary>
    public event Action<int> ClearRequested;

    // ── Setup ────────────────────────────────────────────────────────────────

    public void Setup(int index)
    {
        SlotIndex = index;

        if (hotkeyLabel != null)
            hotkeyLabel.text = (index + 1).ToString();

        Refresh(null);
    }

    /// <summary>Called by HotbarUI whenever the model or inventory changes.</summary>
    public void Refresh(ItemData item)
    {
        assignedItem = item;
        bool hasItem = item != null;

        if (iconImage != null)
        {
            iconImage.sprite  = hasItem ? item.icon : null;
            iconImage.enabled = hasItem;
        }

        int qty = hasItem && InventoryUI.Model != null ? InventoryUI.Model.CountItem(item) : 0;

        if (quantityText != null)
        {
            bool showQty = hasItem && qty > 0;
            quantityText.text    = showQty ? qty.ToString() : string.Empty;
            quantityText.enabled = showQty;
        }

        if (backgroundImage != null)
            backgroundImage.color = normalColor;
    }

    // ── Pointer events ────────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (backgroundImage != null) backgroundImage.color = hoveredColor;
        if (assignedItem != null) InventoryTooltip.Show(assignedItem);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (backgroundImage != null) backgroundImage.color = normalColor;
        InventoryTooltip.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            ClearRequested?.Invoke(SlotIndex);
    }

    // ── Drop target ───────────────────────────────────────────────────────────

    /// <summary>
    /// Catches drags from InventorySlotUI. Reads the drag source index from InventoryUI
    /// (must fire before InventorySlotUI.OnEndDrag clears it) and assigns the item here.
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        int fromIndex = InventoryUI.Instance != null ? InventoryUI.Instance.DragFromIndex : -1;
        if (fromIndex < 0) return;

        var slot = InventoryUI.Model?.GetSlot(fromIndex);
        if (slot == null || slot.IsEmpty) return;

        HotbarUI.AssignSlot(SlotIndex, slot.item);
    }
}
