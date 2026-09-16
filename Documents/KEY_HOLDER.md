# Key Holders

## Overview

`IKeyHolder` is the contract for anything that owns and spends key items. Doors and other locks
resolve a key holder from the **interacting entity**, so the same lock works for the player or an
NPC without knowing which one is interacting.

Before this, `SlidingDoor` always checked the global `PlayerKeyring`. That meant an NPC could
never actually own or use a key — the door would check the player's keys instead.

## The Interface

`Assets/Scripts/GamePresentation/Inventory/IKeyHolder.cs`

```csharp
public interface IKeyHolder
{
    bool HasKey(string itemId, int quantity = 1);
    bool RemoveKey(string itemId, int quantity = 1);
    event Action OnKeysChanged;
}
```

## Implementations

| Type | Scope | Notes |
|---|---|---|
| `PlayerKeyring` | Player (persistent singleton) | Implements `IKeyHolder`; `OnKeysChanged` forwards to its existing `OnChanged` |
| NPC keyring | Per NPC | Planned for Phase 2 (`NpcKeyring`). Until then an NPC with no holder cannot open locked doors |

Assign keys to the player through the existing `PlayerKeyring.AddKey` / quest actions. Give keys
to an NPC through its own holder once it exists.

## How Doors Resolve A Holder

`SlidingDoor.ResolveKeyHolder(interactor)`:

1. Ask the interactor for an `IKeyHolder` (`GetComponentInParent<IKeyHolder>()`). This covers any
   entity that carries one.
2. If none, and the interactor is the **player** (tag `Player` or has `PlayerController2D`),
   fall back to the persistent `PlayerKeyring` singleton.
3. If none and the interactor is not the player, return `null` → the door reports `Locked`.
4. A `null` interactor (scripted calls) resolves to the player keyring.

Doors no longer reference `PlayerKeyring` for unlocking; only the player-facing **Locked Message**
preview still reads the player keyring.

## Door Results

`SlidingDoor.TryUse(interactor)` returns a `GateUseResult` so callers can react precisely:

| Result | Meaning |
|---|---|
| `Opened` | The gate is open (opened now or already open) |
| `Locked` | The required key was missing |
| `Busy` | The gate is mid-animation |
| `Unavailable` | The gate is disabled or not built |

`SlidingDoor.OnUseResolved(interactor, result)` fires after every attempt. `TryOpen(interactor)`
remains as a `bool` wrapper for existing callers.

## Roadmap

- **Phase 2 (done):** `NpcKeyring : MonoBehaviour, IKeyHolder`, `NpcMemory`, and
  `NpcUseDoorBehavior` so NPCs approach doors, open them when keyed, and remember locked ones
  until they get the key. See [NPC_AI.md](NPC_AI.md).
- **Phase 3:** shared AI helpers (`NpcPerception`, `NpcBehaviorBase`).
- **Phase 4:** persist NPC lock knowledge; pathfinding.

See [SLIDING_DOORS.md](SLIDING_DOORS.md) for the gate itself.
