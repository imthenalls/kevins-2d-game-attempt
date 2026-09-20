using UnityEngine;

/// <summary>
/// Creates the persistent systems layer once, before any scene loads, and keeps it alive across
/// scenes. Gameplay scenes then only need their own content (grid, spawn point, world identity,
/// camera, NPCs, scene rules) instead of re-adding the managers in every scene.
///
/// Uses <c>RuntimeInitializeOnLoadMethod</c> rather than a preload scene so it also works when you
/// press Play directly on a gameplay scene in the editor.
///
/// The managers it adds are existing singletons that already set their own <c>Instance</c> and
/// <c>DontDestroyOnLoad</c> in <c>Awake</c>; any duplicate placed in a scene destroys itself.
///
/// Unity setup: none — created automatically. Do not add this component to a scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameBootstrap : MonoBehaviour
{
    private static GameBootstrap instance;

    /// <summary>The persistent systems root, or null before bootstrap.</summary>
    public static GameBootstrap Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (instance != null)
            return;

        var root = new GameObject("Game Systems");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<GameBootstrap>();

        // Persistent managers. Each sets its own Instance + DontDestroyOnLoad in Awake.
        root.AddComponent<GameSessionHost>();
        root.AddComponent<SaveManager>();
        root.AddComponent<PortalManager>();
        root.AddComponent<SceneLoader>();
        root.AddComponent<QuestManager>();
        root.AddComponent<WorldStateManager>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
