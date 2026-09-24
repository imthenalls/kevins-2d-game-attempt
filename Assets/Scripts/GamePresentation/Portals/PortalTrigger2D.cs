using System.Collections.Generic;
using Game.Core;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// A scene-authored portal. This component is the single source of truth for
/// portal identity, destination, arrival point, and trigger behavior. Same-world
/// portals activate on contact; world-changing portals require G while the player
/// is inside the trigger.
///
/// Unity setup:
///   1. Add to a portal GameObject with a Collider2D; the collider is forced to Is Trigger.
///   2. Set Portal Id, Destination Scene, Destination Portal Id, and Exit Point.
///   3. Enable Changes World and choose Destination World when G should be required.
///   4. Set Required Tag to the traveler tag, normally Player.
///
/// Runtime API: PortalManager calls the public route properties, IsUnlocked(), and
/// BlockForSeconds(). Other systems may pass this component to PortalManager.TryUsePortal().
/// </summary>
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public class PortalTrigger2D : MonoBehaviour, IPortalRoute
{
    [Header("Config (Game.Data)")]
    [SerializeField] private PortalTriggerConfig config = new PortalTriggerConfig();

    [Header("Identity")]
    [SerializeField] private string portalId;

    [Header("Destination")]
    [Tooltip("Leave blank when the destination portal is in this scene.")]
    [SerializeField] private string destinationScene;
    [SerializeField] private string destinationPortalId;

    [Header("World Switching")]
    [Tooltip("Enable when this route changes which world and playable character are active.")]
    [SerializeField] private bool changesWorld;
    [SerializeField] private WorldLayer destinationWorld = WorldLayer.WorldA;
    [Tooltip("Optional WorldStateManager flag required to use this route.")]
    [SerializeField] private string requiredUnlockFlag;
    [Tooltip("Optional key item id the traveler must hold (via IKeyHolder) to use this route.")]
    [SerializeField] private string requiredKeyId;

    [Header("Arrival")]
    [Tooltip("Exact position where travelers arrive at this portal.")]
    [SerializeField] private Transform exitPoint;

    [Header("Incoming Sources (Documentation)")]
    [Tooltip("Optional notes for non-portal sources that can send a traveler here, such as an NPC, quest, or scripted event. Portal-to-portal links are derived automatically by the map exporter.")]
    [SerializeField] private List<string> additionalIncomingSources = new List<string>();

    private float nextAllowedUseTime;
    private Collider2D waitingWorldTraveler;

    public string PortalId => portalId;
    public string DestinationScene => destinationScene;
    public string DestinationPortalId => destinationPortalId;
    public bool ChangesWorld => changesWorld;
    public WorldLayer DestinationWorld => destinationWorld;
    public string RequiredUnlockFlag => requiredUnlockFlag;
    public string RequiredKeyId => requiredKeyId;
    public Transform ExitPoint => exitPoint;
    public List<string> AdditionalIncomingSources => additionalIncomingSources;
    public float TravelCooldown => config.TravelCooldown;
    public Vector3 ArrivalPosition => exitPoint != null ? exitPoint.position : transform.position;
    public Component Self => this;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnValidate()
    {
        portalId = portalId != null ? portalId.Trim() : string.Empty;
        destinationScene = destinationScene != null ? destinationScene.Trim() : string.Empty;
        destinationPortalId = destinationPortalId != null ? destinationPortalId.Trim() : string.Empty;
        requiredUnlockFlag = requiredUnlockFlag != null ? requiredUnlockFlag.Trim() : string.Empty;
        requiredKeyId = requiredKeyId != null ? requiredKeyId.Trim() : string.Empty;

        if (additionalIncomingSources == null)
        {
            additionalIncomingSources = new List<string>();
        }
        else
        {
            for (int i = 0; i < additionalIncomingSources.Count; i++)
            {
                additionalIncomingSources[i] = additionalIncomingSources[i] != null
                    ? additionalIncomingSources[i].Trim()
                    : string.Empty;
            }
        }

        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!CanUseTrigger(other))
        {
            return;
        }

        if (changesWorld)
        {
            waitingWorldTraveler = other;
            return;
        }

        TryTravel(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (waitingWorldTraveler == other)
        {
            waitingWorldTraveler = null;
        }
    }

    // Keeps the pending world-traveler set while the traveler stands inside, so G works even if the
    // traveler never produced an enter event (for example, it spawned already inside the trigger).
    private void OnTriggerStay2D(Collider2D other)
    {
        if (changesWorld && CanUseTrigger(other))
            waitingWorldTraveler = other;
    }

    // Contact portals: a traveler already inside when the trigger starts (spawned on the portal)
    // never fires OnTriggerEnter2D, so send it through once here.
    private void Start()
    {
        if (changesWorld)
            return;

        Collider2D col = GetComponent<Collider2D>();
        if (col == null)
            return;

        Bounds bounds = col.bounds;
        Collider2D[] hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] != null && hits[i] != col && CanUseTrigger(hits[i]))
            {
                TryTravel(hits[i]);
                return;
            }
        }
    }

    private void OnDisable()
    {
        waitingWorldTraveler = null;
    }

    private void Update()
    {
        if (!changesWorld || waitingWorldTraveler == null ||
            Time.time < nextAllowedUseTime || !WasWorldTravelPressedThisFrame())
        {
            return;
        }

        TryTravel(waitingWorldTraveler);
    }

    private bool CanUseTrigger(Collider2D other)
    {
        return other != null &&
               (string.IsNullOrEmpty(config.RequiredTag) || other.CompareTag(config.RequiredTag));
    }

    private void TryTravel(Collider2D travelerCollider)
    {
        if (travelerCollider == null || Time.time < nextAllowedUseTime)
        {
            return;
        }

        Transform traveler = travelerCollider.attachedRigidbody != null
            ? travelerCollider.attachedRigidbody.transform
            : travelerCollider.transform;

        PortalManager manager = PortalManager.Instance;
        if (manager != null && manager.TryUsePortal(this, traveler))
        {
            BlockForSeconds(config.TravelCooldown);
        }
    }

    private static bool WasWorldTravelPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.G);
#endif
    }

    public void BlockForSeconds(float seconds)
    {
        if (seconds > 0f)
        {
            nextAllowedUseTime = Mathf.Max(nextAllowedUseTime, Time.time + seconds);
        }
    }

    public bool IsUnlocked()
    {
        return string.IsNullOrEmpty(requiredUnlockFlag) ||
               (WorldStateManager.Instance != null &&
                WorldStateManager.Instance.HasFlag(requiredUnlockFlag));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = exitPoint != null ? Color.cyan : Color.red;
        Vector3 arrival = ArrivalPosition;
        Gizmos.DrawLine(transform.position, arrival);
        Gizmos.DrawWireSphere(arrival, 0.2f);
    }
}
