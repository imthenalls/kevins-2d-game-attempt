using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates reusable World A and World B player prefabs by copying each scene's authored player.
///
/// Unity setup:
///   1. Keep PlayerAvatarProfile assets at Assets/Settings/WorldAPlayerProfile.asset and
///      Assets/Settings/WorldBPlayerProfile.asset.
///   2. Use Tools > Worlds > Rebuild Player Avatar Prefabs, or create the documented Temp
///      request file and let this editor utility process it after compilation.
///   3. The source scene objects are read only; the generated prefabs are written to Assets/Prefabs.
///
/// Runtime API: none; this class runs only in the Unity Editor.
/// </summary>
public static class PlayerAvatarPrefabCreator
{
    private const string RequestPath = "Temp/player-avatar-prefabs.request";
    private const string ResultPath = "Temp/player-avatar-prefabs.result";

    [InitializeOnLoadMethod]
    private static void Initialize() => EditorApplication.update += ProcessRequest;

    private static void ProcessRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath))
            return;

        File.Delete(RequestPath);
        try
        {
            Build();
            File.WriteAllText(ResultPath, "PASS: World A and World B player avatar prefabs rebuilt.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(ResultPath, exception.ToString());
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Worlds/Rebuild Player Avatar Prefabs")]
    public static void Build()
    {
        BuildFromScene(
            "Assets/Scenes/Overworld.unity",
            "Player",
            "Assets/Settings/WorldAPlayerProfile.asset",
            "Assets/Prefabs/WorldAPlayer.prefab",
            WorldLayer.WorldA);
        BuildFromScene(
            "Assets/Scenes/WorldB.unity",
            "World B Player",
            "Assets/Settings/WorldBPlayerProfile.asset",
            "Assets/Prefabs/WorldBPlayer.prefab",
            WorldLayer.WorldB);
        AssetDatabase.SaveAssets();
    }

    private static void BuildFromScene(
        string scenePath,
        string sourceName,
        string profilePath,
        string prefabPath,
        WorldLayer world)
    {
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForBuild = !scene.IsValid() || !scene.isLoaded;
        if (openedForBuild)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            GameObject source = FindRoot(scene, sourceName);
            if (source == null)
                throw new InvalidOperationException($"Player '{sourceName}' was not found in {scenePath}.");

            PlayerAvatarProfile profile = AssetDatabase.LoadAssetAtPath<PlayerAvatarProfile>(profilePath);
            if (profile == null)
                throw new InvalidOperationException($"Avatar profile was not found at {profilePath}.");

            GameObject clone = UnityEngine.Object.Instantiate(source);
            try
            {
                clone.name = world == WorldLayer.WorldA ? "World A Player" : "World B Player";
                clone.transform.SetParent(null, false);
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.identity;
                clone.transform.localScale = Vector3.one;
                WorldCharacter character = clone.GetComponent<WorldCharacter>();
                if (character == null)
                    character = clone.AddComponent<WorldCharacter>();

                SerializedObject serializedCharacter = new SerializedObject(character);
                serializedCharacter.FindProperty("world").enumValueIndex = (int)world;
                serializedCharacter.FindProperty("profile").objectReferenceValue = profile;
                serializedCharacter.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(clone, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }
        finally
        {
            if (openedForBuild && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].name == name && transforms[j].GetComponent<PlayerController2D>() != null)
                    return transforms[j].gameObject;
            }
        }

        return null;
    }
}
