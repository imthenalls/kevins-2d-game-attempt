using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

/// <summary>
/// A keyboard (E) interactable gate built from whole grid cells. Each covered cell gets a
/// diamond sprite positioned with Grid.GetCellCenterWorld, so the gate is always aligned to the
/// tilemap. The gate is split into two halves that retract outward by whole-cell vectors to open.
/// A solid diamond collider per cell blocks passage while closed. It can require a key item, and
/// it tints its cells by lock state.
///
/// Tuning and lock configuration live in the pure-C# Game.Core.SlidingDoorConfig (Game.Data);
/// this component keeps only the Unity references (Grid, gate Sprite, traveler LayerMask) and a
/// stable gate id string, and reads the config.
///
/// Unity setup:
///   1. Place the gate root on the first cell of the opening, inside the target Grid hierarchy
///      (or assign Grid explicitly).
///   2. Add this component and a trigger Collider2D (added automatically by the prefab).
///   3. Assign Grid, the Gate Sprite, and configure Settings (SlidingDoorConfig).
///   4. Keep the root on a layer included by PlayerInteractionController.Interactable Layers.
///
/// Runtime API:
///   TryOpen(interactor) / TryUse(interactor) perform the key check. Open(), Close(), Toggle()
///   provide direct control. IsOpen, IsMoving, and IsLocked expose state.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class SlidingDoor : MonoBehaviour, IInteractable
{
    [Header("Config (Game.Data)")]
    [SerializeField] private SlidingDoorConfig config = new SlidingDoorConfig();

    [Header("Grid Gate (Unity)")]
    [Tooltip("Grid the gate snaps to. Defaults to the parent Grid when left empty.")]
    [SerializeField] private Grid grid;
    [Tooltip("Diamond sprite drawn on each covered cell.")]
    [SerializeField] private Sprite gateSprite;
    [Tooltip("Stable id used by NPC memory and save data. Falls back to the GameObject name.")]
    [SerializeField] private string gateId;
    [Tooltip("Layers counted as travelers that can open/auto-close the gate.")]
    [SerializeField] private LayerMask travelerLayers = ~0;

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
    public bool IsLocked => !unlocked && !string.IsNullOrWhiteSpace(config.RequiredKeyId);
    public string RequiredKeyId => config.RequiredKeyId;
    public string GateId => string.IsNullOrWhiteSpace(gateId) ? name : gateId;

    public event Action OnUnlocked;
    public event Action OnUnlockFailed;
    public event Action OnOpened;
    public event Action OnClosed;

    /// <summary>Fired after every use attempt with the interacting entity and the outcome.</summary>
    public event Action<GameObject, GateUseResult> OnUseResolved;

    private Vector3Int AxisVector => config.Axis == DoorAxis.GridX ? new Vector3Int(1, 0, 0) : new Vector3Int(0, 1, 0);
    private Color LockedColor => new Color(config.LockedR, config.LockedG, config.LockedB, config.LockedA);
    private Color UnlockedColor => new Color(config.UnlockedR, config.UnlockedG, config.UnlockedB, config.UnlockedA);

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
        if (built && anchor == builtAnchor && config.Axis == builtAxis &&
            config.CellLength == builtLength && gateSprite == builtSprite)
            return;

        transform.SetPositionAndRotation(grid.GetCellCenterWorld(anchor), Quaternion.identity);
        transform.localScale = Vector3.one;
        BuildGate(anchor);

        builtAnchor = anchor;
        builtAxis = config.Axis;
        builtLength = config.CellLength;
        builtSprite = gateSprite;

        IsOpen = config.StartsOpen;
        unlocked = config.StartsOpen || string.IsNullOrWhiteSpace(config.RequiredKeyId);
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

        int length = Mathf.Max(1, config.CellLength);
        int mid = length <= 1 ? 1 : length / 2;
        for (int i = 0; i < length; i++)
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

        float sign = config.ReverseSlideDirection ? -1f : 1f;
        openDeltaA = -sign * mid * step;
        openDeltaB = sign * (length - mid) * step;
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

        if (grid == null || config.CellLength <= 0)
            return Vector2.Distance(transform.position, worldPosition) <= config.InteractionRange;

        Vector3Int anchor = built ? builtAnchor : grid.WorldToCell(transform.position);
        Vector3Int dir = AxisVector;
        float nearest = float.MaxValue;
        for (int i = 0; i < config.CellLength; i++)
        {
            Vector3 center = grid.GetCellCenterWorld(anchor + dir * i);
            nearest = Mathf.Min(nearest, Vector2.Distance(center, worldPosition));
        }

        return nearest <= config.InteractionRange;
    }

    // Supplies the name used by the shared interaction system.
    public string GetDisplayName() => config.DisplayName;

    // Shows a locked message when the player lacks the key; otherwise proceeds directly to opening.
    public bool TryGetCurrentLine(out string line)
    {
        line = string.Empty;

        if (lockedMessagePending)
        {
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
        if (string.IsNullOrWhiteSpace(config.RequiredKeyId))
            return true;

        PlayerKeyring keyring = PlayerKeyring.GetOrCreate();
        return keyring != null && keyring.HasKey(config.RequiredKeyId);
    }

    // Builds the locked interaction message, substituting the key's display name when known.
    private string BuildLockedMessage()
    {
        string keyName = config.RequiredKeyId;
        ItemDatabase database = ItemDatabase.Instance;
        if (database != null && database.TryGet(config.RequiredKeyId, out ItemData key) &&
            key != null && !string.IsNullOrWhiteSpace(key.itemName))
        {
            keyName = key.itemName;
        }

        return string.IsNullOrWhiteSpace(config.LockedMessage)
            ? $"It's locked. You need the {keyName}."
            : string.Format(config.LockedMessage, keyName);
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
        if (!built || !IsOpen || !config.CanClose || IsMoving) return;
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
            !database.TryGet(config.RequiredKeyId, out ItemData key) || key == null ||
            !holder.HasKey(config.RequiredKeyId))
        {
            Debug.Log($"[SlidingDoor] {config.DisplayName} is locked. Required key: '{config.RequiredKeyId}'.", this);
            OnUnlockFailed?.Invoke();
            return false;
        }

        if (config.ConsumeKeyOnUnlock)
        {
            if (!holder.RemoveKey(config.RequiredKeyId, 1))
            {
                OnUnlockFailed?.Invoke();
                return false;
            }

            QuestEventBus.Raise("ItemUsed", key.itemId, 1);
        }

        if (config.RemainUnlocked) unlocked = true;

        ApplyLockVisuals();
        Debug.Log($"[SlidingDoor] {config.DisplayName} unlocked with '{key.itemName}'.", interactor);
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
            interactor.GetComponentInParent<PlayerControllerBase>() != null)
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
        float duration = Mathf.Max(0.05f, config.SlideDuration);

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
        Color color = IsLocked ? LockedColor : UnlockedColor;
        for (int i = 0; i < cellRenderers.Count; i++)
        {
            if (cellRenderers[i] != null)
                cellRenderers[i].color = color;
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

        if (IsOpen && config.CanClose && config.CloseAfterPassing && travelersInside.Count == 0)
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
        if (config.CloseDelay > 0f)
            yield return new WaitForSeconds(config.CloseDelay);

        autoCloseCoroutine = null;
        if (IsOpen && config.CanClose && config.CloseAfterPassing && travelersInside.Count == 0)
            Close();
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
        Gizmos.DrawWireSphere(transform.position, config.InteractionRange);

        Grid g = grid != null ? grid : GetComponentInParent<Grid>();
        if (g == null) return;

        Gizmos.color = Color.green;
        Vector3Int anchor = g.WorldToCell(transform.position);
        Vector3Int dir = AxisVector;
        Vector2 size = g.cellSize;
        for (int i = 0; i < config.CellLength; i++)
            Gizmos.DrawWireCube(g.GetCellCenterWorld(anchor + dir * i), new Vector3(size.x, size.y, 0f));
    }
}
