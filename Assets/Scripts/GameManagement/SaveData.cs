using System;
using System.Collections.Generic;

// ---------------------------------------------------------------------------
// SaveData — all game state that is written to / read from the save file.
// Serialized with JsonUtility (no Dictionary fields — those become Lists).
// ---------------------------------------------------------------------------

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
