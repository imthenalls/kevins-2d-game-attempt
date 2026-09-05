using System;
using System.Collections.Generic;

/// <summary>
/// Plain serializable snapshot of all persistent game state written to and read from disk.
/// Serialized with JsonUtility — no Dictionary fields are used (uses List-of-entry types instead
/// because JsonUtility cannot serialize Dictionaries).
///
/// Contains:
///   - Current scene name and player world position
///   - Player HP
///   - Canonical player mana balance, capacity, and transaction history
///   - Legacy player MP fields retained for one-time migration of older saves
///   - WorldStateManager facts (serialized as a FactEntry list)
///   - Active quest state (node positions + objective counts)
///   - Occupied inventory slots (referenced by itemId, resolved via ItemDatabase on load)
///   - Player keyring entries stored separately from inventory slots
///   - Player equipment slots (referenced by itemId and EquipSlotType)
///   - NPC / enemy state: world position, HP / MP (enemies only), and inventory slots
///   - NPC Wallet snapshots and the completed market-trade ledger
///
/// Unity setup: none — this is a plain C# class, not a MonoBehaviour.
///   Created and consumed entirely by SaveManager.Save() and SaveManager.Load().
///   All ItemData assets must be inside Assets/Resources/Items/ OR registered in
///   ItemDatabase so they can be resolved by itemId when the save is loaded.
/// </summary>

[Serializable]
public class SaveData
{
    // Version 2 unifies the former wallet balance and EntityStats MP into Wallet mana.
    // Version 3 adds persistent player equipment slots.
    // Version 4 adds the persistent slot-free player keyring.
    // Missing fields deserialize as 0, so pre-unification saves are version 0.
    public int saveVersion;

    // ── Scene ────────────────────────────────────────────────────────────────
    public string currentScene = "";

    // ── Player transform ─────────────────────────────────────────────────────
    public float playerX;
    public float playerY;

    // ── Player stats ──────────────────────────────────────────────────────────
    public int playerHp;
    public int playerMp;
    public int playerMaxHp;
    public int playerMaxMp;

    public WalletSaveData wallet = new();

    // Completed item-for-mana market exchanges retained by TradeService.
    public List<MarketTransaction> marketTransactions = new();

    // ── World facts (WorldStateManager) ────────────────────────────────────────────
    public List<FactEntry> worldFacts = new();

    // ── Active quests ─────────────────────────────────────────────────────────
    // Reuses the [Serializable] entry types already defined in QuestManager.
    public List<QuestManager.QuestSaveEntry> activeQuests = new();

    // ── Inventory ─────────────────────────────────────────────────────────────
    // Only occupied slots are stored. Items are referenced by their asset name
    // so they can be reloaded via Resources.Load<ItemData>("Items/<name>").
    // Place all ItemData ScriptableObjects inside Assets/Resources/Items/.
    public List<InventorySlotEntry> inventorySlots = new();

    // KeyItem ownership is stored outside the slot grid.
    public List<KeyringSaveEntry> playerKeys = new();

    // Equipped items are owned outside the inventory grid and saved separately.
    public List<EquipmentSaveEntry> playerEquipment = new();

    // ── NPC / Enemy state ─────────────────────────────────────────────────────
    // One entry per NPC in the current scene. Keyed by NpcController.NpcId.
    public List<NpcSaveEntry> npcStates = new();

    // ── Hotbar ────────────────────────────────────────────────────────────────
    // Assigned item per slot index (only non-empty slots stored).
    public List<HotbarEntry> hotbarSlots = new();
}

// ---------------------------------------------------------------------------
// Supporting entry types
// ---------------------------------------------------------------------------

[Serializable]
public class FactEntry
{
    public string key;
    public string value; // always stored as a string
    public string type;  // "bool" | "int" | "float" | "string"
}

[Serializable]
public class InventorySlotEntry
{
    public int    slotIndex;
    public string itemId;    // matches ItemData.itemId (registered in ItemDatabase)
    public int    quantity;
}

[Serializable]
public class EquipmentSaveEntry
{
    public string slot;
    public string itemId;
}

[Serializable]
public class KeyringSaveEntry
{
    public string itemId;
    public int quantity;
}

[Serializable]
public class NpcSaveEntry
{
    public string npcId;

    // World position
    public float x;
    public float y;

    // Stats (populated only when the NPC has an EntityStats component, e.g. enemies)
    public bool hasStats;
    public int  hp;
    public int  mp;
    public int  maxHp;
    public int  maxMp;
    public WalletSaveData wallet;

    // Inventory (populated only when the NPC has an InventoryModel)
    public List<InventorySlotEntry> inventorySlots = new();
}

[Serializable]
public class HotbarEntry
{
    public int    slotIndex;
    public string itemId;
}
