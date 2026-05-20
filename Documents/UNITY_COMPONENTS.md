# Unity Components Reference

A guide to every major built-in Unity component, what it does, and how to use it.

---

## Table of Contents

1. [Transform](#transform)
2. [Rendering](#rendering)
3. [Physics (3D)](#physics-3d)
4. [Physics (2D)](#physics-2d)
5. [Colliders (3D)](#colliders-3d)
6. [Colliders (2D)](#colliders-2d)
7. [Audio](#audio)
8. [Camera](#camera)
9. [Lighting](#lighting)
10. [UI](#ui)
11. [Animation](#animation)
12. [Navigation / AI](#navigation--ai)
13. [Effects / Particles](#effects--particles)
14. [Networking](#networking)
15. [Tilemap](#tilemap)
16. [Miscellaneous](#miscellaneous)

---

## Transform

### Transform
Every GameObject has exactly one. Stores **position**, **rotation**, and **scale**.

| Field | Description |
|---|---|
| Position | World-space or local-space coordinates |
| Rotation | Euler angles (Inspector) / Quaternion (code) |
| Scale | Multiplier applied to the object and its children |

**Common usage:**
```csharp
transform.position = new Vector3(0, 1, 0);
transform.Rotate(Vector3.up, 90f);
transform.localScale = Vector3.one * 2f;
transform.SetParent(otherTransform);
```

---

## Rendering

### Mesh Filter
Holds a reference to a **Mesh** asset. Feeds geometry data to a Mesh Renderer. Attach a mesh here, then add a Mesh Renderer to see it.

```csharp
GetComponent<MeshFilter>().mesh = myMesh;
```

### Mesh Renderer
Renders the mesh provided by the Mesh Filter. Assign one or more **Materials** in the Inspector.

```csharp
GetComponent<MeshRenderer>().material.color = Color.red;
```

### Skinned Mesh Renderer
Like Mesh Renderer but supports **skeletal animation** (bone deformation). Used automatically when you import a rigged character. Exposes blend shapes (shape keys).

### Sprite Renderer
Renders a 2D **Sprite** in the scene. Core component for 2D games.

| Field | Description |
|---|---|
| Sprite | The sprite asset to display |
| Color | Tint / alpha |
| Flip X / Y | Mirror the sprite |
| Sorting Layer & Order | Controls draw order |

```csharp
GetComponent<SpriteRenderer>().sprite = mySprite;
GetComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0.5f); // 50% transparent
```

### Line Renderer
Draws a line (or polyline) through a list of world-space points. Great for laser beams, trajectories, ropes.

```csharp
LineRenderer lr = GetComponent<LineRenderer>();
lr.positionCount = 2;
lr.SetPosition(0, startPoint);
lr.SetPosition(1, endPoint);
lr.startWidth = 0.05f;
lr.endWidth   = 0.05f;
```

### Trail Renderer
Leaves a fading trail behind a moving object. Attach to any GameObject; the trail follows automatically.

### Billboard Renderer
Renders a quad that always faces the camera. Used for distant trees, particles as sprites.

---

## Physics (3D)

### Rigidbody
Gives a GameObject **physics simulation** (gravity, forces, collisions). Requires at least one Collider to interact with other objects.

| Field | Description |
|---|---|
| Mass | Affects force calculations |
| Drag | Air resistance (linear) |
| Angular Drag | Resistance to spinning |
| Use Gravity | Enable/disable gravity |
| Is Kinematic | Move via transform/script only; not affected by physics forces |
| Collision Detection | Discrete, Continuous, or Continuous Dynamic |
| Constraints | Freeze position/rotation on individual axes |

```csharp
Rigidbody rb = GetComponent<Rigidbody>();
rb.AddForce(Vector3.up * 10f, ForceMode.Impulse);
rb.velocity = new Vector3(5f, 0f, 0f);
rb.MovePosition(targetPosition);
```

> **Tip:** Use `ForceMode.Impulse` for instant pushes (e.g., jumping). Use `ForceMode.Force` for continuous pushes (e.g., thrusters).

### Character Controller
A capsule-shaped controller for character movement that bypasses Rigidbody physics. Gives you direct control with `Move()` and `SimpleMove()`.

```csharp
CharacterController cc = GetComponent<CharacterController>();
cc.Move(velocity * Time.deltaTime);
bool grounded = cc.isGrounded;
```

> **Note:** Does not interact with Rigidbody-based physics. For physics interactions use a Rigidbody instead.

### Constant Force
Applies a constant force and/or torque to a Rigidbody every frame. Useful for wind zones, conveyor belts.

---

## Physics (2D)

### Rigidbody 2D
The 2D equivalent of Rigidbody. Same concepts; all forces are restricted to the XY plane.

| Field | Description |
|---|---|
| Body Type | Dynamic, Kinematic, or Static |
| Gravity Scale | Multiplier for 2D gravity |
| Collision Detection | Discrete or Continuous |
| Interpolate | Smooths rendering between physics steps |

```csharp
Rigidbody2D rb2d = GetComponent<Rigidbody2D>();
rb2d.AddForce(Vector2.up * 500f);
rb2d.linearVelocity = new Vector2(3f, rb2d.linearVelocity.y);
```

### Constant Force 2D
Applies a constant 2D force/torque every frame to a Rigidbody 2D.

---

## Colliders (3D)

All colliders define the **physical shape** used for collision detection. They require a Rigidbody to participate in physics. Without a Rigidbody they act as **static** colliders.

| Collider | Shape | Notes |
|---|---|---|
| Box Collider | Box / cube | Fast; good for crates, walls |
| Sphere Collider | Sphere | Very fast; good for balls, grenades |
| Capsule Collider | Capsule | Standard for characters |
| Mesh Collider | Matches mesh | Accurate but expensive; avoid on fast-moving objects |
| Wheel Collider | Wheel + suspension | Vehicle wheels only |
| Terrain Collider | Terrain heightmap | Used automatically with Terrain component |

**Common properties:**
- **Is Trigger** — Disables physical collision; fires `OnTriggerEnter` / `OnTriggerStay` / `OnTriggerExit` instead.
- **Material** — Physics Material controlling friction and bounciness.

```csharp
// Trigger example
void OnTriggerEnter(Collider other) {
    if (other.CompareTag("Player")) Debug.Log("Player entered!");
}

// Collision example
void OnCollisionEnter(Collision col) {
    Debug.Log("Hit: " + col.gameObject.name);
}
```

---

## Colliders (2D)

| Collider 2D | Shape |
|---|---|
| Box Collider 2D | Rectangle |
| Circle Collider 2D | Circle |
| Capsule Collider 2D | Capsule |
| Polygon Collider 2D | Custom polygon (traced from sprite) |
| Edge Collider 2D | Open path / line |
| Composite Collider 2D | Merges multiple child colliders into one shape |
| Tilemap Collider 2D | Auto-generates colliders from a Tilemap |

All 2D colliders have **Is Trigger** and **Physics Material 2D** options.

```csharp
void OnTriggerEnter2D(Collider2D other) { }
void OnCollisionEnter2D(Collision2D col)  { }
```

---

## Audio

### Audio Source
**Plays audio** in the scene. Attach to any GameObject and assign an Audio Clip.

| Field | Description |
|---|---|
| Audio Clip | The clip to play |
| Play On Awake | Auto-play when the scene loads |
| Loop | Repeat the clip |
| Volume | 0–1 |
| Pitch | 1 = normal; < 1 slow, > 1 fast |
| Spatial Blend | 0 = 2D (no falloff), 1 = 3D (positional audio) |
| Min / Max Distance | Range for 3D attenuation |

```csharp
AudioSource src = GetComponent<AudioSource>();
src.PlayOneShot(clipRef);          // play without interrupting current clip
src.clip = backgroundMusic;
src.Play();
src.Stop();
```

### Audio Listener
Represents the **ears** in the scene. Captures all audio sources and sends to output. There should be exactly one active Audio Listener per scene — typically on the main camera.

### Audio Reverb Zone
Applies a reverb effect (e.g., cave, hallway) to all Audio Sources within its radius.

### Audio Chorus Filter / Echo Filter / Distortion Filter / High/Low Pass Filter / Reverb Filter
DSP effect components added to an Audio Source to modify its output.

---

## Camera

### Camera
Renders the scene to a display or render texture.

| Field | Description |
|---|---|
| Projection | Perspective (3D) or Orthographic (2D/UI) |
| Field of View | Viewing angle (Perspective only) |
| Size | Orthographic half-height |
| Clipping Planes | Near / far render distance |
| Culling Mask | Which layers this camera renders |
| Depth | Render order when multiple cameras exist |
| Target Texture | Render to a RenderTexture instead of screen |
| Clear Flags | What fills the background (Skybox, Solid Color, etc.) |

```csharp
Camera cam = Camera.main;
Ray ray = cam.ScreenPointToRay(Input.mousePosition);
Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(x, y, cam.nearClipPlane));
```

### Flare Layer
Enables lens flares from Light components to be visible through this camera.

### Universal Additional Camera Data *(URP)*
Extends the Camera with URP-specific settings: Render Type (Base / Overlay), Anti-aliasing, Post-processing toggle.

---

## Lighting

### Light
Illuminates the scene.

| Type | Description |
|---|---|
| Directional | Simulates sunlight; no position, infinite range |
| Point | Omnidirectional; radiates from a point |
| Spot | Cone-shaped beam |
| Area | Rectangular/disc emitter; baked only |

```csharp
Light lt = GetComponent<Light>();
lt.intensity = 2f;
lt.color = Color.yellow;
lt.range = 10f;
```

### Light Probe Group
Places **Light Probes** in the scene to capture lighting for dynamic objects in baked lighting setups.

### Reflection Probe
Captures a cubemap of its surroundings for **real-time or baked reflections** on nearby surfaces.

---

## UI

> UI components live on GameObjects inside a **Canvas**. Coordinates are in the Canvas's local space, controlled by a **Rect Transform** (replaces Transform).

### Canvas
The root of every UI hierarchy. Controls how UI is rendered.

| Render Mode | Description |
|---|---|
| Screen Space - Overlay | Always on top of everything |
| Screen Space - Camera | Rendered by a specific camera |
| World Space | UI lives in the 3D world |

### Rect Transform
2D layout version of Transform. Adds **anchors**, **pivot**, **anchored position**, and **size delta** for responsive layout.

### Canvas Scaler
Controls how the Canvas scales relative to screen size. Set **Scale With Screen Size** and a reference resolution for responsive UI.

### Graphic Raycaster
Enables mouse/touch input on UI elements within a Canvas.

### Image
Displays a Sprite as a UI element.

```csharp
GetComponent<Image>().sprite = mySprite;
GetComponent<Image>().color = Color.white;
```

### Raw Image
Displays any Texture (not just Sprites). Useful for render textures or webcam feeds.

### Text (Legacy) / TextMeshPro - Text (UI)
Renders text. Prefer **TextMeshPro** for quality and performance.

```csharp
using TMPro;
GetComponent<TextMeshProUGUI>().text = "Score: " + score;
```

### Button
Clickable UI element. Attach listeners via Inspector or code.

```csharp
Button btn = GetComponent<Button>();
btn.onClick.AddListener(() => Debug.Log("Clicked!"));
btn.interactable = false;
```

### Toggle
A checkbox. Has an `isOn` bool and an `onValueChanged` event.

```csharp
Toggle toggle = GetComponent<Toggle>();
toggle.isOn = true;
toggle.onValueChanged.AddListener(val => Debug.Log(val));
```

### Slider
A draggable range control.

```csharp
Slider s = GetComponent<Slider>();
s.minValue = 0; s.maxValue = 100; s.value = 50;
s.onValueChanged.AddListener(v => Debug.Log(v));
```

### Scrollbar
A standalone scroll bar. Usually auto-created by Scroll View.

### Scroll Rect (Scroll View)
Creates a scrollable viewport for content larger than the view area.

### Input Field (Legacy) / TMP_InputField
A text-entry field.

```csharp
TMP_InputField field = GetComponent<TMP_InputField>();
string userInput = field.text;
field.onSubmit.AddListener(text => Debug.Log("Submitted: " + text));
```

### Dropdown (Legacy) / TMP_Dropdown
A dropdown list.

```csharp
TMP_Dropdown dd = GetComponent<TMP_Dropdown>();
dd.value = 0;
dd.onValueChanged.AddListener(idx => Debug.Log(dd.options[idx].text));
```

### Mask / Rect Mask 2D
Clips child UI elements to the bounds of the parent. Rect Mask 2D is cheaper (no stencil buffer).

### Layout Group (Horizontal / Vertical / Grid)
Automatically arranges child elements. Set spacing, padding, alignment, and whether children control their own size.

### Content Size Fitter
Resizes a RectTransform to fit its content. Common with text boxes and scroll views.

### Aspect Ratio Fitter
Maintains an element's aspect ratio as the layout changes.

### Canvas Group
Controls alpha, interactability, and raycasting for an entire group of UI elements at once.

```csharp
CanvasGroup cg = GetComponent<CanvasGroup>();
cg.alpha = 0.5f;
cg.interactable = false;
cg.blocksRaycasts = false;
```

---

## Animation

### Animator
Drives **animation state machines** (Animator Controllers). Plays and blends animation clips.

```csharp
Animator anim = GetComponent<Animator>();
anim.SetBool("isRunning", true);
anim.SetFloat("speed", 3.5f);
anim.SetTrigger("jump");
anim.Play("Idle");
```

### Animation *(Legacy)*
Older animation system. Plays `AnimationClip` assets directly without a state machine. Use Animator for new projects.

```csharp
GetComponent<Animation>().Play("Walk");
```

---

## Navigation / AI

### Nav Mesh Agent
Moves a character along a **NavMesh** (baked navigation surface). Handles pathfinding automatically.

| Field | Description |
|---|---|
| Speed | Max movement speed |
| Stopping Distance | How close to get before stopping |
| Radius / Height | Agent capsule size |
| Area Mask | Which NavMesh areas to use |

```csharp
NavMeshAgent agent = GetComponent<NavMeshAgent>();
agent.SetDestination(targetPosition);
bool arrived = !agent.pathPending && agent.remainingDistance < agent.stoppingDistance;
agent.isStopped = true; // pause movement
```

### Nav Mesh Obstacle
Marks a moving object as an obstacle that other agents must avoid. Set **Carve** to cut a hole in the NavMesh.

### Off Mesh Link
Manually defines a path between two disconnected NavMesh surfaces (e.g., a jump or ladder).

---

## Effects / Particles

### Particle System
The primary tool for **visual effects**: fire, smoke, sparks, rain, magic, etc.

**Key modules:**
| Module | Purpose |
|---|---|
| Main | Lifetime, speed, start size/color, looping |
| Emission | Rate over time/distance, bursts |
| Shape | Emitter shape: sphere, cone, box, mesh |
| Velocity over Lifetime | Accelerate/decelerate particles |
| Color over Lifetime | Gradient fade |
| Size over Lifetime | Grow/shrink |
| Renderer | Material and render mode |
| Collision | Particles bounce or die on surfaces |
| Triggers | Run script events when particles enter colliders |

```csharp
ParticleSystem ps = GetComponent<ParticleSystem>();
ps.Play();
ps.Stop();
ps.Emit(10); // burst-emit 10 particles
var main = ps.main;
main.startColor = Color.red;
```

### Visual Effect Graph *(VFX Graph)*
GPU-accelerated visual effects built in a node graph. Can simulate millions of particles. Requires URP or HDRP.

### Halo
Adds a simple glow halo around a light. Legacy; prefer post-processing bloom.

### Lens Flare
Adds a lens flare effect. Requires a Flare asset assigned to the component.

### Projector *(Legacy)*
Projects a material onto surfaces like a shadow or decal. Legacy; prefer Decal Projector in URP/HDRP.

---

## Networking

### Network Transform / Network Animator *(Netcode for GameObjects)*
Synchronizes Transform or Animator state across the network.

### Network Object *(Netcode for GameObjects)*
Marks a GameObject as a networked object that can be spawned/despawned across all clients.

---

## Tilemap

### Tilemap
Stores and renders a grid of **Tiles** (sprites/prefabs) efficiently. Requires a **Grid** component on the parent.

```csharp
Tilemap tm = GetComponent<Tilemap>();
tm.SetTile(new Vector3Int(0, 0, 0), myTile);
TileBase t = tm.GetTile(new Vector3Int(1, 2, 0));
tm.ClearAllTiles();
```

### Tilemap Renderer
Renders the Tilemap. Controls sorting layer, order, and chunk/individual tile render modes.

### Tilemap Collider 2D
Generates colliders for every tile in the Tilemap. Combine with **Composite Collider 2D** for better performance.

### Grid
Parent component for Tilemaps. Defines cell size, gap, and layout (Rectangle, Hexagon, Isometric).

---

## Miscellaneous

### Event System
Required in every UI scene. Routes input events (clicks, keyboard) to UI elements. Usually auto-created with a Canvas.

### Event Trigger
Attach to any GameObject to hook into pointer, drag, and other input events without scripting.

### Physics Raycaster / Physics 2D Raycaster
Allows the Event System to raycast against 3D/2D physics colliders, enabling pointer events on world-space objects.

### LOD Group
Manages **Level of Detail** — swaps meshes at different distances for performance.

```csharp
LODGroup lod = GetComponent<LODGroup>();
lod.ForceLOD(0); // force highest quality level
```

### Occlusion Area / Occlusion Portal
Baked occlusion culling helpers. Areas define volumes for dynamic objects; Portals act as occlusion-aware doorways.

### Wind Zone
Affects particle systems and SpeedTree foliage with wind forces.

### Terrain
Renders large outdoor landscapes using a heightmap. Has sub-components for:
- **Terrain Collider** — physics surface
- **Detail Renderer** — grass and small plants
- **Tree Renderer** — instanced SpeedTree or mesh trees

### Grid Layout Group
Like a Layout Group but arranges children in a grid. Useful for inventories and tile-based menus.

### Sortable *(Sorting Group)*
Controls the sorting order of a group of renderers as if they were a single renderer.

### Sprite Shape Renderer / Sprite Shape Controller
Draw free-form 2D shapes (roads, rivers, terrain outlines) with a Sprite Shape profile.

### Sprite Mask
Masks (clips) Sprite Renderers to a sprite's shape. Controlled by the Sprite Renderer's **Mask Interaction** field.

### Distance Joint 2D / Spring Joint 2D / Hinge Joint 2D / Slider Joint 2D / Wheel Joint 2D / Relative Joint 2D / Fixed Joint 2D / Target Joint 2D *(2D Joints)*
2D physics joints that constrain the relative motion between two Rigidbody 2D objects.

### Fixed Joint / Hinge Joint / Spring Joint / Character Joint / Configurable Joint *(3D Joints)*
3D physics joints that constrain two Rigidbodies. Configurable Joint is the most flexible and can simulate all other joint types.

---

## Quick Reference: Common Component Access Patterns

```csharp
// Get a component on the same GameObject
var rb = GetComponent<Rigidbody>();

// Get a component on a child
var rb = GetComponentInChildren<Rigidbody>();

// Get a component on the parent
var rb = GetComponentInParent<Rigidbody>();

// Get multiple components
var colliders = GetComponents<Collider>();

// Safe get (null check)
if (TryGetComponent<Animator>(out var anim)) {
    anim.SetTrigger("Jump");
}
```

---

*Last updated: May 2026 — Unity 6*
