using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Hotbar controller. Manages a row of 6 quick-use slots that are always visible.
/// Pressing 1–6 uses the assigned item: removes one from the player's inventory,
/// applies its heal effects (healHp / healMp on ItemData), and fires ItemUsed on
/// QuestEventBus. Drag an item from the inventory grid onto a hotbar slot to assign it;
/// right-click a hotbar slot to clear it.
///
/// Unity setup:
///   1. Place this on a persistent GameObject (DontDestroyOnLoad recommended — put it
///      on the same Canvas root as InventoryUI so it survives scene loads).
///   2. Assign slotPrefab (a prefab with HotbarSlotUI) and slotContainer
///      (the RectTransform that holds the instantiated slots).
///   3. Set Slot Count to 6 (default). Slots are built automatically in Awake.
///   The Canvas must have a GraphicRaycaster so drag-drop targets work.
/// </summary>
[DisallowMultipleComponent]
public class HotbarUI : MonoBehaviour
{
    private static HotbarUI instance;

    [Header("References")]
    [SerializeField] private HotbarSlotUI   slotPrefab;
    [SerializeField] private RectTransform  slotContainer;

    private static HotbarModel model;
    private HotbarSlotUI[]     slotUIs;
    private PlayerController2D player;

    // ── Static API ────────────────────────────────────────────────────────────

    /// <summary>The hotbar data model. Null until Awake.</summary>
    public static HotbarModel Model => model;

    /// <summary>Assigns <paramref name="item"/> to hotbar slot <paramref name="index"/>.</summary>
    public static void AssignSlot(int index, ItemData item) => model?.Assign(index, item);

    /// <summary>Clears hotbar slot <paramref name="index"/>.</summary>
    public static void ClearSlot(int index) => model?.Clear(index);

    /// <summary>
    /// Assigns <paramref name="item"/> to the first empty slot.
    /// Returns the slot index used, or -1 if every slot is already filled.
    /// </summary>
    public static int AssignFirstEmpty(ItemData item)
    {
        if (model == null) return -1;
        int idx = model.FirstEmptySlot();
        if (idx >= 0) model.Assign(idx, item);
        return idx;
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        instance = this;
        model    = new HotbarModel();
        model.OnChanged += RefreshAll;

        BuildSlots();
    }

    private void Start()
    {
        player = FindFirstObjectByType<PlayerController2D>();

        // Keep quantity labels in sync whenever the player's inventory changes
        if (InventoryUI.Model != null)
            InventoryUI.Model.OnChanged += RefreshAll;
    }

    private void OnDestroy()
    {
        if (InventoryUI.Model != null)
            InventoryUI.Model.OnChanged -= RefreshAll;
    }

    private void Update()
    {
        for (int i = 0; i < HotbarModel.SlotCount; i++)
        {
            if (WasHotkeyPressed(i))
            {
                UseSlot(i);
                break;
            }
        }
    }

    // ── Slot building ─────────────────────────────────────────────────────────

    private void BuildSlots()
    {
        if (slotPrefab == null)
        {
            Debug.LogWarning("[HotbarUI] slotPrefab is not assigned — hotbar will be empty.", this);
            return;
        }

        slotUIs = new HotbarSlotUI[HotbarModel.SlotCount];

        for (int i = 0; i < HotbarModel.SlotCount; i++)
        {
            var ui = Instantiate(slotPrefab, slotContainer);
            ui.Setup(i);
            ui.ClearRequested += ClearSlot;
            slotUIs[i] = ui;
        }
    }

    // ── Use ───────────────────────────────────────────────────────────────────

    private void UseSlot(int index)
    {
        var item = model.GetSlot(index);
        if (item == null) return;

        var inv = InventoryUI.Model;
        if (inv == null || !inv.HasItem(item)) return;

        inv.RemoveItem(item, 1);
        ApplyUseEffect(item);
        QuestEventBus.Raise("ItemUsed", item.itemId, 1);
    }

    private void ApplyUseEffect(ItemData item)
    {
        if (player == null) player = FindFirstObjectByType<PlayerController2D>();
        if (player?.Stats == null) return;

        if (item.healHp > 0) player.Stats.Heal(item.healHp);
        if (item.healMp > 0) player.Stats.RestoreMp(item.healMp);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private void RefreshAll()
    {
        if (slotUIs == null) return;
        for (int i = 0; i < slotUIs.Length; i++)
            slotUIs[i].Refresh(model.GetSlot(i));
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private static bool WasHotkeyPressed(int index)
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return false;
        return index switch
        {
            0 => Keyboard.current.digit1Key.wasPressedThisFrame,
            1 => Keyboard.current.digit2Key.wasPressedThisFrame,
            2 => Keyboard.current.digit3Key.wasPressedThisFrame,
            3 => Keyboard.current.digit4Key.wasPressedThisFrame,
            4 => Keyboard.current.digit5Key.wasPressedThisFrame,
            5 => Keyboard.current.digit6Key.wasPressedThisFrame,
            _ => false,
        };
#else
        return Input.GetKeyDown(KeyCode.Alpha1 + index);
#endif
    }
}
