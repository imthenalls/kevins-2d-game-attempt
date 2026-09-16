using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Visual representation of a single inventory slot cell inside the inventory grid.
/// Handles hover highlighting, tooltip display, drag-and-drop item movement,
/// and right-click context menu. Communicates with InventoryUI via C# events.
///
/// Unity setup (as a prefab):
///   1. Create a prefab with this component.
///   2. Assign backgroundImage (slot background Image), iconImage (item icon Image),
///      and quantityText (TextMeshProUGUI for stack count) in the Inspector.
///   3. Assign this prefab to the Slot Prefab field on InventoryUI.
///   InventoryUI instantiates and configures slot UIs at runtime — do not place manually.
///   The parent Canvas must have a GraphicRaycaster for drag-and-drop to work.
/// </summary>
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
    [SerializeField] private Color selectedColor = new Color(0.95f, 0.72f, 0.18f, 1f);

    public int SlotIndex { get; private set; }
    public InventorySlot Slot { get; private set; }

    // InventoryUI subscribes to these
    public event Action<int>          DragStarted;   // slotIndex
    public event Action<int>          DragEnded;     // slotIndex (always fires after drag)
    public event Action<int>          Dropped;       // slotIndex (this slot is the drop target)
    public event Action<int, Vector2> RightClicked;  // slotIndex, screen position
    public event Action<int, Vector2> ShiftClicked;  // slotIndex, screen position (Shift+left-click)
    public event Action<int>          LeftClicked;   // slotIndex

    private bool isHovered;
    private bool isSelected;

    public void Setup(int index, InventorySlot slot)
    {
        // The slot's root Image is its background. Resolve it automatically so a
        // missing prefab reference cannot silently disable hover/selection feedback.
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

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

        RefreshBackground();
    }

    /// <summary>Show or clear the persistent keyboard/mouse selection highlight.</summary>
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        RefreshBackground();
    }

    // --- Pointer events ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        RefreshBackground();

        if (Slot != null && !Slot.IsEmpty)
            InventoryTooltip.Show(Slot.item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        RefreshBackground();

        InventoryTooltip.Hide();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (Slot != null && !Slot.IsEmpty && IsShiftHeld()
                && Slot.item.IsStackable && Slot.quantity > 1)
            {
                ShiftClicked?.Invoke(SlotIndex, eventData.position);
                return;
            }

            LeftClicked?.Invoke(SlotIndex);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Right
            && Slot != null && !Slot.IsEmpty)
            RightClicked?.Invoke(SlotIndex, eventData.position);
    }

    private void RefreshBackground()
    {
        if (backgroundImage == null) return;
        backgroundImage.color = isSelected ? selectedColor : isHovered ? hoveredColor : normalColor;
    }

    private static bool IsShiftHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null
            && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
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
