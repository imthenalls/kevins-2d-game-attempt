using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Controls a single-panel door that toggles with the normal E interaction and slides
/// sideways like one half of an elevator door. It can require a key item from the player's
/// inventory before opening. The solid panel collider is disabled once open while a separate
/// trigger on the root keeps the door interactable.
///
/// Unity setup:
///   1. Add this component to a door root with a trigger Collider2D.
///   2. Create a child panel with SpriteRenderer and a non-trigger Collider2D.
///   3. Assign Sliding Panel and Blocking Collider, then configure Open Offset, Slide
///      Duration, Interaction Range, Starts Open, Can Close, Required Key Id, whether the
///      key is consumed, and whether the door remains unlocked afterward.
///   4. Keep the root on a layer included by PlayerInteractionController.Interactable Layers.
///   The provided Assets/Prefabs/SlidingDoor.prefab is already wired this way.
///
/// Runtime API:
///   TryOpen(interactor) performs the inventory key check. Open(), Close(), and Toggle()
///   provide direct scripted control. IsOpen, IsMoving, and IsLocked expose state.
///   OnUnlocked, OnUnlockFailed, OnOpened, and OnClosed report state changes.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class SlidingDoor : MonoBehaviour, IInteractable
{
    [Header("Door Parts")]
    [SerializeField] private Transform slidingPanel;
    [SerializeField] private Collider2D blockingCollider;

    [Header("Movement")]
    [SerializeField] private Vector2 openOffset = new Vector2(1.25f, 0f);
    [SerializeField, Min(0.05f)] private float slideDuration = 0.45f;

    [Header("Interaction")]
    [SerializeField] private string displayName = "Door";
    [SerializeField, Min(0.25f)] private float interactionRange = 2f;
    [SerializeField] private bool startsOpen;
    [SerializeField] private bool canClose = true;

    [Header("Lock")]
    [SerializeField] private string requiredKeyId = "golden_key";
    [SerializeField] private bool consumeKeyOnUnlock;
    [SerializeField] private bool remainUnlocked = true;

    private Vector3 closedLocalPosition;
    private Coroutine slideCoroutine;
    private bool unlocked;

    public bool IsOpen { get; private set; }
    public bool IsMoving => slideCoroutine != null;
    public bool IsLocked => !unlocked && !string.IsNullOrWhiteSpace(requiredKeyId);
    public event Action OnUnlocked;
    public event Action OnUnlockFailed;
    public event Action OnOpened;
    public event Action OnClosed;

    // Records the closed panel position and applies the configured starting state.
    private void Awake()
    {
        if (slidingPanel == null && transform.childCount > 0)
            slidingPanel = transform.GetChild(0);
        if (blockingCollider == null && slidingPanel != null)
            blockingCollider = slidingPanel.GetComponent<Collider2D>();

        if (slidingPanel == null)
        {
            Debug.LogError("[SlidingDoor] A Sliding Panel child is required.", this);
            enabled = false;
            return;
        }

        closedLocalPosition = slidingPanel.localPosition;
        IsOpen = startsOpen;
        unlocked = startsOpen || string.IsNullOrWhiteSpace(requiredKeyId);
        ApplyImmediatePose(IsOpen);
    }

    // Returns true when the player is close enough and the panel is not currently moving.
    public bool CanInteract(Vector3 worldPosition) =>
        enabled && !IsMoving && Vector2.Distance(transform.position, worldPosition) <= interactionRange;

    // Supplies the name used by the shared interaction system.
    public string GetDisplayName() => displayName;

    // Doors do not open dialogue; interaction proceeds directly to EndInteraction.
    public bool TryGetCurrentLine(out string line)
    {
        line = string.Empty;
        return false;
    }

    // Required by IInteractable; doors have no text pages to advance.
    public void Advance() { }

    // Closes an open door or attempts a key-checked open when E interaction completes.
    public void EndInteraction(GameObject interactor)
    {
        if (IsOpen)
            Close();
        else
            TryOpen(interactor);
    }

    /// <summary>
    /// Attempts to unlock and open the door using the player's inventory. Returns false when
    /// the configured key is missing, the item id is invalid, or the door is already moving.
    /// </summary>
    public bool TryOpen(GameObject interactor)
    {
        if (IsOpen)
            return true;
        if (IsMoving || !TryUnlock(interactor))
            return false;

        StartSlide(true);
        return true;
    }

    /// <summary>Slides the panel to its open position if it is closed.</summary>
    public void Open()
    {
        if (!IsOpen && !IsMoving)
            StartSlide(true);
    }

    /// <summary>Slides the panel back to its closed position when closing is enabled.</summary>
    public void Close()
    {
        if (IsOpen && canClose && !IsMoving)
            StartSlide(false);
    }

    /// <summary>Toggles between open and closed states.</summary>
    public void Toggle()
    {
        if (IsMoving)
            return;

        if (IsOpen)
            Close();
        else
            Open();
    }

    // Resolves the configured key, checks the player inventory, and optionally consumes it.
    private bool TryUnlock(GameObject interactor)
    {
        if (!IsLocked)
            return true;

        ItemDatabase database = ItemDatabase.Instance;
        PlayerKeyring keyring = PlayerKeyring.GetOrCreate();
        if (database == null || keyring == null ||
            !database.TryGet(requiredKeyId, out ItemData key) || key == null ||
            !keyring.HasKey(requiredKeyId))
        {
            Debug.Log($"[SlidingDoor] {displayName} is locked. Required key: '{requiredKeyId}'.", this);
            OnUnlockFailed?.Invoke();
            return false;
        }

        if (consumeKeyOnUnlock)
        {
            if (!keyring.RemoveKey(requiredKeyId, 1))
            {
                OnUnlockFailed?.Invoke();
                return false;
            }

            QuestEventBus.Raise("ItemUsed", key.itemId, 1);
        }

        if (remainUnlocked)
            unlocked = true;

        Debug.Log($"[SlidingDoor] {displayName} unlocked with '{key.itemName}'.", interactor);
        OnUnlocked?.Invoke();
        return true;
    }

    // Starts one panel slide and enables collision immediately when closing begins.
    private void StartSlide(bool opening)
    {
        if (!opening && blockingCollider != null)
            blockingCollider.enabled = true;

        slideCoroutine = StartCoroutine(SlideRoutine(opening));
    }

    // Smoothly moves the panel between its closed position and configured open offset.
    private IEnumerator SlideRoutine(bool opening)
    {
        Vector3 start = slidingPanel.localPosition;
        Vector3 target = closedLocalPosition + (Vector3)(opening ? openOffset : Vector2.zero);
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, slideDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = progress * progress * (3f - 2f * progress);
            slidingPanel.localPosition = Vector3.LerpUnclamped(start, target, eased);
            yield return null;
        }

        slidingPanel.localPosition = target;
        IsOpen = opening;
        slideCoroutine = null;

        if (blockingCollider != null)
            blockingCollider.enabled = !opening;

        if (opening)
            OnOpened?.Invoke();
        else
            OnClosed?.Invoke();
    }

    // Places the panel instantly for scene startup without playing an animation.
    private void ApplyImmediatePose(bool open)
    {
        slidingPanel.localPosition = closedLocalPosition + (Vector3)(open ? openOffset : Vector2.zero);
        if (blockingCollider != null)
            blockingCollider.enabled = !open;
    }

    // Shows the interaction radius and slide destination while editing the prefab or scene.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        Transform panel = slidingPanel != null
            ? slidingPanel
            : transform.childCount > 0 ? transform.GetChild(0) : null;
        if (panel == null)
            return;

        Vector3 openWorldPosition = transform.TransformPoint(panel.localPosition + (Vector3)openOffset);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(openWorldPosition, panel.lossyScale);
    }
}
