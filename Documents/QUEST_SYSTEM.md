# Quest System Design

## Approach: Graph-Based Quests, JSON-Authored

Quests in this project are **directed graphs of nodes**. This was chosen specifically to support convoluted quest design — branching outcomes, mutually exclusive paths, parallel objectives, quests that affect each other, and multiple endings per quest.

A linear objective-list model (as used in MMOs/Skyrim) breaks down when:
- Objectives only appear if a prior choice was made a certain way
- Two quests share state and completing one modifies or locks out the other
- A single objective can be satisfied by multiple different player actions (kill vs. pay vs. expose)
- Quests have partial failure states where some objectives fail but the quest continues in a degraded branch

Quest data lives in **JSON files under `StreamingAssets/quests/`** — not ScriptableObjects — so quests are plain text, version-controllable, and editable outside of Unity.

---

## File Layout

```
StreamingAssets/
  quests/
    bandit_king.json
    find_the_thief.json
    ...
```

---

## JSON Schema

```json
{
  "questId": "bandit_king",
  "startNodeId": "start",
  "nodes": [
    {
      "id": "start",
      "objectives": [
        {
          "id": "obj_talk",
          "eventType": "NpcTalkedTo",
          "targetId": "sheriff",
          "requiredCount": 1
        }
      ],
      "onEnterActions": [],
      "transitions": [
        {
          "targetNodeId": "investigation",
          "automatic": false,
          "conditions": [
            { "type": "ObjectiveComplete", "objectiveId": "obj_talk" }
          ]
        }
      ]
    },
    {
      "id": "investigation",
      "objectives": [
        { "id": "obj_kill",  "eventType": "EnemyKilled",  "targetId": "bandit_king",      "requiredCount": 1 },
        { "id": "obj_pay",   "eventType": "NpcTalkedTo",  "targetId": "bandit_king",      "requiredCount": 1 },
        { "id": "obj_proof", "eventType": "ItemCollected", "targetId": "evidence_letter", "requiredCount": 1 }
      ],
      "onEnterActions": [],
      "transitions": [
        {
          "targetNodeId": "ending_kill",
          "automatic": true,
          "conditions": [{ "type": "ObjectiveComplete", "objectiveId": "obj_kill" }]
        },
        {
          "targetNodeId": "ending_paid",
          "automatic": true,
          "conditions": [{ "type": "ObjectiveComplete", "objectiveId": "obj_pay" }]
        },
        {
          "targetNodeId": "ending_expose",
          "automatic": true,
          "conditions": [
            { "type": "ObjectiveComplete", "objectiveId": "obj_proof" },
            { "type": "Fact", "key": "sheriffTrusted", "value": true }
          ]
        }
      ]
    }
  ]
}
```

### Condition types (discriminator: `"type"`)

| type | Fields | Description |
|---|---|---|
| `ObjectiveComplete` | `objectiveId` | All required count met for that objective on the current node |
| `Fact` | `key`, `value` | `WorldStateDB` entry equals value (bool, int, string, float) |
| `QuestInNode` | `questId`, `nodeId` | Another active quest instance is currently at a specific node |
| `HasItem` | `itemId`, `count` | Player inventory contains at least count of item |

Conditions in a single transition are **all AND**. To express OR, use multiple transitions pointing to the same target.

### Action types (discriminator: `"type"`)

| type | Fields | Description |
|---|---|---|
| `SetFact` | `key`, `value` | Write a value into `WorldStateDB` |
| `GiveItem` | `itemId`, `count` | Add item(s) to player inventory |
| `RemoveItem` | `itemId`, `count` | Remove item(s) from player inventory |
| `StartQuest` | `questId` | Activate another quest graph |

`onEnterActions` run once when a node is entered.

---

## C# Architecture

### Data classes (plain C#, no MonoBehaviour)

```
QuestGraph
  ├── questId        : string
  ├── startNodeId    : string
  └── nodes          : List<QuestNode>

QuestNode
  ├── id             : string
  ├── objectives     : List<QuestObjective>
  ├── transitions    : List<QuestTransition>
  └── onEnterActions : List<QuestAction>

QuestObjective
  ├── id             : string
  ├── eventType      : string
  ├── targetId       : string
  └── requiredCount  : int

QuestTransition
  ├── targetNodeId   : string
  ├── automatic      : bool
  └── conditions     : List<ICondition>
```

### Interfaces

```csharp
interface ICondition  { bool Evaluate(QuestInstance ctx); }
interface IQuestAction { void Execute(); }
```

### Runtime classes

```
WorldStateDB : MonoBehaviour (singleton)
  └── Dictionary<string, object> facts
      — the single global source of truth for all game state
      — facts are written by SetFactAction and read by FactCondition and QuestStateCondition

QuestEventBus : static class
  └── static event Action<string, string, int> OnEvent
  └── static void Raise(string eventType, string targetId, int amount = 1)
      — called by game systems; has no knowledge of quests

QuestLoader : static class
  ├── LoadAll() — reads every .json from StreamingAssets/quests/
  └── Load(string path) → QuestGraph
      — uses Newtonsoft.Json (com.unity.nuget.newtonsoft-json)
      — constructs concrete ICondition / IQuestAction from "type" discriminator field

QuestInstance : class  (runtime wrapper around a QuestGraph)
  ├── graph            : QuestGraph
  ├── activeNodeIds    : HashSet<string>    — supports multiple parallel active nodes
  ├── objectiveCounts  : Dictionary<string, int>
  └── TryAdvance()     — evaluate all transitions on active nodes; enter new nodes if conditions pass

QuestManager : MonoBehaviour (singleton)
  ├── activeQuests     : List<QuestInstance>
  ├── StartQuest(string questId)
  ├── OnEvent(string eventType, string targetId, int amount)  — subscribed to QuestEventBus
  └── Update()         — calls TryAdvance on each instance for automatic transitions
```

---

## Runtime Flow

```
1. A game system fires an event:
     QuestEventBus.Raise("EnemyKilled", "bandit_king");

2. QuestManager.OnEvent receives it and loops all active QuestInstances.

3. Each QuestInstance checks its active nodes:
   a. Increments objectiveCounts for matching objectives.
   b. Evaluates transitions on those nodes.
   c. If a transition's full condition set passes → enter target node.

4. On node entry:
   a. Run onEnterActions (SetFact, GiveItem, StartQuest, etc.).
   b. Register new objectives from the new node.
   c. If the node has no outgoing transitions, this path ends (success, failure, or branch terminus).

5. Multiple active nodes are supported:
   — a node can have a transition to two different nodes at once
   — both become active simultaneously (parallel objectives)

6. Cross-quest influence:
   — SetFactAction writes facts that FactCondition on other quests reads
   — QuestInNode condition directly checks another QuestInstance's activeNodeIds
   — StartQuestAction activates a new QuestInstance mid-quest
```

---

## What other systems do

| System | Call |
|---|---|
| Combat | `QuestEventBus.Raise("EnemyKilled", enemy.id)` |
| Inventory pickup | `QuestEventBus.Raise("ItemCollected", item.id)` |
| NPC Dialogue | `QuestEventBus.Raise("NpcTalkedTo", npc.id)` |
| Portals / area entry | `QuestEventBus.Raise("LocationReached", areaId)` |
| Dialogue system | `QuestManager.StartQuest("questId")` to activate a quest from a conversation |

No game system imports quest types. They only fire events or call `StartQuest`. The graph reacts.

---

## Save / Load

Only two things need to be serialized per save file:

1. **`WorldStateDB.facts`** — the full dictionary
2. **Per `QuestInstance`**: `questId`, `activeNodeIds`, `objectiveCounts`

The `QuestGraph` JSON files are read-only authored data and are never written at runtime. Reloading a save reconstructs `QuestInstance` objects from those two pieces and re-reads graphs from disk.

---

## Package Dependency

**Newtonsoft.Json** is required for deserializing polymorphic conditions and actions.

Add to `Packages/manifest.json`:
```json
"com.unity.nuget.newtonsoft-json": "3.2.1"
```

---

## Implementation Order

1. `WorldStateDB`
2. `QuestEventBus`
3. `ICondition` / `IQuestAction` interfaces + concrete implementations + discriminator factory
4. Plain C# data classes: `QuestGraph`, `QuestNode`, `QuestObjective`, `QuestTransition`
5. `QuestLoader` (JSON → data classes)
6. `QuestInstance`
7. `QuestManager`
