using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// Boundary helper that converts the pathfinder facades' Unity waypoint lists into the engine-free
/// <see cref="PathPoint"/> routes that <see cref="RouteFollower"/> consumes. It exists so no
/// UnityEngine type crosses into Game.Data when an NPC starts following a computed path.
///
/// Unity setup: none — static helper used by the NPC movement facades.
///
/// Runtime API: FromXZ(List&lt;Vector3&gt;) for the 3D Town, FromXY(List&lt;Vector2&gt;) for 2D scenes.
/// </summary>
public static class NpcRoute
{
    /// <summary>Converts 3D waypoints (world X/Z) into a Core route.</summary>
    public static List<PathPoint> FromXZ(List<Vector3> points)
    {
        var route = new List<PathPoint>(points.Count);
        for (int i = 0; i < points.Count; i++)
            route.Add(new PathPoint(points[i].x, points[i].z));
        return route;
    }

    /// <summary>Converts 2D waypoints (world X/Y) into a Core route.</summary>
    public static List<PathPoint> FromXY(List<Vector2> points)
    {
        var route = new List<PathPoint>(points.Count);
        for (int i = 0; i < points.Count; i++)
            route.Add(new PathPoint(points[i].x, points[i].y));
        return route;
    }
}
