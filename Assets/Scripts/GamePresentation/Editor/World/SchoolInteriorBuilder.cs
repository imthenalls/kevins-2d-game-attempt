using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the Akron Community School interior as content inside the existing Town interior scene,
/// under a single <c>School Interior</c> root. It does not create or load a scene: the school and the
/// main-building foyer share <c>Assets/Scenes/Town.unity</c>, and the foyer's <c>school_gate</c>
/// portal teleports (same-scene) to the school's <c>school_entrance</c> portal.
///
/// Layout (abstracted from the school floor plan): south entrance, central commons, west
/// classroom/science/art wings, central auditorium and offices, north kitchen/dining and workshops,
/// east gym with lockers, southeast music/wrestling, joined by corridors with genuine doorways.
/// Presentation matches the Town interior: coloured cell-mesh floors, height-1 wall boxes on the
/// <c>Walls</c> layer, and simple coloured prop boxes.
///
/// The caller creates the returned root at the origin during the build, then moves the root to a
/// position clear of the foyer; because every object is parented under the root, the whole school
/// moves with it. See <c>Town3DSceneBuilder.SchoolOrigin</c>.
///
/// Unity setup: none directly — Tools &gt; Worlds &gt; Rebuild Town Scene (3D) calls this, and
/// Tools &gt; Worlds &gt; Town 3D &gt; Refresh School Interior rebuilds it in the open Town scene.
///
/// Runtime API: none (editor only).
/// </summary>
public static class SchoolInteriorBuilder
{
    public const string RootName = "School Interior";
    public const string EntrancePortalId = "school_entrance";
    public const string FoyerPortalId = "school_gate";

    private const string MaterialsDir = "Assets/Materials/School3D";
    private const string SpritePath =
        "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png";

    private const int GridW = 100;
    private const int GridH = 74;
    private const float CellSize = 1f;
    private const float WallHeight = 1f;
    private const float WallThickness = 0.3f;

    // ── Layout data ──────────────────────────────────────────────────────────

    private enum RoomKind
    {
        Classroom, Science, Art, Auditorium, Admin, Dining, Kitchen,
        Gym, Lockers, Music, Wrestling, Workshop, Library, Study
    }

    private sealed class RoomDef
    {
        public string Id;
        public string Name;
        public Color Color;
        public RoomKind Kind;
        public Rect[] Rects;

        public RoomDef(string id, string name, Color color, RoomKind kind, params Rect[] rects)
        {
            Id = id; Name = name; Color = color; Kind = kind; Rects = rects;
        }
    }

    // Orientation: +X east, +Z north, entrance at the south (-Z) edge. Cell coordinates.
    private static readonly Rect[] Corridors =
    {
        new Rect(44, 2, 12, 8),    // entrance lobby (south)
        new Rect(44, 10, 12, 29),  // commons (central)
        new Rect(44, 39, 12, 13),  // cafeteria / dining (central-north)
        new Rect(6, 22, 38, 4),    // west corridor
        new Rect(56, 22, 37, 4),   // east corridor
        new Rect(6, 56, 87, 4),    // north corridor
        new Rect(6, 2, 4, 69),     // west vertical corridor
        new Rect(28, 2, 4, 69),    // mid-west vertical corridor
        new Rect(56, 16, 37, 6),   // south-east corridor
        new Rect(56, 2, 4, 20),    // south-east vertical corridor
        new Rect(66, 26, 4, 30),   // east vertical corridor
    };

    private static readonly RoomDef[] Rooms =
    {
        // Southwest: middle school.
        new RoomDef("ms_classroom_1", "Middle School Classroom 1", new Color(0.55f, 0.72f, 0.86f), RoomKind.Classroom, new Rect(10, 3, 9, 10)),
        new RoomDef("ms_classroom_2", "Middle School Classroom 2", new Color(0.55f, 0.72f, 0.86f), RoomKind.Classroom, new Rect(19, 3, 9, 10)),
        new RoomDef("ms_classroom_3", "Middle School Classroom 3", new Color(0.55f, 0.72f, 0.86f), RoomKind.Classroom, new Rect(10, 13, 9, 9)),
        new RoomDef("ms_classroom_4", "Middle School Classroom 4", new Color(0.55f, 0.72f, 0.86f), RoomKind.Classroom, new Rect(19, 13, 9, 9)),

        // Far west: science.
        new RoomDef("chem_physics", "Chemistry & Physics Lab", new Color(0.45f, 0.78f, 0.68f), RoomKind.Science, new Rect(32, 3, 12, 10)),
        new RoomDef("biology", "Biology Lab", new Color(0.45f, 0.78f, 0.68f), RoomKind.Science, new Rect(32, 13, 12, 9)),

        // Northwest: high school + practical teaching.
        new RoomDef("hs_classroom_1", "High School Classroom 1", new Color(0.58f, 0.70f, 0.88f), RoomKind.Classroom, new Rect(10, 26, 9, 10)),
        new RoomDef("hs_classroom_2", "High School Classroom 2", new Color(0.58f, 0.70f, 0.88f), RoomKind.Classroom, new Rect(19, 26, 9, 10)),
        new RoomDef("sewing", "Sewing Room", new Color(0.86f, 0.74f, 0.62f), RoomKind.Workshop, new Rect(10, 36, 9, 9)),
        new RoomDef("cooking", "Cooking Room", new Color(0.90f, 0.80f, 0.55f), RoomKind.Kitchen, new Rect(19, 36, 9, 9)),
        new RoomDef("living_learning", "Living & Learning Center", new Color(0.72f, 0.78f, 0.66f), RoomKind.Study, new Rect(10, 45, 9, 11)),
        new RoomDef("hs_lab", "High School Lab", new Color(0.50f, 0.76f, 0.72f), RoomKind.Science, new Rect(19, 45, 9, 11)),

        // Central-west: auditorium + administration.
        new RoomDef("auditorium", "Auditorium", new Color(0.62f, 0.55f, 0.85f), RoomKind.Auditorium, new Rect(32, 26, 12, 19)),
        new RoomDef("admin_office", "Administrative Office", new Color(0.80f, 0.75f, 0.62f), RoomKind.Admin, new Rect(32, 45, 6, 11)),
        new RoomDef("nurse", "Nurse's Office", new Color(0.86f, 0.80f, 0.80f), RoomKind.Admin, new Rect(38, 45, 6, 11)),

        // North: art, library, study, workshops.
        new RoomDef("art", "Art Room", new Color(0.88f, 0.62f, 0.72f), RoomKind.Art, new Rect(10, 60, 18, 11)),
        new RoomDef("library", "Library", new Color(0.55f, 0.75f, 0.55f), RoomKind.Library, new Rect(32, 60, 12, 11)),
        new RoomDef("study_hall", "Study Hall", new Color(0.74f, 0.76f, 0.70f), RoomKind.Study, new Rect(44, 60, 12, 11)),
        new RoomDef("wood_shop", "Industrial Arts Shop", new Color(0.70f, 0.58f, 0.45f), RoomKind.Workshop, new Rect(56, 60, 17, 11)),
        new RoomDef("auto_shop", "Vocational Agriculture Shop", new Color(0.64f, 0.56f, 0.44f), RoomKind.Workshop, new Rect(73, 60, 20, 11)),

        // Southeast: music + wrestling.
        new RoomDef("vocal_music", "Vocal Music", new Color(0.90f, 0.66f, 0.42f), RoomKind.Music, new Rect(60, 2, 13, 14)),
        new RoomDef("instrumental_music", "Instrumental Music", new Color(0.90f, 0.62f, 0.36f), RoomKind.Music, new Rect(73, 2, 12, 14)),
        new RoomDef("wrestling", "Wrestling / Apparatus", new Color(0.80f, 0.55f, 0.48f), RoomKind.Wrestling, new Rect(85, 2, 8, 14)),

        // East-central: gym + lockers, kitchen.
        new RoomDef("staff_room", "Staff Room", new Color(0.78f, 0.78f, 0.68f), RoomKind.Admin, new Rect(56, 26, 10, 13)),
        new RoomDef("kitchen", "Kitchen", new Color(0.90f, 0.85f, 0.55f), RoomKind.Kitchen, new Rect(56, 39, 10, 17)),
        new RoomDef("gym", "Gymnasium", new Color(0.85f, 0.72f, 0.45f), RoomKind.Gym, new Rect(70, 26, 19, 30)),
        new RoomDef("boys_lockers", "Boys' Locker Room", new Color(0.66f, 0.68f, 0.72f), RoomKind.Lockers, new Rect(89, 26, 4, 15)),
        new RoomDef("girls_lockers", "Girls' Locker Room", new Color(0.72f, 0.68f, 0.74f), RoomKind.Lockers, new Rect(89, 41, 4, 15)),
    };

    // ── Build state ──────────────────────────────────────────────────────────

    private static Sprite squareSprite;
    private static readonly Dictionary<string, Color> RoomColors = new Dictionary<string, Color>();
    private static int wallsLayer;
    private static readonly Dictionary<Vector2Int, string> regions = new Dictionary<Vector2Int, string>();
    private static readonly HashSet<(Vector2Int a, Vector2Int b)> openEdges = new HashSet<(Vector2Int, Vector2Int)>();

    /// <summary>
    /// Builds the school under a new root at the origin. The caller moves the returned root clear of
    /// the foyer; every generated object is parented under it, so the whole school moves together.
    /// </summary>
    public static GameObject BuildInto()
    {
        Directory.CreateDirectory(MaterialsDir);
        squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (squareSprite == null)
            throw new InvalidOperationException("Square sprite not found: " + SpritePath);

        wallsLayer = Mathf.Max(0, LayerMask.NameToLayer("Walls"));

        BuildRegionMap();
        ComputeDoorways();
        VerifyConnectivity();

        var root = new GameObject(RootName);

        BuildGround(root.transform);
        var roomRoot = new GameObject("Rooms");
        roomRoot.transform.SetParent(root.transform, false);
        BuildFloors(roomRoot.transform);
        BuildWalls(roomRoot.transform);
        BuildProps(roomRoot.transform);
        BuildRoomMarkers(roomRoot.transform);
        BuildEntrancePortal(root.transform);
        BuildNpcs(root.transform);

        Debug.Log("[SchoolInterior] Built " + RootName + ": " + Rooms.Length + " rooms, " +
                  Corridors.Length + " corridor segments, " + regions.Count + " walkable cells.");
        return root;
    }

    // ── Region map + doorways ────────────────────────────────────────────────

    private static void BuildRegionMap()
    {
        regions.Clear();
        RoomColors.Clear();

        foreach (Rect c in Corridors)
            FillRegion(c, "hall");

        foreach (RoomDef room in Rooms)
        {
            RoomColors[room.Id] = room.Color;
            foreach (Rect r in room.Rects)
                FillRegion(r, room.Id);
        }
    }

    private static void FillRegion(Rect rect, string region)
    {
        for (int x = Mathf.RoundToInt(rect.x); x < Mathf.RoundToInt(rect.x + rect.width); x++)
        {
            for (int z = Mathf.RoundToInt(rect.y); z < Mathf.RoundToInt(rect.y + rect.height); z++)
            {
                var cell = new Vector2Int(x, z);
                if (regions.TryGetValue(cell, out string existing) && existing != region)
                    throw new InvalidOperationException(
                        "School layout overlap at " + cell + " between '" + existing + "' and '" + region + "'.");
                regions[cell] = region;
            }
        }
    }

    // Opens a two-cell doorway at the middle of every contiguous room↔hall boundary run, so each room
    // gets a genuine, readable opening into the corridor it touches.
    private static void ComputeDoorways()
    {
        openEdges.Clear();

        var horizontal = new Dictionary<(int line, string room), List<int>>();
        var vertical = new Dictionary<(int line, string room), List<int>>();

        foreach (KeyValuePair<Vector2Int, string> entry in regions)
        {
            Vector2Int cell = entry.Key;
            if (entry.Value == "hall")
                continue;

            foreach (Vector2Int dir in Directions)
            {
                Vector2Int neighbor = cell + dir;
                if (!regions.TryGetValue(neighbor, out string neighborRegion) || neighborRegion != "hall")
                    continue;

                if (dir.y != 0)
                {
                    int line = Mathf.Max(cell.y, neighbor.y);
                    AddTo(horizontal, (line, entry.Value), cell.x);
                }
                else
                {
                    int line = Mathf.Max(cell.x, neighbor.x);
                    AddTo(vertical, (line, entry.Value), cell.y);
                }
            }
        }

        foreach (KeyValuePair<(int line, string room), List<int>> group in horizontal)
        {
            foreach ((int a, int b) run in MergeRuns(group.Value))
            {
                int length = run.b - run.a + 1;
                int start = run.a + Mathf.Max(0, (length - 2) / 2);
                int count = Mathf.Min(2, length);
                for (int i = 0; i < count; i++)
                {
                    int x = start + i;
                    AddOpenEdge(new Vector2Int(x, group.Key.line - 1), new Vector2Int(x, group.Key.line));
                }
            }
        }

        foreach (KeyValuePair<(int line, string room), List<int>> group in vertical)
        {
            foreach ((int a, int b) run in MergeRuns(group.Value))
            {
                int length = run.b - run.a + 1;
                int start = run.a + Mathf.Max(0, (length - 2) / 2);
                int count = Mathf.Min(2, length);
                for (int i = 0; i < count; i++)
                {
                    int z = start + i;
                    AddOpenEdge(new Vector2Int(group.Key.line - 1, z), new Vector2Int(group.Key.line, z));
                }
            }
        }
    }

    private static void AddOpenEdge(Vector2Int a, Vector2Int b)
    {
        if (Order(a, b, out Vector2Int lo, out Vector2Int hi))
            openEdges.Add((lo, hi));
    }

    private static bool Order(Vector2Int a, Vector2Int b, out Vector2Int lo, out Vector2Int hi)
    {
        if (a.x < b.x || (a.x == b.x && a.y <= b.y)) { lo = a; hi = b; return true; }
        lo = b; hi = a; return true;
    }

    private static void AddTo<TKey>(Dictionary<TKey, List<int>> map, TKey key, int value)
    {
        if (!map.TryGetValue(key, out List<int> list))
        {
            list = new List<int>();
            map[key] = list;
        }
        list.Add(value);
    }

    private static void VerifyConnectivity()
    {
        if (!regions.TryGetValue(new Vector2Int(50, 7), out _))
            throw new InvalidOperationException("School entrance anchor (50,7) is not walkable.");

        var reached = FloodWalkable(new Vector2Int(50, 7));

        foreach (RoomDef room in Rooms)
        {
            bool any = false;
            foreach (Rect r in room.Rects)
            {
                for (int x = Mathf.RoundToInt(r.x); x < Mathf.RoundToInt(r.x + r.width) && !any; x++)
                {
                    for (int z = Mathf.RoundToInt(r.y); z < Mathf.RoundToInt(r.y + r.height); z++)
                    {
                        if (reached.Contains(new Vector2Int(x, z))) { any = true; break; }
                    }
                }
                if (any) break;
            }

            if (!any)
                throw new InvalidOperationException("School room '" + room.Id + "' is not reachable from the entrance.");
        }
    }

    private static HashSet<Vector2Int> FloodWalkable(Vector2Int start)
    {
        var reached = new HashSet<Vector2Int> { start };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            foreach (Vector2Int dir in Directions)
            {
                Vector2Int neighbor = cell + dir;
                if (reached.Contains(neighbor) ||
                    !regions.TryGetValue(neighbor, out string neighborRegion))
                    continue;

                bool sameRegion = neighborRegion == regions[cell];
                if (!sameRegion && !openEdges.Contains(Canonical(cell, neighbor)))
                    continue;

                reached.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return reached;
    }

    private static (Vector2Int, Vector2Int) Canonical(Vector2Int a, Vector2Int b)
    {
        Order(a, b, out Vector2Int lo, out Vector2Int hi);
        return (lo, hi);
    }

    // ── Geometry ─────────────────────────────────────────────────────────────

    private static void BuildGround(Transform parent)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(parent, false);
        ground.transform.position = new Vector3(GridW * 0.5f, 0f, GridH * 0.5f);
        ground.transform.localScale = new Vector3(GridW / 10f, 1f, GridH / 10f);
        ground.GetComponent<MeshRenderer>().sharedMaterial =
            EnsureMaterial("SchoolGround", new Color(0.30f, 0.28f, 0.26f), unlit: true);
    }

    private static void BuildFloors(Transform parent)
    {
        var byRegion = new Dictionary<string, HashSet<Vector2Int>>();
        foreach (KeyValuePair<Vector2Int, string> entry in regions)
        {
            if (!byRegion.TryGetValue(entry.Value, out HashSet<Vector2Int> set))
            {
                set = new HashSet<Vector2Int>();
                byRegion[entry.Value] = set;
            }
            set.Add(entry.Key);
        }

        foreach (KeyValuePair<string, HashSet<Vector2Int>> region in byRegion)
        {
            string label = region.Key == "hall" ? "Corridor" : RegionName(region.Key);
            Color color = region.Key == "hall" ? new Color(0.72f, 0.71f, 0.68f) : RoomColors[region.Key];
            CreateCellMesh(label + " Floor", region.Value, 0.03f, EnsureMaterial("Floor_" + region.Key, color, unlit: true), parent);
        }
    }

    private static void BuildWalls(Transform parent)
    {
        Material wall = EnsureMaterial("SchoolWall", new Color(0.42f, 0.36f, 0.30f), unlit: false);

        var north = new Dictionary<int, List<int>>();
        var south = new Dictionary<int, List<int>>();
        var east = new Dictionary<int, List<int>>();
        var west = new Dictionary<int, List<int>>();

        foreach (KeyValuePair<Vector2Int, string> entry in regions)
        {
            Vector2Int cell = entry.Key;
            foreach (Vector2Int dir in Directions)
            {
                Vector2Int neighbor = cell + dir;
                bool blocked;
                if (!regions.TryGetValue(neighbor, out string neighborRegion))
                    blocked = true;
                else
                    blocked = neighborRegion != entry.Value && !openEdges.Contains(Canonical(cell, neighbor));

                if (!blocked)
                    continue;

                if (dir.y > 0) AddTo(north, cell.y + 1, cell.x);
                else if (dir.y < 0) AddTo(south, cell.y, cell.x);
                else if (dir.x > 0) AddTo(east, cell.x + 1, cell.y);
                else AddTo(west, cell.x, cell.y);
            }
        }

        foreach (KeyValuePair<int, List<int>> group in north)
            foreach ((int a, int b) run in MergeRuns(group.Value))
                AddWall(parent, wall, new Vector3((run.a + run.b + 1) * 0.5f, WallHeight * 0.5f, group.Key),
                    new Vector3(run.b - run.a + 1, WallHeight, WallThickness));

        foreach (KeyValuePair<int, List<int>> group in south)
            foreach ((int a, int b) run in MergeRuns(group.Value))
                AddWall(parent, wall, new Vector3((run.a + run.b + 1) * 0.5f, WallHeight * 0.5f, group.Key),
                    new Vector3(run.b - run.a + 1, WallHeight, WallThickness));

        foreach (KeyValuePair<int, List<int>> group in east)
            foreach ((int a, int b) run in MergeRuns(group.Value))
                AddWall(parent, wall, new Vector3(group.Key, WallHeight * 0.5f, (run.a + run.b + 1) * 0.5f),
                    new Vector3(WallThickness, WallHeight, run.b - run.a + 1));

        foreach (KeyValuePair<int, List<int>> group in west)
            foreach ((int a, int b) run in MergeRuns(group.Value))
                AddWall(parent, wall, new Vector3(group.Key, WallHeight * 0.5f, (run.a + run.b + 1) * 0.5f),
                    new Vector3(WallThickness, WallHeight, run.b - run.a + 1));
    }

    private static void AddWall(Transform parent, Material material, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.layer = wallsLayer;
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void BuildRoomMarkers(Transform parent)
    {
        var placed = new HashSet<Vector2Int>();

        foreach (RoomDef room in Rooms)
        {
            Vector2Int anchor = FindDoorwayCell(room, placed);
            if (anchor.x < 0)
                throw new InvalidOperationException("School room '" + room.Id + "' has no doorway cell.");

            placed.Add(anchor);
            var marker = new GameObject("Room Marker " + room.Id);
            marker.transform.SetParent(parent, false);
            marker.transform.position = CellToWorld(anchor.x, anchor.y);
            SetStringField(marker.AddComponent<SchoolRoomMarker>(), "roomId", room.Id);
        }
    }

    private static Vector2Int FindDoorwayCell(RoomDef room, HashSet<Vector2Int> used)
    {
        Vector2Int best = new Vector2Int(-1, -1);

        foreach (Rect r in room.Rects)
        {
            for (int x = Mathf.RoundToInt(r.x); x < Mathf.RoundToInt(r.x + r.width); x++)
            {
                for (int z = Mathf.RoundToInt(r.y); z < Mathf.RoundToInt(r.y + r.height); z++)
                {
                    Vector2Int cell = new Vector2Int(x, z);
                    if (used.Contains(cell))
                        continue;

                    foreach (Vector2Int dir in Directions)
                    {
                        Vector2Int neighbor = cell + dir;
                        if (regions.TryGetValue(neighbor, out string region) && region == "hall" &&
                            openEdges.Contains(Canonical(cell, neighbor)))
                        {
                            return cell;
                        }
                    }

                    if (best.x < 0)
                        best = cell;
                }
            }
        }

        return best;
    }

    // ── Props ────────────────────────────────────────────────────────────────

    private static void BuildProps(Transform parent)
    {
        var root = new GameObject("Props");
        root.transform.SetParent(parent, false);

        Material desk = EnsureMaterial("Desk", new Color(0.55f, 0.40f, 0.26f), unlit: false);
        Material shelf = EnsureMaterial("Shelf", new Color(0.45f, 0.32f, 0.21f), unlit: false);
        Material bench = EnsureMaterial("Bench", new Color(0.50f, 0.34f, 0.30f), unlit: false);
        Material counter = EnsureMaterial("Counter", new Color(0.80f, 0.80f, 0.82f), unlit: false);
        Material gear = EnsureMaterial("Gear", new Color(0.32f, 0.32f, 0.38f), unlit: false);
        Material court = EnsureMaterial("CourtLine", new Color(0.95f, 0.95f, 0.98f), unlit: true);
        Material mat = EnsureMaterial("WrestlingMat", new Color(0.78f, 0.30f, 0.30f), unlit: true);

        foreach (RoomDef room in Rooms)
        {
            Rect r = room.Rects[0];
            switch (room.Kind)
            {
                case RoomKind.Classroom: FurnishClassroom(root.transform, r, desk, shelf); break;
                case RoomKind.Science: FurnishRows(root.transform, r, desk, gear, 2); break;
                case RoomKind.Art: FurnishRows(root.transform, r, desk, gear, 2); break;
                case RoomKind.Study: FurnishRows(root.transform, r, desk, bench, 2); break;
                case RoomKind.Library: FurnishLibrary(root.transform, r, shelf, desk); break;
                case RoomKind.Admin: FurnishAdmin(root.transform, r, desk, shelf); break;
                case RoomKind.Dining: FurnishDining(root.transform, r, desk); break;
                case RoomKind.Kitchen: FurnishKitchen(root.transform, r, counter, gear); break;
                case RoomKind.Auditorium: FurnishAuditorium(root.transform, r, bench, shelf); break;
                case RoomKind.Gym: FurnishGym(root.transform, r, court, gear, bench); break;
                case RoomKind.Lockers: FurnishLockers(root.transform, r, gear); break;
                case RoomKind.Music: FurnishMusic(root.transform, r, gear, desk); break;
                case RoomKind.Wrestling: FurnishWrestling(root.transform, r, mat, gear); break;
                case RoomKind.Workshop: FurnishWorkshop(root.transform, r, desk, gear, shelf); break;
            }
        }
    }

    private static void FurnishClassroom(Transform parent, Rect r, Material desk, Material shelf)
    {
        for (int z = (int)r.y + 2; z <= r.yMax - 4; z += 2)
            for (int x = (int)r.x + 2; x <= r.xMax - 3; x += 2)
                Prop(parent, CellToWorld(x, z), new Vector3(0.8f, 0.5f, 0.5f), desk, true, 0.25f);

        Prop(parent, CellToWorld((int)r.x + 1, (int)r.y + 1), new Vector3(0.9f, 0.8f, 0.5f), shelf, true, 0.4f);
    }

    private static void FurnishRows(Transform parent, Rect r, Material a, Material b, int spacing)
    {
        for (int z = (int)r.y + 2; z <= r.yMax - 3; z += spacing + 1)
            for (int x = (int)r.x + 2; x <= r.xMax - 3; x += 3)
                Prop(parent, CellToWorld(x, z), new Vector3(1.2f, 0.6f, 0.6f), a, true, 0.3f);

        Prop(parent, CellToWorld((int)r.x + 1, (int)r.y + 1), new Vector3(0.8f, 0.9f, 0.6f), b, true, 0.45f);
    }

    private static void FurnishLibrary(Transform parent, Rect r, Material shelf, Material desk)
    {
        for (int x = (int)r.x + 2; x <= r.xMax - 3; x += 3)
            Prop(parent, CellToWorld(x, (int)r.y + 2), new Vector3(0.6f, 1f, 3f), shelf, true, 0.5f);

        for (int z = (int)r.y + 5; z <= r.yMax - 3; z += 3)
            Prop(parent, CellToWorld((int)r.x + 4, z), new Vector3(3f, 0.5f, 1f), desk, true, 0.25f);
    }

    private static void FurnishAdmin(Transform parent, Rect r, Material desk, Material shelf)
    {
        Prop(parent, CellToWorld((int)r.x + 2, (int)r.y + 2), new Vector3(1.4f, 0.6f, 0.9f), desk, true, 0.3f);
        Prop(parent, CellToWorld((int)r.x + (int)r.width / 2, (int)r.y + (int)r.height - 2), new Vector3(0.6f, 1f, 2f), shelf, true, 0.5f);
    }

    private static void FurnishDining(Transform parent, Rect r, Material table)
    {
        for (int z = (int)r.y + 2; z <= r.yMax - 3; z += 3)
            for (int x = (int)r.x + 2; x <= r.xMax - 3; x += 3)
                Prop(parent, CellToWorld(x, z), new Vector3(1.6f, 0.5f, 1f), table, true, 0.25f);
    }

    private static void FurnishKitchen(Transform parent, Rect r, Material counter, Material gear)
    {
        for (int x = (int)r.x + 1; x <= r.xMax - 2; x += 2)
        {
            Prop(parent, CellToWorld(x, (int)r.y + 1), new Vector3(1.6f, 0.9f, 0.7f), counter, true, 0.45f);
            Prop(parent, CellToWorld(x, (int)r.yMax - 2), new Vector3(1.6f, 0.9f, 0.7f), counter, true, 0.45f);
        }
        Prop(parent, CellToWorld((int)r.x + (int)r.width / 2, (int)r.y + (int)r.height / 2), new Vector3(2f, 0.9f, 1.6f), gear, true, 0.45f);
    }

    private static void FurnishAuditorium(Transform parent, Rect r, Material bench, Material shelf)
    {
        float stageZ = r.yMax - 2.5f;
        Prop(parent, new Vector3(r.x + r.width * 0.5f, 0.25f, stageZ), new Vector3(r.width - 2f, 0.5f, 3f), shelf, true, 0f);
        Prop(parent, new Vector3(r.x + r.width * 0.5f, 1.1f, r.yMax - 1f), new Vector3(r.width - 2f, 1.4f, 0.4f), shelf, true, 0f);

        for (int z = (int)r.y + 2; z <= r.y + 9; z += 2)
            Prop(parent, new Vector3(r.x + r.width * 0.5f, 0.3f, z + 0.5f), new Vector3(r.width - 4f, 0.5f, 0.8f), bench, true, 0f);
    }

    private static void FurnishGym(Transform parent, Rect r, Material court, Material gear, Material bench)
    {
        float cx = r.x + r.width * 0.5f;
        float cz = r.y + r.height * 0.5f;

        Prop(parent, new Vector3(cx, 0f, cz), new Vector3(0.15f, 0.02f, r.height - 6f), court, false, 0.05f);
        Prop(parent, new Vector3(cx - 6f, 0f, cz), new Vector3(0.15f, 0.02f, 8f), court, false, 0.05f);
        Prop(parent, new Vector3(cx + 6f, 0f, cz), new Vector3(0.15f, 0.02f, 8f), court, false, 0.05f);

        Prop(parent, new Vector3(cx, 0.9f, r.y + 2f), new Vector3(1.8f, 0.25f, 0.25f), gear, true, 0f);
        Prop(parent, new Vector3(cx, 0.9f, r.yMax - 2f), new Vector3(1.8f, 0.25f, 0.25f), gear, true, 0f);

        for (int step = 0; step < 3; step++)
            Prop(parent, new Vector3(r.xMax - 2f - step * 0.8f, 0.3f + step * 0.35f, cz),
                new Vector3(0.8f, 0.7f + step * 0.7f, r.height - 8f), bench, true, 0f);
    }

    private static void FurnishLockers(Transform parent, Rect r, Material gear)
    {
        for (int z = (int)r.y + 1; z <= r.yMax - 2; z += 1)
            Prop(parent, CellToWorld((int)r.x + (int)r.width - 1, z), new Vector3(0.6f, 1f, 0.9f), gear, true, 0.5f);
    }

    private static void FurnishMusic(Transform parent, Rect r, Material gear, Material desk)
    {
        Prop(parent, CellToWorld((int)r.x + 2, (int)r.yMax - 3), new Vector3(1.4f, 0.9f, 1f), gear, true, 0.45f);
        Prop(parent, CellToWorld((int)r.x + (int)r.width / 2, (int)r.y + 3), new Vector3(2f, 0.5f, 1f), desk, true, 0.25f);
        Prop(parent, CellToWorld((int)r.x + (int)r.width / 2, (int)r.y + 5), new Vector3(2f, 0.5f, 1f), desk, true, 0.25f);
    }

    private static void FurnishWrestling(Transform parent, Rect r, Material mat, Material gear)
    {
        Prop(parent, CellToWorld((int)r.x + (int)r.width / 2, (int)r.y + (int)r.height / 2),
            new Vector3(r.width - 3f, 0.06f, r.height - 5f), mat, false, 0.03f);
        Prop(parent, CellToWorld((int)r.x + 1, (int)r.y + 1), new Vector3(0.6f, 1f, 1.6f), gear, true, 0.5f);
    }

    private static void FurnishWorkshop(Transform parent, Rect r, Material work, Material gear, Material shelf)
    {
        for (int x = (int)r.x + 2; x <= r.xMax - 4; x += 3)
            Prop(parent, CellToWorld(x, (int)r.y + 2), new Vector3(2f, 0.9f, 1.2f), work, true, 0.45f);

        Prop(parent, CellToWorld((int)r.x + 1, (int)r.y + 1), new Vector3(0.8f, 1f, 3f), shelf, true, 0.5f);
        Prop(parent, CellToWorld((int)r.x + (int)r.width - 2, (int)r.yMax - 2), new Vector3(1.2f, 0.9f, 1.2f), gear, true, 0.45f);
    }

    private static void Prop(Transform parent, Vector3 position, Vector3 size, Material material, bool collider, float yOffset)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Prop";
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, yOffset + size.y * 0.5f, position.z);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;

        if (!collider)
            UnityEngine.Object.DestroyImmediate(go.GetComponent<BoxCollider>());
        else
            go.layer = wallsLayer;
    }

    // ── Portal + NPCs ────────────────────────────────────────────────────────

    private static void BuildEntrancePortal(Transform parent)
    {
        var portalObject = new GameObject("School Entrance Portal");
        portalObject.transform.SetParent(parent, false);
        portalObject.transform.position = CellToWorld(50, 3);

        var collider = portalObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1.6f, 2f, 1.6f);
        collider.center = new Vector3(0f, 1f, 0f);

        var visual = new GameObject("PortalVisual");
        visual.transform.SetParent(portalObject.transform, false);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        visual.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.color = new Color(0.35f, 0.65f, 1f);
        renderer.sortingOrder = 50;
        visual.AddComponent<BillboardSprite>();

        // Arrival point two units north of the trigger, so a traveler never re-triggers it.
        var exitPoint = new GameObject("ExitPoint").transform;
        exitPoint.SetParent(portalObject.transform, false);
        exitPoint.localPosition = new Vector3(0f, 0f, 2f);

        var portal = portalObject.AddComponent<PortalTrigger3D>();
        SetStringField(portal, "portalId", EntrancePortalId);
        SetStringField(portal, "destinationPortalId", FoyerPortalId);
        SetObjectField(portal, "exitPoint", exitPoint);

        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "PortalPad";
        pad.transform.SetParent(portalObject.transform, false);
        pad.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        pad.transform.localScale = new Vector3(2.2f, 0.04f, 2.2f);
        pad.GetComponent<MeshRenderer>().sharedMaterial =
            EnsureMaterial("SchoolPortalPad", new Color(0.35f, 0.65f, 1f), unlit: true);
        UnityEngine.Object.DestroyImmediate(pad.GetComponent<BoxCollider>());
    }

    private static void BuildNpcs(Transform parent)
    {
        int npcLayer = Mathf.Max(0, LayerMask.NameToLayer("Npc"));
        int wallMask = 1 << Mathf.Max(0, LayerMask.NameToLayer("Walls"));

        var cells = new[]
        {
            new Vector2Int(48, 20), new Vector2Int(22, 30), new Vector2Int(75, 40), new Vector2Int(35, 8),
        };

        var root = new GameObject("School NPCs");
        root.transform.SetParent(parent, false);

        for (int i = 0; i < cells.Length; i++)
        {
            var npc = new GameObject("Student " + (i + 1));
            npc.layer = npcLayer;
            npc.transform.SetParent(root.transform, false);
            npc.transform.position = CellToWorld(cells[i].x, cells[i].y);

            npc.AddComponent<Rigidbody>();
            var collider = npc.AddComponent<CapsuleCollider>();
            collider.height = 1.4f;
            collider.radius = 0.35f;
            collider.center = new Vector3(0f, 0.7f, 0f);

            var controller = npc.AddComponent<NpcController>();
            SetStringField(controller, "npcId", "school_student_" + (i + 1));
            SetStringField(controller, "displayName", "Student " + (i + 1));
            SetNestedFloat(controller, "config", "InteractionRange", 2.5f);

            var dialogue = npc.AddComponent<NpcDialogue>();
            SetStringField(dialogue, "dialogueId", i % 2 == 0 ? "town_villager_a" : "town_villager_b");
            npc.AddComponent<NpcStateView>();

            var wanderer = npc.AddComponent<NpcWander3D>();
            SetLayerMaskField(wanderer, "wallLayers", wallMask);
            SetLayerMaskField(wanderer, "neighborLayers", 1 << npcLayer);
            SetNestedFloat(wanderer, "wanderConfig", "WanderRadius", 7f);
            SetNestedFloat(wanderer, "behaviorConfig", "MoveSpeed", 1.5f);

            var pathfinder = npc.AddComponent<NpcPathfinder3D>();
            SetLayerMaskField(pathfinder, "obstacleLayers", wallMask);
            SetIntField(pathfinder, "searchPadding", 8);
            SetIntField(pathfinder, "maxNodes", 600);

            var visual = new GameObject("NpcVisual");
            visual.transform.SetParent(npc.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            visual.transform.localScale = Vector3.one * 0.8f;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = squareSprite;
            renderer.color = new Color(0.95f, 0.62f, 0.20f);
            renderer.sortingOrder = 50;
            visual.AddComponent<BillboardSprite>();
        }
    }

    // ── Mesh / material helpers ──────────────────────────────────────────────

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(-1, 0),
    };

    private static Vector3 CellToWorld(int cx, int cz) =>
        new Vector3((cx + 0.5f) * CellSize, 0f, (cz + 0.5f) * CellSize);

    private static string RegionName(string id)
    {
        foreach (RoomDef room in Rooms)
            if (room.Id == id)
                return room.Name;
        return id;
    }

    private static List<(int a, int b)> MergeRuns(List<int> values)
    {
        values.Sort();
        var runs = new List<(int a, int b)>();
        int i = 0;
        while (i < values.Count)
        {
            int start = values[i];
            int end = start;
            while (i + 1 < values.Count && values[i + 1] == end + 1)
            {
                i++;
                end = values[i];
            }
            runs.Add((start, end));
            i++;
        }
        return runs;
    }

    private static void CreateCellMesh(string name, HashSet<Vector2Int> cells, float height, Material material, Transform parent)
    {
        var vertices = new List<Vector3>(cells.Count * 4);
        var triangles = new List<int>(cells.Count * 6);
        float s = CellSize;

        foreach (Vector2Int cell in cells)
        {
            float x = cell.x * s;
            float z = cell.y * s;
            int b = vertices.Count;
            vertices.Add(new Vector3(x, height, z));
            vertices.Add(new Vector3(x + s, height, z));
            vertices.Add(new Vector3(x + s, height, z + s));
            vertices.Add(new Vector3(x, height, z + s));
            triangles.Add(b + 0); triangles.Add(b + 3); triangles.Add(b + 2);
            triangles.Add(b + 0); triangles.Add(b + 2); triangles.Add(b + 1);
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;

        string meshPath = MaterialsDir + "/" + Sanitize(name) + "Mesh.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
            AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
    }

    private static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    private static Material EnsureMaterial(string name, Color color, bool unlit)
    {
        string path = MaterialsDir + "/" + Sanitize(name) + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (existing != null && existing.shader == shader)
        {
            ApplyMaterial(existing, color, unlit);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        var material = new Material(shader) { name = name };
        ApplyMaterial(material, color, unlit);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void ApplyMaterial(Material material, Color color, bool unlit)
    {
        material.SetColor("_BaseColor", color);
        if (!unlit)
        {
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_Metallic", 0f);
        }
    }

    // ── Serialized field helpers ─────────────────────────────────────────────

    private static void SetObjectField(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetStringField(UnityEngine.Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetIntField(UnityEngine.Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.intValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetLayerMaskField(UnityEngine.Object target, string field, int mask)
    {
        var fieldInfo = target.GetType().GetField(
            field,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (fieldInfo == null) return;

        fieldInfo.SetValue(target, (LayerMask)mask);
        EditorUtility.SetDirty(target);
    }

    private static void SetNestedFloat(UnityEngine.Object target, string parentField, string childField, float value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(parentField);
        if (p == null) return;
        SerializedProperty q = p.FindPropertyRelative(childField);
        if (q != null) { q.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
