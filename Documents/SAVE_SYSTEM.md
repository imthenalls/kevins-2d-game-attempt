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
| World facts | `WorldStateDB` |
| Active quest states (node + objective counts) | `QuestManager` |
| Inventory slots (index, item, quantity) | `InventoryUI.Model` |
| Player keyring entries | `PlayerKeyring` |
| Player equipment slots | `EquipmentManager.Model` |

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

### 2. Move ItemData assets into Resources/Items/

The inventory restore uses `Resources.Load<ItemData>("Items/<assetName>")`. Without this, items will not be found on load.

1. In the **Project window**, create the folder path `Assets/Resources/Items/` if it doesn't exist.
2. Move (or copy) every `ItemData` ScriptableObject asset into that folder.
3. The asset's **filename** (without `.asset`) is what gets saved — keep names unique and don't rename them after shipping a save.

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

1. Reads `save.json` from disk and deserializes it into a `SaveData` object.
2. **Immediately** restores `WorldStateDB` facts and active quest state — these need to be in place before any scene objects evaluate conditions on `Awake`/`Start`.
3. Registers a one-shot callback on `SceneLoader.OnLoadComplete`.
4. Calls `SceneLoader.Instance.LoadScene(data.currentScene)` — this triggers the normal fade transition.
5. Once the scene finishes loading, the callback fires and restores:
   - Player position (`transform.position`)
   - Player HP via `EntityStats.Configure()` → `SetHp()`
   - Canonical mana balance, capacity, and retained transaction history via `Wallet.LoadSaveData()`
   - Inventory slots (clears all first, then sets saved slots, then calls `ForceRefresh()` to update the UI)

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

- **Items must be in `Resources/Items/`** — see step 2 above. Any item not found there will be skipped with a warning in the console.
- **Quest `onEnterActions` do not re-fire on load** — this is intentional. `QuestInstance.FromSave` restores node/objective state directly without replaying entry actions (which might grant items, set facts, etc. a second time).
- **Save is not automatic** — call `Save()` explicitly at checkpoints, scene transitions, or via a save menu. There is no autosave by default.
- **Only one save slot** — the file is always `save.json`. Multiple slots would require parameterizing the filename.
