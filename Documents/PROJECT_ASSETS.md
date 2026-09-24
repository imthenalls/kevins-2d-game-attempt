# Third-Party Project Assets

Store packages imported into the project. Both belong to the **Shell** (`Game.Presentation` or the
Editor) — `Game.Data` must never reference them (its `noEngineReferences` guard forbids Unity types,
and neither package is engine-free).

| Package | Version | Path | Assembly | Layer |
|---|---|---|---|---|
| [NaughtyAttributes](#naughtyattributes) | 2.1.6 | `Assets/NaughtyAttributes/` | `NaughtyAttributes.Core` (runtime), `NaughtyAttributes.Editor` (editor) | Presentation / editor UX |
| [PathGrid](#pathgrid-giantgrey) | 1.0.1 | `Assets/PathGrid/` | `PathGrid` | Presentation (3D authoring + navigation tooling) |

> **Cross-assembly note:** `autoReferenced` only covers the predefined `Assembly-CSharp`. To use either
> package's API from a custom asmdef (`Game.Presentation`, `Game.Presentation.Editor`), add the package
> assembly to that asmdef's `references` (`NaughtyAttributes.Core`, `PathGrid`). Merely adding
> attributes needs the reference too.

---

## NaughtyAttributes

**What it is:** an inspector extension (by Denis Rizov) that adds attributes to make custom inspectors
cleaner without writing `Editor` classes. Namespace: `NaughtyAttributes`.

**Grouping / layout**

| Attribute | Effect |
|---|---|
| `[BoxGroup("x")]`, `[Foldout("x")]` | Wrap fields in a titled box / collapsible section |
| `[HorizontalLine]` | Separator line |
| `[Label("text")]`, `[InfoBox("text")]` | Field label / inline help box (also `InfoBox(..., EInfoBoxType.Warning/Error)`) |
| `[ReadOnly]`, `[ShowIf(nameof(flag))]`, `[HideIf]`, `[EnableIf]`, `[DisableIf]` | Conditional display/enable |

**Editing helpers**

`[Button]` (call a method from the inspector), `[Dropdown]`, `[EnumFlags]`, `[MinMaxSlider]`,
`[Range]`, `[MinValue]`, `[MaxValue]`, `[ProgressBar]`, `[ResizableTextArea]`, `[CurveRange]`,
`[Layer]`, `[Tag]`, `[Scene]`, `[SortingLayer]`, `[AnimatorParam]`, `[InputAxis]`,
`[ShowAssetPreview]`, `[ReorderableList]`, `[ShowNonSerializedField]`, `[ShowNativeProperty]`.

**Validators:** `[Required]`, `[RequiredType]`, `[ValidateInput(nameof(Validate))]`.

**How to use it in this project**

- Decorate Presentation MonoBehaviours to group Inspector fields and add quick actions, e.g. a
  `[Button]` "Rebuild Town" on an authoring component, `[ReadOnly]` on runtime-set state,
  `[ShowIf]` to hide config that does not apply to the current `NpcType`.
- Prefer it over hand-written editor inspectors for simple cases; the package provides
  `NaughtyInspector`, which replaces the default inspector for types that use these attributes.
- **Do not** put these attributes on `GameData` types — the Core has no Unity and no package
  references.

```csharp
using NaughtyAttributes;
using UnityEngine;

public class TownTuning : MonoBehaviour
{
    [BoxGroup("Movement"), MinValue(0f)] public float speed = 3f;
    [BoxGroup("Movement"), ReadOnly] public int cachedLength;

    [Button("Log state")]
    private void LogState() => Debug.Log("ok");
}
```

---

## PathGrid (GiantGrey)

**What it is:** a modular **road / bridge / pipe network builder** for 3D, plus an A* pathfinding
demo. It snaps authored cells to a grid and instantiates the correct 3D piece (straight, curve,
three-way, crossway, dead-end) from a preset. This is a strong fit now that the project renders real
3D geometry (see [ISOMETRIC_3D.md](ISOMETRIC_3D.md)).

**Key types (`namespace PathGrid`, `Assets/PathGrid/Code/`)**

| Type | Role |
|---|---|
| `PathNetwork` | A closed network with its own grid (`gridCellSize`). Cells carry a type: `none`, `terrain`, `water`, `blocked`. Optionally adapts to terrain height. |
| `PathPreset` | `ScriptableObject` (`Create > PathGrid > Path Preset`) mapping each tile shape to a prefab (`single`, `straight`, `curve`, `threeway`, `crossway`, `deadEnd`) plus per-shape Y rotation offsets and Y position. |
| `PathGridSystem` | Runtime "bridge": holds `pathNetworks` + `pathPresets`, selects the active pair, and builds/removes tiles. |
| `PathGridInputSystem` | Example mouse-driven authoring: first click starts a preview, second click commits, right-click cancels. |
| `PathCellData` | Per-cell struct (`TileType`, neighbour flags, rotation, spawned `tileObject`, `pathPreset`). |

**Public API**

```csharp
// PathGridSystem
void AddPathTiles(List<Vector3> positions, Action onComplete = null);
void AddPathTiles(List<Vector3> positions, int networkIndex, int presetIndex, Action onComplete = null);
void RemovePathTiles(List<Vector3> positions);
void SetPathNetworkIndex(int index);
void SetPathPresetIndex(int index);

// PathNetwork
void AddPathCells(List<Vector3> positions, PathPreset preset, Action onComplete);
void RemovePathCells(List<Vector3> positions);
void SetCellTypeAtPosition(Vector3 position, PathNetwork.CellType type);
PathNetwork.CellType GetGridCellTypeAtPosition(Vector3 position);
PathCellData GetExistingPathCell(Vector3 position);
```

**How to use it in this project**

- **Roads / bridges:** replace the code-generated flat street quads in
  `Town3DSceneBuilder`/`WorldBSceneBuilder` with a `PathGridSystem` + a road `PathPreset`, so the
  town has real 3D streets and bridges. This is authoring/tooling → keep it in the **Presentation**
  layer (editor builder or runtime component), never in `GameData`.
- **Navigation:** PathGrid authors connectivity, but gameplay decisions stay in our engine-free
  `Game.Core.GridPathfinder`. If we want NPCs to follow PathGrid roads, implement `IWalkabilityGrid`
  on top of `PathNetwork` (`blocked` cell type → not walkable) and keep using `GridPathfinder`, so the
  algorithm and its tests remain dimension-agnostic and scene-free. PathGrid's A* demo is a useful
  reference/alternative but should not replace the Core model without the same guardrails.
- **Cell types:** use `SetCellTypeAtPosition` / `GetGridCellTypeAtPosition` to mark `blocked` (buildings)
  and `terrain`/`water` lanes so path presets only place the correct pieces.

**Samples:** `Assets/PathGrid/_Demo/` (`Demo_00..Demo_02`) show the road builder and the A* pathfinding
demo. `Assets/PathGrid/Presets/` ships `Bridge`, `Pipes`, `Road`, `SimpleRoad` presets (meshes, prefabs,
materials).

---

## Cautions

1. **Stay out of `GameData`.** Neither package may be referenced by `Game.Core` types; they are Unity
   / editor tooling. Apply the [ARCHITECTURE_GUARDRAILS.md](ARCHITECTURE_GUARDRAILS.md) checklist.
2. **Add the asmdef reference before using the API** from a custom assembly (see the note above).
3. **Vendor upgrades:** both packages carry their own `.asmdef` and version metadata; update them in
   place and re-run `Tools/verify-all.ps1`.
