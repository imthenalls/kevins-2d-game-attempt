# TODO

- [ ] **Quest System** — Build a quest manager to track active and completed quests, objectives, and rewards, with data persistence across play sessions.

- [ ] **HP / MP** — Add player health and mana stats with damage, regeneration, death handling, and simple UI bars showing current and maximum values.

- [ ] **NPC System** — Create a base NPC component with configurable identity, behavior state, interaction range, and support for multiple NPC types.
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
