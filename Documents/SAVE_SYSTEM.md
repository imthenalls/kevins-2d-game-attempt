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
| Player HP / MP (current + max) | `EntityStats` |
| World facts | `WorldStateDB` |
| Active quest states (node + objective counts) | `QuestManager` |
| Inventory slots (index, item, quantity) | `InventoryUI.Model` |

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
   - Player HP/MP via `EntityStats.Configure()` → `SetHp()` / `SetMp()`
   - Inventory slots (clears all first, then sets saved slots, then calls `ForceRefresh()` to update the UI)

---

## Adding New Data to the Save

1. Add a serializable field to `SaveData.cs`:

```csharp
public int playerGold;
```

2. Write to it in `SaveManager.Save()`:

```csharp
data.playerGold = PlayerWallet.Instance.Gold;
```

3. Read it back in `SaveManager.RestoreSceneState()` (for scene-dependent data) or directly in `Load()` (for persistent singletons):

```csharp
PlayerWallet.Instance.SetGold(data.playerGold);
```

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
