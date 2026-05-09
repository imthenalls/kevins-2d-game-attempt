using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class InventorySlotUI : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IDropHandler,
    IPointerClickHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI quantityText;

    [Header("Colors")]
    [SerializeField] private Color normalColor  = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    [SerializeField] private Color hoveredColor = new Color(0.30f, 0.30f, 0.30f, 0.90f);

    public int SlotIndex { get; private set; }
    public InventorySlot Slot { get; private set; }

    // InventoryUI subscribes to these
    public event Action<int>         DragStarted;  // slotIndex
    public event Action<int>         DragEnded;    // slotIndex (always fires after drag)
    public event Action<int>         Dropped;      // slotIndex (this slot is the drop target)
    public event Action<int, Vector2> RightClicked; // slotIndex, screen position

    public void Setup(int index, InventorySlot slot)
    {
        SlotIndex = index;
        Slot = slot;
        Refresh();
    }

    public void Refresh()
    {
        if (Slot == null || Slot.IsEmpty)
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
            if (quantityText != null)
            {
                quantityText.text = string.Empty;
                quantityText.enabled = false;
            }
        }
        else
        {
            if (iconImage != null)
            {
                iconImage.sprite = Slot.item.icon;
                iconImage.enabled = true;
            }
            if (quantityText != null)
            {
                bool showQty = Slot.item.IsStackable && Slot.quantity > 1;
                quantityText.text = showQty ? Slot.quantity.ToString() : string.Empty;
                quantityText.enabled = showQty;
            }
        }

        if (backgroundImage != null)
            backgroundImage.color = normalColor;
    }

    // --- Pointer events ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (backgroundImage != null)
            backgroundImage.color = hoveredColor;

        if (Slot != null && !Slot.IsEmpty)
            InventoryTooltip.Show(Slot.item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (backgroundImage != null)
            backgroundImage.color = normalColor;

        InventoryTooltip.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right
            && Slot != null && !Slot.IsEmpty)
        {
            RightClicked?.Invoke(SlotIndex, eventData.position);
        }
    }

    // --- Drag events ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Slot == null || Slot.IsEmpty)
        {
            // Cancel the drag so IDropHandler on target won't fire
            eventData.pointerDrag = null;
            return;
        }
        DragStarted?.Invoke(SlotIndex);
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Required by Unity to activate IDropHandler on other objects
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragEnded?.Invoke(SlotIndex);
    }

    public void OnDrop(PointerEventData eventData)
    {
        Dropped?.Invoke(SlotIndex);
    }
}
