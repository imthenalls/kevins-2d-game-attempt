# TODO

- [ ] **Quest System** — Build a quest manager to track active and completed quests, objectives, and rewards, with data persistence across play sessions.

- [ ] **HP / MP** — Add player health and mana stats with damage, regeneration, death handling, and simple UI bars showing current and maximum values.

- [ ] **NPC System** — Create a base NPC component with configurable identity, behavior state, interaction range, and support for multiple NPC types.

Within the NPC system (still incomplete):

Sprite flipping on move — NpcWanderBehavior moves the NPC but doesn't flip the sprite to face the direction of travel
NPC type support — the NpcController has no way to distinguish NPC types (vendor, quest giver, etc.) which will be needed once quests land
Mark the NPC system TODO as done and clean up the leftover notes/pseudocode below it in TODO.md
Next big features (from TODO):
4. HP / MP system — self-contained, no dependencies on NPC work, good to do next
5. Quest system — depends on NPC types and dialogue hooks, so best after HP/MP
6. Dialogue with NPCs — already partially there (NpcDialogue, DialogueData), needs branching choices and quest hooks

5. Component-Based AI (Unity-style architecture)

Lean into Unity’s strength: composition over inheritance.

Example components
MovementController
VisionSystem
CombatSystem
DecisionSystem

Each handles one responsibility.

public class NPCController : MonoBehaviour {
    public MovementController movement;
    public VisionSystem vision;
    public DecisionSystem decision;

    void Update() {
        decision.Tick();
    }
}
When to use it
Larger projects
You want reusable building blocks
Multiple NPC types share systems

2. Behavior Trees — modular and scalable

Used heavily in professional games.

Core idea

You build a tree of decisions:

Selector (try options until one succeeds)
Sequence (run steps in order)
Actions (do something)
Conditions (check something)
Example structure
Selector
 ├── Sequence (Enemy in range)
 │    ├── Check distance
 │    └── Attack
 └── Patrol
Simple pseudo-code
if (playerInRange)
    Attack();
else
    Patrol();

But implemented as reusable nodes.

Unity implementation options
Build your own node system
Use assets like:
NodeCanvas
Behavior Designer
When to use it
Medium to large projects
Complex enemy logic
You want reusable behaviors
Downsides
More setup time
Can feel abstract at first

- [ ] **Dialogue with NPCs** — Implement dialogue trees for NPC interactions with branching choices, text display, and hooks to trigger quest updates.
💡 A good mental model

Think of NPCs like LEGO:

Base object = the brick
Components = attachments
Behavior = combination of pieces

- [ ] **Scene Manager** — Implement scene loading and transitions with loading screens and persistent game state across scenes.


item suggestions:
Suggestions
Stack splitting — Shift+click or drag to split a stack into two. Essential for trading/sharing consumables.

Item rarity tiers — Common / Uncommon / Rare / Unique, indicated by slot border color. Ties nicely into loot drops and quest rewards later.

Equipment slots panel — A separate "paper doll" or slot list (weapon, armor, accessory) adjacent to the grid. Equipment moves between the grid and these slots.

Hotbar — 4–6 quick-use slots the player binds items to, usable without opening inventory. Great for consumables.

Currency outside the grid — Gold/coins tracked as a stat, not taking up inventory cells. Avoids the classic "my inventory is full of coins" problem.

Item tooltips — On hover: name, description, type, stack count, sell value. Keep it tight per your no-on-screen-hints guideline — the tooltip is discoverable, not intrusive.

Auto-sort button — Groups items by category, then sorts by name. Low-effort QoL that players love.

Loot/container panel — A mirrored grid UI that slides in when opening chests or looting enemies, with a "Take All" button.

