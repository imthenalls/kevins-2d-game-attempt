# Town Rebuild Point

`Town3DSceneBuilder` regenerates `Assets/Scenes/Town.unity` from scratch, so any hand edit you make
to the built scene (moving the exit portal, nudging the spawn, tweaking a component) is lost the next
time you run **Tools > Worlds > Rebuild Town Scene (3D)**. The **rebuild point** lets you snapshot
those edits and have the builder re-apply them on every subsequent rebuild.

## Workflow

1. Open `Assets/Scenes/Town.unity`.
2. Make your changes (move objects, edit fields in the Inspector, disable objects you don't want).
3. **Tools > Worlds > Town 3D > Set Rebuild Point** — snapshots the current scene.
4. **Tools > Worlds > Rebuild Town Scene (3D)** — regenerates the scene, then re-applies the snapshot,
   so your changes come back.
5. Repeat from step 2 whenever you make more changes and re-run **Set Rebuild Point**.

**Tools > Worlds > Town 3D > Clear Rebuild Point** discards the snapshot so a rebuild uses the
builder's hard-coded defaults again.

## What is captured

For every object in the scene, keyed by its hierarchy path (e.g. `Town Exit Portal`,
`Buildings/Building_4_14/Door/Approach`):

- **Transform** — local position, rotation, and scale.
- **Active state** — `activeSelf` (so you can hide an object you don't want).
- **Simple serialized fields** on every component — numbers, strings, enums, booleans, layer masks,
  vectors, colors, rects, and bounds. This covers things like a portal's id / destination / required
  key, item ids, sprite colors, and movement tuning.

## What is not captured

- **Object-reference links** (a `Transform`, `Material`, or component assigned in a slot). The
  referenced *object* keeps its captured transform, so moving a referenced child still persists; only
  re-pointing a reference is not tracked.
- **Arrays and lists** of serialized values.
- **Added or removed objects** — the builder always recreates its own set. Deleting a built object
  and rebuilding brings it back; new objects you add by hand are removed.

## Storage

The snapshot is written to `Assets/Settings/Town3DRebuildPoint.asset` (a `Town3DRebuildPoint`
ScriptableObject, Editor-only tooling). It is created on the first **Set Rebuild Point** and can be
inspected/edited in the Inspector. It is not referenced by any scene, so it is not included in a
build.

## Notes

- The snapshot is a full freeze of the captured values: on rebuild it overrides the builder's
  defaults for everything it captured, even if you later change a default in the builder code. Use
  **Set Rebuild Point** again (or **Clear Rebuild Point**) to pick up new defaults.
- Path keys tolerate duplicate sibling names by appending `#n` (e.g. `Perimeter Walls/Wall#2`), so
  objects sharing a name still round-trip.
- Editor-only class; nothing to add to a GameObject at runtime.

## Verified

Capture/apply round-trips in the live Editor: after moving `Town Exit Portal` to `(15, 0, 10)`,
running **Set Rebuild Point** and then **Rebuild Town Scene (3D)**, the portal stays at
`(15, 0, 10)`.
