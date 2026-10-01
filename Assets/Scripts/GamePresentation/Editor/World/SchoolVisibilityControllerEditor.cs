using System.Collections.Generic;
using Game.Core;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for <see cref="SchoolVisibilityController"/> that replaces the raw preview id with
/// a zone dropdown, so a designer can inspect the complete covered layout or reveal one zone without
/// entering Play Mode.
///
/// Unity setup: none; applied automatically to the controller component.
///
/// Runtime API: none (editor only).
/// </summary>
[CustomEditor(typeof(SchoolVisibilityController))]
public sealed class SchoolVisibilityControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty preview = serializedObject.FindProperty("previewZoneId");

        var ids = new List<string> { string.Empty };
        var labels = new List<string> { "None (all covered)" };
        foreach (SchoolZone zone in SchoolZoneLayout.Default.Zones)
        {
            ids.Add(zone.Id);
            labels.Add(zone.Name + "  [" + zone.Id + "]");
        }

        EditorGUILayout.LabelField("Visibility Preview (editor only)", EditorStyles.boldLabel);

        int current = ids.IndexOf(preview.stringValue);
        if (current < 0)
            current = 0;

        int chosen = EditorGUILayout.Popup("Preview Zone", current, labels.ToArray());
        if (chosen != current)
        {
            preview.stringValue = ids[chosen];
            serializedObject.ApplyModifiedProperties();
            ((SchoolVisibilityController)target).EditorApplyPreview();
        }

        if (GUILayout.Button("Show Complete Covered Layout"))
        {
            preview.stringValue = string.Empty;
            serializedObject.ApplyModifiedProperties();
            ((SchoolVisibilityController)target).EditorApplyPreview();
        }

        EditorGUILayout.Space();
        DrawPropertiesExcluding(serializedObject, "previewZoneId");
    }
}
