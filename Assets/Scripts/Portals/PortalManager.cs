using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Executes same-scene and cross-scene portal travel. Portal routes are authored
/// entirely on PortalTrigger2D components; this manager does not load portal JSON.
/// </summary>
public class PortalManager : MonoBehaviour
{
    public static PortalManager Instance { get; private set; }

    [Header("Traveler")]
    [SerializeField] private string defaultTravelerTag = "Player";
    [SerializeField, Min(0f)] private float travelerCooldownSeconds = 0.2f;
    [SerializeField] private bool resetVelocityOnTeleport = true;
    [SerializeField] private Vector2 exitVelocity = Vector2.zero;

    private readonly Dictionary<EntityId, float> travelerReadyTime =
        new Dictionary<EntityId, float>();

    private string pendingScene;
    private string pendingDestinationPortalId;
    private bool pendingWorldChange;
    private WorldLayer pendingDestinationWorld;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    public bool TryUsePortal(PortalTrigger2D sourcePortal, Transform traveler)
    {
        if (sourcePortal == null || traveler == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(sourcePortal.DestinationPortalId))
        {
            Debug.LogWarning(
                $"Portal '{sourcePortal.PortalId}' has no Destination Portal Id.",
                sourcePortal);
            return false;
        }

        if (!sourcePortal.IsUnlocked())
        {
            Debug.Log($"Portal '{sourcePortal.PortalId}' is locked by world-state flag " +
                      $"'{sourcePortal.RequiredUnlockFlag}'.", sourcePortal);
            return false;
        }

        return TryTeleportToPortal(
            sourcePortal.DestinationPortalId,
            traveler,
            sourcePortal.DestinationScene,
            sourcePortal.ChangesWorld,
            sourcePortal.DestinationWorld);
    }

    /// <summary>
    /// Uses a source portal by ID. Useful for quest or scripted activation.
    /// </summary>
    public bool TryUsePortal(string sourcePortalId, Transform traveler)
    {
        if (!TryFindPortal(sourcePortalId, out PortalTrigger2D sourcePortal))
        {
            Debug.LogWarning($"Source portal '{sourcePortalId}' was not found in the active scene.");
            return false;
        }

        return TryUsePortal(sourcePortal, traveler);
    }

    /// <summary>
    /// Sends a traveler directly to a destination portal. Leave destinationScene
    /// blank when the portal is in the currently loaded scene.
    /// </summary>
    public bool TryTeleportToPortal(
        string destinationPortalId,
        Transform traveler,
        string destinationScene = null,
        bool changesWorld = false,
        WorldLayer destinationWorld = WorldLayer.WorldA)
    {
        if (traveler == null || string.IsNullOrWhiteSpace(destinationPortalId))
        {
            return false;
        }

        EntityId travelerId = traveler.GetEntityId();
        if (!IsTravelerReady(travelerId))
        {
            return false;
        }

        string activeScene = SceneManager.GetActiveScene().name;
        bool sameScene = string.IsNullOrWhiteSpace(destinationScene) ||
                         string.Equals(destinationScene, activeScene, StringComparison.OrdinalIgnoreCase);

        if (sameScene)
        {
            if (!TryFindPortal(destinationPortalId, out PortalTrigger2D destinationPortal))
            {
                Debug.LogWarning(
                    $"Destination portal '{destinationPortalId}' was not found in scene '{activeScene}'.");
                return false;
            }

            if (destinationPortal.ExitPoint == null)
            {
                Debug.LogWarning(
                    $"Destination portal '{destinationPortalId}' has no Exit Point.",
                    destinationPortal);
                return false;
            }

            if (changesWorld && WorldTravelState.Instance != null)
            {
                WorldTravelState.Instance.RememberTravelerPosition(traveler);
                WorldTravelState.Instance.SetCurrentWorld(destinationWorld);
                traveler = WorldTravelState.Instance.ResolveActiveTraveler(traveler);
            }

            return TeleportTraveler(traveler, destinationPortal);
        }

        pendingScene = destinationScene.Trim();
        pendingDestinationPortalId = destinationPortalId.Trim();
        pendingWorldChange = changesWorld;
        pendingDestinationWorld = destinationWorld;
        if (changesWorld && WorldTravelState.Instance != null)
            WorldTravelState.Instance.RememberTravelerPosition(traveler);
        MarkTravelerCooldown(travelerId);

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene(pendingScene);
        }
        else
        {
            SceneManager.LoadScene(pendingScene);
        }

        return true;
    }

    public bool TryFindPortal(string portalId, out PortalTrigger2D portal)
    {
        portal = null;
        if (string.IsNullOrWhiteSpace(portalId))
        {
            return false;
        }

        PortalTrigger2D[] portals =
            FindObjectsByType<PortalTrigger2D>(FindObjectsInactive.Include);

        for (int i = 0; i < portals.Length; i++)
        {
            PortalTrigger2D candidate = portals[i];
            if (candidate != null &&
                candidate.gameObject.scene == SceneManager.GetActiveScene() &&
                string.Equals(candidate.PortalId, portalId, StringComparison.OrdinalIgnoreCase))
            {
                portal = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TeleportTraveler(Transform traveler, PortalTrigger2D destinationPortal)
    {
        if (traveler == null || destinationPortal == null)
        {
            return false;
        }

        if (destinationPortal.ExitPoint == null)
        {
            Debug.LogWarning(
                $"Destination portal '{destinationPortal.PortalId}' has no Exit Point.",
                destinationPortal);
            return false;
        }

        Vector3 targetPosition = destinationPortal.ArrivalPosition;
        targetPosition.z = traveler.position.z;
        traveler.position = targetPosition;

        if (resetVelocityOnTeleport)
        {
            Rigidbody2D body = traveler.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = exitVelocity;
            }
        }

        MarkTravelerCooldown(traveler.GetEntityId());
        destinationPortal.BlockForSeconds(
            Mathf.Max(travelerCooldownSeconds, destinationPortal.TravelCooldown));
        return true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrWhiteSpace(pendingScene) ||
            !string.Equals(scene.name, pendingScene, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string destinationPortalId = pendingDestinationPortalId;
        bool changesWorld = pendingWorldChange;
        WorldLayer destinationWorld = pendingDestinationWorld;
        ClearPendingDestination();

        if (changesWorld && WorldTravelState.Instance != null)
            WorldTravelState.Instance.SetCurrentWorld(destinationWorld);

        GameObject travelerObject = GameObject.FindGameObjectWithTag(defaultTravelerTag);
        Transform traveler = travelerObject != null ? travelerObject.transform : null;
        if (WorldTravelState.Instance != null)
            traveler = WorldTravelState.Instance.ResolveActiveTraveler(traveler);

        if (traveler == null)
        {
            Debug.LogWarning(
                $"Scene '{scene.name}' loaded, but no traveler tagged '{defaultTravelerTag}' was found.");
            return;
        }

        if (!TryFindPortal(destinationPortalId, out PortalTrigger2D destinationPortal))
        {
            Debug.LogWarning(
                $"Destination portal '{destinationPortalId}' was not found in scene '{scene.name}'.");
            return;
        }

        TeleportTraveler(traveler, destinationPortal);
    }

    private bool IsTravelerReady(EntityId travelerId)
    {
        return !travelerReadyTime.TryGetValue(travelerId, out float readyTime) ||
               Time.time >= readyTime;
    }

    private void MarkTravelerCooldown(EntityId travelerId)
    {
        travelerReadyTime[travelerId] = Time.time + travelerCooldownSeconds;
    }

    private void ClearPendingDestination()
    {
        pendingScene = null;
        pendingDestinationPortalId = null;
        pendingWorldChange = false;
    }
}
