using Game.Core;
using System;
using UnityEngine;

/// <summary>
/// Adds optional Inspector-configured items to an enemy's owned inventory. The automatic
/// EnemyLootDrop system turns that inventory into a separate interactable pile on death.
///
/// Unity setup:
///   1. On the enemy GameObject, add NpcController and set Type to Enemy.
///   2. Add this component.
///   3. Fill the Loot array with the items this enemy drops.
///   This component is optional; JSON loot in enemy_loot.json needs no scene component.
/// </summary>
[RequireComponent(typeof(NpcController))]
[RequireComponent(typeof(CombatReceiver))]
public class EnemyLootPresenter : MonoBehaviour
{
    // ── Nested type ───────────────────────────────────────────────────────────

    [Serializable]
    public class LootEntry
    {
        public ItemData item;
        [Min(1)] public int quantity = 1;
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [SerializeField] private LootEntry[] loot = Array.Empty<LootEntry>();

    // ── State ─────────────────────────────────────────────────────────────────

    private NpcController   _npc;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _npc = GetComponent<NpcController>();
    }

    private void Start()
    {
        // Pre-populate the NPC's inventory with the defined loot
        InventoryModel inventory = _npc.EnsureInventory();
        foreach (var entry in loot)
        {
            if (entry?.item != null)
                inventory.AddItem(entry.item, entry.quantity);
        }
    }
}
