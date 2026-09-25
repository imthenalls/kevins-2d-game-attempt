using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-time setup that adds the World A (Overworld) → 3D Town portal to the hand-authored Overworld
/// scene. The Town's return portal is authored by Town3DSceneBuilder; the two are linked by
/// Portal Id / Destination Portal Id.
///
/// Unity setup: Tools &gt; Worlds &gt; Setup Overworld-Town Portal. Idempotent — no-op if the portal
/// already exists. Refuses in Play Mode.
/// </summary>
public static class TownPortalSetup
{
    private const string ScenePath = "Assets/Scenes/Overworld.unity";
    private const string PortalName = "portal_to_town";

    [MenuItem("Tools/Worlds/Setup Overworld-Town Portal")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new System.InvalidOperationException("Exit Play Mode first.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        if (GameObject.Find(PortalName) != null)
        {
            Debug.Log("[TownPortalSetup] Overworld-Town portal already exists; nothing to do.");
            return;
        }

        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Packages/com.unity.2d.sprite/Editor/ObjectMenuCreation/DefaultAssets/Textures/Square.png");
        Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

        var portalObject = new GameObject(PortalName, typeof(SpriteRenderer));
        portalObject.transform.position = new Vector3(2.5f, 1.5f, 0f);
        portalObject.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        SpriteRenderer renderer = portalObject.GetComponent<SpriteRenderer>();
        renderer.sprite = square;
        if (material != null) renderer.sharedMaterial = material;
        renderer.color = new Color(0.45f, 1f, 0.55f);
        renderer.sortingOrder = 3;

        BoxCollider2D trigger = portalObject.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.2f, 1.2f);

        Transform exitPoint = new GameObject("ExitPoint").transform;
        exitPoint.SetParent(portalObject.transform, false);
        exitPoint.localPosition = new Vector3(1.6f, 0f, 0f);

        PortalTrigger2D portal = portalObject.AddComponent<PortalTrigger2D>();
        SetString(portal, "portalId", "overworld_town");
        SetString(portal, "destinationScene", "Town");
        SetString(portal, "destinationPortalId", "town_exit");
        SetObject(portal, "exitPoint", exitPoint);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[TownPortalSetup] Overworld-Town portal added at " + portalObject.transform.position + ".");
    }

    private static void SetString(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property != null) { property.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetObject(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property != null) { property.objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
