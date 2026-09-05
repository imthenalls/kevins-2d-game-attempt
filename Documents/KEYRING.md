# Player Keyring

## Overview

`PlayerKeyring` stores all items flagged `KeyItem` outside the normal inventory grid. It has
no slot limit, so keys never consume inventory capacity. `KeyringUI` adds a **Keyring** button
while the inventory is open and displays every owned key in a white, black-text panel.

## Unity setup

No manual scene setup is required:

1. `InventoryUI` adds `PlayerKeyring` to its persistent GameObject during `Awake`.
2. `InventoryUI` creates `KeyringUI` under its Canvas and opens/closes it with the inventory.
3. Mark key definitions with `"KeyItem"` in `items.json` or `ItemFlags.KeyItem` on ItemData.
4. Use `InventoryHelper.GiveItem` or `InventoryTransferService.TryGive` for player rewards.
   Both automatically route keys to the keyring.

NPCs may still own keys in their normal `InventoryModel`; routing occurs only when ownership
is transferred to the player.

## Runtime API

```csharp
PlayerKeyring ring = PlayerKeyring.GetOrCreate();
bool owned = ring.HasKey("golden_key");
ring.AddKey(keyItem, 1);
ring.RemoveKey("golden_key", 1);

KeyringUI.Instance?.Open();
```

## Doors, quests, and saves

- `SlidingDoor` checks `PlayerKeyring` using its Required Key Id.
- Quest GiveItem/RemoveItem/HasItem actions route `KeyItem` definitions through the keyring.
- Save version 4 stores keys separately and migrates keys out of older inventory saves.
