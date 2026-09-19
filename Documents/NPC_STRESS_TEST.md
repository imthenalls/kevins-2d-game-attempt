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
  (walls-only) to ~4.5 ms (NPCs-as-obstacles) in the Editor — a single frame hitch, not a stall.
- **Steady state scales gently**: 25 → 100 NPCs raises frame time only ~0.35 ms.
- The obstacle mask roughly doubles the 100-NPC burst cost in the **Editor**; see the build numbers
  below for the more realistic picture.
- Numbers are Editor figures; a build is much faster (below).

## Running it in a build

`NpcStressHarness` also runs from a Player build:

1. Place a harness object in the target scene (e.g. via the run tool, or an eval that adds
   `NpcStressHarness` and sets `spawnCellMinX/Y/MaxX/Y`), then save the scene.
2. Build with that scene as the startup scene. `unity command build` does **not** accept a
   single-value `--scenes`; drop the other scenes from Build Settings instead.
3. Run the exe with its working directory at the build root; it writes
   `Temp/npc-stress-report.txt` there and quits itself.
4. **Remove the harness object from the scene afterwards** so it does not run during normal play.

The harness disables v-sync and target-framerate so the build reports raw frame time.

## Latest build results (StandaloneWindows64, v-sync off)

| NPCs | baseline | steady | burst: repath-all avg / worst | paths ok |
|---:|---:|---:|---:|---:|
| 25  | ~0.36–0.44 ms (≈2300–2800 fps) | ~0.36–0.39 ms | 0.92 / 1.17–1.22 ms | 25/25 |
| 50  | ~0.36–0.37 ms | ~0.38 ms | 1.91–2.00 / 1.98–2.32 ms | 50/50 |
| 100 | ~0.42–0.47 ms (≈2100–2400 fps) | ~0.44–0.45 ms (≈2200–2280 fps) | 3.14 (Everything) / 3.63 (NoNpcBodies) ms; worst 3.17 / 4.27 ms | 100/100 |

### Build interpretation

- A build runs roughly **3–4× faster per frame** than the Editor (~0.44 ms vs ~1.6 ms at 100 NPCs).
- The synchronized burst scales ~linearly: 25 → 0.92 ms, 50 → ~1.95 ms, 100 → ~3.1–3.6 ms.
- **The obstacle mask barely matters for speed in a build** (NoNpcBodies was even marginally slower,
  because unblocked cells make A\* find longer paths). So the NPC/wall layer separation is a
  **correctness** fix — NPCs should not treat each other's bodies as walls — not a performance win.
- Occasional frame spikes (≈2–20 ms) appear in both runs; nothing sustained.
