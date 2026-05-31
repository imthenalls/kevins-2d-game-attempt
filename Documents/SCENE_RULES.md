# Scene Rules System

## Overview

The Scene Rules system lets you define per-scene gameplay overrides — disabling inventory access, toggling combat and attacks, locking movement, and applying damage over time — without touching individual entity scripts. Each scene owns a `SceneRules` asset that is applied automatically when the scene loads and cleaned up when it unloads.

---

## Files

| File | Description |
|---|---|
| `Assets/Scripts/GameManagement/SceneRules.cs` | ScriptableObject with all rule fields |
| `Assets/Scripts/GameManagement/SceneRulesManager.cs` | MonoBehaviour that applies and restores the rules |

---

## SceneRules (ScriptableObject)

**Create:** Assets → Create → **Game → Scene Rules**

### Inspector Fields

#### Inventory
| Field | Default | Description |
|---|---|---|
| Lock Inventory | false | Prevents the player from opening or toggling the inventory panel. Programmatic `Close()` / `Open()` calls still work. |

#### Player Combat
| Field | Default | Description |
|---|---|---|
| Player Invincible | false | The player's `CombatReceiver.Invincible` is set to true — all incoming hits are ignored. |
| Disable Player Attack | false | Disables the player's `CombatAttacker` component — no attacks can be initiated. |

#### NPC Combat
| Field | Default | Description |
|---|---|---|
| Enemies Invincible | false | All enemy NPCs' `CombatReceiver.Invincible` is set to true. |
| Disable NPC Attack | false | Disables the `CombatAttacker` component on all enemy NPCs in the scene. |

#### Damage Over Time
| Field | Default | Description |
|---|---|---|
| Dot Enabled | false | Enables periodic damage ticks while in this scene. |
| Dot Damage Per Tick | 5 | Raw damage applied each tick. |
| Dot Interval | 2.0 | Seconds between damage ticks. |
| Dot Affects Player | true | DOT targets the player. |
| Dot Affects Enemies | false | DOT targets all enemy NPCs. |

> **Note:** DOT calls `ReceiveHit()` directly. If an entity also has `CombatEnabled = false` or is `Invincible`, the hit is still blocked. To have DOT bypass invincibility, the entity's Invincible flag must not be set.

#### Movement
| Field | Default | Description |
|---|---|---|
| Lock Player Movement | false | Calls `SetMovementEnabled(false)` on the player. |
| Lock NPC Movement | false | Calls `SetMovementEnabled(false)` on every NPC in the scene. |

---

## SceneRulesManager (MonoBehaviour)

### Unity Setup

1. Add an empty **Manager** GameObject to the scene.
2. Add the `SceneRulesManager` component.
3. Assign a `SceneRules` asset to the **Rules** field.

That's it — the manager auto-discovers the player and all NPCs at `Start`.

### Behaviour

- **On Start:** gathers the player (`PlayerController2D`), all `NpcController` instances, and their combat/attacker components, then applies every enabled rule.
- **On Destroy (scene unload):** restores all changed flags to their default state so the next scene starts clean.

### Important Notes

- `InventoryUI` is `DontDestroyOnLoad`. The manager locks it via `InventoryUI.Instance.InputLocked` and unlocks it on destroy.
- Rules are **not additive**. If two scenes each have a `SceneRulesManager`, the outgoing scene fully restores state before the incoming one applies its own.
- NPCs spawned *after* `Start` are not tracked. If late-spawning enemies need to respect scene rules, apply rules to them in the spawner logic.

---

## Example Scenarios

### Safe Zone (no combat, no attacks)
```
playerInvincible     = true
disablePlayerAttack  = true
enemiesInvincible    = true
disableNpcAttack     = true
```

### Poison Swamp (DOT on the player)
```
dotEnabled           = true
dotDamagePerTick     = 3
dotInterval          = 1.5
dotAffectsPlayer     = true
dotAffectsEnemies    = false
```

### Cutscene / Scripted Area (no movement, no inventory)
```
lockPlayerMovement   = true
lockNpcMovement      = true
lockInventory        = true
```
