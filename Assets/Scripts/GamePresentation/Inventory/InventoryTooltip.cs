using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Floating tooltip that displays full item details — icon, name, type + equip slot + flags + world
/// scope, description, equipment bonuses, and sell value — when the player hovers over an occupied
/// inventory slot. The panel follows the cursor each frame via Update().
///
/// The icon and bonus lines are built programmatically (the prefab only wires the base text fields),
/// so equipment-heavy items show their full effect without a prefab change.
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
    private static readonly Color BonusTextColor = new Color(0.1f, 0.35f, 0.1f);
    private static readonly Vector2 TooltipSize = new Vector2(340f, 250f);

    private static InventoryTooltip instance;

    [SerializeField] private RectTransform panel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI typeText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI sellValueText;
    [SerializeField] private Vector2 cursorOffset = new Vector2(14f, -14f);

    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private Image iconImage;
    private TextMeshProUGUI bonusesText;

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
        if (instance.typeText != null)        instance.typeText.text = BuildTypeLabel(item);
        if (instance.descriptionText != null) instance.descriptionText.text = item.description;
        if (instance.sellValueText != null)
            instance.sellValueText.text = item.sellValue > 0 ? $"Sell: {item.sellValue}g" : string.Empty;

        instance.ApplyIcon(item.icon);
        instance.ApplyBonuses(item);

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

    private void ApplyIcon(Sprite icon)
    {
        if (iconImage == null) return;

        iconImage.sprite = icon;
        iconImage.gameObject.SetActive(icon != null);
    }

    private void ApplyBonuses(ItemData item)
    {
        if (bonusesText == null) return;

        string bonus = BuildBonusLabel(item);
        bonusesText.text = bonus;
        bonusesText.gameObject.SetActive(bonus.Length > 0);
    }

    // "Equipment · Weapon [Unique] · World A" — combines category, equip slot, flags, and world scope.
    private static string BuildTypeLabel(ItemData item)
    {
        string label = item.type.ToString();
        if (item.IsEquip)
            label += " · " + item.equipSlot.ToString();

        label += BuildFlagLabel(item.flags);
        label += BuildScopeLabel(item.scope);
        return label;
    }

    private static string BuildFlagLabel(ItemFlags flags)
    {
        var parts = new System.Collections.Generic.List<string>();
        if ((flags & ItemFlags.QuestItem) != 0) parts.Add("Quest");
        if ((flags & ItemFlags.KeyItem)   != 0) parts.Add("Key");
        if ((flags & ItemFlags.Unique)    != 0) parts.Add("Unique");
        return parts.Count > 0 ? " [" + string.Join(", ", parts) + "]" : string.Empty;
    }

    private static string BuildScopeLabel(ItemScope scope)
    {
        switch (scope)
        {
            case ItemScope.WorldA: return " · World A only";
            case ItemScope.WorldB: return " · World B only";
            default: return string.Empty;
        }
    }

    private static string BuildBonusLabel(ItemData item)
    {
        if (!item.IsEquip)
            return string.Empty;

        var parts = new System.Collections.Generic.List<string>();
        if (item.bonusMaxHp  > 0) parts.Add("+" + item.bonusMaxHp  + " Max HP");
        if (item.bonusMaxMp  > 0) parts.Add("+" + item.bonusMaxMp  + " Max MP");
        if (item.bonusAttack > 0) parts.Add("+" + item.bonusAttack + " Attack");
        if (item.bonusDefense > 0) parts.Add("+" + item.bonusDefense + " Defense");
        return parts.Count > 0 ? string.Join("\n", parts) : string.Empty;
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
        ConfigureText(typeText, 17f, FontStyles.Normal, -48f, 24f);
        ConfigureText(descriptionText, 16f, FontStyles.Normal, -78f, 78f);
        ConfigureText(sellValueText, 17f, FontStyles.Bold, -232f, 24f);

        BuildIcon();
        BuildBonusesText();
        ConfigureText(bonusesText, 17f, FontStyles.Bold, -162f, 64f);
        if (bonusesText != null)
            bonusesText.color = BonusTextColor;

        foreach (Graphic graphic in panel.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
    }

    private void BuildIcon()
    {
        var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconObject.transform.SetParent(panel, false);
        iconImage = iconObject.GetComponent<Image>();
        iconImage.color = Color.white;

        RectTransform rect = iconImage.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-12f, -12f);
        rect.sizeDelta = new Vector2(44f, 44f);
        iconImage.gameObject.SetActive(false);
    }

    private void BuildBonusesText()
    {
        var bonusObject = new GameObject("Bonuses", typeof(RectTransform), typeof(TextMeshProUGUI));
        bonusObject.transform.SetParent(panel, false);
        bonusesText = bonusObject.GetComponent<TextMeshProUGUI>();
        bonusesText.fontSize = 17f;
        bonusesText.fontStyle = FontStyles.Bold;
        bonusesText.color = BonusTextColor;
        bonusesText.alignment = TextAlignmentOptions.TopLeft;
        bonusesText.raycastTarget = false;
        bonusesText.gameObject.SetActive(false);
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
