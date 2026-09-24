using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Executes same-scene and cross-scene portal travel for both 2D and 3D portals. Portal routes are
/// authored entirely on portal components (<see cref="PortalTrigger2D"/> / <see cref="PortalTrigger3D"/>,
/// surfaced through <see cref="IPortalRoute"/>); this manager does not load portal JSON.
/// </summary>
public class PortalManager : MonoBehaviour
{
    public static PortalManager Instance { get; private set; }

    [Header("Config (Game.Data)")]
    [SerializeField] private PortalManagerConfig config = new PortalManagerConfig();

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

    public bool TryUsePortal(IPortalRoute sourcePortal, Transform traveler)
    {
        if (sourcePortal == null || traveler == null)
            return false;

        if (string.IsNullOrWhiteSpace(sourcePortal.DestinationPortalId))
        {
            Debug.LogWarning(
                $"Portal '{sourcePortal.PortalId}' has no Destination Portal Id.",
                sourcePortal.Self);
            return false;
        }

        if (!sourcePortal.IsUnlocked())
        {
            Debug.Log($"Portal '{sourcePortal.PortalId}' is locked by world-state flag " +
                      $"'{sourcePortal.RequiredUnlockFlag}'.", sourcePortal.Self);
            return false;
        }

        if (!IsKeySatisfied(sourcePortal, traveler))
            return false;

        return TryTeleportToPortal(
            sourcePortal.DestinationPortalId,
            traveler,
            sourcePortal.DestinationScene,
            sourcePortal.ChangesWorld,
            sourcePortal.DestinationWorld);
    }

    /// <summary>Uses a source portal by ID. Useful for quest or scripted activation.</summary>
    public bool TryUsePortal(string sourcePortalId, Transform traveler)
    {
        if (!TryFindPortal(sourcePortalId, out IPortalRoute sourcePortal))
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
            return false;

        EntityId travelerId = traveler.GetEntityId();
        if (!IsTravelerReady(travelerId))
            return false;

        string activeScene = SceneManager.GetActiveScene().name;
        bool sameScene = string.IsNullOrWhiteSpace(destinationScene) ||
                         string.Equals(destinationScene, activeScene, StringComparison.OrdinalIgnoreCase);

        if (sameScene)
        {
            if (!TryFindPortal(destinationPortalId, out IPortalRoute destinationPortal))
            {
                Debug.LogWarning(
                    $"Destination portal '{destinationPortalId}' was not found in scene '{activeScene}'.");
                return false;
            }

            if (destinationPortal.ExitPoint == null)
            {
                Debug.LogWarning(
                    $"Destination portal '{destinationPortalId}' has no Exit Point.",
                    destinationPortal.Self);
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
            SceneLoader.Instance.LoadScene(pendingScene);
        else
            SceneManager.LoadScene(pendingScene);

        return true;
    }

    public bool TryFindPortal(string portalId, out IPortalRoute portal)
    {
        portal = null;
        if (string.IsNullOrWhiteSpace(portalId))
            return false;

        Scene active = SceneManager.GetActiveScene();
        List<IPortalRoute> portals = FindAllPortals();
        for (int i = 0; i < portals.Count; i++)
        {
            IPortalRoute candidate = portals[i];
            if (candidate != null &&
                candidate.Self.gameObject.scene == active &&
                string.Equals(candidate.PortalId, portalId, StringComparison.OrdinalIgnoreCase))
            {
                portal = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TeleportTraveler(Transform traveler, IPortalRoute destinationPortal)
    {
        if (traveler == null || destinationPortal == null)
            return false;

        if (destinationPortal.ExitPoint == null)
        {
            Debug.LogWarning(
                $"Destination portal '{destinationPortal.PortalId}' has no Exit Point.",
                destinationPortal.Self);
            return false;
        }

        Vector3 targetPosition = destinationPortal.ArrivalPosition;

        if (traveler.TryGetComponent(out Rigidbody2D body2D))
        {
            targetPosition.z = traveler.position.z;
            traveler.position = targetPosition;
            body2D.position = targetPosition;
            if (config.ResetVelocityOnTeleport)
                body2D.linearVelocity = new Vector2(config.ExitVelocityX, config.ExitVelocityY);
        }
        else if (traveler.TryGetComponent(out Rigidbody body3D))
        {
            targetPosition.y = body3D.position.y;
            traveler.position = targetPosition;
            body3D.position = targetPosition;
            body3D.linearVelocity = config.ResetVelocityOnTeleport
                ? new Vector3(config.ExitVelocityX, 0f, config.ExitVelocityY)
                : Vector3.zero;
        }
        else
        {
            traveler.position = targetPosition;
        }

        MarkTravelerCooldown(traveler.GetEntityId());
        destinationPortal.BlockForSeconds(
            Mathf.Max(config.TravelerCooldownSeconds, destinationPortal.TravelCooldown));
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

        GameObject travelerObject = GameObject.FindGameObjectWithTag(config.DefaultTravelerTag);
        Transform traveler = travelerObject != null ? travelerObject.transform : null;
        if (WorldTravelState.Instance != null)
            traveler = WorldTravelState.Instance.ResolveActiveTraveler(traveler);

        if (traveler == null)
        {
            Debug.LogWarning(
                $"Scene '{scene.name}' loaded, but no traveler tagged '{config.DefaultTravelerTag}' was found.");
            return;
        }

        if (!TryFindPortal(destinationPortalId, out IPortalRoute destinationPortal))
        {
            Debug.LogWarning(
                $"Destination portal '{destinationPortalId}' was not found in scene '{scene.name}'.");
            return;
        }

        TeleportTraveler(traveler, destinationPortal);
    }

    private static List<IPortalRoute> FindAllPortals()
    {
        var portals = new List<IPortalRoute>();

        foreach (PortalTrigger2D portal in
            FindObjectsByType<PortalTrigger2D>(FindObjectsInactive.Include))
        {
            portals.Add(portal);
        }

        foreach (PortalTrigger3D portal in
            FindObjectsByType<PortalTrigger3D>(FindObjectsInactive.Include))
        {
            portals.Add(portal);
        }

        return portals;
    }

    // A route that names a key only blocks travelers that actually carry a key holder; entities with
    // no key holder (the player, until the keyring is attached to the body) pass through.
    private static bool IsKeySatisfied(IPortalRoute sourcePortal, Transform traveler)
    {
        if (string.IsNullOrWhiteSpace(sourcePortal.RequiredKeyId))
            return true;

        IKeyHolder holder = traveler.GetComponentInParent<IKeyHolder>();

        // The player's keyring is a persistent singleton, not on the player body.
        if (holder == null && traveler.GetComponent<PlayerControllerBase>() != null)
            holder = PlayerKeyring.Instance;

        if (holder != null && holder.HasKey(sourcePortal.RequiredKeyId))
            return true;

        // Keys can also be real items held in an NPC's inventory.
        NpcController npc = traveler.GetComponentInParent<NpcController>();
        if (npc != null && InventoryHasKey(npc.Inventory, sourcePortal.RequiredKeyId))
            return true;

        Debug.Log($"Portal '{sourcePortal.PortalId}' is locked; '{traveler.name}' lacks key " +
                  $"'{sourcePortal.RequiredKeyId}'.");
        return false;
    }

    // True when the inventory holds an item with the given id.
    private static bool InventoryHasKey(InventoryModel inventory, string itemId)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(itemId))
            return false;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            InventorySlot slot = inventory.GetSlot(i);
            if (!slot.IsEmpty && slot.item != null &&
                string.Equals(slot.item.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTravelerReady(EntityId travelerId)    {
        return !travelerReadyTime.TryGetValue(travelerId, out float readyTime) ||
               Time.time >= readyTime;
    }

    private void MarkTravelerCooldown(EntityId travelerId)
    {
        travelerReadyTime[travelerId] = Time.time + config.TravelerCooldownSeconds;
    }

    private void ClearPendingDestination()
    {
        pendingScene = null;
        pendingDestinationPortalId = null;
        pendingWorldChange = false;
    }
}
