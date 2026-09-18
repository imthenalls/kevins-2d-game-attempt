using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Enforces the project tilemap alignment rule: every Grid and Tilemap GameObject must sit at
/// local position (0, 0, 0), rotation identity, and scale (1, 1, 1). Tile placement must be
/// expressed through cell coordinates, never through transform offsets, so that separately
/// authored tilemaps, prefabs, and painted instances all line up on the same cells.
///
/// Validate warns about any violating Grid/Tilemap in the open scene. Normalize bakes each
/// tilemap's local offset into its tile cell coordinates, then zeroes the transform, preserving
/// the visual position while making the transform compliant. Both are also exposed from
/// Tools > World > Tilemap Alignment.
///
/// Unity setup: none. These are editor-only utilities.
///
/// Runtime API: none; this class only runs inside the Unity Editor.
/// </summary>
public static class TilemapAlignmentTool
{
    [MenuItem("Tools/World/Tilemap Alignment/Validate Open Scene")]
    public static void ValidateMenu()
    {
        int issues = ValidateScene();
        Debug.Log(issues == 0
            ? "[TilemapAlignment] All Grids and Tilemaps are aligned."
            : $"[TilemapAlignment] Found {issues} alignment issue(s).");
    }

    [MenuItem("Tools/World/Tilemap Alignment/Normalize Open Scene")]
    public static void NormalizeMenu()
    {
        string report = NormalizeScene();
        Debug.Log("[TilemapAlignment] " + report);
    }

    /// <summary>Returns the number of Grid/Tilemap transforms that are not at the origin.</summary>
    public static int ValidateScene()
    {
        var sb = new StringBuilder();
        int issues = 0;

        foreach (Grid grid in Object.FindObjectsByType<Grid>(FindObjectsInactive.Include))
            issues += CheckTransform(grid.transform, "Grid", sb);

        foreach (Tilemap tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include))
            issues += CheckTransform(tilemap.transform, "Tilemap", sb);

        if (issues > 0)
            Debug.LogWarning("[TilemapAlignment]\n" + sb);
        return issues;
    }

    /// <summary>
    /// Bakes each Tilemap's local offset into its cell coordinates and zeroes the transform.
    /// Returns a short report of what changed.
    /// </summary>
    public static string NormalizeScene()
    {
        var sb = new StringBuilder();
        int normalized = 0;

        foreach (Tilemap tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include))
        {
            Vector3 offset = tilemap.transform.localPosition;
            if (offset.sqrMagnitude > 0.000001f)
            {
                Vector2Int shift = OffsetToCellShift(tilemap.layoutGrid, offset);
                if (shift != Vector2Int.zero)
                    ShiftTiles(tilemap, shift);
                sb.AppendLine($"'{tilemap.name}' offset {offset} baked as cell shift {shift}.");
                normalized++;
            }

            if (!IsIdentity(tilemap.transform))
            {
                tilemap.transform.localPosition = Vector3.zero;
                tilemap.transform.localRotation = Quaternion.identity;
                tilemap.transform.localScale = Vector3.one;
                EditorUtility.SetDirty(tilemap.transform);
            }
        }

        foreach (Grid grid in Object.FindObjectsByType<Grid>(FindObjectsInactive.Include))
        {
            if (!IsIdentity(grid.transform))
            {
                grid.transform.localPosition = Vector3.zero;
                grid.transform.localRotation = Quaternion.identity;
                grid.transform.localScale = Vector3.one;
                EditorUtility.SetDirty(grid.transform);
                sb.AppendLine($"Zeroed Grid '{grid.name}' transform.");
                normalized++;
            }
        }

        if (normalized == 0)
            return "No tilemap offsets found; nothing to normalize.";
        return sb.ToString();
    }

    private static int CheckTransform(Transform t, string kind, StringBuilder sb)
    {
        if (IsIdentity(t))
            return 0;

        sb.AppendLine($"{kind} '{t.name}': pos={t.localPosition} rot={t.localEulerAngles} scale={t.localScale}");
        return 1;
    }

    private static bool IsIdentity(Transform t)
    {
        return t.localPosition.sqrMagnitude < 0.000001f &&
               Quaternion.Angle(t.localRotation, Quaternion.identity) < 0.01f &&
               (t.localScale - Vector3.one).sqrMagnitude < 0.000001f;
    }

    // Converts a world-space offset into a whole-cell vector for the given grid.
    private static Vector2Int OffsetToCellShift(GridLayout grid, Vector3 offset)
    {
        if (grid == null)
            return Vector2Int.zero;

        Vector3 origin = grid.CellToWorld(Vector3Int.zero);
        Vector3 bx = grid.CellToWorld(new Vector3Int(1, 0, 0)) - origin;
        Vector3 by = grid.CellToWorld(new Vector3Int(0, 1, 0)) - origin;

        float det = bx.x * by.y - by.x * bx.y;
        if (Mathf.Abs(det) < 0.000001f)
            return Vector2Int.zero;

        float a = (offset.x * by.y - by.x * offset.y) / det;
        float b = (bx.x * offset.y - offset.x * bx.y) / det;
        return new Vector2Int(Mathf.RoundToInt(a), Mathf.RoundToInt(b));
    }

    /// <summary>
    /// Shifts every painted tile in the tilemap by a whole-cell vector, preserving the tiles and
    /// leaving the transform at the origin. Used by Normalize and the Tilemap Offset window.
    /// </summary>
    public static void ShiftTiles(Tilemap tilemap, Vector2Int shift)
    {
        var snapshot = new System.Collections.Generic.List<(Vector3Int cell, TileBase tile)>();
        foreach (Vector3Int c in tilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = tilemap.GetTile(c);
            if (tile != null)
                snapshot.Add((c, tile));
        }

        tilemap.ClearAllTiles();
        foreach ((Vector3Int cell, TileBase tile) in snapshot)
            tilemap.SetTile(cell + new Vector3Int(shift.x, shift.y, 0), tile);
        tilemap.RefreshAllTiles();
    }
}
