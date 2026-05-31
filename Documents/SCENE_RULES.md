# Scene Rules System

## Overview

The Scene Rules system lets you define per-scene gameplay overrides — disabling inventory access, toggling combat and attacks, locking movement, applying damage/heal over time, and more — without touching individual entity scripts. Each scene owns a `SceneRules` asset that is applied automatically when the scene loads and cleaned up when it unloads.

Rules are layered in priority order (highest first):

1. **Individual Setters** — direct calls from scripts, items, or quests.
2. **Zone Overrides** — pushed/popped at runtime by `RuleZone2D`; top of stack wins.
3. **Base Rules** — the `SceneRules` asset assigned in the Inspector; applied at scene load.

---

## Files

| File | Description |
|---|---|
| `Assets/Scripts/GameManagement/SceneRules.cs` | ScriptableObject with all rule fields |
| `Assets/Scripts/GameManagement/SceneRulesManager.cs` | MonoBehaviour that applies, restores, and exposes a runtime API |
| `Assets/Scripts/GameManagement/RuleZone2D.cs` | Trigger zone that pushes/pops an override onto the rule stack |

---

## SceneRules (ScriptableObject)

**Create:** Assets → Create → **Game → Scene Rules**

### Inspector Fields

#### Inventory
| Field | Default | Description |
|---|---|---|
| Lock Inventory | false | Prevents the player from opening or toggling the inventory panel. Programmatic `Close()` / `Open()` calls still work. |
| Disable Saving | false | `SaveManager.Save()` becomes a no-op while in this scene. |

#### Player Combat
| Field | Default | Description |
|---|---|---|
| Player Invincible | false | Sets `CombatReceiver.Invincible = true` — all incoming hits are ignored. |
| Player Damage Multiplier | 1.0 | Multiplies all damage the player receives. `2.0` = double, `0.5` = half. |
| Disable Player Attack | false | Disables the player's `CombatAttacker` component. |

#### NPC Combat
| Field | Default | Description |
|---|---|---|
| Enemies Invincible | false | Sets `Invincible = true` on all enemy `CombatReceiver` components. |
| Enemy Damage Multiplier | 1.0 | Multiplies all damage enemy NPCs receive. |
| Disable NPC Attack | false | Disables the `CombatAttacker` component on all enemy NPCs. |

#### Damage Over Time
| Field | Default | Description |
|---|---|---|
| Dot Enabled | false | Enables periodic damage ticks while in this scene. |
| Dot Damage Per Tick | 5 | Raw damage applied each tick. |
| Dot Interval | 2.0 | Seconds between damage ticks. |
| Dot Affects Player | true | DOT targets the player. |
| Dot Affects Enemies | false | DOT targets all enemy NPCs. |

> DOT calls `ReceiveHit()` directly. If the target is `Invincible`, the hit is still blocked.

#### Heal Over Time
| Field | Default | Description |
|---|---|---|
| Hot Enabled | false | Enables periodic heal ticks while in this scene. |
| Hot Heal Per Tick | 5 | HP restored each tick. |
| Hot Interval | 2.0 | Seconds between heal ticks. |
| Hot Affects Player | true | HOT targets the player. |
| Hot Affects Enemies | false | HOT targets all enemy NPCs. |

#### Movement
| Field | Default | Description |
|---|---|---|
| Lock Player Movement | false | Calls `SetMovementEnabled(false)` on the player. |
| Player Speed Multiplier | 1.0 | Multiplies the player's `MoveSpeed`. `0.5` = half speed, `2.0` = double. |
| Lock NPC Movement | false | Calls `SetMovementEnabled(false)` on every NPC in the scene. |

#### NPC Behavior
| Field | Default | Description |
|---|---|---|
| Force NPC Behavior State | false | Overrides every NPC's behavior state to `Forced NPC State`. |
| Forced NPC State | Idle | The `NpcBehaviorState` to force when the above is enabled. |
| Disable NPC Dialogue | false | Disables all `NpcDialogue` components — NPCs cannot be talked to. |
| Block Portals | false | Disables all `PortalTrigger2D` components — the player cannot use portals. |
| NPC Aggro Range Multiplier | 1.0 | Multiplies the aggro range of all enemy NPCs. `0.25` = near-blind, `2.0` = twice the radius. |

#### Player Death
| Field | Default | Description |
|---|---|---|
| Player Death Behavior | Default | What happens when the player dies in this scene (see below). |
| Death Scene | "" | Scene to load when `Player Death Behavior` is `SendToScene`. |

**PlayerDeathBehavior values:**

| Value | Effect |
|---|---|
| `Default` | Does nothing — existing subscribers handle death normally. |
| `RespawnInPlace` | Restores the player to full HP and keeps them in the scene. |
| `SendToScene` | Loads `deathScene` via `SceneLoader`. |
| `GameOver` | Loads the `"GameOver"` scene. |

---

## SceneRulesManager (MonoBehaviour)

### Unity Setup

1. Add an empty **Manager** GameObject to the scene.
2. Add the `SceneRulesManager` component.
3. Assign a `SceneRules` asset to the **Rules** field.

The manager auto-discovers the player and all NPCs at `Start`. It is also a singleton (`SceneRulesManager.Instance`).

### Behaviour

- **On Start:** gathers `PlayerController2D`, all `NpcController` instances, `CombatReceiver`/`CombatAttacker`/`NpcDialogue` components, and `PortalTrigger2D` components, then applies every enabled rule.
- **On Destroy (scene unload):** restores all changed flags to their defaults so the next scene starts clean.

### Runtime API

Call these from scripts, items, quests, or any other runtime code:

```csharp
// Override stack (used by RuleZone2D; also callable directly)
SceneRulesManager.Instance.PushOverride(rulesAsset);
SceneRulesManager.Instance.PopOverride(rulesAsset);

// Individual setters (bypass the stack; caller is responsible for reversing)
SceneRulesManager.Instance.SetInventoryLocked(true);
SceneRulesManager.Instance.SetSavingEnabled(false);
SceneRulesManager.Instance.SetPlayerInvincible(true);
SceneRulesManager.Instance.SetPlayerAttackEnabled(false);
SceneRulesManager.Instance.SetPlayerMovementEnabled(false);
SceneRulesManager.Instance.SetEnemiesInvincible(true);
SceneRulesManager.Instance.SetNpcAttackEnabled(false);
SceneRulesManager.Instance.SetNpcMovementEnabled(false);
SceneRulesManager.Instance.SetNpcDialogueEnabled(false);
SceneRulesManager.Instance.SetPortalsBlocked(true);
SceneRulesManager.Instance.SetDotEnabled(true);
SceneRulesManager.Instance.SetHotEnabled(true);

// Late-spawned NPCs
SceneRulesManager.Instance.RegisterNpc(npcController);
```

### Late-Spawned NPCs

NPCs instantiated *after* `Start` are not automatically tracked. Call `RegisterNpc(npc)` from your spawner immediately after instantiation — it adds the NPC to all internal lists and applies the current effective rules to it right away.

### Important Notes

- `InventoryUI` is `DontDestroyOnLoad`. The manager locks it via `InventoryUI.Instance.InputLocked` and unlocks it on `OnDestroy`.
- Rules are **not additive**. If two scenes each have a `SceneRulesManager`, the outgoing scene fully restores state before the incoming one applies its own rules.
- `SaveManager.SaveEnabled` is restored to `true` on destroy regardless of what value it had before — there is no "original value" tracking for it.

---

## RuleZone2D (MonoBehaviour)

A trigger zone that swaps the active rules when the player (or another tagged collider) enters or exits.

### Unity Setup

1. Add a `Collider2D` to the GameObject — `RuleZone2D` forces it to `isTrigger` automatically.
2. Set **Enter Rules** to the `SceneRules` asset to apply inside the zone.
3. Optionally set **Exit Rules** if you want a distinct rule set on exit (e.g. a "safe zone" transition). Leave it empty to simply pop the enter rules and revert to the previous layer.
4. Change **Activator Tag** if something other than `"Player"` should trigger the zone.

### Stacking

Multiple overlapping `RuleZone2D` zones work correctly. Each zone independently pushes and pops its own entry on the override stack, so the last zone entered always wins while each zone still restores correctly when exited.

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
