using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Snapshot of hand-edited values in the generated Town scene, keyed by hierarchy path. Written by
/// <see cref="Town3DRebuildPointTool"/> ("Set Rebuild Point") and re-applied by
/// <see cref="Town3DSceneBuilder"/> after it regenerates the scene, so manual moves and Inspector
/// tweaks survive a rebuild. This is Editor-only tooling and is not shipped in a build.
///
/// Unity setup:
///   1. Do not create this asset by hand; use Tools &gt; Worlds &gt; Town 3D &gt; Set Rebuild Point.
///   2. It is written to Assets/Settings/Town3DRebuildPoint.asset.
///
/// Runtime API: none.
/// </summary>
public class Town3DRebuildPoint : ScriptableObject
{
    /// <summary>Saved transform and serialized value fields for a single scene object.</summary>
    [Serializable]
    public class ObjectEntry
    {
        public string path;
        public Vector3 localPosition;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
        public bool activeSelf = true;
        public List<FieldEntry> fields = new List<FieldEntry>();
    }

    /// <summary>One serialized value field on a component, stored as an invariant-culture string.</summary>
    [Serializable]
    public class FieldEntry
    {
        public string component;
        public string property;
        public string type;
        public string value;
    }

    [Tooltip("Objects captured by the last Set Rebuild Point, in scene order.")]
    public List<ObjectEntry> objects = new List<ObjectEntry>();
}
