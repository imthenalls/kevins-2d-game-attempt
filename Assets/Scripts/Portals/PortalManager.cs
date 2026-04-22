using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PortalManager : MonoBehaviour
{
    public static PortalManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private TextAsset portalConfigJson;
    [SerializeField] private string resourcesConfigPath = "Portals/portals";
    [SerializeField] private string streamingAssetsFileName = "portals.json";

    [Header("Teleport")]
    [SerializeField] private string defaultTravelerTag = "Player";
    [SerializeField, Min(0f)] private float travelerCooldownSeconds = 0.2f;
    [SerializeField] private bool applyDestinationRotation;
    [SerializeField] private bool resetVelocityOnTeleport = true;
    [SerializeField] private Vector2 exitVelocity = Vector2.zero;

    private readonly Dictionary<string, PortalDefinition> portalById = new Dictionary<string, PortalDefinition>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, float> travelerReadyTime = new Dictionary<int, float>();

    private string pendingScene;
    private string pendingSpawnId;
    private bool pendingUsePortalExitOffset;
    private string pendingDestinationPortalId;
    private string pendingExitSide;
    private float pendingExitDistance;
    private Vector3 pendingFallbackPosition;
    private Vector3 pendingRotationEuler;

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
        LoadPortalConfig();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    public bool TryUsePortal(string portalId, Transform traveler)
    {
        if (traveler == null || string.IsNullOrWhiteSpace(portalId))
        {
            return false;
        }

        int travelerId = traveler.GetInstanceID();
        if (travelerReadyTime.TryGetValue(travelerId, out float readyTime) && Time.time < readyTime)
        {
            return false;
        }

        if (!portalById.TryGetValue(portalId, out PortalDefinition portal))
        {
            Debug.LogWarning($"Portal ID '{portalId}' not found in portal config.");
            return false;
        }

        MarkTravelerCooldown(travelerId);

        string destinationScene = portal.destination.scene;
        bool sameScene = string.IsNullOrWhiteSpace(destinationScene) ||
                         string.Equals(destinationScene, SceneManager.GetActiveScene().name, StringComparison.OrdinalIgnoreCase);

        if (sameScene)
        {
            TeleportTraveler(traveler, portal.destination);
            return true;
        }

        pendingScene = destinationScene;
        pendingSpawnId = portal.destination.spawnId;
        pendingUsePortalExitOffset = portal.destination.usePortalExitOffset;
        pendingDestinationPortalId = portal.destination.destinationPortalId;
        pendingExitSide = portal.destination.exitSide;
        pendingExitDistance = portal.destination.exitDistance;
        pendingFallbackPosition = portal.destination.position.ToVector3();
        pendingRotationEuler = portal.destination.rotationEuler.ToVector3();

        SceneManager.LoadScene(destinationScene);
        return true;
    }

    public bool TryGetPortal(string portalId, out PortalDefinition portal)
    {
        return portalById.TryGetValue(portalId, out portal);
    }

    private void LoadPortalConfig()
    {
        string json = LoadConfigJsonText();
        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogWarning("Portal config not found. Assign a TextAsset or provide StreamingAssets/portals.json.");
            return;
        }

        PortalDatabaseJson database;
        try
        {
            database = JsonUtility.FromJson<PortalDatabaseJson>(json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to parse portal config JSON: {ex.Message}");
            return;
        }

        if (database == null || database.portals == null)
        {
            Debug.LogError("Portal config JSON parsed as null or missing portals array.");
            return;
        }

        portalById.Clear();

        for (int i = 0; i < database.portals.Count; i++)
        {
            PortalDefinition portal = database.portals[i];
            if (portal == null || string.IsNullOrWhiteSpace(portal.id))
            {
                Debug.LogWarning($"Skipping portal entry at index {i} because id is missing.");
                continue;
            }

            if (portalById.ContainsKey(portal.id))
            {
                Debug.LogWarning($"Duplicate portal id '{portal.id}' found. Keeping first entry.");
                continue;
            }

            portalById.Add(portal.id, portal);
        }
    }

    private string LoadConfigJsonText()
    {
        if (portalConfigJson != null)
        {
            return portalConfigJson.text;
        }

        if (!string.IsNullOrWhiteSpace(resourcesConfigPath))
        {
            TextAsset resource = Resources.Load<TextAsset>(resourcesConfigPath);
            if (resource != null)
            {
                return resource.text;
            }
        }

        if (string.IsNullOrWhiteSpace(streamingAssetsFileName))
        {
            return null;
        }

        string path = Path.Combine(Application.streamingAssetsPath, streamingAssetsFileName);
        if (File.Exists(path))
        {
            return File.ReadAllText(path);
        }

        return null;
    }

    private void TeleportTraveler(Transform traveler, PortalDestination destination)
    {
        Vector3 targetPosition = ResolveDestinationPosition(destination);
        traveler.position = targetPosition;

        if (applyDestinationRotation)
        {
            Vector3 euler = destination.rotationEuler.ToVector3();
            traveler.rotation = Quaternion.Euler(euler);
        }

        if (resetVelocityOnTeleport)
        {
            Rigidbody2D body = traveler.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = exitVelocity;
            }
        }
    }

    private Vector3 ResolveDestinationPosition(PortalDestination destination)
    {
        if (destination.usePortalExitOffset &&
            !string.IsNullOrWhiteSpace(destination.destinationPortalId) &&
            TryResolvePortalExitPosition(destination.destinationPortalId, destination.exitSide, destination.exitDistance, out Vector3 portalExitPosition))
        {
            return portalExitPosition;
        }

        if (destination.useSpawnPoint && !string.IsNullOrWhiteSpace(destination.spawnId))
        {
            PortalSpawnPoint[] points = FindObjectsByType<PortalSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
            {
                PortalSpawnPoint point = points[i];
                if (point != null && string.Equals(point.SpawnId, destination.spawnId, StringComparison.OrdinalIgnoreCase))
                {
                    return point.transform.position;
                }
            }

            Debug.LogWarning($"Spawn point '{destination.spawnId}' not found. Falling back to raw destination position.");
        }

        return destination.position.ToVector3();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.IsNullOrWhiteSpace(pendingScene))
        {
            return;
        }

        if (!string.Equals(scene.name, pendingScene, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        GameObject travelerObj = GameObject.FindGameObjectWithTag(defaultTravelerTag);
        if (travelerObj == null)
        {
            Debug.LogWarning($"Scene '{scene.name}' loaded but no traveler with tag '{defaultTravelerTag}' was found.");
            ClearPendingDestination();
            return;
        }

        Transform traveler = travelerObj.transform;

        Vector3 targetPosition = pendingFallbackPosition;
        if (pendingUsePortalExitOffset &&
            !string.IsNullOrWhiteSpace(pendingDestinationPortalId) &&
            TryResolvePortalExitPosition(pendingDestinationPortalId, pendingExitSide, pendingExitDistance, out Vector3 portalExitPosition))
        {
            targetPosition = portalExitPosition;
        }
        else if (!string.IsNullOrWhiteSpace(pendingSpawnId))
        {
            PortalSpawnPoint[] points = FindObjectsByType<PortalSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
            {
                PortalSpawnPoint point = points[i];
                if (point != null && string.Equals(point.SpawnId, pendingSpawnId, StringComparison.OrdinalIgnoreCase))
                {
                    targetPosition = point.transform.position;
                    break;
                }
            }
        }

        traveler.position = targetPosition;

        if (applyDestinationRotation)
        {
            traveler.rotation = Quaternion.Euler(pendingRotationEuler);
        }

        if (resetVelocityOnTeleport)
        {
            Rigidbody2D body = traveler.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = exitVelocity;
            }
        }

        MarkTravelerCooldown(traveler.GetInstanceID());
        ClearPendingDestination();
    }

    private void MarkTravelerCooldown(int travelerId)
    {
        travelerReadyTime[travelerId] = Time.time + travelerCooldownSeconds;
    }

    private void ClearPendingDestination()
    {
        pendingScene = null;
        pendingSpawnId = null;
        pendingUsePortalExitOffset = false;
        pendingDestinationPortalId = null;
        pendingExitSide = null;
        pendingExitDistance = 0f;
        pendingFallbackPosition = Vector3.zero;
        pendingRotationEuler = Vector3.zero;
    }

    private bool TryResolvePortalExitPosition(string portalId, string exitSide, float exitDistance, out Vector3 targetPosition)
    {
        PortalTrigger2D[] portalTriggers = FindObjectsByType<PortalTrigger2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < portalTriggers.Length; i++)
        {
            PortalTrigger2D trigger = portalTriggers[i];
            if (trigger == null || !string.Equals(trigger.PortalId, portalId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Vector3 direction = ExitSideToVector(exitSide);
            float distance = Mathf.Max(0.01f, exitDistance);
            targetPosition = trigger.transform.position + (direction * distance);
            return true;
        }

        targetPosition = Vector3.zero;
        return false;
    }

    private static Vector3 ExitSideToVector(string exitSide)
    {
        if (string.IsNullOrWhiteSpace(exitSide))
        {
            return Vector3.right;
        }

        switch (exitSide.Trim().ToLowerInvariant())
        {
            case "up":
            case "above":
            case "top":
                return Vector3.up;
            case "down":
            case "below":
            case "bottom":
                return Vector3.down;
            case "left":
                return Vector3.left;
            case "right":
                return Vector3.right;
            default:
                return Vector3.right;
        }
    }
}
