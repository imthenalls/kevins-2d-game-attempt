using UnityEngine;
using UnityEngine.SceneManagement;

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

        // Persistent UI canvas + EventSystem; a scene with its own UI overrides it on load.
        GameUI.Create(root.transform);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        instance = null;
    }

    /// <summary>
    /// Default spawn: after a scene loads, place the player at its PlayerSpawnPoint. A save load or a
    /// portal arrival happens later and overrides this, giving the documented precedence.
    /// </summary>
    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        // Let a scene's own UI override the persistent canvas/EventSystem.
        if (GameUI.Instance != null)
            GameUI.Instance.SyncForScene(scene);

        PlayerSpawnPoint spawn = Object.FindAnyObjectByType<PlayerSpawnPoint>();
        if (spawn == null)
            return;

        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        if (player == null)
            return;

        Vector3 target = spawn.Position;
        target.z = player.transform.position.z;

        if (player.TryGetComponent(out Rigidbody2D body))
            body.position = target;
        player.transform.position = target;
        Physics2D.SyncTransforms();
    }
}
