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

    private const int CurrentSaveVersion = 7;
    private const int ManaUnifiedSaveVersion = 2;
    private const string FileName = "save.json";
    private string SavePath => Path.Combine(Application.persistentDataPath, FileName);

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
        var data = new SaveData();
        data.saveVersion = CurrentSaveVersion;
        data.currentScene = SceneManager.GetActiveScene().name;

        // Player position + stats
        var player = FindAnyObjectByType<PlayerController2D>();
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
                }
            }
        }

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
        File.WriteAllText(SavePath, JsonUtility.ToJson(data, prettyPrint: true));
        Debug.Log($"[SaveManager] Saved → {SavePath}");
    }

    /// <summary>
    /// Reads the save file, restores world-state and quest data immediately,
    /// then loads the saved scene and restores the player + inventory once it is ready.
    /// </summary>
    public void Load()
    {
        if (!HasSave())
        {
            Debug.LogWarning("[SaveManager] No save file found.");
            return;
        }

        var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
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

    // ── Scene-dependent restore ───────────────────────────────────────────────

    private void RestoreSceneState(SaveData data)
    {
        // Player position + stats
        var player = FindAnyObjectByType<PlayerController2D>();
        if (player != null)
        {
            ApplyPlayerPosition(player, data);

            if (player.TryGetComponent<EntityStats>(out var stats))
            {
                stats.Configure(data.playerMaxHp, data.playerMaxMp);
                stats.SetMaxHp(data.playerMaxHp);
                stats.SetHp(data.playerHp);
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
        // Restore equipment after base stats and inventory so bonuses apply once.
        if (player != null && player.TryGetComponent(out EquipmentManager equipment))
        {
            foreach (EquipSlotType slotType in Enum.GetValues(typeof(EquipSlotType)))
                equipment.Unequip(slotType);

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

                // Model-backed NPCs restore through the pure-C# model; the bound NpcStateView
                // refreshes the transform and EntityStats from the model.
                if (entry.hasModelState && GameSessionHost.Session != null)
                {
                    GameSessionHost.Session.NpcStates.Apply(
                        new NpcStateSnapshot(entry.npcId, entry.hp, entry.maxHp, entry.cellX, entry.cellY));
                    continue;
                }

                npc.transform.position = new Vector3(entry.x, entry.y, 0f);

                if (entry.hasStats && npc.Stats != null)
                {
                    npc.Stats.Configure(entry.maxHp, entry.maxMp);
                    npc.Stats.SetHp(entry.hp);
                    npc.Stats.SetMp(entry.mp);
                }

                if (entry.inventorySlots != null && npc.Inventory != null)
                {
                    for (int i = 0; i < npc.Inventory.SlotCount; i++)
                        npc.Inventory.GetSlot(i).Clear();

                    foreach (var slotEntry in entry.inventorySlots)
                    {
                        var item = ItemDatabase.Instance != null
                            ? ItemDatabase.Instance.Get(slotEntry.itemId)
                            : null;

                        if (item == null)
                        {
                            Debug.LogWarning($"[SaveManager] NPC '{entry.npcId}': item not found '{slotEntry.itemId}'");
                            continue;
                        }
                        npc.Inventory.GetSlot(slotEntry.slotIndex).Set(item, slotEntry.quantity);
                    }
                    npc.Inventory.ForceRefresh();
                }

                if (entry.wallet != null && npc.ManaWallet != null)
                    npc.ManaWallet.LoadSaveData(entry.wallet);
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
    private static void ApplyPlayerPosition(PlayerController2D player, SaveData data)
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
}
