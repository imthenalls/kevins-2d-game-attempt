using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Modal dialog for choosing exactly how many items to split from a stack.
/// Appears on Shift+left-click or via the context menu Split button.
/// Use a Slider to pick any amount from 1 to (quantity - 1), then Confirm or Cancel.
///
/// Unity setup:
///   1. Place one instance inside the same Canvas as InventoryUI, above the inventory panel
///      in sibling order so it renders on top.
///   2. Assign all serialized fields in the Inspector.
///   3. The panel starts hidden; InventorySlotUI / InventoryContextMenu call Show() at runtime.
///   Only one instance should exist in the scene — uses a static singleton pattern.
/// </summary>
public class InventorySplitDialog : MonoBehaviour
{
    private static InventorySplitDialog instance;

    [SerializeField] private RectTransform panel;
    [SerializeField] private Slider        amountSlider;
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private TextMeshProUGUI totalText;    // shows original stack size (optional)
    [SerializeField] private Button        confirmButton;
    [SerializeField] private Button        cancelButton;

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

        if (amountSlider != null)
        {
            amountSlider.wholeNumbers = true;
            amountSlider.minValue     = 1;
            amountSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton  != null) cancelButton.onClick.AddListener(Hide);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Opens the split dialog for the given slot.
    /// screenPosition is the UI screen position of the click that triggered the dialog.
    /// </summary>
    public static void Show(InventoryModel inventoryModel, int slotIndex, Vector2 screenPosition)
    {
        if (instance == null) return;

        var slot = inventoryModel.GetSlot(slotIndex);
        if (slot.IsEmpty || !slot.item.IsStackable || slot.quantity < 2) return;

        instance.model            = inventoryModel;
        instance.targetSlotIndex  = slotIndex;

        if (instance.amountSlider != null)
        {
            instance.amountSlider.maxValue = slot.quantity - 1;
            instance.amountSlider.value    = Mathf.CeilToInt(slot.quantity / 2f);
        }

        if (instance.totalText != null)
            instance.totalText.text = $"/ {slot.quantity}";

        instance.UpdateAmountText();
        instance.PositionNearClick(screenPosition);

        instance.panel.gameObject.SetActive(true);
        instance.panel.SetAsLastSibling();
    }

    public static void Hide()
    {
        if (instance != null && instance.panel != null)
            instance.panel.gameObject.SetActive(false);
    }

    public static bool IsOpen =>
        instance != null && instance.panel != null && instance.panel.gameObject.activeSelf;

    // -------------------------------------------------------------------------
    // Internal
    // -------------------------------------------------------------------------

    private void OnSliderChanged(float _) => UpdateAmountText();

    private void UpdateAmountText()
    {
        if (amountText == null || amountSlider == null) return;
        amountText.text = ((int)amountSlider.value).ToString();
    }

    private void OnConfirm()
    {
        if (model == null) return;
        model.SplitStack(targetSlotIndex, (int)amountSlider.value);
        Hide();
    }

    private void PositionNearClick(Vector2 screenPosition)
    {
        if (panel == null || canvasRect == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Camera.main,
            out Vector2 localPoint);

        // Offset slightly so the dialog doesn't sit directly under the cursor
        localPoint += new Vector2(10f, -10f);
        panel.anchoredPosition = localPoint;
    }

    private void Update()
    {
        // Close on Escape
        if (IsOpen && WasEscapePressed())
            Hide();
    }

    private bool WasEscapePressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
