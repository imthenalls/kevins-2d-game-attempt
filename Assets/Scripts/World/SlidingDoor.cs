using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which grid axis a gate's cells progress along. The halves retract along this axis.
/// </summary>
public enum DoorAxis
{
    /// <summary>Cells advance along the grid +X axis (down-right diamond edge).</summary>
    GridX,
    /// <summary>Cells advance along the grid +Y axis (up-right diamond edge).</summary>
    GridY
}

/// <summary>
/// Outcome of an entity attempting to use a gate. Returned by SlidingDoor.TryUse so scripts and
/// AI can react (for example, an NPC remembering that a gate is locked).
/// </summary>
public enum GateUseResult
{
    /// <summary>The gate is open (opened now, or was already open).</summary>
    Opened,
    /// <summary>The required key was missing, so the gate stayed closed.</summary>
    Locked,
    /// <summary>The gate is mid-animation; try again shortly.</summary>
    Busy,
    /// <summary>The gate cannot be used (disabled or not built).</summary>
    Unavailable
}

/// <summary>
/// A keyboard (E) interactable gate built from whole grid cells. Each covered cell gets a
/// diamond sprite positioned with <see cref="GridLayout.GetCellCenterWorld"/>, so the gate is
/// always aligned to the tilemap instead of an eyeballed rotated rectangle. The gate is split
/// into two halves that retract outward by whole-cell vectors to open. A solid collider per
/// cell blocks passage while closed. It can require a key item from the player's inventory
/// before opening, and it tints its cells by lock state.
///
/// Unity setup:
///   1. Place the gate root on the first cell of the opening, inside the target Grid hierarchy
///      (or assign Grid explicitly).
///   2. Add this component and a trigger Collider2D (added automatically by the prefab).
///   3. Assign Grid, Door Axis, Cell Length, and the diamond Gate Sprite.
///   4. Optionally configure Display Name, Interaction Range, Starts Open, Can Close,
///      Required Key Id, key consumption, and unlocked persistence.
///   5. Keep the root on a layer included by PlayerInteractionController.Interactable Layers.
///   The generated GateHalfA/GateHalfB/GateCell children are created automatically.
///
/// Runtime API:
///   TryOpen(interactor) performs the inventory key check. Open(), Close(), and Toggle()
///   provide direct scripted control. IsOpen, IsMoving, and IsLocked expose state.
///   OnUnlocked, OnUnlockFailed, OnOpened, and OnClosed report state changes.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class SlidingDoor : MonoBehaviour, IInteractable
{
    [Header("Grid Gate")]
    [Tooltip("Grid the gate snaps to. Defaults to the parent Grid when left empty.")]
    [SerializeField] private Grid grid;
    [SerializeField] private DoorAxis axis = DoorAxis.GridX;
    [Tooltip("Number of grid cells the closed gate covers.")]
    [SerializeField, Min(1)] private int cellLength = 2;
    [Tooltip("Diamond sprite drawn on each covered cell.")]
    [SerializeField] private Sprite gateSprite;
    [Tooltip("Stable id used by NPC memory and save data. Falls back to the GameObject name.")]
    [SerializeField] private string gateId;

    [Header("Movement")]
    [Tooltip("Swap which half retracts in which direction.")]
    [SerializeField] private bool reverseSlideDirection;
    [SerializeField, Min(0.05f)] private float slideDuration = 0.45f;

    [Header("Auto Close")]
    [Tooltip("Closes the gate shortly after the last traveler leaves the gate trigger.")]
    [SerializeField] private bool closeAfterPassing = true;
    [SerializeField, Min(0f)] private float closeDelay = 0.25f;
    [Tooltip("Layers counted as travelers that can trigger an auto close.")]
    [SerializeField] private LayerMask travelerLayers = ~0;

    [Header("Interaction")]
    [SerializeField] private string displayName = "Gate";
    [Tooltip("Interaction range in world units. Measured to the nearest covered cell, so a long gate is reachable from any of its cells.")]
    [SerializeField, Min(0.25f)] private float interactionRange = 1f;
    [SerializeField] private bool startsOpen;
    [SerializeField] private bool canClose = true;

    [Header("Lock")]
    [SerializeField] private string requiredKeyId = "golden_key";
    [SerializeField] private bool consumeKeyOnUnlock;
    [SerializeField] private bool remainUnlocked = true;
    [Tooltip("Interaction message shown when the gate is locked and the player lacks the key. {0} is replaced by the key name.")]
    [SerializeField] private string lockedMessage = "It's locked. You need the {0}.";

    [Header("Gate Colors")]
    [SerializeField] private Color lockedColor = new Color(0.95f, 0.28f, 0.16f, 1f);
    [SerializeField] private Color unlockedColor = new Color(0.22f, 0.9f, 0.82f, 1f);

    private Transform halfA;
    private Transform halfB;
    private readonly List<SpriteRenderer> cellRenderers = new List<SpriteRenderer>();
    private readonly List<Collider2D> cellColliders = new List<Collider2D>();
    private Vector3 openDeltaA;
    private Vector3 openDeltaB;
    private Coroutine slideCoroutine;
    private Coroutine autoCloseCoroutine;
    private readonly HashSet<Collider2D> travelersInside = new HashSet<Collider2D>();
    private bool built;
    private Vector3Int builtAnchor;
    private DoorAxis builtAxis;
    private int builtLength;
    private Sprite builtSprite;
    private bool unlocked;
    private bool lockedMessagePending;

    public bool IsOpen { get; private set; }
    public bool IsMoving => slideCoroutine != null;
    public bool IsLocked => !unlocked && !string.IsNullOrWhiteSpace(requiredKeyId);
    public string RequiredKeyId => requiredKeyId;
    public string GateId => string.IsNullOrWhiteSpace(gateId) ? name : gateId;
    public event Action OnUnlocked;
    public event Action OnUnlockFailed;
    public event Action OnOpened;
    public event Action OnClosed;

    /// <summary>Fired after every use attempt with the interacting entity and the outcome.</summary>
    public event Action<GameObject, GateUseResult> OnUseResolved;

    private Vector3Int AxisVector => axis == DoorAxis.GridX ? new Vector3Int(1, 0, 0) : new Vector3Int(0, 1, 0);

    // Builds (or rebuilds) the gate whenever the component is enabled, in edit and play mode.
    private void OnEnable() => EnsureBuilt();

    private void OnDisable()
    {
        built = false;
        travelersInside.Clear();
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }
    }

    // Tracks entities passing through the interaction trigger so the gate can close behind them.
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsTraveler(other)) return;

        travelersInside.Add(other);
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!travelersInside.Remove(other)) return;

        if (IsOpen && canClose && closeAfterPassing && travelersInside.Count == 0)
            ScheduleAutoClose();
    }

    // A traveler is anything on a traveler layer that has a Rigidbody2D or an IEntityController.
    private bool IsTraveler(Collider2D other)
    {
        if (other == null) return false;
        if ((travelerLayers.value & (1 << other.gameObject.layer)) == 0) return false;
        return other.attachedRigidbody != null ||
               other.GetComponentInParent<IEntityController>() != null;
    }

    // Closes the gate after a short delay once the trigger is clear.
    private void ScheduleAutoClose()
    {
        if (autoCloseCoroutine != null) StopCoroutine(autoCloseCoroutine);
        autoCloseCoroutine = StartCoroutine(AutoCloseRoutine());
    }

    private IEnumerator AutoCloseRoutine()
    {
        if (closeDelay > 0f)
            yield return new WaitForSeconds(closeDelay);

        autoCloseCoroutine = null;
        if (IsOpen && canClose && closeAfterPassing && travelersInside.Count == 0)
            Close();
    }

    // Editor: rebuild a live preview when serialized settings change.
    private void OnValidate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying) return;
        built = false;
        UnityEditor.EditorApplication.delayCall -= EditorRebuild;
        UnityEditor.EditorApplication.delayCall += EditorRebuild;
#endif
    }

#if UNITY_EDITOR
    private void EditorRebuild()
    {
        UnityEditor.EditorApplication.delayCall -= EditorRebuild;
        if (this == null || Application.isPlaying) return;
        EnsureBuilt();
    }
#endif

    // Resolves the grid, snaps the root onto its cell, and builds the cell visuals if needed.
    private void EnsureBuilt()
    {
        if (this == null) return;

        if (grid == null)
            grid = GetComponentInParent<Grid>();

        if (grid == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[SlidingDoor] No Grid found. Assign Grid or parent the gate under a Grid.", this);
                enabled = false;
            }
            return;
        }

        Vector3Int anchor = grid.WorldToCell(transform.position);
        if (built && anchor == builtAnchor && axis == builtAxis &&
            cellLength == builtLength && gateSprite == builtSprite)
            return;

        transform.SetPositionAndRotation(grid.GetCellCenterWorld(anchor), Quaternion.identity);
        transform.localScale = Vector3.one;
        BuildGate(anchor);

        builtAnchor = anchor;
        builtAxis = axis;
        builtLength = cellLength;
        builtSprite = gateSprite;

        IsOpen = startsOpen;
        unlocked = startsOpen || string.IsNullOrWhiteSpace(requiredKeyId);
        ApplyPose(IsOpen, immediate: true);
        ApplyLockVisuals();
    }

    // Clears previously generated children and lays the gate out one diamond per cell.
    private void BuildGate(Vector3Int anchor)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "GateHalfA" || child.name == "GateHalfB" || child.name.StartsWith("GateCell"))
                DestroyObject(child.gameObject);
        }

        halfA = new GameObject("GateHalfA").transform;
        halfA.SetParent(transform, false);
        halfB = new GameObject("GateHalfB").transform;
        halfB.SetParent(transform, false);

        cellRenderers.Clear();
        cellColliders.Clear();

        Vector3Int dir = AxisVector;
        Vector3 firstCenter = grid.GetCellCenterWorld(anchor);
        Vector3 step = grid.GetCellCenterWorld(anchor + dir) - firstCenter;

        int mid = cellLength <= 1 ? 1 : cellLength / 2;
        for (int i = 0; i < cellLength; i++)
        {
            Vector3Int cell = anchor + dir * i;
            var cellObject = new GameObject($"GateCell_{i}", typeof(SpriteRenderer));
            cellObject.transform.SetParent(i < mid ? halfA : halfB, false);
            cellObject.transform.position = grid.GetCellCenterWorld(cell);

            var renderer = cellObject.GetComponent<SpriteRenderer>();
            renderer.sprite = gateSprite;
            renderer.sortingOrder = 2;

            var collider = cellObject.AddComponent<PolygonCollider2D>();
            collider.SetPath(0, DiamondPoints());
            collider.isTrigger = false;

            cellRenderers.Add(renderer);
            cellColliders.Add(collider);
        }

        float sign = reverseSlideDirection ? -1f : 1f;
        openDeltaA = -sign * mid * step;
        openDeltaB = sign * (cellLength - mid) * step;
        built = true;
    }

    // Four diamond corners matching one isometric cell, relative to the cell center.
    private Vector2[] DiamondPoints()
    {
        Vector2 size = grid.cellSize;
        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        return new[]
        {
            new Vector2(hx, 0f),
            new Vector2(0f, hy),
            new Vector2(-hx, 0f),
            new Vector2(0f, -hy)
        };
    }

    private static void DestroyObject(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    // Returns true when the position is within interaction range of the nearest covered cell.
    public bool CanInteract(Vector3 worldPosition)
    {
        if (!enabled || IsMoving)
            return false;

        if (grid == null || cellLength <= 0)
            return Vector2.Distance(transform.position, worldPosition) <= interactionRange;

        Vector3Int anchor = built ? builtAnchor : grid.WorldToCell(transform.position);
        Vector3Int dir = AxisVector;
        float nearest = float.MaxValue;
        for (int i = 0; i < cellLength; i++)
        {
            Vector3 center = grid.GetCellCenterWorld(anchor + dir * i);
            nearest = Mathf.Min(nearest, Vector2.Distance(center, worldPosition));
        }

        return nearest <= interactionRange;
    }

    // Supplies the name used by the shared interaction system.
    public string GetDisplayName() => displayName;

    // Shows a locked message when the player lacks the key; otherwise proceeds directly to opening.
    public bool TryGetCurrentLine(out string line)
    {
        line = string.Empty;

        if (lockedMessagePending)
        {
            // The message was already shown; end the interaction on the next advance.
            lockedMessagePending = false;
            return false;
        }

        if (!IsLocked || PlayerHasRequiredKey())
            return false;

        line = BuildLockedMessage();
        lockedMessagePending = true;
        return true;
    }

    // Required by IInteractable; gates have no text pages to advance.
    public void Advance() { }

    // Closes an open gate or attempts a key-checked open when E interaction completes.
    public void EndInteraction(GameObject interactor)
    {
        lockedMessagePending = false;
        if (IsOpen) Close();
        else TryOpen(interactor);
    }

    // True when the player's keyring holds the required key.
    private bool PlayerHasRequiredKey()
    {
        if (string.IsNullOrWhiteSpace(requiredKeyId))
            return true;

        PlayerKeyring keyring = PlayerKeyring.GetOrCreate();
        return keyring != null && keyring.HasKey(requiredKeyId);
    }

    // Builds the locked interaction message, substituting the key's display name when known.
    private string BuildLockedMessage()
    {
        string keyName = requiredKeyId;
        ItemDatabase database = ItemDatabase.Instance;
        if (database != null && database.TryGet(requiredKeyId, out ItemData key) &&
            key != null && !string.IsNullOrWhiteSpace(key.itemName))
        {
            keyName = key.itemName;
        }

        return string.IsNullOrWhiteSpace(lockedMessage)
            ? $"It's locked. You need the {keyName}."
            : string.Format(lockedMessage, keyName);
    }

    /// <summary>
    /// Attempts to unlock and open the gate for the given interactor. Returns true only when the
    /// gate ends up open. Use <see cref="TryUse"/> when you need the failure reason.
    /// </summary>
    public bool TryOpen(GameObject interactor) => TryUse(interactor) == GateUseResult.Opened;

    /// <summary>
    /// Attempts to use the gate for the given entity and reports the outcome. Key checks resolve
    /// the entity's own <see cref="IKeyHolder"/> (falling back to the player keyring for the
    /// player), so the same call works for a player or an NPC.
    /// </summary>
    public GateUseResult TryUse(GameObject interactor)
    {
        if (!built)
        {
            ResolveUse(interactor, GateUseResult.Unavailable);
            return GateUseResult.Unavailable;
        }

        if (IsOpen)
        {
            ResolveUse(interactor, GateUseResult.Opened);
            return GateUseResult.Opened;
        }

        if (IsMoving)
        {
            ResolveUse(interactor, GateUseResult.Busy);
            return GateUseResult.Busy;
        }

        if (!TryUnlock(interactor, out GateUseResult failure))
        {
            ResolveUse(interactor, failure);
            return failure;
        }

        ApplyPose(true, immediate: false);
        ResolveUse(interactor, GateUseResult.Opened);
        return GateUseResult.Opened;
    }

    // Raises the outcome event for observers such as NPC AI.
    private void ResolveUse(GameObject interactor, GateUseResult result)
    {
        OnUseResolved?.Invoke(interactor, result);
    }

    /// <summary>Retracts both halves to the open position if the gate is closed.</summary>
    public void Open()
    {
        if (!built || IsOpen || IsMoving) return;
        ApplyPose(true, immediate: false);
    }

    /// <summary>Returns both halves to the closed position when closing is enabled.</summary>
    public void Close()
    {
        if (!built || !IsOpen || !canClose || IsMoving) return;
        ApplyPose(false, immediate: false);
    }

    /// <summary>Toggles between open and closed states.</summary>
    public void Toggle()
    {
        if (!built || IsMoving) return;
        ApplyPose(!IsOpen, immediate: false);
    }

    // Resolves the interactor's key holder, checks the required key, and optionally consumes it.
    private bool TryUnlock(GameObject interactor, out GateUseResult failure)
    {
        failure = GateUseResult.Locked;
        if (!IsLocked) return true;

        IKeyHolder holder = ResolveKeyHolder(interactor);
        ItemDatabase database = ItemDatabase.Instance;
        if (database == null || holder == null ||
            !database.TryGet(requiredKeyId, out ItemData key) || key == null ||
            !holder.HasKey(requiredKeyId))
        {
            Debug.Log($"[SlidingDoor] {displayName} is locked. Required key: '{requiredKeyId}'.", this);
            OnUnlockFailed?.Invoke();
            return false;
        }

        if (consumeKeyOnUnlock)
        {
            if (!holder.RemoveKey(requiredKeyId, 1))
            {
                OnUnlockFailed?.Invoke();
                return false;
            }

            QuestEventBus.Raise("ItemUsed", key.itemId, 1);
        }

        if (remainUnlocked) unlocked = true;

        ApplyLockVisuals();
        Debug.Log($"[SlidingDoor] {displayName} unlocked with '{key.itemName}'.", interactor);
        OnUnlocked?.Invoke();
        return true;
    }

    // Resolves the key holder owned by the interacting entity. The player's keyring is a separate
    // persistent singleton, so the player falls back to it; NPCs use their own IKeyHolder.
    private static IKeyHolder ResolveKeyHolder(GameObject interactor)
    {
        if (interactor == null)
            return PlayerKeyring.GetOrCreate();

        IKeyHolder holder = interactor.GetComponentInParent<IKeyHolder>();
        if (holder != null)
            return holder;

        if (interactor.CompareTag("Player") ||
            interactor.GetComponentInParent<PlayerController2D>() != null)
        {
            return PlayerKeyring.GetOrCreate();
        }

        return null;
    }

    // Moves both halves instantly or with a slide to the requested state.
    private void ApplyPose(bool open, bool immediate)
    {
        Vector3 targetA = open ? openDeltaA : Vector3.zero;
        Vector3 targetB = open ? openDeltaB : Vector3.zero;

        if (immediate)
        {
            if (halfA != null) halfA.localPosition = targetA;
            if (halfB != null) halfB.localPosition = targetB;
            SetBlockersEnabled(!open);
            IsOpen = open;
            ApplyLockVisuals();
            return;
        }

        if (slideCoroutine != null) StopCoroutine(slideCoroutine);
        slideCoroutine = StartCoroutine(SlideRoutine(targetA, targetB, open));
    }

    // Smoothly retracts or extends both halves.
    private IEnumerator SlideRoutine(Vector3 targetA, Vector3 targetB, bool opening)
    {
        Vector3 startA = halfA.localPosition;
        Vector3 startB = halfB.localPosition;
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, slideDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = progress * progress * (3f - 2f * progress);
            halfA.localPosition = Vector3.LerpUnclamped(startA, targetA, eased);
            halfB.localPosition = Vector3.LerpUnclamped(startB, targetB, eased);
            yield return null;
        }

        halfA.localPosition = targetA;
        halfB.localPosition = targetB;
        IsOpen = opening;
        SetBlockersEnabled(!opening);
        slideCoroutine = null;
        ApplyLockVisuals();

        if (opening) OnOpened?.Invoke();
        else OnClosed?.Invoke();
    }

    private void SetBlockersEnabled(bool value)
    {
        for (int i = 0; i < cellColliders.Count; i++)
        {
            if (cellColliders[i] != null)
                cellColliders[i].enabled = value;
        }
    }

    // Tints every cell to communicate whether the gate is locked.
    private void ApplyLockVisuals()
    {
        Color color = IsLocked ? lockedColor : unlockedColor;
        for (int i = 0; i < cellRenderers.Count; i++)
        {
            if (cellRenderers[i] != null)
                cellRenderers[i].color = color;
        }
    }

    // Editor helper: rebuild the gate immediately using the assigned grid.
    [ContextMenu("Rebuild Gate")]
    private void RebuildGateContext()
    {
        if (grid == null)
            grid = GetComponentInParent<Grid>();
        if (grid == null) return;

        Vector3Int anchor = grid.WorldToCell(transform.position);
        transform.SetPositionAndRotation(grid.GetCellCenterWorld(anchor), Quaternion.identity);
        transform.localScale = Vector3.one;
        BuildGate(anchor);
        ApplyPose(Application.isPlaying && IsOpen, immediate: true);
    }

    // Shows the covered cells while editing.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        Grid g = grid != null ? grid : GetComponentInParent<Grid>();
        if (g == null) return;

        Gizmos.color = Color.green;
        Vector3Int anchor = g.WorldToCell(transform.position);
        Vector3Int dir = AxisVector;
        Vector2 size = g.cellSize;
        for (int i = 0; i < cellLength; i++)
            Gizmos.DrawWireCube(g.GetCellCenterWorld(anchor + dir * i), new Vector3(size.x, size.y, 0f));
    }
}
