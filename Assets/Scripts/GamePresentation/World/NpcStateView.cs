using Game.Core;
using UnityEngine;

/// <summary>
/// Unity adapter that binds a pure-C# <see cref="NpcState"/> model to an NPC. The model is the
/// authority for saved HP and logical cell; this component only presents it and mirrors physics
/// movement back into the model as a command.
///
/// Works in 2D and 3D scenes: a 2D NPC (Rigidbody2D) maps through the scene Grid on XY; a 3D
/// planar-isometric NPC (Rigidbody) maps cells on the XZ plane using Cell Size, since 3D scenes
/// have no Grid. In both cases CellX → world X and CellY → the plane's second axis.
///
/// Unity setup:
///   1. Add to the NPC root, next to NpcController (+ EntityStats for enemies).
///   2. Set Npc Id to a stable, unique id (defaults to NpcController.NpcId).
///   3. 2D: Grid defaults to the nearest parent Grid. 3D: set Cell Size to the layout grid size.
///   4. Body defaults to this transform; assign a child transform if the visual is offset.
///
/// Runtime API:
///   NpcId — the stable model key.
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcStateView : MonoBehaviour
{
    [Tooltip("Stable model key. Defaults to NpcController.NpcId, then the GameObject name.")]
    [SerializeField] private string npcId = "";
    [SerializeField] private EntityStats stats;
    [SerializeField] private Transform body;
    [SerializeField] private Grid grid;
    [Tooltip("World size of one logical cell for 3D scenes (ignored when a Grid is present).")]
    [SerializeField, Min(0.01f)] private float cellSize = 1f;

    private NpcState model;
    private int lastCellX = int.MinValue;
    private int lastCellY = int.MinValue;

    /// <summary>Stable id identifying the model this view displays.</summary>
    public string NpcId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(npcId))
                return npcId;

            var controller = GetComponent<NpcController>();
            return controller != null && !string.IsNullOrWhiteSpace(controller.NpcId)
                ? controller.NpcId
                : gameObject.name;
        }
    }

    private void Awake()
    {
        if (stats == null)
            stats = GetComponent<EntityStats>();
        if (body == null)
            body = transform;
        if (grid == null)
            grid = GetComponentInParent<Grid>();
    }

    private void OnEnable()
    {
        GameSessionHost.EnsureExists();
        GameSession session = GameSessionHost.Session;
        if (session == null)
            return;

        int maxHp = stats != null ? stats.MaxHp : 1;
        int hp = stats != null ? stats.Hp : maxHp;
        Vector3Int cell = WorldToCell(body.position);

        // GetOrCreate returns the existing model after a view is destroyed and recreated, so the
        // gameplay state is not reset by the view lifecycle.
        model = session.NpcStates.Register(NpcId, Mathf.Max(1, maxHp), hp, cell.x, cell.y);

        model.Changed -= HandleModelChanged;
        model.Changed += HandleModelChanged;

        // EntityStats becomes a facade over the model; it no longer owns HP.
        stats?.BindHealthModel(model);

        lastCellX = cell.x;
        lastCellY = cell.y;
        ApplyModelToView();
    }

    private void OnDisable()
    {
        if (model != null)
            model.Changed -= HandleModelChanged;

        // Leaving the view still leaves the model intact in the session.
        stats?.BindHealthModel(null);
        model = null;
    }

    private void LateUpdate()
    {
        if (model == null || body == null)
            return;

        // Transitional compromise: physics still moves the transform, so mirror the logical cell
        // into the model as a command. Save/load reads the model, not the MonoBehaviour.
        Vector3Int cell = WorldToCell(body.position);
        if (cell.x != lastCellX || cell.y != lastCellY)
        {
            lastCellX = cell.x;
            lastCellY = cell.y;
            model.MoveToCell(cell.x, cell.y);
        }
    }

    private void HandleModelChanged(NpcState state) => ApplyModelToView();

    // 3D planar-isometric NPCs have no Grid: cells are Cell Size units on the XZ plane.
    private bool Is3D => !TryGetComponent(out Rigidbody2D _);

    private Vector3Int WorldToCell(Vector3 world)
    {
        if (!Is3D && grid != null)
            return grid.WorldToCell(world);

        return new Vector3Int(
            Mathf.FloorToInt(world.x / cellSize),
            Mathf.FloorToInt(world.z / cellSize),
            0);
    }

    private Vector3 CellToWorld(int cellX, int cellY, Vector3 reference)
    {
        if (!Is3D && grid != null)
        {
            Vector3 world = grid.GetCellCenterWorld(new Vector3Int(cellX, cellY, 0));
            world.z = reference.z;
            return world;
        }

        // Cells drive X/Z; keep the body's current height.
        return new Vector3((cellX + 0.5f) * cellSize, reference.y, (cellY + 0.5f) * cellSize);
    }

    private void ApplyModelToView()
    {
        if (model == null)
            return;

        stats?.SetHpFromModel(model.Hp);

        // Only reposition when the model's logical cell differs from where the body already is,
        // so binding does not nudge an authored NPC on startup. After a load, the restored cell
        // moves the body to the saved position.
        if (body != null)
        {
            Vector3Int currentCell = WorldToCell(body.position);
            if (currentCell.x != model.CellX || currentCell.y != model.CellY)
                body.position = CellToWorld(model.CellX, model.CellY, body.position);
        }
    }
}
