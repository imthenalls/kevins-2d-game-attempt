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
/// </summary>
[DisallowMultipleComponent]
public class InventoryTooltip : MonoBehaviour
{
    private static readonly Color TooltipBackgroundColor = Color.white;
    private static readonly Color TooltipTextColor = Color.black;
    private static readonly Vector2 TooltipSize = new Vector2(320f, 190f);

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
        {
            ConfigurePanelVisuals();
            panel.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (panel == null || !panel.gameObject.activeSelf || canvasRect == null) return;

        UpdatePanelPosition();
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
        {
            instance.transform.SetAsLastSibling();
            instance.panel.SetAsLastSibling();
            instance.panel.gameObject.SetActive(true);
            instance.UpdatePanelPosition();
        }
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

    private void ConfigurePanelVisuals()
    {
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0f, 1f);
        panel.sizeDelta = TooltipSize;
        panel.localScale = Vector3.one;

        Image background = panel.GetComponent<Image>();
        if (background == null)
            background = panel.gameObject.AddComponent<Image>();
        background.color = TooltipBackgroundColor;
        background.raycastTarget = false;

        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        ConfigureText(nameText, 24f, FontStyles.Bold, -12f, 32f);
        ConfigureText(typeText, 18f, FontStyles.Normal, -48f, 24f);
        ConfigureText(descriptionText, 17f, FontStyles.Normal, -78f, 70f);
        ConfigureText(sellValueText, 17f, FontStyles.Bold, -158f, 24f);

        foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }

    private static void ConfigureText(
        TextMeshProUGUI text,
        float fontSize,
        FontStyles style,
        float topOffset,
        float height)
    {
        if (text == null) return;

        text.color = TooltipTextColor;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, topOffset);
        rect.sizeDelta = new Vector2(-28f, height);
    }

    private void UpdatePanelPosition()
    {
        if (panel == null || canvasRect == null || parentCanvas == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            GetMousePosition(),
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
            out Vector2 localPoint);

        Vector2 desired = localPoint + cursorOffset;
        Rect canvasBounds = canvasRect.rect;
        desired.x = Mathf.Clamp(desired.x, canvasBounds.xMin, canvasBounds.xMax - panel.rect.width);
        desired.y = Mathf.Clamp(desired.y, canvasBounds.yMin + panel.rect.height, canvasBounds.yMax);
        panel.anchoredPosition = desired;
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
