using Game.Core;
using UnityEngine;

/// <summary>
/// Unity composition root for the pure-C# <see cref="GameSession"/>. Creates the session once,
/// keeps it alive across scene loads, and lets adapters (NpcStateView) reach it without a large
/// global static game state.
///
/// Unity setup:
///   1. Optional. NpcStateView calls EnsureExists(), which creates the host at runtime if the
///      scene has none.
///   2. To pin it explicitly, add this component to any bootstrap GameObject; it becomes
///      DontDestroyOnLoad.
///
/// Runtime API:
///   GameSessionHost.Session.NpcStates  — command service for NPC state.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameSessionHost : MonoBehaviour
{
    private static GameSessionHost instance;

    private GameSession gameSession = new GameSession();

    /// <summary>The active session, or null if no host exists yet.</summary>
    public static GameSession Session => instance != null ? instance.gameSession : null;

    /// <summary>Finds, or creates (DontDestroyOnLoad), the session host.</summary>
    public static GameSessionHost EnsureExists()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<GameSessionHost>();
        if (instance != null)
            return instance;

        var hostObject = new GameObject("Game Session");
        DontDestroyOnLoad(hostObject);
        instance = hostObject.AddComponent<GameSessionHost>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
