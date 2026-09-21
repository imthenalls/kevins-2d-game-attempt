using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Creates (once) a 3D <see cref="UniversalRendererData"/> asset and registers it in the project's
/// URP asset, so 3D planar-isometric scenes can render real geometry, lights and depth while 2D
/// scenes keep the existing <c>Renderer2D</c>.
///
/// The renderer is selected per camera (via <c>UniversalAdditionalCameraData.SetRenderer</c>), so
/// converting one scene does not disturb the others.
///
/// Unity setup: menu Tools &gt; Worlds &gt; Isometric 3D &gt; Ensure 3D Renderer is idempotent.
///
/// Runtime API: none (editor only).
/// </summary>
public static class Isometric3DRendererSetup
{
    public const string RendererPath = "Assets/Settings/Renderer3D.asset";
    public const string PipelinePath = "Assets/Settings/UniversalRP.asset";

    [MenuItem("Tools/Worlds/Isometric 3D/Ensure 3D Renderer")]
    public static void EnsureFromMenu()
    {
        UniversalRendererData data = Ensure();
        int index = IndexOf(data);
        Debug.Log($"[Isometric3D] 3D renderer ready at '{RendererPath}' (URP renderer index {index}).");
    }

    /// <summary>Creates the 3D renderer asset if missing and adds it to the URP renderer list.</summary>
    public static UniversalRendererData Ensure()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(data, RendererPath);
            AssetDatabase.SaveAssets();
        }

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline != null)
        {
            var so = new SerializedObject(pipeline);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            if (list != null && IndexOfAsset(list, data) < 0)
            {
                int index = list.arraySize;
                list.arraySize++;
                list.GetArrayElementAtIndex(index).objectReferenceValue = data;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
                AssetDatabase.SaveAssets();
            }
        }

        return data;
    }

    /// <summary>The URP renderer index of the given renderer data, or -1.</summary>
    public static int IndexOf(UniversalRendererData data)
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null || data == null)
            return -1;

        var so = new SerializedObject(pipeline);
        return IndexOfAsset(so.FindProperty("m_RendererDataList"), data);
    }

    private static int IndexOfAsset(SerializedProperty list, Object target)
    {
        if (list == null)
            return -1;

        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == target)
                return i;
        }

        return -1;
    }
}
