using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates the persistent systems layer once, before any scene loads, and keeps it alive across
/// scenes. Gameplay scenes then only need their own content (grid, spawn point, world identity,
/// camera, UI, NPCs, scene rules) instead of re-adding the managers in every scene.
///
/// Uses <c>RuntimeInitializeOnLoadMethod</c> rather than a preload scene so it also works when you
/// press Play directly on a gameplay scene in the editor.
///
/// The managers it adds are existing singletons that already set their own <c>Instance</c> and
/// <c>DontDestroyOnLoad</c> in <c>Awake</c>; any duplicate placed in a scene destroys itself.
///
/// Unity setup: none - created automatically. Do not add this component to a scene.
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
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        // sceneLoaded is not guaranteed for the first scene, so sync it explicitly once.
        SyncForScene(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        instance = null;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single)
            return;

        SyncForScene(scene);
    }

    /// <summary>Per-scene wiring: make the primary camera follow the player, then default the spawn.</summary>
    private static void SyncForScene(Scene scene)
    {
        EnsureFollowCamera();
        PlacePlayerAtSpawn();
    }

    /// <summary>
    /// Adds <see cref="CameraFollow"/> to the primary camera only. Deliberately does not disable or
    /// re-tag other cameras - scenes may rely on their existing camera(s).
    /// </summary>
    private static void EnsureFollowCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        if (camera.GetComponent<CameraFollow>() == null)
            camera.gameObject.AddComponent<CameraFollow>();
    }

    /// <summary>
    /// Default spawn: place the player at the scene's PlayerSpawnPoint. A save load or a portal
    /// arrival happens later and overrides this, giving the documented precedence.
    /// </summary>
    private static void PlacePlayerAtSpawn()
    {
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
