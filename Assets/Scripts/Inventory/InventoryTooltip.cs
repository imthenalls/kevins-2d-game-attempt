using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Floating tooltip that displays item details (name, type flags, description, sell value)
/// when the player hovers over an occupied inventory slot. The panel follows the cursor
/// each frame via Update().
///
/// Unity setup:
///   1. Place one instance on a GameObject inside the same Canvas as InventoryUI.
///   2. Assign panel (tooltip RectTransform), nameText, typeText, descriptionText,
///      and sellValueText (all TextMeshProUGUI).
///   3. Adjust Cursor Offset to position the tooltip relative to the mouse pointer.
///   InventorySlotUI calls the static Show(item) / Hide() methods automatically.
///   Only one instance should exist in the scene — uses a static singleton pattern.
/// </summary> : MonoBehaviour
{
    private static InventoryTooltip instance;

    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI sellValueText;
    [SerializeField] private Vector2 cursorOffset = new Vector2(14f, -14f);

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
    }

    private void Update()
    {
        if (panel == null || !panel.gameObject.activeSelf || canvasRect == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            GetMousePosition(),
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
            out Vector2 localPoint);

        panel.anchoredPosition = localPoint + cursorOffset;
    }

    public static void Show(ItemData item)
    {
        if (instance == null || item == null) return;

        if (instance.nameText != null)        instance.nameText.text = item.itemName;
        if (instance.typeText != null)        instance.typeText.text = $"{item.type}{BuildFlagLabel(item.flags)}";
        if (instance.descriptionText != null) instance.descriptionText.text = item.description;
        if (instance.sellValueText != null)
            instance.sellValueText.text = item.sellValue > 0 ? $"Sell: {item.sellValue}g" : string.Empty;

        if (instance.panel != null)
            instance.panel.gameObject.SetActive(true);
    }

    public static void Hide()
    {
        if (instance != null && instance.panel != null)
            instance.panel.gameObject.SetActive(false);
    }

    private static string BuildFlagLabel(ItemFlags flags)
    {
        if ((flags & ItemFlags.QuestItem) != 0) return " [Quest]";
        if ((flags & ItemFlags.KeyItem)   != 0) return " [Key]";
        if ((flags & ItemFlags.Unique)    != 0) return " [Unique]";
        return string.Empty;
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
