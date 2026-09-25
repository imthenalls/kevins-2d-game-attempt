using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that handles writing and reading the save file.
/// Save data is written as JSON to Application.persistentDataPath/save.json.
///
/// What it saves:
///   - Active scene name and player position
///   - Player HP
///   - Canonical player mana balance, capacity, and transaction history
///   - NPC Wallets and the completed market transaction ledger
///   - All WorldStateManager facts
///   - Active quest instances (node positions + objective counts)
///   - Occupied inventory slots (by itemId)
///
/// Unity setup:
///   1. Add to a persistent bootstrap GameObject in your first scene
///      (alongside WorldStateManager, QuestManager, SceneLoader, ItemDatabase).
///   2. Call SaveManager.Instance.Save() from a pause menu or autosave trigger.
///   3. Call SaveManager.Instance.Load() from a main menu "Continue" button.
///   4. Use SaveManager.Instance.HasSave() to decide whether to show the button.
/// </summary>
[DisallowMultipleComponent]
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const int CurrentSaveVersion = 8;
    private const int ManaUnifiedSaveVersion = 2;
    private const string FileName = "save.json";
    private string SavePath => Path.Combine(Application.persistentDataPath, FileName);
    private string TempPath => SavePath + ".tmp";
    private string BackupPath => SavePath + ".bak";

    private bool _saveInProgress;
    private bool _loadInProgress;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public bool HasSave() => File.Exists(SavePath);

    /// <summary>
    /// When false, Save() is a no-op. Set by SceneRulesManager for scenes where
    /// saving should be blocked (dungeons, boss arenas, cutscenes, etc.).
    /// </summary>
    public bool SaveEnabled { get; set; } = true;

    /// <summary>Collects all game state and writes it to disk as JSON.</summary>
    public void Save()
    {
        if (!SaveEnabled) return;
        if (_saveInProgress)
        {
            Debug.LogWarning("[SaveManager] Save already in progress; skipping.");
            return;
        }

        _saveInProgress = true;
        try
        {
            SaveData data = CollectSaveData();
            string json = JsonUtility.ToJson(data, prettyPrint: true);

            string dir = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Serialize the complete save to a temp file before touching the real one.
            File.WriteAllText(TempPath, json);

            // Keep the previous good save as a backup, then replace the main file.
            if (File.Exists(SavePath))
            {
                try { File.Copy(SavePath, BackupPath, overwrite: true); }
                catch (Exception e) { Debug.LogWarning($"[SaveManager] Backup copy failed: {e.Message}"); }
            }

            File.Copy(TempPath, SavePath, overwrite: true);
            File.Delete(TempPath);
            Debug.Log($"[SaveManager] Saved → {SavePath}");
        }
        finally
        {
            _saveInProgress = false;
        }
    }

    /// <summary>Collects the current game state without writing to disk.</summary>
    private SaveData CollectSaveData()
    {
        var data = new SaveData();
        data.saveVersion = CurrentSaveVersion;
        data.currentScene = SceneManager.GetActiveScene().name;

        // Player position + stats
        var player = FindAnyObjectByType<PlayerControllerBase>();
        if (player != null)
        {
            data.playerX = player.transform.position.x;
            data.playerY = player.transform.position.y;

            PositionModel position = GameSessionHost.Session?.PlayerPosition;
            if (position != null)
            {
                data.hasPlayerCell = true;
                data.playerCellX = position.CellX;
                data.playerCellY = position.CellY;
                data.playerOffsetX = position.OffsetX;
                data.playerOffsetY = position.OffsetY;
            }

            if (player.TryGetComponent<EntityStats>(out var stats))
            {
                data.playerHp    = stats.Hp;
                data.playerMp    = stats.Mp;
                data.playerMaxHp = stats.MaxHp;
                data.playerMaxMp = stats.MaxMp;
            }

            if (player.TryGetComponent<Wallet>(out var wallet))
                data.wallet = wallet.GetSaveData();
        }

        if (WorldTravelState.Instance != null)
        {
            if (player != null)
                WorldTravelState.Instance.RememberTravelerPosition(player.transform);
            data.activeWorld = WorldTravelState.Instance.CurrentWorld.ToString();
            WorldTravelState.Instance.WritePositions(data.worldPositions);
            WorldTravelState.Instance.WriteAbilities(data.worldAbilities);
        }

        // World facts
        if (WorldStateManager.Instance != null)
        {
            foreach (var kv in WorldStateManager.Instance.GetSnapshot())
                data.worldFacts.Add(SerializeFact(kv.Key, kv.Value));
        }

        // Active quests
        if (QuestManager.Instance != null)
            data.activeQuests = QuestManager.Instance.GetSaveData();

        // Inventory — only occupied slots, keyed by itemId
        var inv = InventoryUI.Model;
        if (InventoryUI.Instance != null)
        {
            WriteInventory(
                InventoryUI.Instance.GetInventoryForWorld(WorldLayer.WorldA),
                data.worldAInventorySlots);
            WriteInventory(
                InventoryUI.Instance.GetInventoryForWorld(WorldLayer.WorldB),
                data.worldBInventorySlots);
        }
        if (inv != null)
        {
            // Retained as an active-inventory compatibility snapshot for older builds.
            WriteInventory(inv, data.inventorySlots);
        }

        // NPCs — position, stats (enemies), and inventory (vendors/loot)
        // Equipped items are owned outside the inventory grid.
        int equipmentBonusHp = 0;
        int equipmentBonusMp = 0;
        if (player != null && player.TryGetComponent(out EquipmentManager equipment))
        {
            foreach (EquipSlotType slotType in Enum.GetValues(typeof(EquipSlotType)))
            {
                ItemData equippedItem = equipment.Model.GetEquipped(slotType) as ItemData;
                if (equippedItem != null)
                {
                    data.playerEquipment.Add(new EquipmentSaveEntry
                    {
                        slot = slotType.ToString(),
                        itemId = equippedItem.itemId,
                    });
                    equipmentBonusHp += equippedItem.bonusMaxHp;
                    equipmentBonusMp += equippedItem.bonusMaxMp;
                }
            }
        }

        // Base maximums exclude equipment bonuses; the final maximums above include them.
        data.playerBaseMaxHp = Mathf.Max(0, data.playerMaxHp - equipmentBonusHp);
        data.playerBaseMaxMp = Mathf.Max(0, data.playerMaxMp - equipmentBonusMp);

        // Keyring — key items never occupy inventory slots.
        if (PlayerKeyring.Instance != null)
        {
            foreach (var entry in PlayerKeyring.Instance.GetEntries())
            {
                data.playerKeys.Add(new KeyringSaveEntry
                {
                    itemId = entry.Key,
                    quantity = entry.Value,
                });
            }
        }

        // Pending (undelivered) quest rewards.
        if (PendingRewardManager.Instance != null)
            data.pendingRewards = PendingRewardManager.Instance.GetSaveData();

        foreach (var npc in FindObjectsByType<NpcController>())
        {
            var entry = new NpcSaveEntry
            {
                npcId = npc.NpcId,
                x     = npc.transform.position.x,
                y     = npc.transform.position.y,
            };

            if (npc.Stats != null)
            {
                entry.hasStats = true;
                entry.hp       = npc.Stats.Hp;
                entry.mp       = npc.Stats.Mp;
                entry.maxHp    = npc.Stats.MaxHp;
                entry.maxMp    = npc.Stats.MaxMp;
            }

            // Model-backed NPCs (NpcStateView) are saved from the pure-C# model, not the
            // MonoBehaviour: stable npcId + HP + logical cell.
            var stateView = npc.GetComponent<NpcStateView>();
            if (stateView != null && GameSessionHost.Session != null &&
                GameSessionHost.Session.NpcStates.TryCapture(stateView.NpcId, out NpcStateSnapshot snapshot))
            {
                entry.hasModelState = true;
                entry.cellX = snapshot.CellX;
                entry.cellY = snapshot.CellY;
                entry.hasStats = true;
                entry.hp = snapshot.Hp;
                entry.maxHp = snapshot.MaxHp;
            }

            // Home schedule (NpcSchedule3D) is saved from the pure-C# model too.
            if (GameSessionHost.Session != null &&
                GameSessionHost.Session.NpcSchedules.TryCapture(npc.NpcId, out NpcScheduleSnapshot schedule))
            {
                entry.hasSchedule = true;
                entry.schedulePhase = (int)schedule.Phase;
                entry.scheduleSeconds = schedule.SecondsRemaining;
            }

            if (npc.Inventory != null)
            {
                for (int i = 0; i < npc.Inventory.SlotCount; i++)
                {
                    var slot = npc.Inventory.GetSlot(i);
                    if (!slot.IsEmpty)
                        entry.inventorySlots.Add(new InventorySlotEntry
                        {
                            slotIndex = i,
                            itemId    = slot.item.ItemId,
                            quantity  = slot.quantity,
                        });
                }
            }

            if (npc.ManaWallet != null)
                entry.wallet = npc.ManaWallet.GetSaveData();

            data.npcStates.Add(entry);
        }
        // Hotbar
        if (HotbarUI.Model != null)
        {
            for (int i = 0; i < HotbarModel.SlotCount; i++)
            {
                var item = HotbarUI.Model.GetSlot(i);
                if (item != null)
                    data.hotbarSlots.Add(new HotbarEntry { slotIndex = i, itemId = item.ItemId });
            }
        }
        data.marketTransactions = TradeService.GetSaveData();
        return data;
    }

    /// <summary>
    /// Reads and validates the save file completely before applying any state, then restores
    /// world-state and quest data immediately and loads the saved scene. Falls back to the backup
    /// when the main file is corrupt. Returns false (and changes nothing) on failure.
    /// </summary>
    public bool Load()
    {
        if (_loadInProgress)
        {
            Debug.LogWarning("[SaveManager] Load already in progress.");
            return false;
        }

        _loadInProgress = true;
        try
        {
            if (!TryReadValidatedSave(out SaveData data, out string error))
            {
                Debug.LogError($"[SaveManager] Load failed: {error}");
                return false;
            }

            ApplyLoadedData(data);
            return true;
        }
        finally
        {
            _loadInProgress = false;
        }
    }

    private void ApplyLoadedData(SaveData data)
    {
        TradeService.LoadSaveData(data.marketTransactions);

        if (WorldTravelState.Instance != null)
        {
            WorldTravelState.Instance.LoadSharedPlayerState(BuildWalletSaveDataForLoad(data));
            WorldTravelState.Instance.LoadAbilities(data.worldAbilities);
            WorldTravelState.Instance.LoadState(data.activeWorld, data.worldPositions);
        }

        // Restore world facts before the scene loads so quest conditions are
        // already correct when newly-placed triggers evaluate on Awake/Start.
        if (WorldStateManager.Instance != null)
        {
            var snapshot = new Dictionary<string, object>();
            foreach (var fe in data.worldFacts)
                snapshot[fe.key] = DeserializeFact(fe);
            WorldStateManager.Instance.LoadSnapshot(snapshot);
        }

        // Restore quest instances (no onEnterActions re-fired)
        QuestManager.Instance?.LoadSaveData(data.activeQuests);

        // Everything that depends on scene objects is deferred until after load
        Action callback = null;
        callback = () =>
        {
            SceneLoader.Instance.OnLoadComplete -= callback;
            RestoreSceneState(data);
        };
        SceneLoader.Instance.OnLoadComplete += callback;
        SceneLoader.Instance.LoadScene(data.currentScene);
    }

    // ── Read + validation ─────────────────────────────────────────────────────

    private bool TryReadValidatedSave(out SaveData data, out string error)
    {
        data = null;
        error = null;

        if (!File.Exists(SavePath))
        {
            error = "No save file found.";
            return false;
        }

        string[] candidates = File.Exists(BackupPath)
            ? new[] { SavePath, BackupPath }
            : new[] { SavePath };

        SaveData parsed = null;
        string parseError = null;
        string usedPath = null;

        foreach (string path in candidates)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                parsed = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                usedPath = path;
                break;
            }
            catch (Exception e)
            {
                parseError = e.Message;
                parsed = null;
            }
        }

        if (parsed == null)
        {
            error = parseError != null
                ? $"Save file is corrupt and could not be read ({parseError})."
                : "Save file is corrupt and could not be read.";
            return false;
        }

        if (usedPath == BackupPath)
            Debug.LogWarning("[SaveManager] Main save was corrupt; recovered from backup.");

        NormalizeSave(parsed);

        string validation = ValidateSave(parsed);
        if (validation != null)
        {
            // A main file that parses but fails validation may still have a usable backup.
            if (usedPath == SavePath && File.Exists(BackupPath))
            {
                SaveData backup = null;
                try { backup = JsonUtility.FromJson<SaveData>(File.ReadAllText(BackupPath)); }
                catch { backup = null; }
                if (backup != null)
                {
                    NormalizeSave(backup);
                    if (ValidateSave(backup) == null)
                    {
                        Debug.LogWarning("[SaveManager] Main save failed validation; recovered from backup.");
                        data = backup;
                        return true;
                    }
                }
            }

            error = validation;
            return false;
        }

        data = parsed;
        return true;
    }

    // Fills in any collection the deserializer left null so downstream restore code cannot NPE.
    private static void NormalizeSave(SaveData data)
    {
        if (data == null) return;
        data.worldPositions ??= new();
        data.worldAbilities ??= new();
        data.worldFacts ??= new();
        data.activeQuests ??= new();
        data.inventorySlots ??= new();
        data.worldAInventorySlots ??= new();
        data.worldBInventorySlots ??= new();
        data.playerKeys ??= new();
        data.playerEquipment ??= new();
        data.pendingRewards ??= new();
        data.npcStates ??= new();
        data.hotbarSlots ??= new();
        data.marketTransactions ??= new();
        data.wallet ??= new WalletSaveData();
        data.wallet.transactions ??= new();
    }

    private static string ValidateSave(SaveData data)
    {
        if (data == null)
            return "Save data is null.";
        if (data.saveVersion < 0 || data.saveVersion > CurrentSaveVersion)
            return $"Unsupported save version {data.saveVersion}.";
        if (string.IsNullOrWhiteSpace(data.currentScene))
            return "Save is missing the current scene.";
        if (!SceneAvailable(data.currentScene))
            return $"Saved scene '{data.currentScene}' is not available.";
        if (data.playerMaxHp < 0 || data.playerHp < 0 || data.playerMaxMp < 0 || data.playerMp < 0)
            return "Save contains negative stat values.";
        return null;
    }

    private static bool SceneAvailable(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).name == sceneName)
                return true;

        try { return Application.CanStreamedLevelBeLoaded(sceneName); }
        catch { return false; }
    }

    // ── Scene-dependent restore ───────────────────────────────────────────────

    private void RestoreSceneState(SaveData data)
    {
        // Player position + stats
        var player = FindAnyObjectByType<PlayerControllerBase>();
        if (player != null)
        {
            ApplyPlayerPosition(player, data);

            if (player.TryGetComponent<EntityStats>(out var stats))
            {
                // Restore the base max HP first; the equipment pass re-applies bonuses without
                // healing, and the final current HP is set after that pass.
                ComputeBaseMax(data, out int baseHp, out int baseMp);
                stats.SetMaxHp(baseHp);
            }

            if (player.TryGetComponent<Wallet>(out var wallet))
                wallet.LoadSaveData(BuildWalletSaveDataForLoad(data));

            WorldTravelState.Instance?.CaptureSharedPlayerState(player.transform);
        }

        // Keyring
        PlayerKeyring keyring = PlayerKeyring.GetOrCreate();
        keyring.Clear();
        if (data.playerKeys != null)
        {
            foreach (KeyringSaveEntry entry in data.playerKeys)
            {
                ItemData key = ItemDatabase.Instance?.Get(entry.itemId);
                if (key != null && (key.flags & ItemFlags.KeyItem) != 0)
                    keyring.AddKey(key, entry.quantity);
            }
        }

        // Pending (undelivered) quest rewards.
        if (PendingRewardManager.Instance != null)
            PendingRewardManager.Instance.LoadSaveData(data.pendingRewards);

        // Inventory
        var inv = InventoryUI.Model;
        if (InventoryUI.Instance != null)
        {
            InventoryModel worldA = InventoryUI.Instance.GetInventoryForWorld(WorldLayer.WorldA);
            InventoryModel worldB = InventoryUI.Instance.GetInventoryForWorld(WorldLayer.WorldB);
            ClearInventory(worldA);
            ClearInventory(worldB);

            if (data.saveVersion >= 5)
            {
                RestoreInventory(worldA, data.worldAInventorySlots, keyring, data.saveVersion);
                RestoreInventory(worldB, data.worldBInventorySlots, keyring, data.saveVersion);
            }
            else
            {
                RestoreInventory(inv, data.inventorySlots, keyring, data.saveVersion);
            }

            worldA.ForceRefresh();
            worldB.ForceRefresh();
        }

        // NPCs — restore position, stats, and inventory
        // Restore equipment after base stats and inventory so bonuses apply once, WITHOUT the normal
        // equip heal / mana fill (those values are restored from the save afterward).
        if (player != null && player.TryGetComponent(out EquipmentManager equipment))
        {
            foreach (EquipSlotType slotType in Enum.GetValues(typeof(EquipSlotType)))
                equipment.Unequip(slotType);

            equipment.SetRestoring(true);
            try
            {
                if (data.playerEquipment != null)
                {
                    foreach (EquipmentSaveEntry entry in data.playerEquipment)
                    {
                        if (!Enum.TryParse(entry.slot, out EquipSlotType slotType))
                            continue;

                        ItemData item = ItemDatabase.Instance?.Get(entry.itemId);
                        WorldLayer activeWorld = WorldTravelState.Instance != null
                            ? WorldTravelState.Instance.CurrentWorld
                            : WorldLayer.WorldA;
                        if (item == null || !item.IsEquip || item.equipSlot != slotType ||
                            !item.IsAvailableInWorld(activeWorld))
                        {
                            Debug.LogWarning($"[SaveManager] Invalid equipped item '{entry.itemId}' for slot '{entry.slot}'.");
                            continue;
                        }

                        equipment.Equip(slotType, item);
                    }
                }
            }
            finally
            {
                equipment.SetRestoring(false);
            }
        }

        // Final current HP/MP, clamped to the recalculated (base + bonus) maximums.
        if (player != null && player.TryGetComponent<EntityStats>(out var finalStats))
        {
            finalStats.SetHp(data.playerHp);
        }

        if (data.npcStates != null && data.npcStates.Count > 0)
        {
            // Build a lookup by npcId for O(1) access
            var npcLookup = new Dictionary<string, NpcController>();
            foreach (var npc in FindObjectsByType<NpcController>())
                npcLookup[npc.NpcId] = npc;

            foreach (var entry in data.npcStates)
            {
                if (!npcLookup.TryGetValue(entry.npcId, out var npc))
                {
                    Debug.LogWarning($"[SaveManager] NPC not found in scene: '{entry.npcId}'");
                    continue;
                }

                // Home schedule (NpcSchedule3D): restore phase/timer before the body is placed.
                if (entry.hasSchedule && GameSessionHost.Session != null)
                {
                    GameSessionHost.Session.NpcSchedules.Apply(
                        new NpcScheduleSnapshot(entry.npcId, (NpcSchedulePhase)entry.schedulePhase, entry.scheduleSeconds));
                }

                // Position + health. Model-backed NPCs restore through the pure-C# model (the bound
                // NpcStateView then refreshes the transform and EntityStats); everything else uses
                // the legacy scene-transform path. Inventory and wallet are restored afterwards for
                // BOTH kinds so a model-backed NPC still gets its items and mana back.
                if (entry.hasModelState && GameSessionHost.Session != null)
                {
                    GameSessionHost.Session.NpcStates.Apply(
                        new NpcStateSnapshot(entry.npcId, entry.hp, entry.maxHp, entry.cellX, entry.cellY));
                }
                else
                {
                    npc.transform.position = new Vector3(entry.x, entry.y, 0f);

                    if (entry.hasStats && npc.Stats != null)
                    {
                        npc.Stats.Configure(entry.maxHp, entry.maxMp);
                        npc.Stats.SetHp(entry.hp);
                        npc.Stats.SetMp(entry.mp);
                    }
                }

                RestoreNpcInventory(npc, entry);
                RestoreNpcWallet(npc, entry);
            }
        }

        // Hotbar
        if (HotbarUI.Model != null && data.hotbarSlots != null)
        {
            for (int i = 0; i < HotbarModel.SlotCount; i++)
                HotbarUI.ClearSlot(i);

            foreach (var entry in data.hotbarSlots)
            {
                var item = ItemDatabase.Instance?.Get(entry.itemId);
                WorldLayer activeWorld = WorldTravelState.Instance != null
                    ? WorldTravelState.Instance.CurrentWorld
                    : WorldLayer.WorldA;
                if (item != null && item.IsAvailableInWorld(activeWorld))
                    HotbarUI.AssignSlot(entry.slotIndex, item);
            }
        }

        Debug.Log("[SaveManager] Scene state restored.");
    }

    // Restores one NPC's saved inventory. Validates every slot index and item id independently so a
    // single corrupt or missing entry cannot abort the remaining entries.
    private static void RestoreNpcInventory(NpcController npc, NpcSaveEntry entry)
    {
        if (entry == null || entry.inventorySlots == null || npc == null || npc.Inventory == null)
            return;

        for (int i = 0; i < npc.Inventory.SlotCount; i++)
            npc.Inventory.GetSlot(i).Clear();

        foreach (var slotEntry in entry.inventorySlots)
        {
            if (slotEntry == null)
                continue;

            if (slotEntry.slotIndex < 0 || slotEntry.slotIndex >= npc.Inventory.SlotCount)
            {
                Debug.LogWarning($"[SaveManager] NPC '{entry.npcId}': invalid inventory slot index {slotEntry.slotIndex}.");
                continue;
            }

            ItemData item = ItemDatabase.Instance != null
                ? ItemDatabase.Instance.Get(slotEntry.itemId)
                : null;
            if (item == null)
            {
                Debug.LogWarning($"[SaveManager] NPC '{entry.npcId}': item not found '{slotEntry.itemId}'");
                continue;
            }

            npc.Inventory.GetSlot(slotEntry.slotIndex).Set(item, Mathf.Max(1, slotEntry.quantity));
        }

        npc.Inventory.ForceRefresh();
    }

    private static void RestoreNpcWallet(NpcController npc, NpcSaveEntry entry)
    {
        if (entry == null || entry.wallet == null || npc == null || npc.ManaWallet == null)
            return;

        npc.ManaWallet.LoadSaveData(entry.wallet);
    }

    private static void WriteInventory(
        InventoryModel inventory,
        List<InventorySlotEntry> destination)
    {
        if (inventory == null || destination == null) return;
        destination.Clear();
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);
            if (!slot.IsEmpty)
            {
                destination.Add(new InventorySlotEntry
                {
                    slotIndex = i,
                    itemId = slot.item.ItemId,
                    quantity = slot.quantity,
                });
            }
        }
    }

    private static void ClearInventory(InventoryModel inventory)
    {
        if (inventory == null) return;
        for (int i = 0; i < inventory.SlotCount; i++)
            inventory.GetSlot(i).Clear();
    }

    private static void RestoreInventory(
        InventoryModel inventory,
        List<InventorySlotEntry> entries,
        PlayerKeyring keyring,
        int saveVersion)
    {
        if (inventory == null || entries == null) return;
        for (int i = 0; i < entries.Count; i++)
        {
            InventorySlotEntry entry = entries[i];
            ItemData item = ItemDatabase.Instance != null
                ? ItemDatabase.Instance.Get(entry.itemId)
                : null;

            if (item == null)
            {
                Debug.LogWarning($"[SaveManager] Item not found in ItemDatabase: '{entry.itemId}'");
                continue;
            }

            if ((item.flags & ItemFlags.KeyItem) != 0)
            {
                if (saveVersion < 4 && !keyring.HasKey(item.itemId))
                    keyring.AddKey(item, entry.quantity);
                continue;
            }

            if (entry.slotIndex < 0 || entry.slotIndex >= inventory.SlotCount)
            {
                Debug.LogWarning($"[SaveManager] Invalid inventory slot {entry.slotIndex} for '{entry.itemId}'.");
                continue;
            }

            if (!inventory.Accepts(item))
            {
                Debug.LogWarning($"[SaveManager] Item '{entry.itemId}' does not belong in this world inventory.");
                continue;
            }

            inventory.GetSlot(entry.slotIndex).Set(item, entry.quantity);
        }
    }

    // ── Serialization helpers ─────────────────────────────────────────────────

    private static FactEntry SerializeFact(string key, object value)
    {
        return value switch
        {
            bool  b => new FactEntry { key = key, value = b.ToString(), type = "bool" },
            int   i => new FactEntry { key = key, value = i.ToString(), type = "int" },
            float f => new FactEntry { key = key, value = f.ToString(System.Globalization.CultureInfo.InvariantCulture), type = "float" },
            _       => new FactEntry { key = key, value = value?.ToString() ?? "", type = "string" },
        };
    }

    private static object DeserializeFact(FactEntry fe)
    {
        return fe.type switch
        {
            "bool"  => bool.Parse(fe.value),
            "int"   => int.Parse(fe.value),
            "float" => float.Parse(fe.value, System.Globalization.CultureInfo.InvariantCulture),
            _       => fe.value,
        };
    }

    /// <summary>
    /// Convert a pre-unification save into one canonical mana account. Old saves owned both
    /// wallet currency and MP, so migration preserves their combined value and expands capacity
    /// when necessary rather than silently deleting either resource.
    /// </summary>
    /// <summary>
    /// Restores the player's position from the save. v7 saves store a grid cell + local offset;
    /// older saves store only world floats, which are converted through the scene Grid so they do
    /// not land at the origin.
    /// </summary>
    private static void ApplyPlayerPosition(PlayerControllerBase player, SaveData data)
    {
        GameSession session = GameSessionHost.Session;

        if (data.hasPlayerCell && session != null)
        {
            PositionModel model = session.GetOrCreatePlayerPosition(
                data.playerCellX, data.playerCellY, data.playerOffsetX, data.playerOffsetY);
            model.Set(data.playerCellX, data.playerCellY, data.playerOffsetX, data.playerOffsetY);
            return;
        }

        var legacy = new Vector3(data.playerX, data.playerY, 0f);
        Grid grid = FindAnyObjectByType<Grid>();
        if (session != null && grid != null)
        {
            Vector3Int cell = grid.WorldToCell(legacy);
            Vector3 center = grid.GetCellCenterWorld(cell);
            float offsetX = legacy.x - center.x;
            float offsetY = legacy.y - center.y;
            PositionModel model = session.GetOrCreatePlayerPosition(cell.x, cell.y, offsetX, offsetY);
            model.Set(cell.x, cell.y, offsetX, offsetY);
            return;
        }

        player.transform.position = legacy;
    }

    private static WalletSaveData BuildWalletSaveDataForLoad(SaveData data)
    {
        if (data.saveVersion >= ManaUnifiedSaveVersion && data.wallet != null)
            return data.wallet;

        var migrated = new WalletSaveData();
        int legacyWalletBalance = Mathf.Max(0, data.wallet?.balance ?? 0);
        int legacyMp = Mathf.Max(0, data.playerMp);
        long combinedLong = (long)legacyWalletBalance + legacyMp;
        int combinedBalance = combinedLong > int.MaxValue
            ? int.MaxValue
            : (int)combinedLong;

        migrated.balance = combinedBalance;
        migrated.capacity = Mathf.Max(
            Mathf.Max(0, data.playerMaxMp),
            combinedBalance);

        if (data.wallet?.transactions != null)
        {
            foreach (var transaction in data.wallet.transactions)
            {
                if (transaction != null)
                    migrated.transactions.Add(transaction.Clone());
            }
        }

        if (legacyMp > 0)
        {
            migrated.transactions.Add(new WalletTransaction
            {
                transactionId = Guid.NewGuid().ToString("N"),
                utcTimestamp = DateTime.UtcNow.ToString("O"),
                type = WalletTransactionType.Migration,
                amount = legacyMp,
                balanceAfter = combinedBalance,
                reason = "Merged legacy EntityStats MP into canonical mana",
                referenceId = "save.v1.player_mp",
            });
        }

        return migrated;
    }

    /// <summary>
    /// Resolves the player's base (bonus-free) maximum HP/MP for the current save version. v8+ saves
    /// store the base directly; older saves only stored final maximums, so the saved equipment's
    /// bonuses are subtracted back out (the equipment pass re-applies them without healing).
    /// </summary>
    private static void ComputeBaseMax(SaveData data, out int baseHp, out int baseMp)
    {
        if (data.saveVersion >= 8)
        {
            baseHp = Mathf.Max(0, data.playerBaseMaxHp);
            baseMp = Mathf.Max(0, data.playerBaseMaxMp);
            return;
        }

        int bonusHp = 0;
        int bonusMp = 0;
        if (data.playerEquipment != null)
        {
            foreach (var entry in data.playerEquipment)
            {
                if (entry == null) continue;
                ItemData item = ItemDatabase.Instance != null ? ItemDatabase.Instance.Get(entry.itemId) : null;
                if (item != null)
                {
                    bonusHp += item.bonusMaxHp;
                    bonusMp += item.bonusMaxMp;
                }
            }
        }

        baseHp = Mathf.Max(0, data.playerMaxHp - bonusHp);
        baseMp = Mathf.Max(0, data.playerMaxMp - bonusMp);
    }
}
