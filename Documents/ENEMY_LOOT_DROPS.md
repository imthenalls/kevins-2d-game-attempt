# Enemy Loot Drops

## Overview

Enemy loot is owned by the NPC before death, not generated when the killing hit lands. An
enemy receives its configured items from `StreamingAssets/enemy_loot.json` when it starts.
When it dies, its body, equipped weapon, colliders, and HP bar disappear, and a separate
interactable loot pile appears at the death position.

Press **E** near the pile to open it beside the player inventory. Click a stack or use
**Take All** to transfer items. In scenes without a `LootContainerUI`, pressing **E**
collects every stack that fits directly instead. The pile disappears after its inventory
is empty.

## Components and scene setup

No new component or prefab must be added manually.

1. The NPC GameObject must have `NpcController` with **Npc Type = Enemy**.
2. Enemy setup already auto-adds `EntityStats` and `CombatReceiver`.
3. The scene needs the existing `InventoryUI` and `PlayerInteractionController` setup.
   `LootContainerUI` is optional; without it, E performs a direct take-all pickup.
4. `PlayerInteractionController.Interactable Layers` should include the `Interactable`
   layer. If that named layer does not exist, runtime piles use the defeated NPC's layer.

Runtime piles use a 2.25-world-unit interaction range so the player does not need to stand
directly on top of the bag.

`NpcController` prepares the inventory automatically. `CombatReceiver` spawns the pile and
hides the defeated body automatically.

## JSON configuration

Edit `Assets/StreamingAssets/enemy_loot.json`:

```json
{
  "version": 1,
  "enemyLoot": [
    {
      "npcId": "sword_guard",
      "dropName": "Sword Guard Loot",
      "items": [
        { "itemId": "gold_coin", "minQuantity": 5, "maxQuantity": 10 },
        { "itemId": "broken_sword", "minQuantity": 1, "maxQuantity": 1 }
      ]
    }
  ]
}
```

- `npcId` matches `NpcController.NpcId`.
- Every live enemy with that id receives the definition. This permits multiple guards to
  share one loot table.
- `itemId` must exist in `StreamingAssets/items.json`.
- A quantity is rolled inclusively between `minQuantity` and `maxQuantity` during NPC setup.
- Set both quantities to the same value for a guaranteed fixed amount.

The included sword guard definition owns 5-10 Gold Coins and one Broken Sword. Both items
are regular inventory items and use generated runtime icons, so no sprite assignment is
required.

## Runtime flow

1. `NpcController.Start()` calls `EnemyLootDrop.PrepareInventory(this)`.
2. The matching JSON items are added to `NpcController.Inventory`.
3. At zero HP, `CombatReceiver` stops the AI and calls `EnemyLootDrop.Spawn(npc)`.
4. The new `RuntimeEnemyLootPile` references the same `InventoryModel`.
5. `NpcController.HideDefeatedBody()` disables the corpse renderers and colliders.
6. Taking items modifies the original NPC-owned inventory. When empty, the pile closes and
   destroys itself.

## Optional Inspector loot

`EnemyLootPresenter` can still be added to an Enemy NPC to add extra `ItemData` references
on top of its JSON loot. It no longer opens the loot panel at death; the automatic pile owns
that presentation.
