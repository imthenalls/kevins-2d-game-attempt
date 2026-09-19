# NPC Pathfinding Stress Test

A disposable performance harness for measuring how many wandering/pathfinding NPCs the game can run.

## Running it

1. **Tools > Stress > Build NPC Stress Room** — bakes a large empty room into `WorldB.unity`
   (floor cells `x -2..36, y -2..20`, perimeter walls), on the existing isometric grid.
   Safe to re-run: **Tools > Stress > Delete NPC Stress Room** first removes the old one.
2. **Tools > Stress > Run NPC Pathfinding Stress** — enters Play Mode and runs the ramp.
3. Read `Temp/npc-stress-report.txt` (also logged to the Console).

## What it does

`NpcStressHarness` (runtime component, created by the tool) spawns **25 / 50 / 100** Generic NPCs
at scale `0.5`, each with `Rigidbody2D` + collider + sprite + `NpcController` + `NpcBehaviorManager`
+ `NpcWanderBehavior` (now pathfinding — see [NPC_AI.md](NPC_AI.md)) + `NpcPathfinder` +
`NpcPerception`. For every count it measures three phases, for two obstacle-mask configurations:

- **baseline** — behaviours frozen (render + physics + perception only).
- **steady** — every NPC wanders and re-paths on its own cadence.
- **burst** — every 2 s **all NPCs re-path in a single frame** (the "pathing all at once" worst case);
  the harness times that frame and counts how many paths succeeded.

Masks compared: `Everything` (the game default — NPC bodies block pathfinding) vs `WallsOnly`
(what a layer fix gives).

## Layers

The builder creates two project layers if missing: **`Npc` (6)** and **`Walls` (7)**, and puts the
stress room's walls on `Walls`. The harness puts spawned NPCs on `Npc`.

> The rollout is applied: **Tools > World > Apply NPC + Wall Layers** (`NpcLayerRollout`) assigns
> every `NpcController` hierarchy to `Npc`, every collider-bearing Tilemap (walls) to `Walls`, and
> switches `NpcPathfinder.obstacleLayers` / `NpcWanderBehavior.wallLayers` /
> `NpcDashMeleeController.obstacleLayers` from `Everything` to `~(1 << Npc)` — so walls, props, and
> the player remain obstacles but NPC bodies no longer block each other. `NpcPerception` stays
> permissive (`~0`) so detection of the player and gates is unaffected. Re-run the tool after adding
> scenes or NPC prefabs.

## Latest results (editor, isometric WorldB, .NET 10 SDK / Unity 6.4)

| NPCs | baseline | steady | burst: repath-all avg / worst | paths ok |
|---:|---:|---:|---:|---:|
| 25  | 1.19–1.43 ms (≈700–840 fps) | 1.25–1.30 ms | 0.74 / 0.83 ms | 25/25 |
| 50  | 1.29–1.35 ms | 1.36–1.37 ms | 1.42–1.44 / 1.5–1.6 ms | 50/50 |
| 100 | 1.49–1.51 ms (≈660–670 fps) | 1.60–1.65 ms (≈605–625 fps) | 2.53 (WallsOnly) / 4.47 (Everything) ms; worst 2.67 / 7.38 ms | 100/100 |

Frame-time spikes during a burst reach ~18–20 ms (a single sub-frame hitch every 2 s). One-off 598 ms
max in the first baseline sample is a start-up artefact, not steady state.

## Interpretation

- **Pathfinding is not the bottleneck.** 100 NPCs all re-pathing in one frame costs ~2.5 ms
  (walls-only) to ~4.5 ms (NPCs-as-obstacles) — a single frame hitch, not a stall.
- **Steady state scales gently**: 25 → 100 NPCs raises frame time only ~0.35 ms.
- **The obstacle mask roughly doubles the 100-NPC burst cost** because A\* explores around NPC
  bodies; the `Walls`-only mask is the cheaper, correct configuration.
- Numbers are Editor figures; a build will differ. Rendering was kept on-screen (harness camera).
