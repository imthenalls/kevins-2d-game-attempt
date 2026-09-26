# NPC-Owned Inventory Gifts

## Overview

NPC gifts transfer items the NPC already owns. They do not generate new items. Starting ownership is loaded from `Assets/StreamingAssets/npc_inventories.json` into each NPC's existing `InventoryModel`.

`InventoryTransferService` moves the complete quantity atomically. If the NPC lacks the item or the player's inventory is full, neither inventory changes. Once transferred, the NPC no longer owns the item, and `SaveManager` persists that empty NPC inventory.

`KeyItem` gifts are removed from the NPC and routed into `PlayerKeyring`; they never require
or occupy a free player inventory slot. When a key gift opens the inventory, its Keyring panel
opens too so the received key is immediately visible.

## JSON Setup

The format is documented by `Assets/StreamingAssets/npc_inventories.schema.json`.

Every configured NPC needs a unique, stable `NpcController.NpcId`:

```json
{
  "version": 1,
  "npcInventories": [
    {
      "npcId": "trainer_sword_giver",
      "items": [
        { "itemId": "iron_sword", "quantity": 1 }
      ]
    }
  ]
}
```

Item ids reference definitions in `Assets/StreamingAssets/items.json`. `NpcInventoryDatabase` starts automatically, creates the configured NPC inventory through `NpcController.EnsureInventory()`, and seeds any matching NPC whose inventory is still empty (so a freshly (re)loaded or domain-reload-restored scene is seeded again, while a populated/save-restored inventory is left alone). Saved inventory replaces the starting JSON state when loading a save.

## Unity Setup

1. Select the giving NPC and assign a unique **Npc Id** matching `npc_inventories.json`.
2. Confirm the NPC has `NpcController`, `NpcDialogue`, and a `Collider2D`.
3. In `NpcDialogue`, set **Gift Item Id** to `iron_sword`.
4. Set **Gift Quantity** to `1`.
5. Enable **Open Inventory After Gift** to show the transferred sword immediately.

The configured NPC does not need **Has Inventory** enabled manually; the JSON loader calls `EnsureInventory()`.

## Runtime Flow

1. `NpcInventoryDatabase` resolves `iron_sword` through `ItemDatabase` and places one in the NPC inventory.
2. The player completes that NPC's dialogue.
3. `NpcDialogue.GiveInventoryGift` requests an atomic transfer.
4. `InventoryTransferService` verifies NPC stock and player capacity, then moves the item.
5. Both inventories notify their observers, `ItemCollected` and `ItemGifted` fire, and the player inventory opens.
6. Saving records the NPC without the sword, preventing regeneration after load.

Cancelling dialogue or walking away gives nothing. If a transfer fails, completing the conversation again can retry it.

## Current key giver

In `NewScene`, the GameObject named `generic npc` owns the unique NPC id `npc_a`. Its
`npc_a_key_gift` conversation transfers one `golden_key` from the NPC-owned inventory to
the player when the final line completes. The other copied NPCs use their own unique ids so
the JSON inventory loader cannot seed the key onto the wrong character.

## Runtime API

```csharp
int amountTransferred = npcDialogue.GiveInventoryGift(playerGameObject);
```

For transfers outside dialogue:

```csharp
GiftTransferResult result = InventoryTransferService.TryGive(
    npc.Inventory,
    InventoryUI.Model,
    swordItem,
    1,
    playerGameObject);
```
