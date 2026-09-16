using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Visual slot for the loot/container panel. Displays an item icon and stack count.
/// Left-clicking a filled slot fires Clicked — LootContainerUI handles the transfer.
/// Hover shows the standard InventoryTooltip.
///
/// Unity setup (as a prefab):
///   1. Create a prefab with this component.
///   2. Assign backgroundImage, iconImage, and quantityText in the Inspector.
///   3. Assign this prefab to the Slot Prefab field on LootContainerUI.
///   LootContainerUI instantiates and configures these at runtime — do not place manually.
/// </summary>
public class LootSlotUI : MonoBehaviour,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] private Image             backgroundImage;
    [SerializeField] private Image             iconImage;
    [SerializeField] private TextMeshProUGUI   quantityText;

    [Header("Colors")]
    [SerializeField] private Color normalColor  = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    [SerializeField] private Color hoveredColor = new Color(0.30f, 0.30f, 0.30f, 0.90f);

    private InventorySlot slot;

    public int SlotIndex { get; private set; }

    /// <summary>Fires when the player left-clicks a filled slot. Arg: slotIndex.</summary>
    public event Action<int> Clicked;

    public void Setup(int index, InventorySlot inventorySlot)
    {
        SlotIndex = index;
        slot      = inventorySlot;
        Refresh();
    }

    public void Refresh()
    {
        bool empty = slot == null || slot.IsEmpty;

        if (iconImage != null)
        {
            iconImage.sprite  = empty ? null : slot.item.icon;
            iconImage.enabled = !empty;
        }

        if (quantityText != null)
        {
            bool showQty = !empty && slot.item.IsStackable && slot.quantity > 1;
            quantityText.text    = showQty ? slot.quantity.ToString() : string.Empty;
            quantityText.enabled = showQty;
        }

        if (backgroundImage != null)
            backgroundImage.color = normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left
            && slot != null && !slot.IsEmpty)
        {
            Clicked?.Invoke(SlotIndex);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (backgroundImage != null) backgroundImage.color = hoveredColor;
        if (slot != null && !slot.IsEmpty) InventoryTooltip.Show(slot.item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (backgroundImage != null) backgroundImage.color = normalColor;
        InventoryTooltip.Hide();
    }
}
