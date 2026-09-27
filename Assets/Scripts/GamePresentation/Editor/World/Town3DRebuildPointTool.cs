using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Captures and re-applies a <see cref="Town3DRebuildPoint"/>. "Set Rebuild Point" snapshots the
/// open Town scene's object transforms plus every simple serialized field (numbers, strings, enums,
/// vectors, colors, rects, bounds) keyed by hierarchy path; the Town 3D builder calls
/// <see cref="Apply"/> after regenerating so those edits persist. Object references, arrays, and
/// added/removed objects are intentionally not captured.
///
/// Unity setup: none — menu-driven via Tools &gt; Worlds &gt; Town 3D.
///
/// Runtime API: Town3DRebuildPointTool.CaptureCurrentScene() / Apply(Scene) / Clear().
/// </summary>
public static class Town3DRebuildPointTool
{
    public const string AssetPath = "Assets/Settings/Town3DRebuildPoint.asset";

    [MenuItem("Tools/Worlds/Town 3D/Set Rebuild Point", priority = 20)]
    public static void CaptureCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != Town3DSceneBuilder.ScenePath)
        {
            EditorUtility.DisplayDialog(
                "Set Rebuild Point",
                "Open " + Town3DSceneBuilder.ScenePath + " before capturing a rebuild point.",
                "OK");
            return;
        }

        Town3DRebuildPoint point = LoadOrCreate();
        point.objects.Clear();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                point.objects.Add(Capture(t));
        }

        EditorUtility.SetDirty(point);
        AssetDatabase.SaveAssets();
        Debug.Log("[Town3D] Rebuild point set: " + point.objects.Count + " objects captured -> " + AssetPath);
    }

    [MenuItem("Tools/Worlds/Town 3D/Clear Rebuild Point", priority = 21)]
    public static void Clear()
    {
        Town3DRebuildPoint point = AssetDatabase.LoadAssetAtPath<Town3DRebuildPoint>(AssetPath);
        if (point == null)
        {
            Debug.Log("[Town3D] No rebuild point to clear.");
            return;
        }

        point.objects.Clear();
        EditorUtility.SetDirty(point);
        AssetDatabase.SaveAssets();
        Debug.Log("[Town3D] Rebuild point cleared; a rebuild will use builder defaults.");
    }

    /// <summary>Re-applies the saved rebuild point onto a freshly built scene. No-op when unset.</summary>
    public static void Apply(Scene scene)
    {
        Town3DRebuildPoint point = AssetDatabase.LoadAssetAtPath<Town3DRebuildPoint>(AssetPath);
        if (point == null || point.objects == null || point.objects.Count == 0)
            return;

        int applied = 0;
        int missing = 0;
        foreach (Town3DRebuildPoint.ObjectEntry entry in point.objects)
        {
            Transform t = Resolve(scene, entry.path);
            if (t == null)
            {
                missing++;
                continue;
            }

            t.localPosition = entry.localPosition;
            t.localRotation = entry.localRotation;
            t.localScale = entry.localScale;
            if (t.gameObject.activeSelf != entry.activeSelf)
                t.gameObject.SetActive(entry.activeSelf);

            ApplyFields(t, entry.fields);
            applied++;
        }

        Debug.Log("[Town3D] Rebuild point applied: " + applied + " objects" +
                  (missing > 0 ? " (" + missing + " not found)" : "") + ".");
    }

    // ── Capture ───────────────────────────────────────────────────────────────

    private static Town3DRebuildPoint.ObjectEntry Capture(Transform t)
    {
        var entry = new Town3DRebuildPoint.ObjectEntry
        {
            path = PathOf(t),
            localPosition = t.localPosition,
            localRotation = t.localRotation,
            localScale = t.localScale,
            activeSelf = t.gameObject.activeSelf
        };

        foreach (Component component in t.GetComponents<Component>())
        {
            if (component == null || component is Transform)
                continue;

            CaptureFields(component, entry.fields);
        }

        return entry;
    }

    private static void CaptureFields(Component component, List<Town3DRebuildPoint.FieldEntry> into)
    {
        var serialized = new SerializedObject(component);
        SerializedProperty property = serialized.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.propertyType == SerializedPropertyType.Generic && property.hasChildren)
            {
                enterChildren = true;
                continue;
            }

            if (!TryFormat(property, out string value))
                continue;

            into.Add(new Town3DRebuildPoint.FieldEntry
            {
                component = component.GetType().Name,
                property = property.propertyPath,
                type = property.propertyType.ToString(),
                value = value
            });
        }
    }

    private static bool TryFormat(SerializedProperty p, out string value)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
                value = p.intValue.ToString(CultureInfo.InvariantCulture);
                return true;
            case SerializedPropertyType.Boolean:
                value = p.boolValue ? "1" : "0";
                return true;
            case SerializedPropertyType.Float:
                value = p.floatValue.ToString("R", CultureInfo.InvariantCulture);
                return true;
            case SerializedPropertyType.String:
                value = p.stringValue;
                return true;
            case SerializedPropertyType.Enum:
                value = p.enumValueIndex.ToString(CultureInfo.InvariantCulture);
                return true;
            case SerializedPropertyType.Vector2:
                value = Join(p.vector2Value.x, p.vector2Value.y);
                return true;
            case SerializedPropertyType.Vector3:
                value = Join(p.vector3Value.x, p.vector3Value.y, p.vector3Value.z);
                return true;
            case SerializedPropertyType.Vector4:
                value = Join(p.vector4Value.x, p.vector4Value.y, p.vector4Value.z, p.vector4Value.w);
                return true;
            case SerializedPropertyType.Color:
                Color c = p.colorValue;
                value = Join(c.r, c.g, c.b, c.a);
                return true;
            case SerializedPropertyType.Rect:
                Rect r = p.rectValue;
                value = Join(r.x, r.y, r.width, r.height);
                return true;
            case SerializedPropertyType.Bounds:
                Bounds b = p.boundsValue;
                value = Join(b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z);
                return true;
            case SerializedPropertyType.Character:
                value = p.uintValue.ToString(CultureInfo.InvariantCulture);
                return true;
            default:
                value = null;
                return false;
        }
    }

    // ── Apply ─────────────────────────────────────────────────────────────────

    private static void ApplyFields(Transform t, List<Town3DRebuildPoint.FieldEntry> fields)
    {
        if (fields == null)
            return;

        foreach (Town3DRebuildPoint.FieldEntry field in fields)
        {
            Component component = FindComponent(t, field.component);
            if (component == null)
                continue;

            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(field.property);
            if (property == null || !TrySet(property, field.value))
                continue;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Component FindComponent(Transform t, string typeName)
    {
        foreach (Component component in t.GetComponents<Component>())
        {
            if (component != null && component.GetType().Name == typeName)
                return component;
        }

        return null;
    }

    private static bool TrySet(SerializedProperty p, string value)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return false;
                p.intValue = i;
                return true;
            case SerializedPropertyType.Boolean:
                p.boolValue = value == "1" || value == "true";
                return true;
            case SerializedPropertyType.Float:
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) return false;
                p.floatValue = f;
                return true;
            case SerializedPropertyType.String:
                p.stringValue = value;
                return true;
            case SerializedPropertyType.Enum:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int e)) return false;
                p.enumValueIndex = e;
                return true;
            case SerializedPropertyType.Vector2:
                if (!TryParts(value, 2, out float[] v2)) return false;
                p.vector2Value = new Vector2(v2[0], v2[1]);
                return true;
            case SerializedPropertyType.Vector3:
                if (!TryParts(value, 3, out float[] v3)) return false;
                p.vector3Value = new Vector3(v3[0], v3[1], v3[2]);
                return true;
            case SerializedPropertyType.Vector4:
                if (!TryParts(value, 4, out float[] v4)) return false;
                p.vector4Value = new Vector4(v4[0], v4[1], v4[2], v4[3]);
                return true;
            case SerializedPropertyType.Color:
                if (!TryParts(value, 4, out float[] col)) return false;
                p.colorValue = new Color(col[0], col[1], col[2], col[3]);
                return true;
            case SerializedPropertyType.Rect:
                if (!TryParts(value, 4, out float[] rect)) return false;
                p.rectValue = new Rect(rect[0], rect[1], rect[2], rect[3]);
                return true;
            case SerializedPropertyType.Bounds:
                if (!TryParts(value, 6, out float[] bounds)) return false;
                p.boundsValue = new Bounds(
                    new Vector3(bounds[0], bounds[1], bounds[2]),
                    new Vector3(bounds[3], bounds[4], bounds[5]));
                return true;
            case SerializedPropertyType.Character:
                if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint ch)) return false;
                p.uintValue = ch;
                return true;
            default:
                return false;
        }
    }

    // ── Paths ─────────────────────────────────────────────────────────────────

    private static string PathOf(Transform t)
    {
        var segments = new List<string>();
        Transform current = t;
        while (current != null)
        {
            segments.Add(SegmentName(current));
            current = current.parent;
        }

        segments.Reverse();
        return string.Join("/", segments);
    }

    // Appends "#n" when several siblings share a name, so duplicate names stay addressable.
    private static string SegmentName(Transform t)
    {
        int index = 0;
        if (t.parent != null)
        {
            foreach (Transform sibling in t.parent)
            {
                if (sibling == t) break;
                if (sibling.name == t.name) index++;
            }
        }
        else
        {
            foreach (GameObject root in t.gameObject.scene.GetRootGameObjects())
            {
                if (root.transform == t) break;
                if (root.name == t.name) index++;
            }
        }

        return index == 0 ? t.name : t.name + "#" + index;
    }

    private static Transform Resolve(Scene scene, string path)
    {
        Transform current = null;
        foreach (string raw in path.Split('/'))
        {
            string name = raw;
            int index = 0;
            int hash = raw.LastIndexOf('#');
            if (hash > 0 && int.TryParse(raw.Substring(hash + 1), out int parsed))
            {
                name = raw.Substring(0, hash);
                index = parsed;
            }

            current = FindNamedChild(current, scene, name, index);
            if (current == null)
                return null;
        }

        return current;
    }

    private static Transform FindNamedChild(Transform parent, Scene scene, string name, int index)
    {
        int seen = 0;
        if (parent == null)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != name) continue;
                if (seen == index) return root.transform;
                seen++;
            }

            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name != name) continue;
            if (seen == index) return child;
            seen++;
        }

        return null;
    }

    // ── Asset ─────────────────────────────────────────────────────────────────

    private static Town3DRebuildPoint LoadOrCreate()
    {
        Town3DRebuildPoint point = AssetDatabase.LoadAssetAtPath<Town3DRebuildPoint>(AssetPath);
        if (point != null)
            return point;

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        point = ScriptableObject.CreateInstance<Town3DRebuildPoint>();
        AssetDatabase.CreateAsset(point, AssetPath);
        AssetDatabase.SaveAssets();
        return point;
    }

    // ── Format helpers ────────────────────────────────────────────────────────

    private static string Join(params float[] values)
    {
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < values.Length; i++)
        {
            if (i > 0) text.Append(',');
            text.Append(values[i].ToString("R", CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    private static bool TryParts(string value, int count, out float[] parts)
    {
        string[] pieces = value.Split(',');
        parts = new float[count];
        if (pieces.Length < count)
            return false;

        for (int i = 0; i < count; i++)
        {
            if (!float.TryParse(pieces[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parts[i]))
                return false;
        }

        return true;
    }
}
