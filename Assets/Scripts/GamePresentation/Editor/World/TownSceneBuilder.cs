using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds the placeholder town scene (no art): green grass, dark-gray streets in a cross, solid
/// building blocks with one passable pink entrance each, and a park with a red plaza.
///
/// Buildings are solid: the footprint is painted with a colliding tile on a tilemap whose collider is
/// on the `Walls` layer, so pathfinding (obstacleLayers = everything except Npc) treats them as
/// obstacles. The entrance cell is left unpainted on the building tilemap and painted on a
/// non-colliding entrance tilemap, so it is the only passable cell of each building.
///
/// Painting is deferred by one editor tick (`EditorApplication.delayCall`): a tilemap created in the
/// same tick as its Grid does not accept SetTile calls yet, which silently produced empty tilemaps.
/// The result is written to Temp/town-build.txt.
///
/// Unity setup: none. Menu: Tools &gt; Worlds &gt; Create Town Scene (refuses if the scene exists).
/// </summary>
public static class TownSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Town.unity";
    private const string TilesDir = "Assets/tiles/Town";
    private const string SpritePath = "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png";

    private const int GridW = 40;
    private const int GridH = 28;

    private static readonly Color32 GrassColor    = new Color32(76, 175, 80, 255);
    private static readonly Color32 StreetColor   = new Color32(55, 71, 79, 255);
    private static readonly Color32 BuildingColor = new Color32(176, 190, 197, 255);
    private static readonly Color32 EntranceColor = new Color32(255, 105, 180, 255);
    private static readonly Color32 PlazaColor    = new Color32(229, 57, 53, 255);

    private static readonly List<(int x, int y, int w, int d, char side)> Buildings = new()
    {
        (4, 7, 6, 4, 'S'), (12, 7, 5, 4, 'S'), (4, 3, 5, 3, 'S'),
        (22, 7, 7, 4, 'S'), (31, 7, 5, 4, 'S'), (25, 3, 6, 3, 'S'),
        (4, 16, 6, 4, 'N'), (12, 16, 5, 4, 'N'), (4, 21, 5, 3, 'N'),
    };

    // Paint phase state (survives to the deferred call).
    private static Scene scene;
    private static Tilemap grass, park, streets, entrances, buildings;
    private static Tile grassTile, streetTile, buildingTile, entranceTile, plazaTile;

    [MenuItem("Tools/Worlds/Create Town Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play Mode first.");
        if (File.Exists(ScenePath))
            throw new System.InvalidOperationException(ScenePath + " already exists. Delete it first.");

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
            throw new System.InvalidOperationException("Square sprite not found: " + SpritePath);

        grassTile    = EnsureTile("GrassTile", sprite, GrassColor, Tile.ColliderType.None);
        streetTile   = EnsureTile("StreetTile", sprite, StreetColor, Tile.ColliderType.None);
        buildingTile = EnsureTile("BuildingTile", sprite, BuildingColor, Tile.ColliderType.Grid);
        entranceTile = EnsureTile("EntranceTile", sprite, EntranceColor, Tile.ColliderType.None);
        plazaTile    = EnsureTile("PlazaTile", sprite, PlazaColor, Tile.ColliderType.None);
        AssetDatabase.SaveAssets();

        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var gridObject = new GameObject("Town Grid", typeof(Grid));
        var grid = gridObject.GetComponent<Grid>();
        grid.cellSize = new Vector3(1f, 0.5f, 0f);
        grid.cellLayout = GridLayout.CellLayout.Isometric;

        int wallsLayer = LayerMask.NameToLayer("Walls");

        grass     = NewMap(grid.transform, "Grass", -100);
        park      = NewMap(grid.transform, "Park", -95);
        streets   = NewMap(grid.transform, "Streets", -90);
        entrances = NewMap(grid.transform, "Entrances", -85);
        buildings = NewMap(grid.transform, "Buildings", -50, collider: true, layer: wallsLayer);

        BuildCamera();

        var identityObject = new GameObject("Town Scene Identity");
        var identity = identityObject.AddComponent<WorldSceneIdentity>();
        var identitySo = new SerializedObject(identity);
        identitySo.FindProperty("world").intValue = (int)WorldLayer.WorldA;
        identitySo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        // Paint on the next editor tick once the Grid/Tilemaps are live.
        EditorApplication.delayCall += FinishPaint;
    }

    public static void FinishPaint()
    {
        EditorApplication.delayCall -= FinishPaint;

        var isStreet = new HashSet<string>();
        for (int cx = 18; cx <= 20; cx++)
            for (int cy = 0; cy < GridH; cy++) isStreet.Add(cx + "," + cy);
        for (int cy = 12; cy <= 14; cy++)
            for (int cx = 0; cx < GridW; cx++) isStreet.Add(cx + "," + cy);

        for (int cx = 0; cx < GridW; cx++)
        {
            for (int cy = 0; cy < GridH; cy++)
            {
                var cell = new Vector3Int(cx, cy, 0);
                if (isStreet.Contains(cx + "," + cy))
                    streets.SetTile(cell, streetTile);
                else
                    grass.SetTile(cell, grassTile);

                if (cx >= 25 && cx < 33 && cy >= 17 && cy < 23)
                    park.SetTile(cell, (cx >= 28 && cx < 31 && cy >= 19 && cy < 22) ? plazaTile : grassTile);
            }
        }

        int entranceCount = 0;
        foreach (var b in Buildings)
        {
            int ex = b.x + b.w / 2;
            int ey = b.side == 'S' ? b.y + b.d - 1 : b.y;

            for (int cx = b.x; cx < b.x + b.w; cx++)
                for (int cy = b.y; cy < b.y + b.d; cy++)
                    if (!(cx == ex && cy == ey))
                        buildings.SetTile(new Vector3Int(cx, cy, 0), buildingTile);

            entrances.SetTile(new Vector3Int(ex, ey, 0), entranceTile);
            entranceCount++;
        }

        grass.RefreshAllTiles();
        park.RefreshAllTiles();
        streets.RefreshAllTiles();
        entrances.RefreshAllTiles();
        buildings.RefreshAllTiles();

        grass.CompressBounds();
        park.CompressBounds();
        streets.CompressBounds();
        entrances.CompressBounds();
        buildings.CompressBounds();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AddToBuildSettings(ScenePath);

        Directory.CreateDirectory("Temp");
        var report = new StringBuilder();
        report.AppendLine("Town scene built: " + ScenePath);
        report.AppendLine("grid " + GridW + "x" + GridH + " isometric cell (1, 0.5)");
        report.AppendLine("buildings solid on layer " + LayerMask.NameToLayer("Walls") + " ('Walls'); entrances passable: " + entranceCount);
        report.AppendLine("painted: grass=" + CountTiles(grass)
            + " streets=" + CountTiles(streets)
            + " park=" + CountTiles(park)
            + " buildings=" + CountTiles(buildings)
            + " entrances=" + CountTiles(entrances));
        File.WriteAllText("Temp/town-build.txt", report.ToString());
        Debug.Log("[Town] " + report);
    }

    private static int CountTiles(Tilemap map)
    {
        if (map == null)
            return -1;
        int n = 0;
        foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            if (map.HasTile(cell))
                n++;
        return n;
    }

    private static Tilemap NewMap(Transform parent, string name, int order, bool collider = false, int layer = 0)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = order;
        if (layer != 0)
            go.layer = layer;
        if (collider)
            go.AddComponent<TilemapCollider2D>();
        return go.GetComponent<Tilemap>();
    }

    private static Tile EnsureTile(string name, Sprite sprite, Color32 color, Tile.ColliderType collider)
    {
        Directory.CreateDirectory(TilesDir);
        string path = TilesDir + "/" + name + ".asset";
        Tile existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (existing != null)
            return existing;

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.color = color;
        tile.colliderType = collider;
        AssetDatabase.CreateAsset(tile, path);
        return tile;
    }

    private static void BuildCamera()
    {
        var go = new GameObject("Town Camera", typeof(Camera));
        Camera camera = go.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 11f;
        camera.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.transform.position = new Vector3(3f, 8f, -10f);
        go.tag = "MainCamera";
    }

    private static void AddToBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (EditorBuildSettingsScene entry in scenes)
            if (entry.path == scenePath)
                return;

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
