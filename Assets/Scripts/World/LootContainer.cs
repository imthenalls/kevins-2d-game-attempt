using System;
using UnityEngine;

/// <summary>
/// Interactable container (chest, barrel, crate, etc.) with configurable loot.
/// When the player presses E within range, the LootContainerUI opens showing all items.
/// Items can be taken one at a time (click) or all at once (Take All button).
///
/// Looted state is persisted across sessions via WorldStateManager using the flag:
///   Chest.{chestId}.Looted
/// Once all items are removed the flag is set and the chest permanently becomes inactive.
///
/// Implements IInteractable — PlayerInteractionController discovers it automatically as long
/// as the GameObject's physics layer is included in the controller's Interactable Layers mask.
///
/// Unity setup:
///   1. Create a GameObject with a SpriteRenderer and any Collider2D (set as Trigger).
///   2. Add this component.
///   3. Set a unique Chest Id (e.g. "forest_chest_01") — must be unique per scene.
///   4. Set Display Name shown as the panel title (e.g. "Wooden Chest").
///   5. Fill the Loot array with ItemData references and quantities.
///   6. Adjust Inventory Rows/Columns to fit the loot count.
///   7. Set the GameObject's physics layer to match Interactable Layers on
///      PlayerInteractionController (recommended: dedicated "Interactable" layer).
///
/// Quest integration (automatic when Take All / slot click removes items):
///   QuestEventBus.Raise("ItemCollected", itemId, taken)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LootContainer : MonoBehaviour, IInteractable
{
    // ── Nested type ───────────────────────────────────────────────────────────

    [Serializable]
    public class LootEntry
    {
        public ItemData item;
        [Min(1)] public int quantity = 1;
    }

    // ── Inspector fields ──────────────────────────────────────────────────────

    [Header("Identity")]
    [Tooltip("Unique id used for the WorldState looted flag. Must be unique across the scene.")]
    [SerializeField] private string chestId = "chest_001";
    [SerializeField] private string displayName = "Chest";
    [SerializeField, Min(0.25f)] private float interactionRange = 1.5f;

    [Header("Loot")]
    [SerializeField] private LootEntry[] loot = Array.Empty<LootEntry>();

    [Header("Grid")]
    [SerializeField, Min(1)] private int inventoryRows    = 2;
    [SerializeField, Min(1)] private int inventoryColumns = 4;

    // ── State ─────────────────────────────────────────────────────────────────

    private InventoryModel _model;
    private bool           _looted;

    private string LootedFlag => $"Chest.{chestId}.Looted";

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        _looted = WorldStateManager.Instance != null
               && WorldStateManager.Instance.HasFlag(LootedFlag);

        if (!_looted)
            InitModel();
    }

    // ── IInteractable ─────────────────────────────────────────────────────────

    public bool CanInteract(Vector3 worldPosition) =>
        enabled && !_looted
        && Vector2.Distance(transform.position, worldPosition) <= interactionRange;

    public string GetDisplayName() => displayName;

    // No dialogue lines — goes straight to EndInteraction
    public bool TryGetCurrentLine(out string line) { line = string.Empty; return false; }
    public void Advance() { }

    public void EndInteraction(GameObject interactor)
    {
        if (_model != null && !_looted)
            LootContainerUI.Show(_model, displayName);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void InitModel()
    {
        _model = new InventoryModel(inventoryRows, inventoryColumns);

        foreach (var entry in loot)
        {
            if (entry?.item != null)
                _model.AddItem(entry.item, entry.quantity);
        }

        _model.OnChanged += OnModelChanged;
    }

    private void OnModelChanged()
    {
        if (_model == null || _looted) return;

        for (int i = 0; i < _model.SlotCount; i++)
            if (!_model.GetSlot(i).IsEmpty) return; // still has items

        // All items taken — mark as permanently looted
        _looted = true;
        _model.OnChanged -= OnModelChanged;

        WorldStateManager.Instance?.SetFlag(LootedFlag, true);
        LootContainerUI.Hide();
    }

    // ── Editor gizmo ─────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _looted ? Color.gray : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
