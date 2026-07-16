using System;
using UnityEngine;

/// <summary>
/// Populates an enemy's inventory with configurable loot and opens the LootContainerUI
/// when the enemy dies. Add this alongside NpcController (NpcType.Enemy) with Has Inventory
/// enabled so the NPC owns an InventoryModel to receive and hold the items.
///
/// Unity setup:
///   1. On the enemy GameObject, add NpcController, set Type to Enemy, and enable Has Inventory.
///      Set Inventory Rows/Columns to match the expected loot count.
///   2. Add this component.
///   3. Fill the Loot array with the items this enemy drops.
///   Only one instance per enemy — NpcController's inventory is pre-populated on Start().
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
    private CombatReceiver  _receiver;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        _npc      = GetComponent<NpcController>();
        _receiver = GetComponent<CombatReceiver>();
    }

    private void Start()
    {
        // Pre-populate the NPC's inventory with the defined loot
        if (_npc.Inventory != null)
        {
            foreach (var entry in loot)
            {
                if (entry?.item != null)
                    _npc.Inventory.AddItem(entry.item, entry.quantity);
            }
        }
        else
        {
            Debug.LogWarning(
                $"[EnemyLootPresenter] NpcController on '{gameObject.name}' has no inventory. " +
                "Enable 'Has Inventory' on NpcController.", this);
        }

        _receiver.OnDeath += OnDeath;
    }

    private void OnDestroy()
    {
        if (_receiver != null)
            _receiver.OnDeath -= OnDeath;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void OnDeath(CombatReceiver _)
    {
        if (_npc.Inventory == null || IsInventoryEmpty()) return;
        LootContainerUI.Show(_npc.Inventory, _npc.DisplayName);
    }

    private bool IsInventoryEmpty()
    {
        var inv = _npc.Inventory;
        for (int i = 0; i < inv.SlotCount; i++)
            if (!inv.GetSlot(i).IsEmpty) return false;
        return true;
    }
}
