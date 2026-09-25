# Save System

## Overview

The save system serializes all meaningful game state to a single JSON file on disk using Unity's `JsonUtility`. It is split across two files:

| File | Purpose |
|---|---|
| `SaveData.cs` | Plain data container — all the fields written to / read from disk |
| `SaveManager.cs` | Singleton MonoBehaviour — `Save()`, `Load()`, `HasSave()` |

**What gets saved:**

| Data | Source |
|---|---|
| Current scene name | `SceneManager` |
| Player position (X, Y) | `PlayerController2D.transform` |
| Player HP | `EntityStats` |
| Canonical player mana balance, capacity, and transaction history | `Wallet` |
| Inventory-enabled NPC mana balance, capacity, and history | NPC `Wallet` |
| Completed market transaction ledger | `TradeService` |
| World facts | `WorldStateManager` |
| Active quest states (node + objective counts) | `QuestManager` |
| Inventory slots (index, item, quantity) | `InventoryUI.Model` |
| Player keyring entries | `PlayerKeyring` |
| Player equipment slots | `EquipmentManager.Model` |
| Active world and remembered world positions | `WorldTravelState` |
| World A and World B inventories | `InventoryUI` |
| Per-world avatar ability unlocks | `WorldTravelState` |

The save file is written to `Application.persistentDataPath/save.json` (on Windows this is `%APPDATA%\..\LocalLow\<Company>\<Product>\save.json`).

---

## Unity Setup

### 1. Add SaveManager to the bootstrap scene

`SaveManager` must live on a persistent GameObject — the same scene that holds `WorldStateDB`, `QuestManager`, and `SceneLoader`.

1. In the **bootstrap / persistent scene**, select the persistent manager GameObject (or create one named `SaveManager`).
2. Click **Add Component → SaveManager**.
3. That's it — `DontDestroyOnLoad` is handled automatically.

> Only one `SaveManager` should exist. The `[DisallowMultipleComponent]` attribute prevents duplicates on the same object, and the singleton guard destroys any extras that appear in later scenes.

---

### 2. Make item ids resolvable

The inventory restore resolves saved items by id through `ItemDatabase` (populated from
`StreamingAssets/items.json`), with a `Resources.Load<ItemData>` fallback. Items not found there are
skipped with a console warning.

---

### 3. Calling Save and Load

Wire these calls to your UI buttons or keyboard shortcut in any script:

```csharp
// Save
SaveManager.Instance.Save();

// Load (only if a file exists)
if (SaveManager.Instance.HasSave())
    SaveManager.Instance.Load();
```

**Example — save on pressing F5, load on F9:**

```csharp
void Update()
{
#if ENABLE_INPUT_SYSTEM
    if (Keyboard.current.f5Key.wasPressedThisFrame)
        SaveManager.Instance.Save();

    if (Keyboard.current.f9Key.wasPressedThisFrame && SaveManager.Instance.HasSave())
        SaveManager.Instance.Load();
#else
    if (Input.GetKeyDown(KeyCode.F5)) SaveManager.Instance.Save();
    if (Input.GetKeyDown(KeyCode.F9) && SaveManager.Instance.HasSave()) SaveManager.Instance.Load();
#endif
}
```

---

## How Load Works (step by step)

1. Reads `save.json` from disk and deserializes it into a `SaveData` object. If the main file is
   corrupt, falls back to `save.json.bak` (the previous good save).
2. **Validates** the save before applying anything: supported version, a scene that exists in the
   build, and non-negative stat values. An invalid save is rejected with a clear error and changes
   nothing.
3. **Immediately** restores `WorldStateManager` facts and active quest state — these need to be in
   place before any scene objects evaluate conditions on `Awake`/`Start`.
4. Registers a one-shot callback on `SceneLoader.OnLoadComplete`.
5. Calls `SceneLoader.Instance.LoadScene(data.currentScene)` — this triggers the normal fade transition.
6. Once the scene finishes loading, the callback fires and restores:
   - Player position (`transform.position` / logical grid cell)
   - Player HP from the saved **base maximum** + re-applied equipment bonuses (without healing), then the saved current HP
   - Canonical mana balance, capacity, and retained transaction history via `Wallet.LoadSaveData()`
   - Inventory slots (clears all first, then sets saved slots, then calls `ForceRefresh()` to update the UI)
   - Equipment, NPC state (position/stats/inventory/wallet), keyring, hotbar, and pending rewards

---

## Wallet Save Integration

Wallet saving is implemented. `SaveData.wallet` contains the current balance, capacity, and retained transaction records. `SaveManager.Save()` reads it from the Wallet on the Player, and `RestoreSceneState()` restores it after the saved scene loads.

### Pre-unification save migration

Save version 2 unifies player currency and MP. When loading an older save, `SaveManager`:

1. Reads the former Wallet balance.
2. Reads the former `playerMp`.
3. Adds them together so neither owned resource is discarded.
4. Expands capacity when required to hold the combined amount.
5. Adds a `Migration` transaction for the imported MP.

Legacy `playerMp` and `playerMaxMp` fields remain in `SaveData` for this migration and for diagnosing older files. New saves write the canonical Wallet values into both the Wallet snapshot and those compatibility fields.

## Equipment Save Integration

Save version 3 stores the item id assigned to each player `EquipSlotType`. Equipped items
are not duplicated in the inventory slot list. Loading restores equipment after base stats
and inventory so equipment bonuses are applied exactly once.

## Keyring Save Integration

Save version 4 stores player-owned `KeyItem` entries separately from inventory slots. Loading
an older save automatically moves any keys found in inventory slots into `PlayerKeyring`, so
the migration preserves ownership while freeing those slots.

## Two-World Save Integration

Save version 5 records `activeWorld`, remembered scene positions for both worlds, and separate
slot snapshots for the World A and World B inventories. Version 4 and older saves migrate their
single inventory into World A. See [TWO_WORLD_SYSTEM.md](TWO_WORLD_SYSTEM.md).

Save version 6 stores per-world avatar ability IDs. The shared HP and Wallet snapshot is loaded
into `WorldTravelState` before the saved scene appears, then applied to that world's avatar.

Save version 7 stores the player's logical grid position (cell + local offset) via `hasPlayerCell`,
so a save can restore the exact cell rather than converting legacy world floats.

## Equipment Bonus Restoration (version 8)

Version 8 separates the player's **base** maximum HP/MP (`playerBaseMaxHp` / `playerBaseMaxMp`) from
equipment bonuses. Previously the save stored only the *final* maximum (base + bonuses), and loading
re-applied the bonuses a second time while also healing.

Loading now:

1. Restores the base maximum.
2. Re-equips the saved items with `EquipmentManager.SetRestoring(true)`, which raises the maximums
   without healing current HP or touching the mana wallet (`EntityStats.ApplyStatBonus(healDelta: false)`).
3. Sets the saved current HP/MP once, clamped to the recalculated maximums.

Older saves (version < 8) migrate by subtracting the saved equipment's bonuses from the saved totals
to recover the base. Normal gameplay equipping is unchanged — the restore-only path is isolated behind
the `SetRestoring` flag.

## Pending Rewards

Quest rewards that cannot fit in the player's inventory are no longer dropped. `PendingRewardManager`
retains the undelivered quantity (keyed by quest + item) and a `ClaimRewards` quest action retries
delivery later. Pending rewards are stored in `SaveData.pendingRewards` and restored on load, so an
undelivered reward survives a restart. Delivery reuses `InventoryHelper.GiveItem`, so keyring routing
and item world restrictions are respected.

## Safe Save / Corruption Recovery

`Save()` serializes the full save to `save.json.tmp`, copies the previous `save.json` to
`save.json.bak`, then replaces the main file with the temp file. `Load()` validates the main file and
falls back to the backup when it is corrupt, so a failed or interrupted write never destroys the last
usable save.

## Trade Save Integration

`NpcSaveEntry.wallet` stores the Wallet for inventory-enabled NPC participants. Empty saved NPC inventories are cleared correctly on load, so selling the final item remains persistent.

NPC inventory JSON is starting state only. `NpcInventoryDatabase` seeds it before a saved scene is restored, and `SaveManager` then replaces it with the saved NPC slots. Therefore an NPC-owned gift that has already been transferred does not regenerate after loading.

`SaveData.marketTransactions` stores the newest bounded market entries from `TradeService`. Loading restores the ledger without replaying Wallet changes, item movement, trade events, or quest events.

See [WALLET.md](WALLET.md) for the balance API, transaction fields, and Unity setup.

## Adding Other Data to the Save

1. Add a serializable field or DTO to `SaveData.cs`.
2. Capture it in `SaveManager.Save()`.
3. Restore persistent-manager data in `Load()` or scene-owned data in `RestoreSceneState()`.

> **Rule of thumb:** if the data belongs to a `DontDestroyOnLoad` singleton, restore it in `Load()`. If it belongs to a scene object (player, chest, NPC), restore it in `RestoreSceneState()`.

---

## Deleting a Save

```csharp
using System.IO;

string path = Path.Combine(Application.persistentDataPath, "save.json");
if (File.Exists(path))
    File.Delete(path);
```

---

## Caveats

- **Items must be resolvable by id** — via `ItemDatabase` (from `items.json`) or the `Resources.Load` fallback. Any item not found will be skipped with a warning in the console.
- **Quest `onEnterActions` do not re-fire on load** — this is intentional. `QuestInstance.FromSave` restores node/objective state directly without replaying entry actions (which might grant items, set facts, etc. a second time).
- **Save is not automatic** — call `Save()` explicitly at checkpoints, scene transitions, or via a save menu. There is no autosave by default.
- **Only one save slot** — the file is always `save.json`. Multiple slots would require parameterizing the filename.
