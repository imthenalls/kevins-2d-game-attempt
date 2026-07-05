using UnityEngine;

/// <summary>
/// World-placed item that is automatically collected when the player's collider
/// overlaps its trigger. Adds the item to the player's inventory via InventoryUI.Model,
/// notifies CharacterStatistics, and raises the QuestEventBus for quest tracking.
///
/// Unity setup:
///   1. Create a GameObject in the scene (or a prefab) with a SpriteRenderer
///      and any Collider2D — the component forces it to Is Trigger on Awake.
///   2. Add this component.
///   3. Assign Item — the ItemData ScriptableObject to give on pickup.
///   4. Set Quantity (default 1).
///   5. Set Player Layers to the Physics layer(s) your Player is on so that
///      only the player triggers a pickup (enemies walking over it are ignored).
///   6. The GameObject is destroyed once all items are collected.
///      If the inventory is full, the item stays on the ground and is partially
///      collected once space becomes available on the next overlap.
///
/// Quest integration (automatic):
///   QuestEventBus.Raise("ItemCollected", item.itemId, amountTaken)
///   — works with any quest objective whose eventType is "ItemCollected"
///     and targetId matches this item's itemId.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ItemPickup : MonoBehaviour
{
    [Header("Item")]
    [SerializeField] private ItemData item;
    [SerializeField, Min(1)] private int quantity = 1;

    [Header("Detection")]
    [Tooltip("Physics layer(s) allowed to collect this item. Set to your Player layer.")]
    [SerializeField] private LayerMask playerLayers = Physics2D.DefaultRaycastLayers;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (item == null) return;

        // Layer check first — avoids GetComponent calls for non-player objects
        if (((1 << other.gameObject.layer) & playerLayers) == 0) return;

        int taken = InventoryHelper.GiveItem(item, quantity, other.gameObject);

        if (taken <= 0) return; // inventory full — item stays on the ground

        quantity -= taken;

        if (quantity <= 0)
            Destroy(gameObject);
    }

    // ── Editor ────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (item == null) return;
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.4f,
            $"{item.itemName} ×{quantity}");
    }
#endif
}
