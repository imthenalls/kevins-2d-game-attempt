using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Companion panel that displays Weapon, Armor, and Accessory equipment slots beside
/// the inventory and coordinates drag-to-equip and right-click unequip.
///
/// Unity setup:
///   1. Add this component to EquipmentPanel under the same Canvas as InventoryUI.
///   2. Assign Panel to the EquipmentPanel RectTransform.
///   3. Add three EquipmentSlotUI children and assign them to Equipment Slots.
///   4. The Player GameObject must have EquipmentManager.
///   5. InventoryUI finds this scene component and opens/closes it automatically.
///
/// Runtime API:
///   EquipmentUI.GetOrCreate(inventoryPanel) creates/binds the panel.
///   SetVisible(bool) follows inventory visibility.
///   TryEquipFromDraggedInventory(slotType) handles inventory drag drops.
///   TryUnequipToInventory(slotType) returns equipped gear to the player inventory.
/// </summary>
[DisallowMultipleComponent]
public class EquipmentUI : MonoBehaviour
{
    private static EquipmentUI instance;

    [Header("Scene UI")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private EquipmentSlotUI[] equipmentSlots;
    [SerializeField] private bool autoPositionBesideInventory;

    private readonly Dictionary<EquipSlotType, EquipmentSlotUI> slotUIs = new();
    private RectTransform inventoryPanel;
    private RectTransform canvasRect;
    private Canvas parentCanvas;
    private EquipmentManager equipment;

    public static EquipmentUI Instance => instance;
    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    public static EquipmentUI GetOrCreate(RectTransform inventoryPanelRoot)
    {
        if (instance == null)
        {
            instance = FindFirstObjectByType<EquipmentUI>(FindObjectsInactive.Include);
            if (instance == null)
            {
                Canvas canvas = InventoryUI.Instance != null
                    ? InventoryUI.Instance.GetComponentInParent<Canvas>()
                    : FindAnyObjectByType<Canvas>();
                if (canvas == null)
                    return null;

                GameObject uiObject = new GameObject("Equipment UI", typeof(RectTransform));
                uiObject.transform.SetParent(canvas.transform, false);
                instance = uiObject.AddComponent<EquipmentUI>();
            }
        }

        instance.Initialize(inventoryPanelRoot);
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        InventoryUI.OnModelChanged += HandleActiveInventoryChanged;

        // Start hidden so the panel never covers the scene before the inventory opens.
        if (panel != null)
            panel.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        InventoryUI.OnModelChanged -= HandleActiveInventoryChanged;
        UnbindEquipment();
        if (instance == this)
            instance = null;
    }

    private void LateUpdate()
    {
        if (!IsOpen) return;
        EnsureEquipmentBound();
        if (autoPositionBesideInventory)
            PositionNextToInventory();
    }

    public void SetVisible(bool visible)
    {
        if (panel == null) return;

        if (visible)
        {
            EnsureEquipmentBound();
            RefreshAll();
            if (autoPositionBesideInventory)
                PositionNextToInventory();
        }

        panel.gameObject.SetActive(visible);
    }

    /// <summary>Attempts to equip the inventory item currently being dragged.</summary>
    public bool TryEquipFromDraggedInventory(EquipSlotType targetSlot)
    {
        EnsureEquipmentBound();
        int inventoryIndex = InventoryUI.Instance != null ? InventoryUI.Instance.DragFromIndex : -1;
        if (equipment == null || InventoryUI.Model == null || inventoryIndex < 0)
            return false;

        InventorySlot sourceSlot = InventoryUI.Model.GetSlot(inventoryIndex);
        if (sourceSlot == null || sourceSlot.IsEmpty || !sourceSlot.item.IsEquip ||
            sourceSlot.item.equipSlot != targetSlot)
        {
            return false;
        }

        ItemData item = sourceSlot.item;
        if (!equipment.TryEquipFromInventory(InventoryUI.Model, item, out ItemData displaced))
            return false;

        QuestEventBus.Raise("ItemEquipped", item.itemId, 1);
        if (displaced != null)
            QuestEventBus.Raise("ItemUnequipped", displaced.itemId, 1);

        InventoryTooltip.Hide();
        RefreshAll();
        return true;
    }

    /// <summary>Returns one equipped item to the inventory when capacity allows.</summary>
    public bool TryUnequipToInventory(EquipSlotType slotType)
    {
        EnsureEquipmentBound();
        if (equipment == null || InventoryUI.Model == null)
            return false;

        ItemData item = equipment.Model.GetEquipped(slotType);
        if (item == null || !InventoryUI.Model.CanAddItem(item, 1))
            return false;

        ItemData removed = equipment.Unequip(slotType);
        if (removed == null)
            return false;

        if (InventoryUI.Model.AddItem(removed, 1) != 0)
        {
            equipment.Equip(slotType, removed);
            return false;
        }

        QuestEventBus.Raise("ItemUnequipped", removed.itemId, 1);
        InventoryTooltip.Hide();
        RefreshAll();
        return true;
    }

    public ItemData GetEquipped(EquipSlotType slotType)
    {
        EnsureEquipmentBound();
        return equipment?.Model?.GetEquipped(slotType);
    }

    private void Initialize(RectTransform inventoryPanelRoot)
    {
        inventoryPanel = inventoryPanelRoot;
        parentCanvas = GetComponentInParent<Canvas>();
        canvasRect = parentCanvas != null ? parentCanvas.GetComponent<RectTransform>() : null;

        if (panel == null)
            panel = transform as RectTransform;

        BindSceneSlots();
        if (slotUIs.Count == 0)
            BuildRuntimePanel();

        SetVisible(false);
    }

    private void BindSceneSlots()
    {
        slotUIs.Clear();
        if (equipmentSlots == null || equipmentSlots.Length == 0)
            equipmentSlots = GetComponentsInChildren<EquipmentSlotUI>(true);

        foreach (EquipmentSlotUI slotUI in equipmentSlots)
        {
            if (slotUI == null) continue;
            slotUI.Bind(this);
            slotUIs[slotUI.SlotType] = slotUI;
        }
    }

    private void BuildRuntimePanel()
    {
        autoPositionBesideInventory = true;
        panel = (RectTransform)transform;
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0f, 1f);
        panel.sizeDelta = new Vector2(310f, 390f);

        Image background = gameObject.AddComponent<Image>();
        background.color = new Color(0.94f, 0.95f, 0.97f, 0.98f);

        Outline outline = gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.18f, 0.22f, 0.30f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        CreateText(panel, "Equipment Title", "Equipment", 28f, FontStyles.Bold,
            new Vector2(18f, -14f), new Vector2(274f, 38f));

        EquipSlotType[] slotTypes =
        {
            EquipSlotType.Weapon,
            EquipSlotType.Armor,
            EquipSlotType.Accessory,
        };

        for (int i = 0; i < slotTypes.Length; i++)
            CreateEquipmentRow(slotTypes[i], -66f - (i * 102f));

        CreateText(panel, "Unequip Hint", "Right-click equipped gear to unequip", 14f,
            FontStyles.Italic, new Vector2(18f, -358f), new Vector2(274f, 22f));
    }

    private void CreateEquipmentRow(EquipSlotType slotType, float top)
    {
        GameObject rowObject = new GameObject($"{slotType} Row", typeof(RectTransform), typeof(Image));
        rowObject.transform.SetParent(panel, false);
        RectTransform row = rowObject.GetComponent<RectTransform>();
        SetTopLeftRect(row, new Vector2(16f, top), new Vector2(278f, 88f));
        Image rowBackground = rowObject.GetComponent<Image>();
        rowBackground.color = new Color(0.82f, 0.84f, 0.88f, 1f);
        rowBackground.raycastTarget = false;

        CreateText(row, $"{slotType} Label", slotType.ToString(), 20f, FontStyles.Bold,
            new Vector2(12f, -10f), new Vector2(150f, 28f));
        TextMeshProUGUI itemName = CreateText(row, "Item Name", "Empty", 15f, FontStyles.Normal,
            new Vector2(12f, -44f), new Vector2(170f, 28f));

        GameObject slotObject = new GameObject($"{slotType} Slot", typeof(RectTransform), typeof(Image));
        slotObject.transform.SetParent(row, false);
        RectTransform slotRect = slotObject.GetComponent<RectTransform>();
        SetTopLeftRect(slotRect, new Vector2(194f, -8f), new Vector2(72f, 72f));
        Image slotBackground = slotObject.GetComponent<Image>();
        slotBackground.color = new Color(0.18f, 0.21f, 0.27f, 1f);

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(slotRect, false);
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(1f, 1f);
        iconRect.offsetMin = new Vector2(6f, 6f);
        iconRect.offsetMax = new Vector2(-6f, -6f);
        Image icon = iconObject.GetComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        EquipmentSlotUI slotUI = slotObject.AddComponent<EquipmentSlotUI>();
        slotUI.Setup(slotType, this, slotBackground, icon, itemName);
        slotUIs[slotType] = slotUI;
    }

    private void EnsureEquipmentBound()
    {
        if (equipment != null && equipment.Model != null)
            return;

        PlayerController2D player = FindAnyObjectByType<PlayerController2D>();
        EquipmentManager found = player != null ? player.GetComponent<EquipmentManager>() : null;
        if (found == null || found.Model == null)
            return;

        UnbindEquipment();
        equipment = found;
        equipment.Model.OnSlotChanged += HandleEquipmentChanged;
        RefreshAll();
    }

    private void UnbindEquipment()
    {
        if (equipment?.Model != null)
            equipment.Model.OnSlotChanged -= HandleEquipmentChanged;
        equipment = null;
    }

    private void HandleEquipmentChanged(EquipSlotType _, ItemData __, ItemData ___) => RefreshAll();

    private void HandleActiveInventoryChanged(InventoryModel _)
    {
        UnbindEquipment();
        EnsureEquipmentBound();
        RefreshAll();
    }

    private void RefreshAll()
    {
        foreach (EquipmentSlotUI slotUI in slotUIs.Values)
            slotUI.Refresh();
    }

    private void PositionNextToInventory()
    {
        if (panel == null || inventoryPanel == null || canvasRect == null || parentCanvas == null)
            return;

        Canvas.ForceUpdateCanvases();
        Vector3[] corners = new Vector3[4];
        inventoryPanel.GetWorldCorners(corners);

        Camera camera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main;
        Vector2 topRightScreen = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        Vector2 topLeftScreen = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, topRightScreen, camera, out Vector2 topRightLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, topLeftScreen, camera, out Vector2 topLeftLocal);

        Rect bounds = canvasRect.rect;
        Vector2 desired = topRightLocal + new Vector2(16f, 0f);
        if (desired.x + panel.rect.width > bounds.xMax)
            desired.x = topLeftLocal.x - panel.rect.width - 16f;

        desired.x = Mathf.Clamp(desired.x, bounds.xMin, bounds.xMax - panel.rect.width);
        desired.y = Mathf.Clamp(desired.y, bounds.yMin + panel.rect.height, bounds.yMax);
        panel.anchoredPosition = desired;
    }

    private static TextMeshProUGUI CreateText(
        RectTransform parent,
        string objectName,
        string value,
        float fontSize,
        FontStyles style,
        Vector2 position,
        Vector2 size)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        SetTopLeftRect(rect, position, size);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.black;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void SetTopLeftRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
