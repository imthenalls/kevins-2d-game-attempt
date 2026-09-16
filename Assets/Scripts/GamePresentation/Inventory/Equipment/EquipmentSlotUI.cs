using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Drop target for one equipment slot. It accepts a compatible inventory drag,
/// displays the equipped icon/name, shows item tooltips, and right-clicks to unequip.
///
/// Unity setup:
///   1. Add to a Weapon, Armor, or Accessory slot Image under EquipmentPanel.
///   2. Set Slot Type and assign Background, Icon, and Item Name.
///   3. Requires an EventSystem and GraphicRaycaster on the inventory Canvas.
///
/// Runtime API:
///   Bind(EquipmentUI) connects a scene slot to its owning panel.
///   Setup(...) remains available for runtime-created fallback UI.
///   Refresh() redraws from EquipmentUI.GetEquipped(slotType).
/// </summary>
[DisallowMultipleComponent]
public class EquipmentSlotUI : MonoBehaviour,
    IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    private static readonly Color NormalColor = new Color(0.18f, 0.21f, 0.27f, 1f);
    private static readonly Color HoverColor = new Color(0.30f, 0.38f, 0.50f, 1f);
    private static readonly Color ValidDropColor = new Color(0.18f, 0.48f, 0.26f, 1f);

    [Header("Slot")]
    [SerializeField] private EquipSlotType slotType;
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI itemName;

    private EquipmentUI owner;

    public EquipSlotType SlotType => slotType;

    public void Bind(EquipmentUI equipmentUI)
    {
        owner = equipmentUI;
        if (background == null)
            background = GetComponent<Image>();
        Refresh();
    }

    public void Setup(
        EquipSlotType type,
        EquipmentUI equipmentUI,
        Image backgroundImage,
        Image iconImage,
        TextMeshProUGUI itemNameText)
    {
        slotType = type;
        owner = equipmentUI;
        background = backgroundImage;
        icon = iconImage;
        itemName = itemNameText;
        Refresh();
    }

    public void Refresh()
    {
        ItemData item = owner != null ? owner.GetEquipped(slotType) : null;
        if (icon != null)
        {
            icon.sprite = item != null ? item.icon : null;
            icon.enabled = item != null && item.icon != null;
        }

        if (itemName != null)
            itemName.text = item != null ? item.itemName : "Empty";

        if (background != null)
            background.color = NormalColor;
    }

    public void OnDrop(PointerEventData eventData)
    {
        owner?.TryEquipFromDraggedInventory(slotType);
        Refresh();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            owner?.TryUnequipToInventory(slotType);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ItemData item = owner != null ? owner.GetEquipped(slotType) : null;
        if (item != null)
            InventoryTooltip.Show(item);

        if (background == null) return;
        InventorySlot dragged = GetDraggedInventorySlot();
        background.color = dragged != null && !dragged.IsEmpty && dragged.item.IsEquip &&
            dragged.item.equipSlot == slotType
            ? ValidDropColor
            : HoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        InventoryTooltip.Hide();
        if (background != null)
            background.color = NormalColor;
    }

    private static InventorySlot GetDraggedInventorySlot()
    {
        int index = InventoryUI.Instance != null ? InventoryUI.Instance.DragFromIndex : -1;
        return InventoryUI.Model != null && index >= 0
            ? InventoryUI.Model.GetSlot(index)
            : null;
    }
}
