#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// A Tile Palette brush that paints functional SlidingDoor prefab instances at exact square-grid
/// positions. It selects a 1- through 4-tile door, centers odd lengths on the painted cell,
/// offsets even lengths onto cell boundaries, supports 90-degree rotation, and erases doors.
///
/// Unity setup:
///   1. Open Window > 2D > Tile Palette and choose Door Placement Brush from the brush dropdown.
///   2. Select an unscaled rectangular Grid as the Active Target.
///   3. Set Tile Length from 1 through 4 and enable Horizontal when needed.
///   4. Paint or erase in the Scene view; instances are grouped under Painted Doors on the Grid.
///   The matching SlidingDoor_1Tile through SlidingDoor_4Tile prefabs must exist in Assets/Prefabs.
///
/// Runtime API: none; this editor-only brush places normal SlidingDoor prefab instances.
/// </summary>
[CustomGridBrush(false, false, false, "Door Placement Brush")]
public sealed class DoorPlacementBrush : GridBrushBase
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string PaintedDoorsName = "Painted Doors";

    [SerializeField, Range(1, 4)] private int tileLength = 1;
    [SerializeField] private bool horizontal;
    [Tooltip("Vertical: right becomes left. Horizontal: down becomes up.")]
    [SerializeField] private bool reverseSlideDirection;

    public override void Paint(GridLayout gridLayout, GameObject brushTarget, Vector3Int position)
    {
        if (!TryValidateGrid(gridLayout))
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"{PrefabFolder}/SlidingDoor_{tileLength}Tile.prefab");
        if (prefab == null)
        {
            Debug.LogError($"Door Placement Brush could not find the {tileLength}-tile door prefab.");
            return;
        }

        Transform container = GetOrCreateContainer(gridLayout);
        Vector3 worldPosition = GetPlacementPosition(gridLayout, position);
        if (FindDoorAtPoint(container, worldPosition) != null)
            return;

        GameObject instance = PrefabUtility.InstantiatePrefab(
            prefab,
            gridLayout.gameObject.scene) as GameObject;
        if (instance == null)
            return;

        Undo.RegisterCreatedObjectUndo(instance, "Paint Sliding Door");
        instance.transform.SetParent(container, true);
        instance.transform.SetPositionAndRotation(worldPosition, gridLayout.transform.rotation);
        instance.transform.localScale = Vector3.one;
        instance.name = prefab.name;
        SlidingDoor door = instance.GetComponent<SlidingDoor>();
        if (door != null)
        {
            SerializedObject serializedDoor = new SerializedObject(door);
            serializedDoor.FindProperty("grid").objectReferenceValue = gridLayout as Grid;
            serializedDoor.FindProperty("axis").enumValueIndex = (int)(horizontal ? DoorAxis.GridY : DoorAxis.GridX);
            serializedDoor.FindProperty("cellLength").intValue = tileLength;
            serializedDoor.FindProperty("reverseSlideDirection").boolValue = reverseSlideDirection;
            serializedDoor.ApplyModifiedProperties();
            PrefabUtility.RecordPrefabInstancePropertyModifications(door);
        }
        Selection.activeGameObject = instance;
    }

    public override void Erase(GridLayout gridLayout, GameObject brushTarget, Vector3Int position)
    {
        if (gridLayout == null)
            return;

        Transform container = gridLayout.transform.Find(PaintedDoorsName);
        if (container == null)
            return;

        Vector3 cellCenter = GetCellCenter(gridLayout, position);
        SlidingDoor door = FindDoorAtPoint(container, cellCenter);
        if (door != null)
            Undo.DestroyObjectImmediate(door.gameObject);
    }

    public override void Rotate(RotationDirection direction, GridLayout.CellLayout layout)
    {
        horizontal = !horizontal;
        EditorUtility.SetDirty(this);
    }

    private bool TryValidateGrid(GridLayout gridLayout)
    {
        if (gridLayout == null)
        {
            Debug.LogWarning("Door Placement Brush needs a Grid or Tilemap Active Target.");
            return false;
        }

        if (gridLayout.cellLayout != GridLayout.CellLayout.Rectangle &&
            gridLayout.cellLayout != GridLayout.CellLayout.Isometric)
        {
            Debug.LogWarning("Door Placement Brush supports rectangular and isometric grids only.");
            return false;
        }

        Vector3 scale = gridLayout.transform.lossyScale;
        if (!ApproximatelyOne(scale.x) || !ApproximatelyOne(scale.y))
        {
            Debug.LogWarning("Door Placement Brush requires the target Grid scale to be (1, 1, 1).", gridLayout);
            return false;
        }

        return true;
    }

    private Vector3 GetPlacementPosition(GridLayout gridLayout, Vector3Int cell)
    {
        Vector3 center = GetCellCenter(gridLayout, cell);
        if (tileLength % 2 != 0)
            return center;

        Vector3Int nextCell = cell + (horizontal ? Vector3Int.right : Vector3Int.up);
        Vector3 axisStep = GetCellCenter(gridLayout, nextCell) - center;
        return center + axisStep * 0.5f;
    }

    private static Vector3 GetCellCenter(GridLayout gridLayout, Vector3Int cell)
    {
        Vector3 origin = gridLayout.CellToWorld(cell);
        Vector3 xStep = gridLayout.CellToWorld(cell + Vector3Int.right) - origin;
        Vector3 yStep = gridLayout.CellToWorld(cell + Vector3Int.up) - origin;
        return origin + (xStep + yStep) * 0.5f;
    }

    private static Transform GetOrCreateContainer(GridLayout gridLayout)
    {
        Transform existing = gridLayout.transform.Find(PaintedDoorsName);
        if (existing != null)
            return existing;

        GameObject container = new GameObject(PaintedDoorsName);
        Undo.RegisterCreatedObjectUndo(container, "Create Painted Doors Container");
        container.transform.SetParent(gridLayout.transform, false);
        return container.transform;
    }

    private static SlidingDoor FindDoorAtPoint(Transform container, Vector3 worldPoint)
    {
        SlidingDoor[] doors = container.GetComponentsInChildren<SlidingDoor>(true);
        for (int i = 0; i < doors.Length; i++)
        {
            SlidingDoor door = doors[i];
            Collider2D[] colliders = door.GetComponentsInChildren<Collider2D>(true);
            for (int c = 0; c < colliders.Length; c++)
            {
                if (colliders[c] != null && !colliders[c].isTrigger && colliders[c].bounds.Contains(worldPoint))
                    return door;
            }
        }

        return null;
    }

    private static bool ApproximatelyOne(float value) => Mathf.Approximately(Mathf.Abs(value), 1f);
}
#endif
