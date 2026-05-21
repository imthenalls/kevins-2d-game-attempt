# NPC System

## Overview

NPCs are composed from a core identity component plus optional behavior and dialogue components. Each concern is a separate script, assembled on the same GameObject.

```
NPC (GameObject)
  ├── Rigidbody2D             (optional — needed for wander)
  ├── Collider2D              (required for interaction range detection)
  ├── NpcController           — identity, type, interaction range, enemy stats
  ├── NpcBehaviorManager      — picks and drives behaviors (optional)
  ├── NpcIdleBehavior         — stand still for N seconds (optional)
  ├── NpcWanderBehavior       — walk to random nearby points (optional)
  └── NpcDialogue             — links to a DialogueGraphAsset (optional)
```

---

## NpcController

**File:** `Assets/Scripts/NPCs/NpcController.cs`

The root identity component. Every NPC must have one.

### Inspector fields

| Field | Description |
|---|---|
| Npc Id | Unique string identifier (used by quest events: `QuestEventBus.Raise("NpcTalkedTo", npcId)`) |
| Display Name | Shown in dialogue UI; falls back to `gameObject.name` if blank |
| Npc Type | `Generic`, `QuestGiver`, `Vendor`, `Trainer`, or `Enemy` |
| Enemy Max Hp | HP given to enemies on Awake (only used when `Npc Type = Enemy`) |
| Interaction Range | Radius in world units within which interaction is allowed |
| Interaction Point | Optional transform override for where the range is measured from |

### NpcType enum

```
Generic     — default, no special behaviour
QuestGiver  — signals to other systems this NPC gives quests
Vendor      — can be used to gate shop UI
Trainer     — can be used to gate skill UI
Enemy       — automatically adds and configures EntityStats on Awake
```

### Enemy stats

When `Npc Type = Enemy`, `NpcController.Awake()` automatically adds and configures both `EntityStats` and `Combatant`. Access via:

```csharp
EntityStats stats     = npcController.Stats;     // null for non-enemy types
Combatant   combatant = npcController.Combatant; // null for non-enemy types

stats?.TakeDamage(10);
combatant?.ReceiveHit(new DamageInfo(10, gameObject));
```

Do not add `EntityStats` or `Combatant` manually to enemy prefabs — `NpcController` owns them.

### Behavior state

| State | Meaning |
|---|---|
| `Idle` | Normal operation — behaviors tick, interaction allowed |
| `Talking` | Set automatically during dialogue — behaviors paused |
| `Disabled` | Cannot be interacted with, behaviors paused |

---

## NpcBehaviorManager

**File:** `Assets/Scripts/NPCs/NpcBehaviorManager.cs`

Auto-discovers all `INpcBehavior` components on the same GameObject. Uses weighted random selection to pick the next behavior when the current one completes. Behaviors pause automatically when `NpcBehaviorState` is not `Idle`.

### Flow

```
Start → PickNext() → OnEnter()
         ↓
       Tick() each frame
         ↓
       IsComplete()? → OnExit() → PickNext() → ...
```

No configuration needed beyond adding behavior components to the same GameObject.

---

## INpcBehavior Interface

**File:** `Assets/Scripts/NPCs/INpcBehavior.cs`

Implement on a `MonoBehaviour` to create custom behaviors:

```csharp
public interface INpcBehavior
{
    float Weight   { get; }   // relative chance this is chosen (0–100 range is conventional)
    void  OnEnter();          // called once when activated
    void  Tick();             // called every frame while active
    void  OnExit();           // called once when deactivated
    bool  IsComplete();       // return true to trigger the next behavior
}
```

### Built-in behaviors

#### NpcIdleBehavior
Stands still for a random duration between `Min Duration` and `Max Duration` seconds.

| Field | Default |
|---|---|
| Weight | 50 |
| Min Duration | 2s |
| Max Duration | 5s |

#### NpcWanderBehavior
Walks to a random point within `Wander Radius`. Raycasts ahead to detect walls and gives up rather than grinding into geometry. Tries up to 8 candidate directions on enter. Requires `Rigidbody2D` (Gravity Scale 0, Freeze Rotation Z).

| Field | Default | Notes |
|---|---|---|
| Weight | 50 | |
| Wander Radius | 3 | World units |
| Move Speed | 2 | Units per second |
| Arrival Threshold | 0.2 | Distance to count as arrived |
| Wall Look Ahead | 0.3 | Raycast distance ahead (~half NPC width) |
| Wall Layers | ~0 | Set to your wall layer in Inspector |

---

## Dialogue

**File:** `Assets/Scripts/NPCs/NpcDialogue.cs`

Attach alongside `NpcController`. Links to a `DialogueGraphAsset` (a ScriptableObject wrapping a `DialogueGraphDefinition`). Can also look up graphs by `dialogueId` from `DialogueDatabase`, which loads `StreamingAssets/dialogues.json` on first access.

### Dialogue data format

Graphs are serialized as `DialogueGraphDefinition` (see `DialogueData.cs`):

```json
{
  "dialogueId": "shopkeeper_intro",
  "startNodeId": "start",
  "nodes": [
    {
      "id": "start",
      "speakerName": "",
      "text": "Welcome, traveler.",
      "nextNodeId": "offer",
      "endConversation": false,
      "choices": []
    },
    {
      "id": "offer",
      "text": "Need anything?",
      "endConversation": false,
      "choices": [
        { "text": "Show me your wares.", "nextNodeId": "shop",  "endConversation": false },
        { "text": "Just looking.",       "nextNodeId": "",      "endConversation": true  }
      ]
    }
  ]
}
```

- **Linear nodes**: set `nextNodeId`, leave `choices` empty.
- **Choice nodes**: fill `choices`; each choice has its own `nextNodeId` or `endConversation: true`.
- `speakerName` overrides the NPC's display name for a specific node (useful for player responses).

### Sources (priority order)

1. `DialogueGraphAsset` assigned in Inspector.
2. `dialogueId` looked up in `DialogueDatabase` (from `dialogues.json` in `StreamingAssets`).

### DialogueUIController

**File:** `Assets/Scripts/DialogueUIController.cs`

Static-access singleton (`DialogueUIController.GetOrCreate()`). Called by `PlayerInteractionController` to show/hide dialogue panels. Does not need to be manually assigned — it is auto-found or created.

---

## Setting Up an NPC in a Scene

### Minimal (static, talkable)
1. Create a GameObject, add `Collider2D` (set as trigger or use `NpcController.CanInteract` range).
2. Add `NpcController` — set `Npc Id`, `Display Name`, `Npc Type`.
3. Add `NpcDialogue` — assign a `DialogueGraphAsset`.

### Wandering NPC
4. Add `Rigidbody2D` (Gravity Scale 0, Freeze Rotation Z ✓).
5. Add `NpcBehaviorManager`.
6. Add `NpcIdleBehavior` and/or `NpcWanderBehavior` — tune weights and radii.

### Enemy
7. Set `Npc Type = Enemy` and `Enemy Max Hp` on `NpcController`.
8. `EntityStats` is added automatically at runtime — no manual setup needed.

---

## Notifying the Quest System

When the player talks to an NPC, fire:

```csharp
QuestEventBus.Raise("NpcTalkedTo", npcController.NpcId);
```

When an enemy dies:

```csharp
QuestEventBus.Raise("EnemyKilled", npcController.NpcId);
```
