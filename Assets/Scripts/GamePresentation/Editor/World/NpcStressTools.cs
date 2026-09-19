using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Editor tooling for the NPC pathfinding stress test. Builds a large empty room into WorldB
/// (floor + perimeter walls, floor at y = -2..20, x = -2..36, clear of the existing training wing)
/// and launches a harness that ramps 25 / 50 / 100 wandering NPCs.
///
/// Unity setup: none. Menu: Tools &gt; Stress. Run <see cref="BuildRoom"/> once (a saved scene edit),
/// then <see cref="RunStress"/> (enters Play Mode).
/// </summary>
public static class NpcStressTools
{
    public const string RoomRoot = "NPC Stress Room";
    public const string ScenePath = "Assets/Scenes/WorldB.unity";

    private const string TilePath = "Assets/Settings/squares.asset";

    /// <summary>Floor cells (xMin, yMin, width, height). Walls are painted one cell outside.</summary>
    public static readonly RectInt FloorCells = new RectInt(-2, -2, 39, 23);

    [MenuItem("Tools/Stress/Build NPC Stress Room")]
    public static void BuildRoom()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            throw new System.InvalidOperationException("Open " + ScenePath + " first.");
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play Mode first.");
        if (GameObject.Find(RoomRoot) != null)
            throw new System.InvalidOperationException("The stress room already exists.");

        Grid grid = Object.FindAnyObjectByType<Grid>();
        if (grid == null)
            throw new System.InvalidOperationException("WorldB has no Grid to paint into.");

        EnsureLayers();

        TileBase tile = AssetDatabase.LoadAssetAtPath<TileBase>(TilePath);
        if (tile == null)
            throw new System.InvalidOperationException("Missing tile: " + TilePath);

        EnsureAreaIsEmpty();

        var root = new GameObject(RoomRoot);
        root.transform.SetParent(grid.transform, false);

        Tilemap floor = NewMap(root.transform, "Floor", -100, false);
        Tilemap walls = NewMap(root.transform, "Walls", -50, true);
        int wallsLayer = LayerMask.NameToLayer("Walls");
        if (wallsLayer >= 0)
            walls.gameObject.layer = wallsLayer;

        for (int x = FloorCells.xMin; x < FloorCells.xMax; x++)
            for (int y = FloorCells.yMin; y < FloorCells.yMax; y++)
                floor.SetTile(new Vector3Int(x, y, 0), tile);

        for (int x = FloorCells.xMin - 1; x <= FloorCells.xMax; x++)
            for (int y = FloorCells.yMin - 1; y <= FloorCells.yMax; y++)
                if (x < FloorCells.xMin || x >= FloorCells.xMax || y < FloorCells.yMin || y >= FloorCells.yMax)
                    walls.SetTile(new Vector3Int(x, y, 0), tile);

        floor.CompressBounds();
        walls.CompressBounds();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Directory.CreateDirectory("Temp");
        var report = new StringBuilder();
        report.AppendLine("NPC stress room built");
        report.AppendLine("floor cells: " + FloorCells);
        report.AppendLine("spawn world min = " + CellToWorld(grid, FloorCells.xMin, FloorCells.yMin));
        report.AppendLine("spawn world max = " + CellToWorld(grid, FloorCells.xMax, FloorCells.yMax));
        File.WriteAllText("Temp/npc-stress-room.txt", report.ToString());
        Debug.Log("[NPC Stress] Room built and saved. " + report);
    }

    [MenuItem("Tools/Stress/Run NPC Pathfinding Stress")]
    public static void RunStress()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Already in Play Mode.");

        if (EditorSceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (GameObject.Find(RoomRoot) == null)
            throw new System.InvalidOperationException("Build the stress room first (Tools > Stress > Build NPC Stress Room).");

        var harnessObject = new GameObject("NPC Stress Harness");
        var harness = harnessObject.AddComponent<NpcStressHarness>();

        var so = new SerializedObject(harness);
        so.FindProperty("spawnCellMinX").intValue = FloorCells.xMin;
        so.FindProperty("spawnCellMinY").intValue = FloorCells.yMin;
        so.FindProperty("spawnCellMaxX").intValue = FloorCells.xMax;
        so.FindProperty("spawnCellMaxY").intValue = FloorCells.yMax;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Stress/Delete NPC Stress Room")]
    public static void DeleteRoom()
    {
        GameObject root = GameObject.Find(RoomRoot);
        if (root == null)
            return;

        Object.DestroyImmediate(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[NPC Stress] Room deleted.");
    }

    /// <summary>Creates the Npc and Walls layers if they do not already exist.</summary>
    private static void EnsureLayers()
    {
        const int NpcLayer = 6;
        const int WallsLayer = 7;

        Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var so = new SerializedObject(tagManager);
        SerializedProperty layers = so.FindProperty("layers");

        SetLayerName(layers, NpcLayer, "Npc");
        SetLayerName(layers, WallsLayer, "Walls");
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLayerName(SerializedProperty layers, int index, string name)
    {
        SerializedProperty layer = layers.GetArrayElementAtIndex(index);
        if (string.IsNullOrEmpty(layer.stringValue) || layer.stringValue == name)
            layer.stringValue = name;
    }

    private static Tilemap NewMap(Transform parent, string name, int order, bool collider)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = order;
        if (collider)
            go.AddComponent<TilemapCollider2D>();
        return go.GetComponent<Tilemap>();
    }

    private static void EnsureAreaIsEmpty()
    {
        var expanded = new RectInt(FloorCells.xMin - 2, FloorCells.yMin - 2, FloorCells.width + 4, FloorCells.height + 4);
        foreach (Tilemap tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include))
        {
            foreach (Vector3Int cell in expanded.allPositionsWithin)
            {
                if (tilemap.HasTile(cell))
                    throw new System.InvalidOperationException(
                        "Existing tiles from '" + tilemap.gameObject.name + "' overlap the stress room at cell " + cell +
                        ". Choose different FloorCells.");
            }
        }
    }

    private static Vector3 CellToWorld(Grid grid, int x, int y) =>
        grid.GetCellCenterWorld(new Vector3Int(x, y, 0));
}
