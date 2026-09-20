using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds the placeholder town scene (no art).
///
/// Ground is tiles: green grass, dark-gray streets, a park with a red plaza.
///
/// BUILDINGS ARE NOT TILES. Each building is a plain GameObject with real BoxCollider2D colliders on
/// the `Walls` layer (so it blocks the player and NPC pathfinding exactly like an NPC or the training
/// spawn box), a square SpriteRenderer for its body, and a pink square SpriteRenderer at its single
/// doorway. The collider is split into three boxes so the doorway cell is the only way through.
///
/// Painting is deferred one editor tick: a Tilemap created in the same tick as its Grid silently
/// ignores SetTile. See Documents/TOWN_SCENE.md.
///
/// Unity setup: none. Menu: Tools &gt; Worlds &gt; Create Town Scene (refuses if the scene exists).
/// </summary>
public static class TownSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Town.unity";
    private const string TilesDir = "Assets/tiles/Town";
    private const string SpritePath = "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png";

    private const int GridW = 64;
    private const int GridH = 44;
    private const float CellW = 1f;
    private const float CellH = 0.5f;

    private const float BlockHeight = 0.25f;

    private static readonly Color32 GrassColor    = new Color32(76, 175, 80, 255);
    private static readonly Color32 StreetColor   = new Color32(55, 71, 79, 255);
    private static readonly Color32 PlazaColor    = new Color32(229, 57, 53, 255);
    private static readonly Color32 BuildingColor = new Color32(176, 190, 197, 255);
    private static readonly Color32 EntranceColor = new Color32(255, 105, 180, 255);

    private static readonly List<(int x, int y, int w, int d, char side)> Buildings = new()
    {
        // Rows are spaced so the building AABBs (wider than the 4x3 footprint in isometric) never
        // touch: 8 cells apart horizontally, 10 cells apart vertically.
        (4, 14, 4, 3, 'S'), (12, 14, 4, 3, 'S'), (20, 14, 4, 3, 'S'), (4, 4, 4, 3, 'S'),
        (35, 14, 4, 3, 'S'), (43, 14, 4, 3, 'S'), (51, 14, 4, 3, 'S'), (35, 4, 4, 3, 'S'),
        (4, 24, 4, 3, 'N'), (12, 24, 4, 3, 'N'), (4, 34, 4, 3, 'N'),
        (35, 24, 4, 3, 'N'), (53, 24, 4, 3, 'N'), (35, 36, 4, 3, 'N'),
    };

    private static Scene scene;
    private static Grid grid;
    private static Tilemap grass, park, streets;
    private static Tile grassTile, streetTile, plazaTile;
    private static Sprite squareSprite, diamondSprite;

    [MenuItem("Tools/Worlds/Create Town Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play Mode first.");
        if (File.Exists(ScenePath))
            throw new System.InvalidOperationException(ScenePath + " already exists. Delete it first.");

        squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (squareSprite == null)
            throw new System.InvalidOperationException("Square sprite not found: " + SpritePath);

        // Ground/roads use a DIAMOND sprite: a square sprite drawn in an isometric tilemap stays
        // axis-aligned and staircases at every boundary. A 2:1 anti-aliased diamond tiles cleanly.
        diamondSprite = EnsureDiamondSprite();

        grassTile  = EnsureTile("GrassTile", GrassColor);
        streetTile = EnsureTile("StreetTile", StreetColor);
        plazaTile  = EnsureTile("PlazaTile", PlazaColor);
        AssetDatabase.SaveAssets();

        scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var gridObject = new GameObject("Town Grid", typeof(Grid));
        grid = gridObject.GetComponent<Grid>();
        grid.cellSize = new Vector3(CellW, CellH, 0f);
        grid.cellLayout = GridLayout.CellLayout.Isometric;

        grass   = NewMap(grid.transform, "Grass", -100);
        park    = NewMap(grid.transform, "Park", -95);
        streets = NewMap(grid.transform, "Streets", -90);

        BuildCamera();

        var identityObject = new GameObject("Town Scene Identity");
        var identity = identityObject.AddComponent<WorldSceneIdentity>();
        var identitySo = new SerializedObject(identity);
        identitySo.FindProperty("world").intValue = (int)WorldLayer.WorldA;
        identitySo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        EditorApplication.delayCall += FinishPaint;
    }

    /// <summary>Paints the ground tiles and creates the building objects. Safe to call directly.</summary>
    public static void FinishPaint()
    {
        EditorApplication.delayCall -= FinishPaint;

        var isStreet = new HashSet<string>();
        for (int cx = 30; cx <= 32; cx++)
            for (int cy = 0; cy < GridH; cy++) isStreet.Add(cx + "," + cy);
        for (int cy = 20; cy <= 22; cy++)
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

                if (cx >= 44 && cx < 52 && cy >= 30 && cy < 36)
                    park.SetTile(cell, (cx >= 47 && cx < 50 && cy >= 32 && cy < 35) ? plazaTile : grassTile);
            }
        }

        grass.RefreshAllTiles();
        park.RefreshAllTiles();
        streets.RefreshAllTiles();
        grass.CompressBounds();
        park.CompressBounds();
        streets.CompressBounds();

        int built = BuildBuildings();

        // Doors teleport through the portal system, so the scene needs a PortalManager.
        var portalManager = new GameObject("Town Portal Manager");
        portalManager.AddComponent<PortalManager>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AddToBuildSettings(ScenePath);

        Directory.CreateDirectory("Temp");
        var report = new StringBuilder();
        report.AppendLine("Town scene built: " + ScenePath);
        report.AppendLine("grid " + GridW + "x" + GridH + " isometric cell (1, 0.5)");
        report.AppendLine("buildings (GameObjects with BoxCollider2D, not tiles): " + built
            + " ; layer " + LayerMask.NameToLayer("Walls"));
        report.AppendLine("ground tiles: grass=" + CountTiles(grass) + " streets=" + CountTiles(streets)
            + " park=" + CountTiles(park));
        File.WriteAllText("Temp/town-build.txt", report.ToString());
        Debug.Log("[Town] " + report);
    }

    /// <summary>Creates one GameObject per building with a collider gap at the doorway.</summary>
    private static int BuildBuildings()
    {
        var root = new GameObject("Buildings");
        int wallsLayer = Mathf.Max(0, LayerMask.NameToLayer("Walls"));

        foreach (var b in Buildings)
        {
            // The four grid corners of the footprint — a parallelogram, not a rectangle.
            Vector3[] corners =
            {
                grid.CellToWorld(new Vector3Int(b.x, b.y, 0)),
                grid.CellToWorld(new Vector3Int(b.x + b.w, b.y, 0)),
                grid.CellToWorld(new Vector3Int(b.x + b.w, b.y + b.d, 0)),
                grid.CellToWorld(new Vector3Int(b.x, b.y + b.d, 0)),
            };
            Vector3 center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;

            var building = new GameObject("Building_" + b.x + "_" + b.y);
            building.layer = wallsLayer;
            building.transform.SetParent(root.transform, false);
            building.transform.position = center;
            // No rotation: the body and collider are the grid parallelogram itself.

            // Body: a grid-exact parallelogram (edges along both street axes), so it lines up with
            // the whole town, not just one road.
            var body = new GameObject("Body");
            body.transform.SetParent(building.transform, false);
            var bodyRenderer = body.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = EnsureBlockSprite(b.w, b.d);
            bodyRenderer.color = BuildingColor;
            bodyRenderer.sortingOrder = 10;

            // Solid collider with the same four grid corners, on the Walls layer (blocks the player
            // and NPC pathfinding, like an NPC or the training spawn box).
            var collider = building.AddComponent<PolygonCollider2D>();
            collider.points = new[]
            {
                (Vector2)(corners[0] - center),
                (Vector2)(corners[1] - center),
                (Vector2)(corners[2] - center),
                (Vector2)(corners[3] - center),
            };

            // Pink door on the front edge (which is parallel to the street), wired through the portal
            // system with blank destination fields.
            Vector3 edgeA = b.side == 'S' ? corners[3] : corners[0];
            Vector3 edgeB = b.side == 'S' ? corners[2] : corners[1];
            Vector3 edgeMid = (edgeA + edgeB) * 0.5f;

            var door = new GameObject("Door");
            door.transform.SetParent(building.transform, false);
            var doorRenderer = door.AddComponent<SpriteRenderer>();
            doorRenderer.sprite = squareSprite;
            doorRenderer.color = EntranceColor;
            doorRenderer.sortingOrder = 11;
            door.transform.localScale = new Vector3(0.5f, 0.25f, 1f);
            door.transform.localPosition = edgeMid - center;
            door.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(CellH * 0.5f, CellW * 0.5f) * Mathf.Rad2Deg);

            var doorCollider = door.AddComponent<BoxCollider2D>();
            doorCollider.isTrigger = true;

            var portal = door.AddComponent<PortalTrigger2D>();
            var portalSo = new SerializedObject(portal);
            SerializedProperty idProperty = portalSo.FindProperty("portalId");
            if (idProperty != null)
                idProperty.stringValue = "door_" + b.x + "_" + b.y;
            portalSo.ApplyModifiedPropertiesWithoutUndo();
        }

        return Buildings.Count;
    }

    private static IEnumerable<Vector2Int> Corners((int x, int y, int w, int d, char side) b)
    {
        yield return new Vector2Int(b.x, b.y);
        yield return new Vector2Int(b.x + b.w, b.y);
        yield return new Vector2Int(b.x, b.y + b.d);
        yield return new Vector2Int(b.x + b.w, b.y + b.d);
    }

    private static void AddBox(GameObject parent, string name, Vector2 offset, Vector2 size, int layer)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = offset;
        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = size;
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

    private static Tilemap NewMap(Transform parent, string name, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = order;
        return go.GetComponent<Tilemap>();
    }

    /// <summary>
    /// Generates (once) a 2:1 diamond sprite with anti-aliased edges, sized so one tile fills one
    /// isometric cell (64x32 px at 64 px/unit = 1 x 0.5 units).
    /// </summary>
    private static Sprite EnsureDiamondSprite()
    {
        Directory.CreateDirectory(TilesDir);
        string path = TilesDir + "/Diamond.png";
        if (!File.Exists(path))
        {
            const int w = 64;
            const int h = 32;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            const float cx = (w - 1) * 0.5f;
            const float cy = (h - 1) * 0.5f;
            const float a = (w - 1) * 0.5f;
            const float b = (h - 1) * 0.5f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 4x supersample to anti-alias the diagonal edges.
                    float inside = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float px = x + (sx + 0.5f) * 0.5f;
                            float py = y + (sy + 0.5f) * 0.5f;
                            float d = Mathf.Abs(px - cx) / a + Mathf.Abs(py - cy) / b;
                            if (d <= 1f)
                                inside += 0.25f;
                        }
                    }
                    pixels[y * w + x] = new Color(1f, 1f, 1f, inside);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new System.InvalidOperationException("Could not create/load diamond sprite: " + path);
        return sprite;
    }

    private static readonly Dictionary<string, Sprite> blockSprites = new();

    /// <summary>
    /// Generates (once) a parallelogram sprite for a w x d cell block. The edges follow the two
    /// isometric grid axes, so the shape matches the grid exactly and needs no rotation.
    /// </summary>
    private static Sprite EnsureBlockSprite(int w, int d)
    {
        string key = w + "x" + d;
        if (blockSprites.TryGetValue(key, out Sprite cached) && cached != null)
            return cached;

        Directory.CreateDirectory(TilesDir);
        string path = TilesDir + "/Block_" + key + ".png";

        if (!File.Exists(path))
        {
            const float halfW = 32f; // px per grid unit along +x (64 px/unit, 2:1 iso)
            const float halfH = 16f; // px per grid unit along +y
            const float pad = 2f;

            var gridCorners = new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, d), new Vector2(0, d) };
            var pts = new Vector2[4];
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float px = (gridCorners[i].x - gridCorners[i].y) * halfW;
                float py = (gridCorners[i].x + gridCorners[i].y) * halfH;
                pts[i] = new Vector2(px, py);
                minX = Mathf.Min(minX, px); maxX = Mathf.Max(maxX, px);
                minY = Mathf.Min(minY, py); maxY = Mathf.Max(maxY, py);
            }

            int imgW = Mathf.CeilToInt(maxX - minX + pad * 2f);
            int imgH = Mathf.CeilToInt(maxY - minY + pad * 2f);
            var texture = new Texture2D(imgW, imgH, TextureFormat.RGBA32, false);
            var pixels = new Color[imgW * imgH];

            for (int y = 0; y < imgH; y++)
            {
                for (int x = 0; x < imgW; x++)
                {
                    float inside = 0f;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float px = x + pad - minX + (sx + 0.5f) * 0.5f;
                            float py = y + pad - minY + (sy + 0.5f) * 0.5f;
                            if (InsideQuad(px, py, pts))
                                inside += 0.25f;
                        }
                    }
                    pixels[y * imgW + x] = new Color(1f, 1f, 1f, inside);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        blockSprites[key] = sprite;
        return sprite;
    }

    /// <summary>Convex point-in-quad test (all cross products the same sign).</summary>
    private static bool InsideQuad(float x, float y, Vector2[] p)
    {
        float Cross(Vector2 a, Vector2 b) => (b.x - a.x) * (y - a.y) - (b.y - a.y) * (x - a.x);
        float s0 = Cross(p[0], p[1]);
        float s1 = Cross(p[1], p[2]);
        float s2 = Cross(p[2], p[3]);
        float s3 = Cross(p[3], p[0]);
        bool hasNeg = s0 < 0f || s1 < 0f || s2 < 0f || s3 < 0f;
        bool hasPos = s0 > 0f || s1 > 0f || s2 > 0f || s3 > 0f;
        return !(hasNeg && hasPos);
    }

    private static Tile EnsureTile(string name, Color32 color)
    {
        Directory.CreateDirectory(TilesDir);
        string path = TilesDir + "/" + name + ".asset";
        Tile existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (existing != null)
        {
            existing.sprite = diamondSprite;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = diamondSprite;
        tile.color = color;
        tile.colliderType = Tile.ColliderType.None;
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
