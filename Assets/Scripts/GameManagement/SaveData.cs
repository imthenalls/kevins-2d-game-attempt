using System;
using System.Collections.Generic;

/// <summary>
/// Plain serializable snapshot of all persistent game state written to and read from disk.
/// Serialized with JsonUtility — no Dictionary fields are used (uses List-of-entry types instead
/// because JsonUtility cannot serialize Dictionaries).
///
/// Contains:
///   - Current scene name and player world position
///   - Player current and max HP / MP
///   - WorldStateDB facts (serialized as a FactEntry list)
///   - Active quest state (node positions + objective counts)
///   - Occupied inventory slots (referenced by itemId, resolved via ItemDatabase on load)
///   - NPC / enemy state: world position, HP / MP (enemies only), and inventory slots
///
/// Unity setup: none — this is a plain C# class, not a MonoBehaviour.
///   Created and consumed entirely by SaveManager.Save() and SaveManager.Load().
///   All ItemData assets must be inside Assets/Resources/Items/ OR registered in
///   ItemDatabase so they can be resolved by itemId when the save is loaded.
/// </summary>

[Serializable]
public class SaveData
{
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

    // ── World facts (WorldStateDB) ────────────────────────────────────────────
    public List<FactEntry> worldFacts = new();

    // ── Active quests ─────────────────────────────────────────────────────────
    // Reuses the [Serializable] entry types already defined in QuestManager.
    public List<QuestManager.QuestSaveEntry> activeQuests = new();

    // ── Inventory ─────────────────────────────────────────────────────────────
    // Only occupied slots are stored. Items are referenced by their asset name
    // so they can be reloaded via Resources.Load<ItemData>("Items/<name>").
    // Place all ItemData ScriptableObjects inside Assets/Resources/Items/.
    public List<InventorySlotEntry> inventorySlots = new();

    // ── NPC / Enemy state ─────────────────────────────────────────────────────
    // One entry per NPC in the current scene. Keyed by NpcController.NpcId.
    public List<NpcSaveEntry> npcStates = new();
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

    // Inventory (populated only when the NPC has an InventoryModel)
    public List<InventorySlotEntry> inventorySlots = new();
}
