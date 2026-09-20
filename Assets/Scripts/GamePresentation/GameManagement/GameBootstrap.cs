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

    /// <summary>Per-scene wiring: persistent UI override, camera follow, then default spawn.</summary>
    private static void SyncForScene(Scene scene)
    {
        if (GameUI.Instance != null)
            GameUI.Instance.SyncForScene(scene);

        EnsureSingleFollowCamera();
        PlacePlayerAtSpawn();
    }

    /// <summary>
    /// Keeps exactly one camera and makes it follow the player. Scenes can contain more than one
    /// MainCamera-tagged camera (e.g. a static scene camera plus the player's); the extra ones are
    /// disabled so the follow camera is never drawn underneath a static one.
    /// </summary>
    private static void EnsureSingleFollowCamera()
    {
        Camera chosen = null;
        foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (!camera.CompareTag("MainCamera"))
                continue;

            bool better = chosen == null
                || (camera.name == "Main Camera" && chosen.name != "Main Camera");

            if (!better)
            {
                camera.enabled = false;
                camera.tag = "Untagged";
                continue;
            }

            if (chosen != null)
            {
                chosen.enabled = false;
                chosen.tag = "Untagged";
            }

            chosen = camera;
        }

        if (chosen == null)
            return;

        chosen.enabled = true;
        if (chosen.GetComponent<CameraFollow>() == null)
            chosen.gameObject.AddComponent<CameraFollow>();
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
